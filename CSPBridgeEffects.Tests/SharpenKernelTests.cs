// SharpenKernel のユニットテスト。
using CSPBridgeEffects.Effects.Kernels;
using Xunit;

namespace CSPBridgeEffects.Tests;

/// <summary>
/// <see cref="SharpenKernel"/> のテストクラス。
/// </summary>
public class SharpenKernelTests
{
    [Theory]
    [InlineData(128, 128, 100, 128)]  // src == blur → 変化なし
    [InlineData(200, 100, 100, 255)]  // src > blur → 強調（クランプ 255）
    [InlineData(50, 150, 100, 0)]     // src < blur → 暗く（クランプ 0）
    [InlineData(128, 100, 50, 142)]   // 中間的な強調
    [InlineData(128, 128, 0, 128)]    // strength=0 → 変化なし
    public void ApplyUnsharp_ReturnsExpectedValue(int src, int blur, int strength, int expected)
    {
        int result = SharpenKernel.ApplyUnsharp(src, blur, strength);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ApplyUnsharp_ClampsToValidRange()
    {
        // 最大強調
        Assert.InRange(SharpenKernel.ApplyUnsharp(255, 0, 200), 0, 255);
        // 最大減衰
        Assert.InRange(SharpenKernel.ApplyUnsharp(0, 255, 200), 0, 255);
    }

    [Fact]
    public void VerticalBlur_SinglePixel_ReturnsSameValue()
    {
        int[] tmp = { 42 };
        int result = SharpenKernel.VerticalBlur(tmp, x: 0, y: 0, w: 1, h: 1, radius: 5);
        Assert.Equal(42, result);
    }

    [Fact]
    public void VerticalBlur_Radius1_AveragesCorrectly()
    {
        // 1x3 列: [30, 60, 90]
        int[] tmp = { 30, 60, 90 };
        // y=1, radius=1: avg(30,60,90) = 60
        int result = SharpenKernel.VerticalBlur(tmp, x: 0, y: 1, w: 1, h: 3, radius: 1);
        Assert.Equal(60, result);
    }
}
