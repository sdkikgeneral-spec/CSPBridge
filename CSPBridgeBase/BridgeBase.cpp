#include "pch.h"
#include "BridgeBase.h"
#include "TriglavPlugInSDK.h"

#include <filesystem>
#include <cassert>
#include <string>

// hostfxr の "load_assembly_and_get_function_pointer" デリゲート種別
static constexpr int kDelegateLoadAssembly = 4;

// [UnmanagedCallersOnly] メソッドを取得するためのセンチネル値
static const wchar_t* const kUnmanagedCallersOnly = reinterpret_cast<const wchar_t*>(-1);

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

    const auto dllDir = GetThisDllDirectory();

    // ---- (1) hostfxr.dll をロード ----
    {
        auto hostfxrPath = std::filesystem::path(dllDir) / L"hostfxr.dll";
        m_hHostfxrLib = ::LoadLibraryW(hostfxrPath.wstring().c_str());
        if (m_hHostfxrLib == nullptr)
            m_hHostfxrLib = ::LoadLibraryW(L"hostfxr.dll");  // PATH から検索
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

    void* hostContext = nullptr;
    {
        const int rc = hostfxr_init(configPath.c_str(), nullptr, &hostContext);
        // 0 = 新規初期化成功, 1 (Success_HostAlreadyInitialized) も成功
        if (rc < 0)
        {
            ::FreeLibrary(m_hHostfxrLib);
            m_hHostfxrLib = nullptr;
            return kTriglavPlugInCallResultFailed;
        }
    }
    m_pHostContext = hostContext;

    // ---- (4) load_assembly_and_get_function_pointer デリゲートを取得 ----
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

    const int rc = m_pfnLoadAssembly(
        m_assemblyPath.c_str(),
        m_typeName.c_str(),
        methodName.c_str(),
        kUnmanagedCallersOnly,  // [UnmanagedCallersOnly] メソッド
        nullptr,
        fnPtr);

    return rc == 0 && *fnPtr != nullptr;
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

    if (m_pHostContext && m_pfnHostfxrClose)
    {
        m_pfnHostfxrClose(m_pHostContext);
        m_pHostContext = nullptr;
    }
    m_pfnHostfxrClose = nullptr;

    if (m_hHostfxrLib)
    {
        ::FreeLibrary(m_hHostfxrLib);
        m_hHostfxrLib = nullptr;
    }

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
