// HSV のピクセル処理カーネル。
// SDK 非依存で実装されており、xUnit でユニットテストできます。
using System;
using System.Runtime.CompilerServices;
using static CSPBridgeEffects.Effects.Kernels.KernelUtils;

namespace CSPBridgeEffects.Effects.Kernels;

/// <summary>
/// HSV 色変換のピクセル処理カーネル（PIHSVFilter.h の C# 移植）。
/// SDK ポインタ型への依存を持たず、<see cref="Span{T}"/> のみを引数に取ります。
/// <para>H: 0〜6*32768, S: 0〜65536, V: 0〜255 の内部スケールを使用します。</para>
/// </summary>
internal static class HsvKernel
{
    /// <summary>
    /// HSV 調整を適用します（選択範囲なし）。
    /// アルファ 0 のピクセルはスキップします。
    /// </summary>
    /// <param name="dst">書き込み先画像バッファ（行優先、インプレース）。</param>
    /// <param name="dstStride">デスティネーションの行バイト数。</param>
    /// <param name="dstPixBytes">デスティネーションの 1 ピクセルバイト数。</param>
    /// <param name="alpha">アルファバッファ（行優先）。</param>
    /// <param name="alphaStride">アルファの行バイト数。</param>
    /// <param name="alphaPixBytes">アルファの 1 ピクセルバイト数。</param>
    /// <param name="w">ブロック幅。</param>
    /// <param name="h">ブロック高さ。</param>
    /// <param name="rIdx">R チャンネルのバイトオフセット。</param>
    /// <param name="gIdx">G チャンネルのバイトオフセット。</param>
    /// <param name="bIdx">B チャンネルのバイトオフセット。</param>
    /// <param name="hFilter">色相フィルタ値（内部スケール: 0〜6*32768）。</param>
    /// <param name="sFilter">彩度フィルタ値（内部スケール: -32768〜32768）。</param>
    /// <param name="vFilter">明度フィルタ値（内部スケール: -32768〜32768）。</param>
    internal static void Process(
        Span<byte> dst, int dstStride, int dstPixBytes,
        ReadOnlySpan<byte> alpha, int alphaStride, int alphaPixBytes,
        int w, int h, int rIdx, int gIdx, int bIdx,
        int hFilter, int sFilter, int vFilter)
    {
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (alpha[y * alphaStride + x * alphaPixBytes] == 0) continue;

                int offset = y * dstStride + x * dstPixBytes;
                ApplyHsv(dst, offset, rIdx, gIdx, bIdx, hFilter, sFilter, vFilter);
            }
        }
    }

    /// <summary>
    /// HSV 調整を適用します（選択範囲あり）。
    /// アルファ 0 のピクセルはスキップします。選択値に応じてマスク合成します。
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
    /// <param name="rIdx">R チャンネルのバイトオフセット。</param>
    /// <param name="gIdx">G チャンネルのバイトオフセット。</param>
    /// <param name="bIdx">B チャンネルのバイトオフセット。</param>
    /// <param name="hFilter">色相フィルタ値（内部スケール: 0〜6*32768）。</param>
    /// <param name="sFilter">彩度フィルタ値（内部スケール: -32768〜32768）。</param>
    /// <param name="vFilter">明度フィルタ値（内部スケール: -32768〜32768）。</param>
    internal static void ProcessWithSelection(
        Span<byte> dst, int dstStride, int dstPixBytes,
        ReadOnlySpan<byte> alpha, int alphaStride, int alphaPixBytes,
        ReadOnlySpan<byte> selection, int selStride, int selPixBytes,
        int w, int h, int rIdx, int gIdx, int bIdx,
        int hFilter, int sFilter, int vFilter)
    {
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (alpha[y * alphaStride + x * alphaPixBytes] == 0) continue;

                byte selVal = selection[y * selStride + x * selPixBytes];
                if (selVal == 0) continue;

                int offset = y * dstStride + x * dstPixBytes;
                if (selVal == 255)
                {
                    ApplyHsv(dst, offset, rIdx, gIdx, bIdx, hFilter, sFilter, vFilter);
                }
                else
                {
                    ApplyHsvMask(dst, offset, rIdx, gIdx, bIdx, selVal, hFilter, sFilter, vFilter);
                }
            }
        }
    }

    // ================================================================
    // 内部ヘルパー — HSV 変換演算
    // ================================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ApplyHsv(Span<byte> buf, int offset,
        int r, int g, int b, int hFilter, int sFilter, int vFilter)
    {
        RgbToHsv(out uint uH, out uint uS, out uint uV, buf[offset + r], buf[offset + g], buf[offset + b]);
        HsvFilter(ref uH, ref uS, ref uV, hFilter, sFilter, vFilter);
        HsvToRgb(out uint uR, out uint uG, out uint uB, uH, uS, uV);
        buf[offset + r] = (byte)uR;
        buf[offset + g] = (byte)uG;
        buf[offset + b] = (byte)uB;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ApplyHsvMask(Span<byte> buf, int offset,
        int r, int g, int b, byte mask, int hFilter, int sFilter, int vFilter)
    {
        RgbToHsv(out uint uH, out uint uS, out uint uV, buf[offset + r], buf[offset + g], buf[offset + b]);
        HsvFilter(ref uH, ref uS, ref uV, hFilter, sFilter, vFilter);
        HsvToRgb(out uint uR, out uint uG, out uint uB, uH, uS, uV);
        buf[offset + r] = (byte)Blend8((int)uR, buf[offset + r], mask);
        buf[offset + g] = (byte)Blend8((int)uG, buf[offset + g], mask);
        buf[offset + b] = (byte)Blend8((int)uB, buf[offset + b], mask);
    }

    /// <summary>
    /// RGB (各 0〜255) を HSV に変換します。
    /// H: 0〜6*32768, S: 0〜65536, V: 0〜255
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RgbToHsv(out uint ruH, out uint ruS, out uint ruV,
        uint r, uint g, uint b)
    {
        uint uMax, uMin;
        if (r >= g && r >= b)
        {
            uMax = r;
            uMin = g >= b ? b : g;
        }
        else if (g >= b)
        {
            uMax = g;
            uMin = r >= b ? b : r;
        }
        else
        {
            uMax = b;
            uMin = r >= g ? g : r;
        }

        uint uD = uMax - uMin;
        uint uS = uMax == 0u ? 0u : (uD << 16) / uMax;

        int nH;
        if (uS == 0u)
        {
            nH = 0;
        }
        else if (uMax == r)
        {
            nH = GetH(g, b, uD);
            if (nH < 0) nH += 6 * 32768;
        }
        else if (uMax == g)
        {
            nH = GetH(b, r, uD) + 2 * 32768;
        }
        else
        {
            nH = GetH(r, g, uD) + 4 * 32768;
        }

        ruH = (uint)nH;
        ruS = uS;
        ruV = uMax;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetH(uint c1, uint c2, uint d)
        => c1 >= c2
            ?  (int)((c1 - c2) << 15) / (int)d
            : -((int)((c2 - c1) << 15) / (int)d);

    /// <summary>
    /// HSV (H: 0〜6*32768, S: 0〜65536, V: 0〜255) を RGB に変換します。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void HsvToRgb(out uint ruR, out uint ruG, out uint ruB,
        uint h, uint s, uint v)
    {
        uint f = h & 32767u;
        switch (h >> 15)
        {
            case 0:  ruR = v;            ruG = GetP3(v,s,f); ruB = GetP1(v,s); break;
            case 1:  ruR = GetP2(v,s,f); ruG = v;            ruB = GetP1(v,s); break;
            case 2:  ruR = GetP1(v,s);   ruG = v;            ruB = GetP3(v,s,f); break;
            case 3:  ruR = GetP1(v,s);   ruG = GetP2(v,s,f); ruB = v;            break;
            case 4:  ruR = GetP3(v,s,f); ruG = GetP1(v,s);   ruB = v;            break;
            default: ruR = v;            ruG = GetP1(v,s);   ruB = GetP2(v,s,f); break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint GetP1(uint v, uint s) => (v * (65536u - s)) >> 16;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint GetP2(uint v, uint s, uint f) => (v * (65536u - ((s * f) >> 15))) >> 16;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint GetP3(uint v, uint s, uint f) => (v * (65536u - ((s * (32768u - f)) >> 15))) >> 16;

    /// <summary>
    /// HSV フィルタ調整値を H/S/V に適用します（PIHSVFilter::HSVFilter の移植）。
    /// hFilter: 0〜6*32768, sFilter/vFilter: -32768〜32768
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void HsvFilter(ref uint ruH, ref uint ruS, ref uint ruV,
        int hFilter, int sFilter, int vFilter)
    {
        const int HMax = 6 * 32768;
        const int SMax = 65536;
        const int VMax = 255;

        int nH = (int)ruH;
        int nS = (int)ruS;
        int nV = (int)ruV;

        if (hFilter != 0)
        {
            nH += hFilter;
            if (nH >= HMax) nH -= HMax;
            else if (nH < 0) nH += HMax;
        }

        if (vFilter != 0)
        {
            if (vFilter > 0)
            {
                nV += (int)(((uint)(VMax - nV) * (uint)vFilter) >> 15);
                if (nV > VMax) nV = VMax;
                // 明度を上げると白に近づくため、彩度を比例的に下げる（PIHSVFilter.h 移植仕様）
                nS -= (int)(((uint)nS * (uint)vFilter) >> 15);
                if (nS < 0) nS = 0;
            }
            else
            {
                nV -= (int)(((uint)nV * (uint)(-vFilter)) >> 15);
                if (nV < 0) nV = 0;
            }
        }

        if (sFilter != 0 && nS > 0 && nV > 0)
        {
            if (sFilter > 0)
            {
                int nSat = (int)(((uint)(SMax - nS) * (uint)sFilter) >> 15);
                int nVal = (int)(((uint)nV * (uint)nSat) >> 16);
                int nV2  = nV;
                nV += nVal;
                if (nV > VMax)
                {
                    nV  = VMax;
                    if (nVal > 0)
                        nS += (int)((uint)nSat * (uint)(nV - nV2) / (uint)nVal);
                }
                else
                {
                    nS += nSat;
                }
                if (nS > SMax) nS = SMax;
            }
            else
            {
                int nS2 = nS;
                nS -= (int)(((uint)nS * (uint)(-sFilter)) >> 15);
                if (nS < 0) nS = 0;
                nV -= (int)(((uint)(nS2 - nS) * (uint)nV) >> 17);
                if (nV < 0) nV = 0;
            }
        }

        ruH = (uint)nH;
        ruS = (uint)nS;
        ruV = (uint)nV;
    }

}
