#include "pch.h"
#include "BridgeFilter.h"
#include "BridgeBase.h"
#include <assert.h>
#include <filesystem>

// hostfxr の関数型定義
using hostfxr_handle = void*;
using load_assembly_and_get_function_pointer_fn = int(__stdcall*)(
    const wchar_t* assembly_path,
    const wchar_t* type_name,
    const wchar_t* method_name,
    const wchar_t* delegate_type_name,
    /*out*/ void** delegate);

/// <summary>
/// 実行ファイルと同じディレクトリ、もしくは PATH から hostfxr.dll をロードする
/// </summary>
/// <returns></returns>
static HMODULE LoadHostFxr()
{
    HMODULE h = nullptr;
    wchar_t path[MAX_PATH];
    if (::GetModuleFileNameW(nullptr, path, (DWORD)std::size(path)) != 0)
    {
        std::filesystem::path p(path);
        auto dir = p.parent_path();
        std::filesystem::path hostfxrPath = dir / L"hostfxr.dll";
        h = ::LoadLibraryW(hostfxrPath.wstring().c_str());
    }
    if (h == nullptr)
    {
        h = ::LoadLibraryW(L"hostfxr.dll");
    }
    return h;
}


/// <summary>
/// BridgeBase のコンストラクタ
/// </summary>
BridgeBase::BridgeBase()
    : m_hHostfxrLib(nullptr)
    , m_pHostfxrHandle(nullptr)
    , m_pLoadAssemblyAndGetFunctionPointerDelegate(nullptr)
    , m_pManagedFunctionPtr(nullptr)
{
}

/// <summary>
/// BridgeBaseのデストラクタ
/// </summary>
BridgeBase::~BridgeBase()
{
}

/// <summary>
/// BridgeBaseの初期化
/// </summary>
/// <param name="pluginServer"></param>
/// <returns></returns>
TriglavPlugInInt BridgeBase::Initialize(
	TriglavPlugInServer* pluginServer
)
{
	if (pluginServer == nullptr)
	{
		assert(false);
		return kTriglavPlugInCallResultFailed;
	}

    // CoreCLR (hostfxr) を埋め込むため、hostfxr をロードしてデリゲートを取得する必要があります
    m_hHostfxrLib = LoadHostFxr();
    if (m_hHostfxrLib == nullptr)
    {
        // hostfxr を見つけられませんでした。マネージランタイムは利用できません。
        // プラグインがネイティブのみで動作するように致命的エラーにせず true を返します。
        return kTriglavPlugInCallResultFailed;
    }

    // hostfxr_main や hostfxr_initialize_for_runtime_config のシンボルを取得します。
    // 簡潔にするため、ここでは動的に "hostfxr_get_runtime_delegate" を取得します。
    using hostfxr_get_runtime_delegate_fn = int(__stdcall*)(void*, int, void**);
    auto get_delegate = (hostfxr_get_runtime_delegate_fn)::GetProcAddress(m_hHostfxrLib, "hostfxr_get_runtime_delegate");
    if (get_delegate == nullptr)
    {
        // hostfxr のレイアウトが予想と異なります。
        ::FreeLibrary(m_hHostfxrLib);
        m_hHostfxrLib = nullptr;
        return kTriglavPlugInCallResultFailed;
    }

    // 注意: 実際にホストするには runtimeconfig.json を使って hostfxr_initialize_for_runtime_config を呼び、
    // hostfxr_get_runtime_delegate を使って load_assembly_and_get_function_pointer を取得する必要があります。
    // フルフローの実装は長くなるため、マネージの読み込みは後で実装します。

    return kTriglavPlugInCallResultSuccess;
}

/// <summary>
/// BridgeBaseの終了
/// </summary>
/// <param name="pluginServer"></param>
/// <param name="data"></param>
TriglavPlugInInt BridgeBase::Terminate(
	TriglavPlugInServer* pluginServer,
	TriglavPlugInPtr* data
)
{
	if (m_hHostfxrLib != nullptr)
    {
        ::FreeLibrary(m_hHostfxrLib);
        m_hHostfxrLib = nullptr;
    }
    return kTriglavPlugInCallResultSuccess;
}

/// <summary>
/// フィルタの初期化
/// </summary>
/// <param name="pluginServer"></param>
/// <param name="data"></param>
/// <param name="proc"></param>
/// <returns></returns>
TriglavPlugInInt BridgeBase::FilterInitialize(
    TriglavPlugInServer* pluginServer,
    TriglavPlugInPtr* data
)
{
    return kTriglavPlugInCallResultSuccess;
}

/// <summary>
/// フィルタの終了処理
/// </summary>
/// <param name="pluginServer"></param>
/// <param name="data"></param>
/// <returns></returns>
TriglavPlugInInt BridgeBase::FilterTerminate(
    TriglavPlugInServer* pluginServer,
    TriglavPlugInPtr* data
)
{
    return kTriglavPlugInCallResultSuccess;
}

/// <summary>
/// フィルタの実行
/// </summary>
/// <param name="pluginServer"></param>
/// <param name="data"></param>
/// <returns></returns>
TriglavPlugInInt BridgeBase::FilterRun(
    TriglavPlugInServer* pluginServer, 
    TriglavPlugInPtr* data
)
{
    return kTriglavPlugInCallResultSuccess;
}
