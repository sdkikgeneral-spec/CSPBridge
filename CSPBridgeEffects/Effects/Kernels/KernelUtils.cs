// カーネル間で共有されるユーティリティメソッド。
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
}
