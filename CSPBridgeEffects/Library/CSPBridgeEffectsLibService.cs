using System;
using System.Runtime.InteropServices;

namespace CSPBridgeEffects.Library.SDK;

/// <summary>
/// 文字列サービスAPI
/// </summary> <remarks>
/// 文字列サービスAPIは、プラグインがホストアプリケーションに文字列を渡すためのAPIです。プラグインは、文字列サービスAPIを使用して、ホストアプリケーションに文字列を渡すことができます。ホストアプリケーションは、文字列サービスAPIを使用して、プラグインから文字列を受け取ることができます。
/// 文字列サービスAPIは、以下の機能を提供します。
/// - 文字列の作成 
/// - 文字列の参照カウントの管理
/// - 文字列の内容の取得
/// - 文字列の解放
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct TriglavPlugInStringService
{
	public delegate* unmanaged[Cdecl]<TriglavPlugInStringObject*, sbyte*, int, int> createWithAsciiStringProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInStringObject*, ushort*, int, int> createWithUnicodeStringProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInStringObject*, sbyte*, int, int> createWithLocalCodeStringProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInStringObject*, int, TriglavPlugInHostObject, int> createWithStringIDProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInStringObject, int> retainProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInStringObject, int> releaseProc;
	public delegate* unmanaged[Cdecl]<ushort**, TriglavPlugInStringObject, int> getUnicodeCharsProc;
	public delegate* unmanaged[Cdecl]<int*, TriglavPlugInStringObject, int> getUnicodeLengthProc;
	public delegate* unmanaged[Cdecl]<sbyte**, TriglavPlugInStringObject, int> getLocalCodeCharsProc;
	public delegate* unmanaged[Cdecl]<int*, TriglavPlugInStringObject, int> getLocalCodeLengthProc;
}

/// <summary>
/// ビットマップサービスAPI 
/// </summary>
/// <remarks>
/// ビットマップサービスAPIは、プラグインがホストアプリケーションにビットマップを渡すためのAPIです。プラグインは、ビットマップサービスAPIを使用して、ホストアプリケーションにビットマップを渡すことができます。ホストアプリケーションは、ビットマップサービスAPIを使用して、プラグインからビットマップを受け取ることができます。
/// ビットマップサービスAPIは、以下の機能を提供します。
/// - ビットマップの作成
/// - ビットマップの参照カウントの管理
/// - ビットマップの幅、高さ、深度、スキャンラインの取得
/// - ビットマップのアドレスの取得
/// - ビットマップの行バイト数、ピクセルバイト数の取得
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct TriglavPlugInBitmapService
{
	public delegate* unmanaged[Cdecl]<TriglavPlugInBitmapObject*, int, int, int, int, int> createProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInBitmapObject, int> retainProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInBitmapObject, int> releaseProc;
	public delegate* unmanaged[Cdecl]<int*, TriglavPlugInBitmapObject, int> getWidthProc;
	public delegate* unmanaged[Cdecl]<int*, TriglavPlugInBitmapObject, int> getHeightProc;
	public delegate* unmanaged[Cdecl]<int*, TriglavPlugInBitmapObject, int> getDepthProc;
	public delegate* unmanaged[Cdecl]<int*, TriglavPlugInBitmapObject, int> getScanlineProc;
	public delegate* unmanaged[Cdecl]<void**, TriglavPlugInBitmapObject, TriglavPlugInPoint*, int> getAddressProc;
	public delegate* unmanaged[Cdecl]<int*, TriglavPlugInBitmapObject, int> getRowBytesProc;
	public delegate* unmanaged[Cdecl]<int*, TriglavPlugInBitmapObject, int> getPixelBytesProc;
}

/// <summary>
/// オフスクリーンサービスAPI
/// </summary>
/// <remarks>
/// オフスクリーンサービスAPIは、プラグインがホストアプリケーションにオフスクリーンを渡すためのAPIです。プラグインは、オフスクリーンサービスAPIを使用して、ホストアプリケーションにオフスクリーンを渡すことができます。ホストアプリケーションは、オフスクリーンサービスAPIを使用して、プラグインからオフスクリーンを受け取ることができます。
/// オフスクリーンサービスAPIは、以下の機能を提供します。
/// - オフスクリーンの作成
/// - オフスクリーンの参照カウントの管理
/// - オフスクリーンの幅、高さ、矩形の取得
/// - オフスクリーンのチャンネル順序の取得
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct TriglavPlugInOffscreenService
{
	public delegate* unmanaged[Cdecl]<TriglavPlugInOffscreenObject*, int, int, int, int> createPlaneProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInOffscreenObject, int> retainProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInOffscreenObject, int> releaseProc;
	public delegate* unmanaged[Cdecl]<int*, TriglavPlugInOffscreenObject, int> getWidthProc;
	public delegate* unmanaged[Cdecl]<int*, TriglavPlugInOffscreenObject, int> getHeightProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInRect*, TriglavPlugInOffscreenObject, int> getRectProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInRect*, TriglavPlugInOffscreenObject, int> getExtentRectProc;
	public delegate* unmanaged[Cdecl]<int*, TriglavPlugInOffscreenObject, int> getChannelOrderProc;
	public delegate* unmanaged[Cdecl]<int*, int*, int*, TriglavPlugInOffscreenObject, int> getRGBChannelIndexProc;
	public delegate* unmanaged[Cdecl]<int*, int*, int*, int*, TriglavPlugInOffscreenObject, int> getCMYKChannelIndexProc;
	public delegate* unmanaged[Cdecl]<int*, TriglavPlugInOffscreenObject, TriglavPlugInRect*, int> getBlockRectCountProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInRect*, int, TriglavPlugInOffscreenObject, TriglavPlugInRect*, int> getBlockRectProc;
	public delegate* unmanaged[Cdecl]<void**, int*, int*, TriglavPlugInRect*, TriglavPlugInOffscreenObject, TriglavPlugInPoint*, int> getBlockImageProc;
	public delegate* unmanaged[Cdecl]<void**, int*, int*, TriglavPlugInRect*, TriglavPlugInOffscreenObject, TriglavPlugInPoint*, int> getBlockAlphaProc;
	public delegate* unmanaged[Cdecl]<void**, int*, int*, TriglavPlugInRect*, TriglavPlugInOffscreenObject, TriglavPlugInPoint*, int> getBlockSelectAreaProc;
	public delegate* unmanaged[Cdecl]<void**, int*, int*, TriglavPlugInRect*, TriglavPlugInOffscreenObject, TriglavPlugInPoint*, int> getBlockPlaneProc;
	public delegate* unmanaged[Cdecl]<int*, TriglavPlugInOffscreenObject, int> getTileWidthProc;
	public delegate* unmanaged[Cdecl]<int*, TriglavPlugInOffscreenObject, int> getTileHeightProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInBitmapObject, TriglavPlugInPoint*, TriglavPlugInOffscreenObject, TriglavPlugInPoint*, int, int, int, int> getBitmapProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInOffscreenObject, TriglavPlugInPoint*, TriglavPlugInBitmapObject, TriglavPlugInPoint*, int, int, int, int> setBitmapProc;
}

/// <summary>
/// オフスクリーンサービスAPI2
/// </summary>
/// <remarks>
/// オフスクリーンサービスAPI2は、オフスクリーンサービスAPIの拡張版であり、追加の機能を提供します。
/// オフスクリーンサービスAPI2は、以下の機能を提供します。
/// - ビットマップの通常アルファチャンネルのインデックスの取得
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct TriglavPlugInOffscreenService2
{
	public delegate* unmanaged[Cdecl]<int*, TriglavPlugInOffscreenObject, int> getBitmapNormalAlphaChannelIndexProc;
}

/// <summary>
/// プロパティサービスAPI
/// </summary>
/// <remarks>
/// プロパティサービスAPIは、プラグインがホストアプリケーションにプロパティを渡すためのAPIです。プラグインは、プロパティサービスAPIを使用して、ホストアプリケーションにプロパティを渡すことができます。ホストアプリケーションは、プロパティサービスAPIを使用して、プラグインからプロパティを受け取ることができます。
/// プロパティサービスAPIは、以下の機能を提供します。
/// - プロパティの作成
/// - プロパティの参照カウントの管理
/// - プロパティのアイテムの追加
/// - プロパティの値の設定、取得
/// - プロパティのデフォルト値の設定、取得
/// - プロパティの最小値、最大値の設定、取得
/// - プロパティの値の種類の設定、取得
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct TriglavPlugInPropertyService
{
	public delegate* unmanaged[Cdecl]<TriglavPlugInPropertyObject*, int> createProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInPropertyObject, int> retainProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInPropertyObject, int> releaseProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInPropertyObject, int, int, int, int, TriglavPlugInStringObject, sbyte, int> addItemProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInPropertyObject, int, byte, int> setBooleanValueProc;
	public delegate* unmanaged[Cdecl]<byte*, TriglavPlugInPropertyObject, int, int> getBooleanValueProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInPropertyObject, int, byte, int> setBooleanDefaultValueProc;
	public delegate* unmanaged[Cdecl]<byte*, TriglavPlugInPropertyObject, int, int> getBooleanDefaultValueProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInPropertyObject, int, int, int> setIntegerValueProc;
	public delegate* unmanaged[Cdecl]<int*, TriglavPlugInPropertyObject, int, int> getIntegerValueProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInPropertyObject, int, int, int> setIntegerDefaultValueProc;
	public delegate* unmanaged[Cdecl]<int*, TriglavPlugInPropertyObject, int, int> getIntegerDefaultValueProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInPropertyObject, int, int, int> setIntegerMinValueProc;
	public delegate* unmanaged[Cdecl]<int*, TriglavPlugInPropertyObject, int, int> getIntegerMinValueProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInPropertyObject, int, int, int> setIntegerMaxValueProc;
	public delegate* unmanaged[Cdecl]<int*, TriglavPlugInPropertyObject, int, int> getIntegerMaxValueProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInPropertyObject, int, double, int> setDecimalValueProc;
	public delegate* unmanaged[Cdecl]<double*, TriglavPlugInPropertyObject, int, int> getDecimalValueProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInPropertyObject, int, double, int> setDecimalDefaultValueProc;
	public delegate* unmanaged[Cdecl]<double*, TriglavPlugInPropertyObject, int, int> getDecimalDefaultValueProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInPropertyObject, int, double, int> setDecimalMinValueProc;
	public delegate* unmanaged[Cdecl]<double*, TriglavPlugInPropertyObject, int, int> getDecimalMinValueProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInPropertyObject, int, double, int> setDecimalMaxValueProc;
	public delegate* unmanaged[Cdecl]<double*, TriglavPlugInPropertyObject, int, int> getDecimalMaxValueProc;
}

/// <summary>
/// プロパティサービスAPI2
/// </summary>
/// <remarks>
/// プロパティサービスAPI2は、プロパティサービスAPIの拡張版であり、追加の機能を提供します。
/// プロパティサービスAPI2は、以下の機能を提供します。
/// - プロパティのポイント値の設定、取得
/// - プロパティのポイント値のデフォルト値の設定、取得
/// - プロパティのポイント値の最小値、最大値の設定、取得
/// - プロパティの列挙値の設定、取得
/// - プロパティの列挙値のデフォルト値の設定、取得
/// - プロパティの列挙アイテムの追加
/// - プロパティの文字列値の設定、取得
/// - プロパティの文字列値のデフォルト値の設定、取得
/// - プロパティの文字列値の最大長の設定、取得
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct TriglavPlugInPropertyService2
{
	public delegate* unmanaged[Cdecl]<TriglavPlugInPropertyObject, int, byte, int> setItemStoreValueProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInPropertyObject, int, TriglavPlugInPoint*, int> setPointValueProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInPoint*, TriglavPlugInPropertyObject, int, int> getPointValueProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInPropertyObject, int, int, int> setPointDefaultValueKindProc;
	public delegate* unmanaged[Cdecl]<int*, TriglavPlugInPropertyObject, int, int> getPointDefaultValueKindProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInPropertyObject, int, TriglavPlugInPoint*, int> setPointDefaultValueProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInPoint*, TriglavPlugInPropertyObject, int, int> getPointDefaultValueProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInPropertyObject, int, int, int> setPointMinMaxValueKindProc;
	public delegate* unmanaged[Cdecl]<int*, TriglavPlugInPropertyObject, int, int> getPointMinMaxValueKindProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInPropertyObject, int, TriglavPlugInPoint*, int> setPointMinValueProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInPoint*, TriglavPlugInPropertyObject, int, int> getPointMinValueProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInPropertyObject, int, TriglavPlugInPoint*, int> setPointMaxValueProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInPoint*, TriglavPlugInPropertyObject, int, int> getPointMaxValueProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInPropertyObject, int, int, int> setEnumerationValueProc;
	public delegate* unmanaged[Cdecl]<int*, TriglavPlugInPropertyObject, int, int> getEnumerationValueProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInPropertyObject, int, int, int> setEnumerationDefaultValueProc;
	public delegate* unmanaged[Cdecl]<int*, TriglavPlugInPropertyObject, int, int> getEnumerationDefaultValueProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInPropertyObject, int, int, TriglavPlugInStringObject, sbyte, int> addEnumerationItemProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInPropertyObject, int, TriglavPlugInStringObject, int> setStringValueProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInStringObject*, TriglavPlugInPropertyObject, int, int> getStringValueProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInPropertyObject, int, TriglavPlugInStringObject, int> setStringDefaultValueProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInStringObject*, TriglavPlugInPropertyObject, int, int> getStringDefaultValueProc;
	public delegate* unmanaged[Cdecl]<TriglavPlugInPropertyObject, int, int, int> setStringMaxLengthProc;
	public delegate* unmanaged[Cdecl]<int*, TriglavPlugInPropertyObject, int, int> getStringMaxLengthProc;
}

/// <summary>
/// サービススイート
/// </summary>
/// <remarks>
/// サービススイートは、プラグインがホストアプリケーションから提供されるサービスを利用するための構造体です。プラグインは、サービススイートを使用して、ホストアプリケーションから提供されるサービスを利用することができます。
/// サービススイートは、以下のサービスを提供します。
/// - 文字列サービス
/// - ビットマップサービス
/// - オフスクリーンサービス
/// - プロパティサービス
/// - オフスクリーンサービス2
/// - プロパティサービス2
/// - 予約領域
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct TriglavPlugInServiceSuite
{
	public TriglavPlugInStringService* stringService;
	public TriglavPlugInBitmapService* bitmapService;
	public TriglavPlugInOffscreenService* offscreenService;
	public void* reserved1;
	public TriglavPlugInPropertyService* propertyService;
	public void* reserved2;
	public void* reserved3;
	public void* reserved4;
	public TriglavPlugInOffscreenService2* offscreenService2;
	public TriglavPlugInPropertyService2* propertyService2;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 256 - 10)]
	public IntPtr[] reserved;
}
