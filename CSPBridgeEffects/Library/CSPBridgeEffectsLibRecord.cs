using System.Runtime.InteropServices;

namespace CSPBridgeEffects.Library.SDK;

/// <summary>
/// モジュール初期化レコード
/// </summary>
/// <remarks>
/// TriglavPlugInRecord.h の _TriglavPlugInModuleInitializeRecord に対応します。
/// モジュール初期化時にホストバージョン取得、モジュールID設定、モジュール種別設定を行う関数群です。
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct TriglavPlugInModuleInitializeRecord
{
	public delegate* unmanaged[Cdecl]<int*, TriglavPlugInHostObject, int> getHostVersionProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInHostObject, TriglavPlugInStringObject, int> setModuleIDProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInHostObject, int, int> setModuleKindProc;
}

/// <summary>
/// フィルタ初期化レコード
/// </summary>
/// <remarks>
/// TriglavPlugInRecord.h の _TriglavPlugInFilterInitializeRecord に対応します。
/// フィルタ名・カテゴリ、プレビュー可否、対象種別、プロパティ、コールバック設定を提供します。
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct TriglavPlugInFilterInitializeRecord
{
	public delegate* unmanaged[Cdecl]<TriglavPlugInHostObject, TriglavPlugInStringObject, sbyte, int> setFilterCategoryNameProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInHostObject, TriglavPlugInStringObject, sbyte, int> setFilterNameProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInHostObject, byte, int> setCanPreviewProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInHostObject, byte, int> setUseBlankImageProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInHostObject, int*, int, int> setTargetKindsProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInHostObject, TriglavPlugInPropertyObject, int> setPropertyProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInHostObject, delegate* unmanaged[Cdecl]<int*, TriglavPlugInPropertyObject, int, int, void*, void>, void*, int> setPropertyCallBackProc;
}

/// <summary>
/// フィルタ初期化レコード（Activation）
/// </summary>
/// <remarks>
/// TriglavPlugInRecord.h の _TriglavPlugInFilterActivationInitializeRecord に対応します。
/// 各API呼び出し時にホスト権限オブジェクトを併用する Activation 用の初期化関数群です。
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct TriglavPlugInFilterActivationInitializeRecord
{
	public delegate* unmanaged[Cdecl]<TriglavPlugInHostObject, TriglavPlugInHostPermissionObject> getHostPermissionProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInHostObject, TriglavPlugInHostPermissionObject, TriglavPlugInStringObject, sbyte, int> setFilterCategoryNameProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInHostObject, TriglavPlugInHostPermissionObject, TriglavPlugInStringObject, sbyte, int> setFilterNameProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInHostObject, TriglavPlugInHostPermissionObject, byte, int> setCanPreviewProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInHostObject, TriglavPlugInHostPermissionObject, byte, int> setUseBlankImageProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInHostObject, TriglavPlugInHostPermissionObject, int*, int, int> setTargetKindsProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInHostObject, TriglavPlugInHostPermissionObject, TriglavPlugInPropertyObject, int> setPropertyProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInHostObject, TriglavPlugInHostPermissionObject, delegate* unmanaged[Cdecl]<int*, TriglavPlugInPropertyObject, int, int, void*, void>, void*, int> setPropertyCallBackProc;
}

/// <summary>
/// フィルタ実行レコード
/// </summary>
/// <remarks>
/// TriglavPlugInRecord.h の _TriglavPlugInFilterRunRecord に対応します。
/// キャンバス情報取得、オフスクリーン取得、色取得、進捗更新、処理呼び出しなど実行時APIを保持します。
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct TriglavPlugInFilterRunRecord
{
	public delegate* unmanaged[Cdecl]<TriglavPlugInPropertyObject*, TriglavPlugInHostObject, int> getPropertyProc;
	public delegate* unmanaged[Cdecl]<int*, TriglavPlugInHostObject, int> getCanvasWidthProc;
	public delegate* unmanaged[Cdecl]<int*, TriglavPlugInHostObject, int> getCanvasHeightProc;
	public delegate* unmanaged[Cdecl]<double*, TriglavPlugInHostObject, int> getCanvasResolutionProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInPoint*, TriglavPlugInHostObject, int> getLayerOriginProc;
	public delegate* unmanaged[Cdecl]<byte*, TriglavPlugInHostObject, int> isLayerMaskSelectedProc;
	public delegate* unmanaged[Cdecl]<byte*, TriglavPlugInHostObject, int> isAlphaLockedProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInOffscreenObject*, TriglavPlugInHostObject, int> getSourceOffscreenProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInOffscreenObject*, TriglavPlugInHostObject, int> getDestinationOffscreenProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInRect*, TriglavPlugInHostObject, int> getSelectAreaRectProc;
	public delegate* unmanaged[Cdecl]<byte*, TriglavPlugInHostObject, int> hasSelectAreaOffscreenProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInOffscreenObject*, TriglavPlugInHostObject, int> getSelectAreaOffscreenProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInHostObject, TriglavPlugInRect*, int> updateDestinationOffscreenRectProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInRGBColor*, byte*, TriglavPlugInHostObject, int> getMainColorProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInRGBColor*, byte*, TriglavPlugInHostObject, int> getSubColorProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInRGBColor*, byte*, TriglavPlugInHostObject, int> getDrawColorProc;
	public delegate* unmanaged[Cdecl]<int*, TriglavPlugInHostObject, int, int> processProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInHostObject, int, int> setProgressTotalProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInHostObject, int, int> setProgressDoneProc;
}

/// <summary>
/// フィルタ実行レコード（Activation）
/// </summary>
/// <remarks>
/// TriglavPlugInRecord.h の _TriglavPlugInFilterActivationRunRecord に対応します。
/// Activation 環境向けに、各実行APIがホスト権限オブジェクトを受け取る構成です。
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct TriglavPlugInFilterActivationRunRecord
{
	public delegate* unmanaged[Cdecl]<TriglavPlugInHostObject, TriglavPlugInHostPermissionObject> getHostPermissionProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInPropertyObject*, TriglavPlugInHostObject, TriglavPlugInHostPermissionObject, int> getPropertyProc;
	public delegate* unmanaged[Cdecl]<int*, TriglavPlugInHostObject, TriglavPlugInHostPermissionObject, int> getCanvasWidthProc;
	public delegate* unmanaged[Cdecl]<int*, TriglavPlugInHostObject, TriglavPlugInHostPermissionObject, int> getCanvasHeightProc;
	public delegate* unmanaged[Cdecl]<double*, TriglavPlugInHostObject, TriglavPlugInHostPermissionObject, int> getCanvasResolutionProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInPoint*, TriglavPlugInHostObject, TriglavPlugInHostPermissionObject, int> getLayerOriginProc;
	public delegate* unmanaged[Cdecl]<byte*, TriglavPlugInHostObject, TriglavPlugInHostPermissionObject, int> isLayerMaskSelectedProc;
	public delegate* unmanaged[Cdecl]<byte*, TriglavPlugInHostObject, TriglavPlugInHostPermissionObject, int> isAlphaLockedProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInOffscreenObject*, TriglavPlugInHostObject, TriglavPlugInHostPermissionObject, int> getSourceOffscreenProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInOffscreenObject*, TriglavPlugInHostObject, TriglavPlugInHostPermissionObject, int> getDestinationOffscreenProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInRect*, TriglavPlugInHostObject, TriglavPlugInHostPermissionObject, int> getSelectAreaRectProc;
	public delegate* unmanaged[Cdecl]<byte*, TriglavPlugInHostObject, TriglavPlugInHostPermissionObject, int> hasSelectAreaOffscreenProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInOffscreenObject*, TriglavPlugInHostObject, TriglavPlugInHostPermissionObject, int> getSelectAreaOffscreenProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInHostObject, TriglavPlugInHostPermissionObject, TriglavPlugInRect*, int> updateDestinationOffscreenRectProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInRGBColor*, byte*, TriglavPlugInHostObject, TriglavPlugInHostPermissionObject, int> getMainColorProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInRGBColor*, byte*, TriglavPlugInHostObject, TriglavPlugInHostPermissionObject, int> getSubColorProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInRGBColor*, byte*, TriglavPlugInHostObject, TriglavPlugInHostPermissionObject, int> getDrawColorProc;
	public delegate* unmanaged[Cdecl]<int*, TriglavPlugInHostObject, TriglavPlugInHostPermissionObject, int, int> processProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInHostObject, TriglavPlugInHostPermissionObject, int, int> setProgressTotalProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInHostObject, TriglavPlugInHostPermissionObject, int, int> setProgressDoneProc;
}

/// <summary>
/// レコードスイート
/// </summary>
/// <remarks>
/// TriglavPlugInRecord.h の _TriglavPlugInRecordSuite に対応します。
/// モジュール初期化、フィルタ初期化/実行、Activation 用初期化/実行レコードへのポインタを集約し、
/// 将来拡張用の予約領域を含みます。
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct TriglavPlugInRecordSuite
{
	public TriglavPlugInModuleInitializeRecord* moduleInitializeRecord;
	public void* reserved1;
	public void* reserved2;
	public void* reserved3;
	public void* reserved4;
	public TriglavPlugInFilterInitializeRecord* filterInitializeRecord;
	public TriglavPlugInFilterRunRecord* filterRunRecord;
	public void* reserved5;
	public void* reserved6;
	public void* reserved7;
	public void* reserved8;
	public void* reserved9;
	public void* reserved10;
	public void* reserved11;
	public void* reserved12;
	public void* reserved13;
	public void* reserved14;
	public void* reserved15;
	public void* reserved16;
	public void* reserved17;
	public void* reserved18;
	public void* reserved19;
	public TriglavPlugInFilterActivationInitializeRecord* filterActivationInitializeRecord;
	public TriglavPlugInFilterActivationRunRecord* filterActivationRunRecord;

	// C++ 側 void*[256-24] と同サイズのパディング（64bit: 232 * 8 = 1856 バイト）
	public fixed long _reserved[256 - 24];
}
