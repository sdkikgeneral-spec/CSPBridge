#pragma once

#include <Windows.h>
#include <string>
#include "TriglavPlugInSDK.h"

// ============================================================
// マネージド エントリポイントの関数ポインタ型（cdecl / 64 bit）
// C# 側の [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])] と一致させる
// ============================================================
using ManagedModuleInitializeProc  = int(__cdecl*)(TriglavPlugInServer*);
using ManagedFilterInitializeProc  = int(__cdecl*)(TriglavPlugInServer*, void**);
using ManagedFilterRunProc         = int(__cdecl*)(TriglavPlugInServer*, void**);
using ManagedFilterTerminateProc   = int(__cdecl*)(TriglavPlugInServer*, void**);

// ============================================================
// hostfxr 関数ポインタ型
// ============================================================
using hostfxr_initialize_fn  = int(__cdecl*)(const wchar_t*, void*, void**);
using hostfxr_get_delegate_fn = int(__cdecl*)(void*, int, void**);
using hostfxr_close_fn        = int(__cdecl*)(void*);

// load_assembly_and_get_function_pointer
using load_assembly_fn = int(__cdecl*)(
    const wchar_t* assembly_path,
    const wchar_t* type_name,
    const wchar_t* method_name,
    const wchar_t* delegate_type_name,  // (const wchar_t*)-1 = UNMANAGEDCALLERSONLY
    void*          reserved,
    void**         delegate_out);

/// <summary>
/// CLIP STUDIO PRO プラグインを CoreCLR 経由で C# DLL にブリッジするベースクラス。
/// effects.json の各 EFFECT_ID に対して 1 インスタンスずつ生成されます。
/// </summary>
class BridgeBase
{
public:
    BridgeBase();
    virtual ~BridgeBase();

    /// <summary>CoreCLR を初期化し、マネージド ModuleInitialize を呼び出します。</summary>
    TriglavPlugInInt Initialize(TriglavPlugInServer* pluginServer);

    /// <summary>CoreCLR コンテキストを閉じ、hostfxr をアンロードします。</summary>
    TriglavPlugInInt Terminate(TriglavPlugInServer* pluginServer, TriglavPlugInPtr* data);

    /// <summary>マネージド FilterInitialize を呼び出します。</summary>
    TriglavPlugInInt FilterInitialize(TriglavPlugInServer* pluginServer, TriglavPlugInPtr* data);

    /// <summary>マネージド FilterTerminate を呼び出します。</summary>
    TriglavPlugInInt FilterTerminate(TriglavPlugInServer* pluginServer, TriglavPlugInPtr* data);

    /// <summary>マネージド FilterRun を呼び出します。</summary>
    TriglavPlugInInt FilterRun(TriglavPlugInServer* pluginServer, TriglavPlugInPtr* data);

private:
    /// <summary>このDLLと同じディレクトリのパスを返します。</summary>
    static std::wstring GetThisDllDirectory();

    /// <summary>
    /// load_assembly_and_get_function_pointer を使い、マネージド関数ポインタを取得します。
    /// </summary>
    bool GetManagedFunction(const std::wstring& methodName, void** fnPtr) const;

    // ---- hostfxr ----
    HMODULE            m_hHostfxrLib;
    void*              m_pHostContext;
    hostfxr_close_fn   m_pfnHostfxrClose;
    load_assembly_fn   m_pfnLoadAssembly;

    // ---- アセンブリ / 型 ----
    std::wstring       m_assemblyPath;  // ...CSPBridgeEffects.dll のフルパス
    std::wstring       m_typeName;      // "CSPBridgeEffects.Effects.{Id}, CSPBridgeEffects"

    // ---- マネージド エントリポイント ----
    ManagedModuleInitializeProc  m_pfnModuleInitialize;
    ManagedFilterInitializeProc  m_pfnFilterInitialize;
    ManagedFilterRunProc         m_pfnFilterRun;
    ManagedFilterTerminateProc   m_pfnFilterTerminate;
};
