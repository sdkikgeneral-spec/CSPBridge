// BlurKernel のユニットテスト。
using CSPBridgeEffects.Effects.Kernels;
using Xunit;

namespace CSPBridgeEffects.Tests;

/// <summary>
/// <see cref="BlurKernel"/> のテストクラス。
/// </summary>
public class BlurKernelTests
{
    [Fact]
    public void VerticalPass_Radius0_CopiesTmpToDst()
    {
        // 2x2 画像、pixBytes=4 (BGRA)
        int w = 2, h = 2, dstPixBytes = 4, dstStride = w * dstPixBytes;
        int alphaPixBytes = 1, alphaStride = w * alphaPixBytes;

        byte[] dst = new byte[dstStride * h];
        byte[] alpha = { 255, 255, 255, 255 }; // すべて不透明
        int[] tmpR = { 10, 20, 30, 40 };
        int[] tmpG = { 50, 60, 70, 80 };
        int[] tmpB = { 90, 100, 110, 120 };

        BlurKernel.VerticalPass(dst, dstStride, dstPixBytes,
                                alpha, alphaStride, alphaPixBytes,
                                tmpR, tmpG, tmpB,
                                w, h, rIdx: 2, gIdx: 1, bIdx: 0, radius: 0);

        // (0,0): R=10, G=50, B=90
        Assert.Equal(10, dst[0 * dstStride + 0 * dstPixBytes + 2]); // R
        Assert.Equal(50, dst[0 * dstStride + 0 * dstPixBytes + 1]); // G
        Assert.Equal(90, dst[0 * dstStride + 0 * dstPixBytes + 0]); // B
    }

    [Fact]
    public void VerticalPass_SkipsTransparentPixels()
    {
        int w = 2, h = 1, dstPixBytes = 3, dstStride = w * dstPixBytes;
        int alphaPixBytes = 1, alphaStride = w;

        byte[] dst = new byte[dstStride * h];
        byte[] alpha = { 0, 255 }; // 左は透明、右は不透明
        int[] tmpR = { 100, 200 };
        int[] tmpG = { 100, 200 };
        int[] tmpB = { 100, 200 };

        BlurKernel.VerticalPass(dst, dstStride, dstPixBytes,
                                alpha, alphaStride, alphaPixBytes,
                                tmpR, tmpG, tmpB,
                                w, h, rIdx: 0, gIdx: 1, bIdx: 2, radius: 0);

        // 透明ピクセルはスキップ（ゼロのまま）
        Assert.Equal(0, dst[0]);
        Assert.Equal(0, dst[1]);
        Assert.Equal(0, dst[2]);
        // 不透明ピクセルは書き込み
        Assert.Equal(200, dst[3]);
        Assert.Equal(200, dst[4]);
        Assert.Equal(200, dst[5]);
    }

    [Fact]
    public void VerticalPass_Radius1_AveragesVertically()
    {
        // 1x3 画像
        int w = 1, h = 3, dstPixBytes = 3, dstStride = w * dstPixBytes;
        int alphaPixBytes = 1, alphaStride = w;

        byte[] dst = new byte[dstStride * h];
        byte[] alpha = { 255, 255, 255 };
        int[] tmpR = { 30, 60, 90 };
        int[] tmpG = { 0, 0, 0 };
        int[] tmpB = { 0, 0, 0 };

        BlurKernel.VerticalPass(dst, dstStride, dstPixBytes,
                                alpha, alphaStride, alphaPixBytes,
                                tmpR, tmpG, tmpB,
                                w, h, rIdx: 0, gIdx: 1, bIdx: 2, radius: 1);

        // y=0: avg(30,60) = 45
        Assert.Equal(45, dst[0 * dstStride]);
        // y=1: avg(30,60,90) = 60
        Assert.Equal(60, dst[1 * dstStride]);
        // y=2: avg(60,90) = 75
        Assert.Equal(75, dst[2 * dstStride]);
    }
}
