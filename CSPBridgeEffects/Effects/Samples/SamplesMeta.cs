// サンプルエフェクト用のメタデータ定義。
// meson ビルド環境では EffectMeta.cs.in から自動生成されますが、
// dotnet build 単体で動作確認できるようサンプル専用のメタデータをここで定義します。
namespace CSPBridgeEffects.Effects;

internal static class BlurMeta
{
    internal const string Category   = "CSPBridge Samples";
    internal const string FilterName = "Blur";
}

internal static class SharpenMeta
{
    internal const string Category   = "CSPBridge Samples";
    internal const string FilterName = "Sharpen";
}

internal static class MosaicMeta
{
    internal const string Category   = "CSPBridge Samples";
    internal const string FilterName = "Mosaic";
}

internal static class HSVMeta
{
    internal const string Category   = "CSPBridge Samples";
    internal const string FilterName = "HSV";
}
