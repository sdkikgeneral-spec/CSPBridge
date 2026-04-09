// KernelUtils のユニットテスト。
using CSPBridgeEffects.Effects.Kernels;
using Xunit;

namespace CSPBridgeEffects.Tests;

/// <summary>
/// <see cref="KernelUtils"/> のテストクラス。
/// </summary>
public class KernelUtilsTests
{
    [Theory]
    [InlineData(255, 0, 255, 255)]  // mask=255 → dst
    [InlineData(255, 0, 0, 0)]      // mask=0   → src
    [InlineData(255, 0, 128, 128)]   // mask=128 → 中間値
    [InlineData(0, 255, 128, 127)]   // dst < src
    [InlineData(100, 100, 128, 100)] // dst == src → 変化なし
    public void Blend8_ReturnsExpectedValue(int dst, int src, int mask, int expected)
    {
        int result = KernelUtils.Blend8(dst, src, mask);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Blend8_BoundaryValues()
    {
        Assert.Equal(0, KernelUtils.Blend8(0, 0, 0));
        Assert.Equal(0, KernelUtils.Blend8(0, 0, 255));
        Assert.Equal(255, KernelUtils.Blend8(255, 255, 0));
        Assert.Equal(255, KernelUtils.Blend8(255, 255, 255));
    }

    [Fact]
    public void HorizontalBoxBlur_Radius0_CopiesSourceValues()
    {
        // 3x2 画像、RGB、stride=9（3 pixels * 3 bytes）
        int w = 3, h = 2, pixBytes = 3, stride = w * pixBytes;
        byte[] src = new byte[]
        {
            10, 20, 30,   40, 50, 60,   70, 80, 90,    // row 0
            100, 110, 120, 130, 140, 150, 160, 170, 180  // row 1
        };
        int[] tmpR = new int[w * h], tmpG = new int[w * h], tmpB = new int[w * h];

        KernelUtils.HorizontalBoxBlur(src, stride, pixBytes, tmpR, tmpG, tmpB,
                                      w, h, rIdx: 0, gIdx: 1, bIdx: 2, radius: 0);

        // radius=0 → 各ピクセルがそのままコピーされる
        Assert.Equal(10, tmpR[0]);
        Assert.Equal(20, tmpG[0]);
        Assert.Equal(30, tmpB[0]);
        Assert.Equal(130, tmpR[4]); // row1, x=1
        Assert.Equal(140, tmpG[4]);
        Assert.Equal(150, tmpB[4]);
    }

    [Fact]
    public void HorizontalBoxBlur_Radius1_AveragesNeighbors()
    {
        // 3x1 画像、均一 RGB
        int w = 3, h = 1, pixBytes = 3, stride = w * pixBytes;
        byte[] src = new byte[] { 30, 0, 0,  60, 0, 0,  90, 0, 0 };
        int[] tmpR = new int[w * h], tmpG = new int[w * h], tmpB = new int[w * h];

        KernelUtils.HorizontalBoxBlur(src, stride, pixBytes, tmpR, tmpG, tmpB,
                                      w, h, rIdx: 0, gIdx: 1, bIdx: 2, radius: 1);

        // x=0: avg(30,60) = 45
        Assert.Equal(45, tmpR[0]);
        // x=1: avg(30,60,90) = 60
        Assert.Equal(60, tmpR[1]);
        // x=2: avg(60,90) = 75
        Assert.Equal(75, tmpR[2]);
    }
}
