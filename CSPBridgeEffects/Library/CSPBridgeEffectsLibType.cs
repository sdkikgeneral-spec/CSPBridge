using System.Runtime.InteropServices;

namespace CSPBridgeEffects.Library.SDK;

/// <summary>
/// C++ の TriglavPlugInServer に対応するアンマネージド構造体です。
/// C++ 側から TriglavPlugInServer* として渡されます。
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct TriglavPlugInServer
{
	public TriglavPlugInRecordSuite  recordSuite;
	public TriglavPlugInServiceSuite serviceSuite;
	public TriglavPlugInHostObject   hostObject;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct TriglavPlugInPoint
{
	public int x;
	public int y;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct TriglavPlugInSize
{
	public int width;
	public int height;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct TriglavPlugInRect
{
	public int left;
	public int top;
	public int right;
	public int bottom;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct TriglavPlugInRGBColor
{
	public byte red;
	public byte green;
	public byte blue;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct TriglavPlugInCMYKColor
{
	public byte cyan;
	public byte magenta;
	public byte yellow;
	public byte keyplate;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct TriglavPlugInHostObject
{
	public void* value;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct TriglavPlugInHostPermissionObject
{
	public void* value;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct TriglavPlugInStringObject
{
	public void* value;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct TriglavPlugInBitmapObject
{
	public void* value;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct TriglavPlugInOffscreenObject
{
	public void* value;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct TriglavPlugInPropertyObject
{
	public void* value;
}
