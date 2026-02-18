#pragma once

#include <Windows.h>
#include <string>

/// <summary>
/// BridgeBaseクラス
/// </summary>
class BridgeBase
{
public:
    BridgeBase();
    virtual ~BridgeBase();

public:
    /// <summary>
    /// BridgeBaseの初期化
    /// </summary>
    /// <param name="pluginServer"></param>
    /// <returns></returns>
    bool Initialize(TriglavPlugInServer* pluginServer);
    
    /// <summary>
    /// BridgeBaseの終了
    /// </summary>
    /// <param name="pluginServer"></param>
    /// <param name="data"></param>
    void Terminate(TriglavPlugInServer* pluginServer, TriglavPlugInPtr* data);

    // マネージ側のエントリポイントを呼び出す（ロード済みの場合）。引数は void* として渡されます。
    // マネージ側のメソッドは UnmanagedCallersOnly でエクスポートされ、互換のあるシグネチャである必要があります。例:
    // [UnmanagedCallersOnly(EntryPoint = "Invoke")]
    // public static int Invoke(IntPtr args) { ... }
    int InvokeManaged(void* args);

private:
    // hostfxr ライブラリのハンドル
    HMODULE m_hHostfxrLib;

    // hostfxr_initialize_for_runtime_config が返すホストコンテキスト
    LPVOID m_pHostfxrHandle;

    // マネージ関数を読み込むデリゲート
    LPVOID m_pLoadAssemblyAndGetFunctionPointerDelegate;

    // マネージアセンブリから取得した関数ポインタ
    LPVOID m_pManagedFunctionPtr;

    // 管理対象アセンブリや型・メソッド名
    std::wstring m_ManagedAssemblyPath;
    std::wstring m_ManagedTypeName;
    std::wstring m_ManagedMethodName;

};
