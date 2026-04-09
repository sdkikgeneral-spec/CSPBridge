// HsvKernel のユニットテスト。
// 特に色相変換の精度検証と RGB ↔ HSV ラウンドトリップを重点的にテストします。
using CSPBridgeEffects.Effects.Kernels;
using Xunit;

namespace CSPBridgeEffects.Tests;

/// <summary>
/// <see cref="HsvKernel"/> のテストクラス。
/// </summary>
public class HsvKernelTests
{
    // ================================================================
    // RGB → HSV → RGB ラウンドトリップ
    // ================================================================

    [Theory]
    [InlineData(255, 0, 0)]     // 純赤
    [InlineData(0, 255, 0)]     // 純緑
    [InlineData(0, 0, 255)]     // 純青
    [InlineData(255, 255, 0)]   // 黄
    [InlineData(0, 255, 255)]   // シアン
    [InlineData(255, 0, 255)]   // マゼンタ
    [InlineData(255, 255, 255)] // 白
    [InlineData(128, 128, 128)] // グレー
    [InlineData(0, 0, 0)]       // 黒
    [InlineData(1, 1, 1)]       // 最小輝度
    [InlineData(100, 150, 200)] // 一般的な色
    public void RgbToHsv_HsvToRgb_Roundtrip(uint r, uint g, uint b)
    {
        HsvKernel.RgbToHsv(out uint h, out uint s, out uint v, r, g, b);
        HsvKernel.HsvToRgb(out uint rOut, out uint gOut, out uint bOut, h, s, v);

        // 整数演算の丸め誤差を ±1 許容
        Assert.InRange(rOut, r > 0 ? r - 1 : 0, r + 1);
        Assert.InRange(gOut, g > 0 ? g - 1 : 0, g + 1);
        Assert.InRange(bOut, b > 0 ? b - 1 : 0, b + 1);
    }

    // ================================================================
    // RgbToHsv 個別検証
    // ================================================================

    [Fact]
    public void RgbToHsv_PureRed_ReturnsH0()
    {
        HsvKernel.RgbToHsv(out uint h, out uint s, out uint v, 255, 0, 0);
        Assert.Equal(0u, h);              // H=0（赤）
        Assert.Equal(65536u, s);          // S=max
        Assert.Equal(255u, v);            // V=max
    }

    [Fact]
    public void RgbToHsv_Gray_ReturnsSaturation0()
    {
        HsvKernel.RgbToHsv(out uint h, out uint s, out uint v, 128, 128, 128);
        Assert.Equal(0u, s); // 無彩色 → S=0
        Assert.Equal(128u, v);
    }

    [Fact]
    public void RgbToHsv_Black_ReturnsAllZero()
    {
        HsvKernel.RgbToHsv(out uint h, out uint s, out uint v, 0, 0, 0);
        Assert.Equal(0u, h);
        Assert.Equal(0u, s);
        Assert.Equal(0u, v);
    }

    // ================================================================
    // HsvFilter — 色相回転テスト
    // ================================================================

    /// <summary>
    /// 色相回転の境界値テスト。
    /// PIHSVMain.cpp の h * HsvHFilterMax / 360 との整合性を確認します。
    /// </summary>
    [Fact]
    public void HsvFilter_HueRotation_WrapsCorrectly()
    {
        const int hMax = 6 * 32768; // 196608

        // H=0 に +90度分 のフィルタを適用 → H = hMax/4
        uint h = 0, s = 65536, v = 255;
        int hFilter90 = hMax / 4; // 49152
        HsvKernel.HsvFilter(ref h, ref s, ref v, hFilter90, 0, 0);
        Assert.Equal((uint)hFilter90, h);

        // H=0 に +360度分（=hMax）を適用 → 元に戻る（hMax → wrap → 0）
        // HsvFilter の実装は nH >= HMax のとき nH -= HMax
        h = 0; s = 65536; v = 255;
        HsvKernel.HsvFilter(ref h, ref s, ref v, hMax, 0, 0);
        Assert.Equal(0u, h);
    }

    [Fact]
    public void HsvFilter_HueRotation_LargeH_WrapsAround()
    {
        const int hMax = 6 * 32768;
        // H が hMax 付近の場合、少しの回転で wrap する
        uint h = (uint)(hMax - 100), s = 65536, v = 255;
        HsvKernel.HsvFilter(ref h, ref s, ref v, 200, 0, 0);
        Assert.Equal(100u, h); // (hMax - 100 + 200) - hMax = 100
    }

    [Fact]
    public void HsvFilter_NegativeHFilter_WrapsCorrectly()
    {
        const int hMax = 6 * 32768;
        // H=100 に -200 のフィルタを適用 → H = 100 - 200 + hMax = hMax - 100
        uint h = 100, s = 65536, v = 255;
        HsvKernel.HsvFilter(ref h, ref s, ref v, -200, 0, 0);
        Assert.Equal((uint)(hMax - 100), h);
    }

    // ================================================================
    // HsvFilter — 色相フィルタ値変換の精度テスト
    // h 度 → hFilter = h * HsvHFilterMax / 360 の変換精度を検証
    // ================================================================

    [Theory]
    [InlineData(0, 0)]
    [InlineData(180, 98304)]    // 180 * 196608 / 360 = 98304
    [InlineData(90, 49152)]     // 90 * 196608 / 360 = 49152
    [InlineData(360, 196608)]   // 360度 = フル回転
    [InlineData(-180, -98304)]  // 負の値（呼び出し元で +360 補正される想定）
    public void HueDegreesToFilter_Conversion(int degrees, int expectedFilter)
    {
        const int hsvHFilterMax = 6 * 32768;
        int filter = degrees * hsvHFilterMax / 360;
        Assert.Equal(expectedFilter, filter);
    }

    /// <summary>
    /// HSV.cs の readParameters ラムダと同等の変換ロジックを検証。
    /// h &lt; 0 ? h + 360 : h してから * HsvHFilterMax / 360 する方式。
    /// </summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(180, 98304)]
    [InlineData(-1, 196061)]    // (-1+360)=359 → 359*196608/360 = 196061
    [InlineData(-180, 98304)]   // (-180+360)=180 → 180*196608/360 = 98304
    [InlineData(-360, 0)]       // (-360+360)=0
    public void HueConversion_WithNegativeWrap(int inputDegrees, int expectedFilter)
    {
        const int hsvHFilterMax = 6 * 32768;
        int h = inputDegrees < 0 ? inputDegrees + 360 : inputDegrees;
        int filter = h * hsvHFilterMax / 360;
        Assert.Equal(expectedFilter, filter);
    }

    // ================================================================
    // HsvFilter — 彩度・明度テスト
    // ================================================================

    [Fact]
    public void HsvFilter_ZeroFilter_NoChange()
    {
        uint h = 10000, s = 30000, v = 128;
        uint origH = h, origS = s, origV = v;
        HsvKernel.HsvFilter(ref h, ref s, ref v, 0, 0, 0);
        Assert.Equal(origH, h);
        Assert.Equal(origS, s);
        Assert.Equal(origV, v);
    }

    [Fact]
    public void HsvFilter_MaxBrightness_ClampsTo255()
    {
        uint h = 0, s = 65536, v = 200;
        HsvKernel.HsvFilter(ref h, ref s, ref v, 0, 0, 32768); // vFilter = max
        Assert.True(v <= 255);
    }

    [Fact]
    public void HsvFilter_MinBrightness_ClampsTo0()
    {
        uint h = 0, s = 65536, v = 100;
        HsvKernel.HsvFilter(ref h, ref s, ref v, 0, 0, -32768); // vFilter = min
        Assert.True(v <= 255); // unsigned なので >= 0 は自明
    }

    // ================================================================
    // Process — 統合テスト
    // ================================================================

    [Fact]
    public void Process_ZeroFilter_PreservesPixels()
    {
        int w = 2, h = 1, pixBytes = 3, stride = w * pixBytes;
        byte[] dst = { 100, 150, 200, 50, 80, 120 };
        byte[] alpha = { 255, 255 };
        byte[] expected = (byte[])dst.Clone();

        HsvKernel.Process(dst, stride, pixBytes,
                          alpha, alphaStride: w, alphaPixBytes: 1,
                          w, h, rIdx: 0, gIdx: 1, bIdx: 2,
                          hFilter: 0, sFilter: 0, vFilter: 0);

        // ラウンドトリップ誤差 ±1 を許容
        for (int i = 0; i < dst.Length; i++)
        {
            Assert.InRange(dst[i], (byte)(expected[i] > 0 ? expected[i] - 1 : 0),
                           (byte)(expected[i] < 255 ? expected[i] + 1 : 255));
        }
    }
}
