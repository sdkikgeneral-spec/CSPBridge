// カーネル間で共有されるユーティリティメソッド。
using System;
using System.Runtime.CompilerServices;

namespace CSPBridgeEffects.Effects.Kernels;

/// <summary>
/// カーネル間で共有されるユーティリティメソッドを提供します。
/// </summary>
internal static class KernelUtils
{
    /// <summary>dst と src を mask（0〜255）で線形補間します。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Blend8(int dst, int src, int mask) => ((dst - src) * mask / 255) + src;

    /// <summary>
    /// 水平方向の Box Blur を行い、中間バッファ（tmpR / tmpG / tmpB）に書き込みます（src → tmp）。
    /// BlurKernel・SharpenKernel の両カーネルから共有されます。
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
    internal static void HorizontalBoxBlur(
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
}
