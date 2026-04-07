// Blur のピクセル処理カーネル。
// SDK 非依存で実装されており、xUnit でユニットテストできます。
using System;
using static CSPBridgeEffects.Effects.Kernels.KernelUtils;

namespace CSPBridgeEffects.Effects.Kernels;

/// <summary>
/// セパラブル Box Blur のピクセル処理カーネル。
/// SDK ポインタ型への依存を持たず、<see cref="ReadOnlySpan{T}"/> / <see cref="Span{T}"/> のみを引数に取ります。
/// </summary>
internal static class BlurKernel
{
    /// <summary>
    /// 水平方向の Box Blur を行い、中間バッファ（tmpR / tmpG / tmpB）に書き込みます（src → tmp）。
    /// </summary>
    /// <param name="src">ソース画像バッファ（行優先）。</param>
    /// <param name="srcStride">ソースの行バイト数。</param>
    /// <param name="srcPixBytes">ソースの 1 ピクセルバイト数。</param>
    /// <param name="tmpR">水平 Blur 後の R 値の中間バッファ（w*h 要素）。</param>
    /// <param name="tmpG">水平 Blur 後の G 値の中間バッファ（w*h 要素）。</param>
    /// <param name="tmpB">水平 Blur 後の B 値の中間バッファ（w*h 要素）。</param>
    /// <param name="w">ブロック幅。</param>
    /// <param name="h">ブロック高さ。</param>
    /// <param name="rIdx">R チャンネルのバイトオフセット。</param>
    /// <param name="gIdx">G チャンネルのバイトオフセット。</param>
    /// <param name="bIdx">B チャンネルのバイトオフセット。</param>
    /// <param name="radius">ぼかし半径（ピクセル）。</param>
    internal static void HorizontalPass(
        ReadOnlySpan<byte> src, int srcStride, int srcPixBytes,
        Span<int> tmpR, Span<int> tmpG, Span<int> tmpB,
        int w, int h, int rIdx, int gIdx, int bIdx, int radius)
    {
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int sumR = 0, sumG = 0, sumB = 0;
                int x0  = x - radius >= 0 ? x - radius : 0;
                int x1  = x + radius <  w ? x + radius : w - 1;
                int cnt = x1 - x0 + 1;
                for (int nx = x0; nx <= x1; nx++)
                {
                    int offset = y * srcStride + nx * srcPixBytes;
                    sumR += src[offset + rIdx];
                    sumG += src[offset + gIdx];
                    sumB += src[offset + bIdx];
                }
                int i = y * w + x;
                tmpR[i] = sumR / cnt;
                tmpG[i] = sumG / cnt;
                tmpB[i] = sumB / cnt;
            }
        }
    }

    /// <summary>
    /// 垂直方向の Box Blur を行い、デスティネーションバッファに書き込みます（tmp → dst）。
    /// 選択範囲なしのケースで使用します。アルファ 0 のピクセルはスキップします。
    /// </summary>
    /// <param name="dst">書き込み先画像バッファ（行優先）。</param>
    /// <param name="dstStride">デスティネーションの行バイト数。</param>
    /// <param name="dstPixBytes">デスティネーションの 1 ピクセルバイト数。</param>
    /// <param name="alpha">アルファバッファ（行優先）。</param>
    /// <param name="alphaStride">アルファの行バイト数。</param>
    /// <param name="alphaPixBytes">アルファの 1 ピクセルバイト数。</param>
    /// <param name="tmpR">水平 Blur 後の R 中間バッファ。</param>
    /// <param name="tmpG">水平 Blur 後の G 中間バッファ。</param>
    /// <param name="tmpB">水平 Blur 後の B 中間バッファ。</param>
    /// <param name="w">ブロック幅。</param>
    /// <param name="h">ブロック高さ。</param>
    /// <param name="rIdx">R チャンネルのバイトオフセット。</param>
    /// <param name="gIdx">G チャンネルのバイトオフセット。</param>
    /// <param name="bIdx">B チャンネルのバイトオフセット。</param>
    /// <param name="radius">ぼかし半径（ピクセル）。</param>
    internal static void VerticalPass(
        Span<byte> dst, int dstStride, int dstPixBytes,
        ReadOnlySpan<byte> alpha, int alphaStride, int alphaPixBytes,
        ReadOnlySpan<int> tmpR, ReadOnlySpan<int> tmpG, ReadOnlySpan<int> tmpB,
        int w, int h, int rIdx, int gIdx, int bIdx, int radius)
    {
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (alpha[y * alphaStride + x * alphaPixBytes] == 0) continue;

                int sumR = 0, sumG = 0, sumB = 0;
                int y0  = y - radius >= 0 ? y - radius : 0;
                int y1  = y + radius <  h ? y + radius : h - 1;
                int cnt = y1 - y0 + 1;
                for (int ny = y0; ny <= y1; ny++)
                {
                    int i = ny * w + x;
                    sumR += tmpR[i]; sumG += tmpG[i]; sumB += tmpB[i];
                }
                int offset = y * dstStride + x * dstPixBytes;
                dst[offset + rIdx] = (byte)(sumR / cnt);
                dst[offset + gIdx] = (byte)(sumG / cnt);
                dst[offset + bIdx] = (byte)(sumB / cnt);
            }
        }
    }

    /// <summary>
    /// 垂直方向の Box Blur を行い、デスティネーションバッファに書き込みます（tmp → dst）。
    /// 選択範囲付きのケースで使用します。選択値に応じてマスク合成します。
    /// </summary>
    /// <param name="dst">書き込み先画像バッファ（行優先）。</param>
    /// <param name="dstStride">デスティネーションの行バイト数。</param>
    /// <param name="dstPixBytes">デスティネーションの 1 ピクセルバイト数。</param>
    /// <param name="src">ソース画像バッファ（行優先）。マスク合成時の元色参照に使用。</param>
    /// <param name="srcStride">ソースの行バイト数。</param>
    /// <param name="srcPixBytes">ソースの 1 ピクセルバイト数。</param>
    /// <param name="alpha">アルファバッファ（行優先）。</param>
    /// <param name="alphaStride">アルファの行バイト数。</param>
    /// <param name="alphaPixBytes">アルファの 1 ピクセルバイト数。</param>
    /// <param name="selection">選択範囲バッファ（行優先）。0=未選択、255=全選択、1〜254=部分選択。</param>
    /// <param name="selStride">選択範囲の行バイト数。</param>
    /// <param name="selPixBytes">選択範囲の 1 ピクセルバイト数。</param>
    /// <param name="tmpR">水平 Blur 後の R 中間バッファ。</param>
    /// <param name="tmpG">水平 Blur 後の G 中間バッファ。</param>
    /// <param name="tmpB">水平 Blur 後の B 中間バッファ。</param>
    /// <param name="w">ブロック幅。</param>
    /// <param name="h">ブロック高さ。</param>
    /// <param name="rIdx">R チャンネルのバイトオフセット。</param>
    /// <param name="gIdx">G チャンネルのバイトオフセット。</param>
    /// <param name="bIdx">B チャンネルのバイトオフセット。</param>
    /// <param name="radius">ぼかし半径（ピクセル）。</param>
    internal static void VerticalPassWithSelection(
        Span<byte> dst, int dstStride, int dstPixBytes,
        ReadOnlySpan<byte> src, int srcStride, int srcPixBytes,
        ReadOnlySpan<byte> alpha, int alphaStride, int alphaPixBytes,
        ReadOnlySpan<byte> selection, int selStride, int selPixBytes,
        ReadOnlySpan<int> tmpR, ReadOnlySpan<int> tmpG, ReadOnlySpan<int> tmpB,
        int w, int h, int rIdx, int gIdx, int bIdx, int radius)
    {
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (alpha[y * alphaStride + x * alphaPixBytes] == 0) continue;

                byte selVal = selection[y * selStride + x * selPixBytes];
                if (selVal == 0) continue;

                int sumR = 0, sumG = 0, sumB = 0;
                int y0  = y - radius >= 0 ? y - radius : 0;
                int y1  = y + radius <  h ? y + radius : h - 1;
                int cnt = y1 - y0 + 1;
                for (int ny = y0; ny <= y1; ny++)
                {
                    int i = ny * w + x;
                    sumR += tmpR[i]; sumG += tmpG[i]; sumB += tmpB[i];
                }
                int blurR = sumR / cnt, blurG = sumG / cnt, blurB = sumB / cnt;

                int dstOffset = y * dstStride + x * dstPixBytes;
                int srcOffset = y * srcStride + x * srcPixBytes;
                if (selVal == 255)
                {
                    dst[dstOffset + rIdx] = (byte)blurR;
                    dst[dstOffset + gIdx] = (byte)blurG;
                    dst[dstOffset + bIdx] = (byte)blurB;
                }
                else
                {
                    dst[dstOffset + rIdx] = (byte)Blend8(blurR, src[srcOffset + rIdx], selVal);
                    dst[dstOffset + gIdx] = (byte)Blend8(blurG, src[srcOffset + gIdx], selVal);
                    dst[dstOffset + bIdx] = (byte)Blend8(blurB, src[srcOffset + bIdx], selVal);
                }
            }
        }
    }

}
