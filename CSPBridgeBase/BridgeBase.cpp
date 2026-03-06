#include "pch.h"
#include "BridgeBase.h"
#include "TriglavPlugInSDK.h"

#include <filesystem>
#include <cassert>
#include <string>
#include <vector>
#include <windows.h>

// hostfxr_initialize_parameters 相当の構造体（hostfxr.h を参照しない最小定義）
struct HostfxrInitParams
{
    size_t         size;
    const wchar_t* host_path;
    const wchar_t* dotnet_root;
};

/// <summary>
/// Windows レジストリから x64 .NET のインストール先を取得します。
/// 見つからない場合は "C:\Program Files\dotnet" を返します。
/// </summary>
static std::wstring GetDotnetRoot()
{
    HKEY hKey = nullptr;
    if (::RegOpenKeyExW(HKEY_LOCAL_MACHINE,
            L"SOFTWARE\\dotnet\\Setup\\InstalledVersions\\x64",
            0, KEY_READ | KEY_WOW64_64KEY, &hKey) == ERROR_SUCCESS)
    {
        wchar_t path[MAX_PATH] = {};
        DWORD cbData = sizeof(path);
        const LONG res = ::RegQueryValueExW(
            hKey, L"InstallLocation", nullptr, nullptr,
            reinterpret_cast<LPBYTE>(path), &cbData);
        ::RegCloseKey(hKey);
        if (res == ERROR_SUCCESS && path[0] != L'\0')
            return path;
    }
    return L"C:\\Program Files\\dotnet";
}

static std::vector<int> ParseVersionParts(const std::wstring& version)
{
    std::vector<int> parts;
    int current = -1;
    for (wchar_t ch : version)
    {
        if (ch >= L'0' && ch <= L'9')
        {
            if (current < 0)
                current = 0;
            current = (current * 10) + static_cast<int>(ch - L'0');
        }
        else
        {
            if (current >= 0)
            {
                parts.push_back(current);
                current = -1;
            }
        }
    }
    if (current >= 0)
        parts.push_back(current);
    return parts;
}

static bool IsVersionGreater(const std::wstring& lhs, const std::wstring& rhs)
{
    const auto leftParts = ParseVersionParts(lhs);
    const auto rightParts = ParseVersionParts(rhs);
    const size_t n = (leftParts.size() > rightParts.size()) ? leftParts.size() : rightParts.size();

    for (size_t i = 0; i < n; ++i)
    {
        const int lv = (i < leftParts.size()) ? leftParts[i] : 0;
        const int rv = (i < rightParts.size()) ? rightParts[i] : 0;
        if (lv != rv)
            return lv > rv;
    }

    return false;
}

static std::wstring FindLatestHostfxrPathFromDotnetRoot(const std::wstring& dotnetRoot)
{
    const auto fxrDir = std::filesystem::path(dotnetRoot) / L"host" / L"fxr";
    if (!std::filesystem::exists(fxrDir) || !std::filesystem::is_directory(fxrDir))
        return L"";

    std::wstring latestVersion;
    std::filesystem::path latestPath;

    for (const auto& entry : std::filesystem::directory_iterator(fxrDir))
    {
        if (!entry.is_directory())
            continue;

        const auto versionName = entry.path().filename().wstring();
        const auto candidate = entry.path() / L"hostfxr.dll";
        if (!std::filesystem::exists(candidate))
            continue;

        if (latestVersion.empty() || IsVersionGreater(versionName, latestVersion))
        {
            latestVersion = versionName;
            latestPath = candidate;
        }
    }

    return latestPath.empty() ? L"" : latestPath.wstring();
}

// hostfxr の "load_assembly_and_get_function_pointer" デリゲート種別
// hostfxr.h: hdt_load_assembly_and_get_function_pointer = 5
static constexpr int kDelegateLoadAssembly = 5;

// [UnmanagedCallersOnly] メソッドを取得するためのセンチネル値
static const wchar_t* const kUnmanagedCallersOnly = reinterpret_cast<const wchar_t*>(-1);

static bool CallLoadAssemblyWithSeh(
    load_assembly_fn loadAssembly,
    const wchar_t* assemblyPath,
    const wchar_t* typeName,
    const wchar_t* methodName,
    void** fnPtr,
    int* outRc,
    DWORD* outException)
{
    if (loadAssembly == nullptr || fnPtr == nullptr || outRc == nullptr || outException == nullptr)
        return false;

    *outRc = -1;
    *outException = 0;
    *fnPtr = nullptr;

    __try
    {
        *outRc = loadAssembly(
            assemblyPath,
            typeName,
            methodName,
            kUnmanagedCallersOnly,
            nullptr,
            fnPtr);
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        *outException = GetExceptionCode();
        return false;
    }

    return true;
}

// ============================================================
// 内部ユーティリティ
// ============================================================

/// <summary>
/// このDLL自身が格納されているディレクトリを返します。
/// （呼び出し元 DLL の HMODULE を ADDRESS から取得）
/// </summary>
std::wstring BridgeBase::GetThisDllDirectory()
{
    HMODULE hSelf = nullptr;
    ::GetModuleHandleExW(
        GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
        reinterpret_cast<LPCWSTR>(&BridgeBase::GetThisDllDirectory),
        &hSelf);

    wchar_t path[MAX_PATH] = {};
    ::GetModuleFileNameW(hSelf, path, MAX_PATH);
    return std::filesystem::path(path).parent_path().wstring();
}

// ============================================================
// コンストラクタ / デストラクタ
// ============================================================

BridgeBase::BridgeBase()
    : m_hHostfxrLib(nullptr)
    , m_pHostContext(nullptr)
    , m_isPrimaryHost(false)
    , m_pfnHostfxrClose(nullptr)
    , m_pfnLoadAssembly(nullptr)
    , m_pfnModuleInitialize(nullptr)
    , m_pfnFilterInitialize(nullptr)
    , m_pfnFilterRun(nullptr)
    , m_pfnFilterTerminate(nullptr)
{
}

BridgeBase::~BridgeBase()
{
    Terminate(nullptr, nullptr);
}

// ============================================================
// Initialize – CoreCLR ホスティング + マネージド ModuleInitialize
// ============================================================

TriglavPlugInInt BridgeBase::Initialize(TriglavPlugInServer* pluginServer)
{
    assert(pluginServer != nullptr);
    if (pluginServer == nullptr)
        return kTriglavPlugInCallResultFailed;

    // Initialize が複数回呼ばれても、すでに有効な関数ポインタを保持していれば
    // 再初期化せず成功を返す（hostfxr_get_runtime_delegate の再取得失敗を回避）。
    if (m_pfnLoadAssembly != nullptr &&
        m_pfnModuleInitialize != nullptr &&
        m_pfnFilterInitialize != nullptr &&
        m_pfnFilterRun != nullptr &&
        m_pfnFilterTerminate != nullptr)
    {
        return kTriglavPlugInCallResultSuccess;
    }

    // 以前の初期化が途中で失敗して中途半端な状態が残っている場合に備えて、
    // 先にクリーンアップしてから再初期化する。
    if (m_hHostfxrLib != nullptr || m_pHostContext != nullptr)
        Terminate(nullptr, nullptr);

    const auto dllDir = GetThisDllDirectory();
    const auto dotnetRoot = GetDotnetRoot();

    // ---- (1) hostfxr.dll をロード ----
    // 重要: ホストプロセス (CSP) が既に hostfxr.dll を読み込んでいる場合、
    // 別インスタンスを LoadLibrary すると g_active_host_context が別物になり
    // 「coreclr が既に初期化済み」で 0x80008081 が発生する。
    // GetModuleHandleExW で既存インスタンスを優先使用し、CSP と同じ
    // hostfxr 状態を共有することで initialize_for_runtime_config が
    // Success_HostAlreadyInitialized (8) を返し、有効なセカンダリコンテキストが得られる。
    {
        // GET_MODULE_HANDLE_EX_FLAG_PIN なしで呼ぶと参照カウントがインクリメントされるため
        // Terminate での FreeLibrary は安全。
        if (!::GetModuleHandleExW(0, L"hostfxr.dll", &m_hHostfxrLib) || m_hHostfxrLib == nullptr)
        {
            // まだロードされていない場合は .NET インストール先から最新版 hostfxr.dll を直接ロード
            const auto hostfxrPath = FindLatestHostfxrPathFromDotnetRoot(dotnetRoot);
            if (!hostfxrPath.empty())
                m_hHostfxrLib = ::LoadLibraryW(hostfxrPath.c_str());

            if (m_hHostfxrLib == nullptr)
                m_hHostfxrLib = ::LoadLibraryW(L"hostfxr.dll");  // 最終フォールバック（PATH）
        }
    }
    if (m_hHostfxrLib == nullptr)
        return kTriglavPlugInCallResultFailed;

    // ---- (2) hostfxr 関数シンボルを取得 ----
    const auto hostfxr_init = reinterpret_cast<hostfxr_initialize_fn>(
        ::GetProcAddress(m_hHostfxrLib, "hostfxr_initialize_for_runtime_config"));
    const auto hostfxr_get_delegate = reinterpret_cast<hostfxr_get_delegate_fn>(
        ::GetProcAddress(m_hHostfxrLib, "hostfxr_get_runtime_delegate"));
    m_pfnHostfxrClose = reinterpret_cast<hostfxr_close_fn>(
        ::GetProcAddress(m_hHostfxrLib, "hostfxr_close"));

    if (!hostfxr_init || !hostfxr_get_delegate || !m_pfnHostfxrClose)
    {
        ::FreeLibrary(m_hHostfxrLib);
        m_hHostfxrLib = nullptr;
        return kTriglavPlugInCallResultFailed;
    }

    // ---- (3) hostfxr_initialize_for_runtime_config ----
    const auto configPath =
        (std::filesystem::path(dllDir) / L"CSPBridgeEffects.runtimeconfig.json").wstring();

    // hostfxr.dll は自身の場所から dotnet_root を推定するため、
    // プラグインフォルダにコピーされた場合は誤ったパスを参照してしまう。
    // initialize_parameters で dotnet_root を明示的に指定して回避する。
    // host_path には呼び出し元プロセス (.exe) のパスを渡す。
    wchar_t hostExePath[MAX_PATH] = {};
    ::GetModuleFileNameW(nullptr, hostExePath, MAX_PATH);
    HostfxrInitParams initParams{};
    initParams.size        = sizeof(initParams);
    initParams.host_path   = hostExePath;
    initParams.dotnet_root = dotnetRoot.c_str();

    void* hostContext = nullptr;
    {
        int rc = hostfxr_init(configPath.c_str(), &initParams, &hostContext);

        // 0 = 新規初期化成功（CSP が .NET を使っていない場合）
        // 8 (Success_HostAlreadyInitialized) = CSP が既に .NET を初期化済み。
        //   hostContext にセカンダリコンテキストが返る。
        // 正の値はすべて成功扱い。
        // 0x80008081 (InvalidArgFailure) の場合、ホスト側初期化状態によっては
        // initialize_parameters 付き呼び出しを受け付けないケースがあるため、
        // パラメータなしで 1 回だけ再試行する。
        if (rc == static_cast<int>(0x80008081))
        {
            hostContext = nullptr;
            rc = hostfxr_init(configPath.c_str(), nullptr, &hostContext);
        }

        if (rc < 0 || hostContext == nullptr)
        {
            ::FreeLibrary(m_hHostfxrLib);
            m_hHostfxrLib = nullptr;
            return kTriglavPlugInCallResultFailed;
        }

        // rc == 0: we started the CLR (primary context).
        // rc == 8 (Success_HostAlreadyInitialized): secondary context; primary belongs to CSP or
        // a prior load of this plugin.
        m_isPrimaryHost = (rc == 0);
    }
    m_pHostContext = hostContext;

    // ---- (4) load_assembly_and_get_function_pointer デリゲートを取得 ----
    // Success_HostAlreadyInitialized (8) の場合 hostContext はセカンダリコンテキスト。
    // セカンダリコンテキストでは load_runtime 不要でそのままデリゲートを取得できる。
    {
        void* loadFn = nullptr;
        const int rc = hostfxr_get_delegate(
            hostContext, kDelegateLoadAssembly, &loadFn);
        if (rc < 0 || loadFn == nullptr)
        {
            Terminate(nullptr, nullptr);
            return kTriglavPlugInCallResultFailed;
        }
        m_pfnLoadAssembly = reinterpret_cast<load_assembly_fn>(loadFn);
    }

    // ---- (5) アセンブリパスと型名を構築 ----
    m_assemblyPath = (std::filesystem::path(dllDir) / L"CSPBridgeEffects.dll").wstring();

    // EFFECT_ID は meson.build で -DEFFECT_ID="Blur" のように定義される (ASCII)
    const std::string effectIdNarrow{ EFFECT_ID };
    const std::wstring effectId{ effectIdNarrow.cbegin(), effectIdNarrow.cend() };
    m_typeName = L"CSPBridgeEffects.Effects." + effectId + L", CSPBridgeEffects";

    // ---- (6) マネージド関数ポインタをロード ----
    if (!GetManagedFunction(L"ModuleInitialize",  reinterpret_cast<void**>(&m_pfnModuleInitialize)) ||
        !GetManagedFunction(L"FilterInitialize",  reinterpret_cast<void**>(&m_pfnFilterInitialize)) ||
        !GetManagedFunction(L"FilterRun",         reinterpret_cast<void**>(&m_pfnFilterRun))        ||
        !GetManagedFunction(L"FilterTerminate",   reinterpret_cast<void**>(&m_pfnFilterTerminate)))
    {
        Terminate(nullptr, nullptr);
        return kTriglavPlugInCallResultFailed;
    }

    // ---- (7) マネージド ModuleInitialize を呼び出す ----
    return static_cast<TriglavPlugInInt>(m_pfnModuleInitialize(pluginServer));
}

// ============================================================
// GetManagedFunction – load_assembly_and_get_function_pointer ラッパー
// ============================================================

bool BridgeBase::GetManagedFunction(const std::wstring& methodName, void** fnPtr) const
{
    if (m_pfnLoadAssembly == nullptr)
        return false;

    if (fnPtr == nullptr)
        return false;

    int rc = -1;
    DWORD exCode = 0;
    const bool called = CallLoadAssemblyWithSeh(
        m_pfnLoadAssembly,
        m_assemblyPath.c_str(),
        m_typeName.c_str(),
        methodName.c_str(),
        fnPtr,
        &rc,
        &exCode);

    if (!called)
        return false;

    if (rc != 0 || *fnPtr == nullptr)
        return false;

    return true;
}

// ============================================================
// Terminate – CoreCLR コンテキストをクローズしてリソースを解放
// ============================================================

TriglavPlugInInt BridgeBase::Terminate(
    TriglavPlugInServer* /*pluginServer*/, TriglavPlugInPtr* /*data*/)
{
    m_pfnModuleInitialize = nullptr;
    m_pfnFilterInitialize = nullptr;
    m_pfnFilterRun        = nullptr;
    m_pfnFilterTerminate  = nullptr;
    m_pfnLoadAssembly     = nullptr;

    // Only close secondary contexts.  The primary host context MUST remain open so that a
    // subsequent hostfxr_initialize_for_runtime_config call returns
    // Success_HostAlreadyInitialized (8) rather than 0x80008081.
    // Closing the primary context clears hostfxr's global context state; a re-load of
    // hostfxr.dll then has no knowledge of the still-running CLR (coreclr.dll is
    // process-lifetime and cannot be restarted), causing every subsequent init to fail.
    if (m_pHostContext && m_pfnHostfxrClose && !m_isPrimaryHost)
    {
        m_pfnHostfxrClose(m_pHostContext);
    }
    m_pHostContext = nullptr;
    m_pfnHostfxrClose = nullptr;

    // Do NOT call FreeLibrary on hostfxr.dll.
    // If we unload hostfxr while coreclr.dll is still running, a freshly loaded hostfxr
    // has no context to detect the already-running CLR, causing 0x80008081 on re-init.
    // The CLR and hostfxr are effectively process-lifetime; we leave them loaded.
    m_hHostfxrLib = nullptr;

    return kTriglavPlugInCallResultSuccess;
}

// ============================================================
// Filter コールバック – 対応するマネージドメソッドを呼ぶだけ
// ============================================================

TriglavPlugInInt BridgeBase::FilterInitialize(
    TriglavPlugInServer* pluginServer, TriglavPlugInPtr* data)
{
    if (m_pfnFilterInitialize == nullptr)
        return kTriglavPlugInCallResultFailed;
    return static_cast<TriglavPlugInInt>(
        m_pfnFilterInitialize(pluginServer, reinterpret_cast<void**>(data)));
}

TriglavPlugInInt BridgeBase::FilterTerminate(
    TriglavPlugInServer* pluginServer, TriglavPlugInPtr* data)
{
    if (m_pfnFilterTerminate == nullptr)
        return kTriglavPlugInCallResultSuccess;
    return static_cast<TriglavPlugInInt>(
        m_pfnFilterTerminate(pluginServer, reinterpret_cast<void**>(data)));
}

TriglavPlugInInt BridgeBase::FilterRun(
    TriglavPlugInServer* pluginServer, TriglavPlugInPtr* data)
{
    if (m_pfnFilterRun == nullptr)
        return kTriglavPlugInCallResultFailed;
    return static_cast<TriglavPlugInInt>(
        m_pfnFilterRun(pluginServer, reinterpret_cast<void**>(data)));
}
