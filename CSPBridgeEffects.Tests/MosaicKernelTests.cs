// MosaicKernel のユニットテスト。
using CSPBridgeEffects.Effects.Kernels;
using Xunit;

namespace CSPBridgeEffects.Tests;

/// <summary>
/// <see cref="MosaicKernel"/> のテストクラス。
/// </summary>
public class MosaicKernelTests
{
    [Fact]
    public void Process_SingleCell_FillsWithAverage()
    {
        // 2x2 画像、cellSize=2 → 全体が 1 セル
        int w = 2, h = 2, pixBytes = 3, stride = w * pixBytes;
        byte[] dst = new byte[]
        {
            10, 20, 30,   50, 60, 70,     // row 0
            90, 100, 110, 130, 140, 150    // row 1
        };
        byte[] alpha = { 255, 255, 255, 255 };

        MosaicKernel.Process(dst, stride, pixBytes,
                             alpha, alphaStride: w, alphaPixBytes: 1,
                             w, h,
                             blockLeft: 0, blockTop: 0, blockRight: 2, blockBottom: 2,
                             rIdx: 0, gIdx: 1, bIdx: 2, cellSize: 2);

        // 平均: R=(10+50+90+130)/4=70, G=(20+60+100+140)/4=80, B=(30+70+110+150)/4=90
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int offset = y * stride + x * pixBytes;
                Assert.Equal(70, dst[offset + 0]);  // R
                Assert.Equal(80, dst[offset + 1]);   // G
                Assert.Equal(90, dst[offset + 2]);   // B
            }
        }
    }

    [Fact]
    public void Process_SkipsTransparentPixels()
    {
        // 2x1 画像、左が透明
        int w = 2, h = 1, pixBytes = 3, stride = w * pixBytes;
        byte[] dst = new byte[] { 100, 100, 100, 200, 200, 200 };
        byte[] alpha = { 0, 255 };

        MosaicKernel.Process(dst, stride, pixBytes,
                             alpha, alphaStride: w, alphaPixBytes: 1,
                             w, h,
                             blockLeft: 0, blockTop: 0, blockRight: 2, blockBottom: 1,
                             rIdx: 0, gIdx: 1, bIdx: 2, cellSize: 2);

        // 透明ピクセルは集計にも含まれないので、平均は不透明ピクセルの値のみ
        Assert.Equal(100, dst[0]); // 透明 → 変更なし
        Assert.Equal(200, dst[3]); // 不透明 → 平均 = 自身の値
    }

    [Fact]
    public void Process_MultipleCells_ProcessesSeparately()
    {
        // 4x1 画像、cellSize=2 → 2 セル
        int w = 4, h = 1, pixBytes = 3, stride = w * pixBytes;
        byte[] dst = new byte[]
        {
            10, 0, 0,  30, 0, 0,  100, 0, 0,  200, 0, 0
        };
        byte[] alpha = { 255, 255, 255, 255 };

        MosaicKernel.Process(dst, stride, pixBytes,
                             alpha, alphaStride: w, alphaPixBytes: 1,
                             w, h,
                             blockLeft: 0, blockTop: 0, blockRight: 4, blockBottom: 1,
                             rIdx: 0, gIdx: 1, bIdx: 2, cellSize: 2);

        // セル 0: avg(10,30)=20
        Assert.Equal(20, dst[0 * pixBytes]);
        Assert.Equal(20, dst[1 * pixBytes]);
        // セル 1: avg(100,200)=150
        Assert.Equal(150, dst[2 * pixBytes]);
        Assert.Equal(150, dst[3 * pixBytes]);
    }
}
