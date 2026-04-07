// サンプルエフェクト用のメタデータ定義。
// meson ビルド環境では EffectMeta.cs.in から自動生成されますが、
// dotnet build 単体で動作確認できるようサンプル専用のメタデータをここで定義します。
namespace CSPBridgeEffects.Effects;

internal static class BlurMeta
{
    public const string Category   = "CSPBridge Samples";
    public const string FilterName = "Blur";
}

internal static class SharpenMeta
{
    public const string Category   = "CSPBridge Samples";
    public const string FilterName = "Sharpen";
}

internal static class MosaicMeta
{
    public const string Category   = "CSPBridge Samples";
    public const string FilterName = "Mosaic";
}

internal static class HSVMeta
{
    public const string Category   = "CSPBridge Samples";
    public const string FilterName = "HSV";
}
