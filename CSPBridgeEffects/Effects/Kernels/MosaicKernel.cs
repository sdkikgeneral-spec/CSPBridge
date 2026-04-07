// Mosaic のピクセル処理カーネル。
// SDK 非依存で実装されており、xUnit でユニットテストできます。
using System;
using static CSPBridgeEffects.Effects.Kernels.KernelUtils;

namespace CSPBridgeEffects.Effects.Kernels;

/// <summary>
/// モザイク（ピクセル化）のピクセル処理カーネル。
/// SDK ポインタ型への依存を持たず、<see cref="ReadOnlySpan{T}"/> / <see cref="Span{T}"/> のみを引数に取ります。
/// セルグリッドはグローバル座標に整列させるため、ブロック境界をまたいでも継ぎ目が出ません。
/// </summary>
internal static class MosaicKernel
{
    /// <summary>
    /// モザイク処理を行います（選択範囲なし）。
    /// セル単位で非透明ピクセルの平均色を計算し、セル内を塗り潰します。
    /// </summary>
    /// <param name="dst">書き込み先画像バッファ（行優先、インプレース）。</param>
    /// <param name="dstStride">デスティネーションの行バイト数。</param>
    /// <param name="dstPixBytes">デスティネーションの 1 ピクセルバイト数。</param>
    /// <param name="alpha">アルファバッファ（行優先）。</param>
    /// <param name="alphaStride">アルファの行バイト数。</param>
    /// <param name="alphaPixBytes">アルファの 1 ピクセルバイト数。</param>
    /// <param name="w">ブロック幅。</param>
    /// <param name="h">ブロック高さ。</param>
    /// <param name="blockLeft">ブロック左端のグローバル X 座標。</param>
    /// <param name="blockTop">ブロック上端のグローバル Y 座標。</param>
    /// <param name="blockRight">ブロック右端のグローバル X 座標（exclusive）。</param>
    /// <param name="blockBottom">ブロック下端のグローバル Y 座標（exclusive）。</param>
    /// <param name="rIdx">R チャンネルのバイトオフセット。</param>
    /// <param name="gIdx">G チャンネルのバイトオフセット。</param>
    /// <param name="bIdx">B チャンネルのバイトオフセット。</param>
    /// <param name="cellSize">モザイクセルの一辺の大きさ（ピクセル）。</param>
    internal static void Process(
        Span<byte> dst, int dstStride, int dstPixBytes,
        ReadOnlySpan<byte> alpha, int alphaStride, int alphaPixBytes,
        int w, int h,
        int blockLeft, int blockTop, int blockRight, int blockBottom,
        int rIdx, int gIdx, int bIdx, int cellSize)
    {
        int cellLeft = blockLeft / cellSize * cellSize;
        int cellTop  = blockTop  / cellSize * cellSize;

        for (int cy = cellTop; cy < blockBottom; cy += cellSize)
        {
            for (int cx = cellLeft; cx < blockRight; cx += cellSize)
            {
                int lx0 = (cx > blockLeft  ? cx : blockLeft)  - blockLeft;
                int ly0 = (cy > blockTop   ? cy : blockTop)   - blockTop;
                int lx1 = (cx + cellSize < blockRight  ? cx + cellSize : blockRight)  - blockLeft;
                int ly1 = (cy + cellSize < blockBottom ? cy + cellSize : blockBottom) - blockTop;

                long sumR = 0, sumG = 0, sumB = 0;
                int  cnt  = 0;
                for (int ly = ly0; ly < ly1; ly++)
                {
                    for (int lx = lx0; lx < lx1; lx++)
                    {
                        if (alpha[ly * alphaStride + lx * alphaPixBytes] == 0) continue;
                        int offset = ly * dstStride + lx * dstPixBytes;
                        sumR += dst[offset + rIdx];
                        sumG += dst[offset + gIdx];
                        sumB += dst[offset + bIdx];
                        cnt++;
                    }
                }
                if (cnt == 0) continue;

                byte avgR = (byte)(sumR / cnt);
                byte avgG = (byte)(sumG / cnt);
                byte avgB = (byte)(sumB / cnt);

                for (int ly = ly0; ly < ly1; ly++)
                {
                    for (int lx = lx0; lx < lx1; lx++)
                    {
                        if (alpha[ly * alphaStride + lx * alphaPixBytes] == 0) continue;
                        int offset = ly * dstStride + lx * dstPixBytes;
                        dst[offset + rIdx] = avgR;
                        dst[offset + gIdx] = avgG;
                        dst[offset + bIdx] = avgB;
                    }
                }
            }
        }
    }

    /// <summary>
    /// モザイク処理を行います（選択範囲あり）。
    /// セル単位で非透明ピクセルの平均色を計算し、選択値に応じてマスク合成します。
    /// </summary>
    /// <param name="dst">書き込み先画像バッファ（行優先、インプレース）。</param>
    /// <param name="dstStride">デスティネーションの行バイト数。</param>
    /// <param name="dstPixBytes">デスティネーションの 1 ピクセルバイト数。</param>
    /// <param name="alpha">アルファバッファ（行優先）。</param>
    /// <param name="alphaStride">アルファの行バイト数。</param>
    /// <param name="alphaPixBytes">アルファの 1 ピクセルバイト数。</param>
    /// <param name="selection">選択範囲バッファ（行優先）。0=未選択、255=全選択、1〜254=部分選択。</param>
    /// <param name="selStride">選択範囲の行バイト数。</param>
    /// <param name="selPixBytes">選択範囲の 1 ピクセルバイト数。</param>
    /// <param name="w">ブロック幅。</param>
    /// <param name="h">ブロック高さ。</param>
    /// <param name="blockLeft">ブロック左端のグローバル X 座標。</param>
    /// <param name="blockTop">ブロック上端のグローバル Y 座標。</param>
    /// <param name="blockRight">ブロック右端のグローバル X 座標（exclusive）。</param>
    /// <param name="blockBottom">ブロック下端のグローバル Y 座標（exclusive）。</param>
    /// <param name="rIdx">R チャンネルのバイトオフセット。</param>
    /// <param name="gIdx">G チャンネルのバイトオフセット。</param>
    /// <param name="bIdx">B チャンネルのバイトオフセット。</param>
    /// <param name="cellSize">モザイクセルの一辺の大きさ（ピクセル）。</param>
    internal static void ProcessWithSelection(
        Span<byte> dst, int dstStride, int dstPixBytes,
        ReadOnlySpan<byte> alpha, int alphaStride, int alphaPixBytes,
        ReadOnlySpan<byte> selection, int selStride, int selPixBytes,
        int w, int h,
        int blockLeft, int blockTop, int blockRight, int blockBottom,
        int rIdx, int gIdx, int bIdx, int cellSize)
    {
        int cellLeft = blockLeft / cellSize * cellSize;
        int cellTop  = blockTop  / cellSize * cellSize;

        for (int cy = cellTop; cy < blockBottom; cy += cellSize)
        {
            for (int cx = cellLeft; cx < blockRight; cx += cellSize)
            {
                int lx0 = (cx > blockLeft  ? cx : blockLeft)  - blockLeft;
                int ly0 = (cy > blockTop   ? cy : blockTop)   - blockTop;
                int lx1 = (cx + cellSize < blockRight  ? cx + cellSize : blockRight)  - blockLeft;
                int ly1 = (cy + cellSize < blockBottom ? cy + cellSize : blockBottom) - blockTop;

                // 平均色は選択範囲を問わず全非透明ピクセルで集計する
                long sumR = 0, sumG = 0, sumB = 0;
                int  cnt  = 0;
                for (int ly = ly0; ly < ly1; ly++)
                {
                    for (int lx = lx0; lx < lx1; lx++)
                    {
                        if (alpha[ly * alphaStride + lx * alphaPixBytes] == 0) continue;
                        int offset = ly * dstStride + lx * dstPixBytes;
                        sumR += dst[offset + rIdx];
                        sumG += dst[offset + gIdx];
                        sumB += dst[offset + bIdx];
                        cnt++;
                    }
                }
                if (cnt == 0) continue;

                byte avgR = (byte)(sumR / cnt);
                byte avgG = (byte)(sumG / cnt);
                byte avgB = (byte)(sumB / cnt);

                for (int ly = ly0; ly < ly1; ly++)
                {
                    for (int lx = lx0; lx < lx1; lx++)
                    {
                        if (alpha[ly * alphaStride + lx * alphaPixBytes] == 0) continue;

                        byte selVal = selection[ly * selStride + lx * selPixBytes];
                        if (selVal == 0) continue;

                        int offset = ly * dstStride + lx * dstPixBytes;
                        if (selVal == 255)
                        {
                            dst[offset + rIdx] = avgR;
                            dst[offset + gIdx] = avgG;
                            dst[offset + bIdx] = avgB;
                        }
                        else
                        {
                            dst[offset + rIdx] = (byte)Blend8(avgR, dst[offset + rIdx], selVal);
                            dst[offset + gIdx] = (byte)Blend8(avgG, dst[offset + gIdx], selVal);
                            dst[offset + bIdx] = (byte)Blend8(avgB, dst[offset + bIdx], selVal);
                        }
                    }
                }
            }
        }
    }

}
