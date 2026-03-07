// セパラブル Box Blur フィルタ実装
// EffectTemplate.cs.in と同じ 4 エントリポイント構造で実装しています。
using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CSPBridgeEffects.Library.SDK;
using static CSPBridgeEffects.Library.SDK.CSPBridgeEffectsLibDefine;
using static CSPBridgeEffects.Library.SDK.CSPBridgeEffectsLibRecordFunction;

namespace CSPBridgeEffects.Effects;

/// <summary>
/// Blur フィルタ。セパラブル Box Blur（水平パス → 垂直パス）で滑らかなぼかしを実現します。
/// ソースオフスクリーンから読み取り、デスティネーションオフスクリーンに書き込むため
/// ブロック境界をまたいでも元データが壊れません。
/// </summary>
public static unsafe class Blur
{
    private const int ItemKeyRadius = 1;

    private static readonly int[] s_targetKinds =
    [
        kTriglavPlugInFilterTargetKindRasterLayerRGBAlpha,
        kTriglavPlugInFilterTargetKindRasterLayerGrayAlpha,
    ];

    // ================================================================
    // エントリポイント
    // ================================================================

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    public static int ModuleInitialize(TriglavPlugInServer* pluginServer)
        => EffectHelper.InitializeModule(pluginServer, "com.example.cspbridge.blur");

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    public static int FilterInitialize(TriglavPlugInServer* pluginServer, void** data)
    {
        int rc = EffectHelper.InitializeFilter(pluginServer, BlurMeta.Category, BlurMeta.FilterName, s_targetKinds);
        if (rc != kTriglavPlugInCallResultSuccess)
            return rc;

        var record  = &pluginServer->recordSuite;
        var service = &pluginServer->serviceSuite;
        var host    = pluginServer->hostObject;
        var propSvc = service->propertyService;

        TriglavPlugInPropertyObject propObj;
        propSvc->createProc(&propObj);

        // Radius スライダー: 1 〜 20（デフォルト 3）
        var label = EffectHelper.CreateAsciiString(service->stringService, "Radius");
        propSvc->addItemProc(propObj, ItemKeyRadius,
            kTriglavPlugInPropertyValueTypeInteger,
            kTriglavPlugInPropertyValueKindDefault,
            kTriglavPlugInPropertyInputKindDefault,
            label, (sbyte)'r');
        propSvc->setIntegerValueProc(propObj,        ItemKeyRadius, 3);
        propSvc->setIntegerDefaultValueProc(propObj, ItemKeyRadius, 3);
        propSvc->setIntegerMinValueProc(propObj,     ItemKeyRadius, 1);
        propSvc->setIntegerMaxValueProc(propObj,     ItemKeyRadius, 20);
        service->stringService->releaseProc(label);

        var handle = GCHandle.Alloc(new object());
        *data = (void*)GCHandle.ToIntPtr(handle);

        TriglavPlugInFilterInitializeSetProperty(record, host, propObj);
        TriglavPlugInFilterInitializeSetPropertyCallBack(record, host, &PropertyCallback, *data);
        propSvc->releaseProc(propObj);

        return kTriglavPlugInCallResultSuccess;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    public static int FilterTerminate(TriglavPlugInServer* pluginServer, void** data)
    {
        if (data != null && *data != null)
        {
            GCHandle.FromIntPtr((IntPtr)(*data)).Free();
            *data = null;
        }
        return kTriglavPlugInCallResultSuccess;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    public static int FilterRun(TriglavPlugInServer* pluginServer, void** data)
    {
        var record       = &pluginServer->recordSuite;
        var offscreenSvc = pluginServer->serviceSuite.offscreenService;
        var propertySvc  = pluginServer->serviceSuite.propertyService;

        if (TriglavPlugInGetFilterRunRecord(record) == null
            || offscreenSvc == null || propertySvc == null)
            return kTriglavPlugInCallResultFailed;

        var host = pluginServer->hostObject;

        TriglavPlugInPropertyObject  propObj;
        TriglavPlugInOffscreenObject srcOffscreen, dstOffscreen, selectOffscreen;
        TriglavPlugInRect            selectRect;

        TriglavPlugInFilterRunGetProperty(record, &propObj, host);
        TriglavPlugInFilterRunGetSourceOffscreen(record, &srcOffscreen, host);
        TriglavPlugInFilterRunGetDestinationOffscreen(record, &dstOffscreen, host);
        TriglavPlugInFilterRunGetSelectAreaRect(record, &selectRect, host);
        TriglavPlugInFilterRunGetSelectAreaOffscreen(record, &selectOffscreen, host);

        int rIdx, gIdx, bIdx;
        offscreenSvc->getRGBChannelIndexProc(&rIdx, &gIdx, &bIdx, dstOffscreen);

        int blockCount;
        offscreenSvc->getBlockRectCountProc(&blockCount, dstOffscreen, &selectRect);
        var blockRects = ArrayPool<TriglavPlugInRect>.Shared.Rent(blockCount);
        try
        {
            fixed (TriglavPlugInRect* pBlockRects = blockRects)
                for (int i = 0; i < blockCount; i++)
                    offscreenSvc->getBlockRectProc(pBlockRects + i, i, dstOffscreen, &selectRect);

            TriglavPlugInFilterRunSetProgressTotal(record, host, blockCount);

            bool restart    = true;
            int  blockIndex = 0;
            int  radius     = 3;

            while (true)
            {
                if (restart)
                {
                    restart = false;

                    int procResult = kTriglavPlugInFilterRunProcessResultContinue;
                    TriglavPlugInFilterRunProcess(record, &procResult, host,
                        kTriglavPlugInFilterRunProcessStateStart);
                    if (procResult == kTriglavPlugInFilterRunProcessResultExit) break;

                    propertySvc->getIntegerValueProc(&radius, propObj, ItemKeyRadius);
                    blockIndex = 0;
                }

                if (blockIndex < blockCount)
                {
                    TriglavPlugInFilterRunSetProgressDone(record, host, blockIndex);
                    ProcessBlock(offscreenSvc, dstOffscreen, srcOffscreen, selectOffscreen,
                                 ref blockRects[blockIndex], rIdx, gIdx, bIdx, radius);
                    fixed (TriglavPlugInRect* pBlockRects = blockRects)
                        TriglavPlugInFilterRunUpdateDestinationOffscreenRect(
                            record, host, pBlockRects + blockIndex);
                    blockIndex++;
                }

                {
                    int procResult = kTriglavPlugInFilterRunProcessResultContinue;
                    int procState  = blockIndex < blockCount
                        ? kTriglavPlugInFilterRunProcessStateContinue
                        : kTriglavPlugInFilterRunProcessStateEnd;
                    if (procState == kTriglavPlugInFilterRunProcessStateEnd)
                        TriglavPlugInFilterRunSetProgressDone(record, host, blockCount);
                    TriglavPlugInFilterRunProcess(record, &procResult, host, procState);
                    if      (procResult == kTriglavPlugInFilterRunProcessResultRestart) restart = true;
                    else if (procResult == kTriglavPlugInFilterRunProcessResultExit)    break;
                }
            }
        }
        finally
        {
            ArrayPool<TriglavPlugInRect>.Shared.Return(blockRects);
        }

        return kTriglavPlugInCallResultSuccess;
    }

    // ================================================================
    // プロパティコールバック
    // ================================================================

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void PropertyCallback(
        int* result, TriglavPlugInPropertyObject propObj,
        int itemKey, int notify, void* callbackData)
    {
        *result = notify == kTriglavPlugInPropertyCallBackNotifyValueChanged
            ? kTriglavPlugInPropertyCallBackResultModify
            : kTriglavPlugInPropertyCallBackResultNoModify;
    }

    // ================================================================
    // ブロック処理 — セパラブル Box Blur
    // ================================================================

    private static void ProcessBlock(
        TriglavPlugInOffscreenService* offscreenSvc,
        TriglavPlugInOffscreenObject   dstOffscreen,
        TriglavPlugInOffscreenObject   srcOffscreen,
        TriglavPlugInOffscreenObject   selectOffscreen,
        ref TriglavPlugInRect          blockRect,
        int rIdx, int gIdx, int bIdx, int radius)
    {
        var pos = new TriglavPlugInPoint { x = blockRect.left, y = blockRect.top };
        TriglavPlugInRect outRect;

        void* srcImg;   int srcRowBytes,   srcPixBytes;
        void* dstImg;   int dstRowBytes,   dstPixBytes;
        void* dstAlpha; int alphaRowBytes,  alphaPixBytes;

        offscreenSvc->getBlockImageProc(&srcImg,   &srcRowBytes,   &srcPixBytes,   &outRect, srcOffscreen, &pos);
        offscreenSvc->getBlockImageProc(&dstImg,   &dstRowBytes,   &dstPixBytes,   &outRect, dstOffscreen, &pos);
        offscreenSvc->getBlockAlphaProc(&dstAlpha, &alphaRowBytes, &alphaPixBytes, &outRect, dstOffscreen, &pos);

        if (srcImg == null || dstImg == null || dstAlpha == null) return;

        int w = blockRect.right  - blockRect.left;
        int h = blockRect.bottom - blockRect.top;
        if (w <= 0 || h <= 0) return;

        // 中間バッファ: 水平ぼかし後の R/G/B 値を格納（1ピクセル 1 int × 3 チャンネル）
        int size = w * h;
        int[] tmpR = ArrayPool<int>.Shared.Rent(size);
        int[] tmpG = ArrayPool<int>.Shared.Rent(size);
        int[] tmpB = ArrayPool<int>.Shared.Rent(size);
        try
        {
            // --- パス 1: 水平方向の Box Blur (src → tmp) ---
            byte* src = (byte*)srcImg;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int sumR = 0, sumG = 0, sumB = 0;
                    int x0 = x - radius >= 0 ? x - radius : 0;
                    int x1 = x + radius <  w ? x + radius : w - 1;
                    int cnt = x1 - x0 + 1;
                    for (int nx = x0; nx <= x1; nx++)
                    {
                        byte* p = src + y * srcRowBytes + nx * srcPixBytes;
                        sumR += p[rIdx]; sumG += p[gIdx]; sumB += p[bIdx];
                    }
                    int i = y * w + x;
                    tmpR[i] = sumR / cnt;
                    tmpG[i] = sumG / cnt;
                    tmpB[i] = sumB / cnt;
                }
            }

            // --- パス 2: 垂直方向の Box Blur (tmp → dst) ---
            if (selectOffscreen.value == null)
            {
                // 選択範囲なし
                byte* dst   = (byte*)dstImg;
                byte* alpha = (byte*)dstAlpha;
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        if (alpha[y * alphaRowBytes + x * alphaPixBytes] == 0) continue;

                        int sumR = 0, sumG = 0, sumB = 0;
                        int y0 = y - radius >= 0 ? y - radius : 0;
                        int y1 = y + radius <  h ? y + radius : h - 1;
                        int cnt = y1 - y0 + 1;
                        for (int ny = y0; ny <= y1; ny++)
                        {
                            int i = ny * w + x;
                            sumR += tmpR[i]; sumG += tmpG[i]; sumB += tmpB[i];
                        }
                        byte* d = dst + y * dstRowBytes + x * dstPixBytes;
                        d[rIdx] = (byte)(sumR / cnt);
                        d[gIdx] = (byte)(sumG / cnt);
                        d[bIdx] = (byte)(sumB / cnt);
                    }
                }
            }
            else
            {
                // 選択範囲あり: selVal=255 でフル適用、1〜254 でマスク合成
                void* selPtr; int selRowBytes, selPixBytes;
                offscreenSvc->getBlockSelectAreaProc(
                    &selPtr, &selRowBytes, &selPixBytes, &outRect, selectOffscreen, &pos);
                if (selPtr == null) return;

                byte* dst   = (byte*)dstImg;
                byte* alpha = (byte*)dstAlpha;
                byte* sel   = (byte*)selPtr;
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        if (alpha[y * alphaRowBytes + x * alphaPixBytes] == 0) continue;

                        byte selVal = sel[y * selRowBytes + x * selPixBytes];
                        if (selVal == 0) continue;

                        int sumR = 0, sumG = 0, sumB = 0;
                        int y0 = y - radius >= 0 ? y - radius : 0;
                        int y1 = y + radius <  h ? y + radius : h - 1;
                        int cnt = y1 - y0 + 1;
                        for (int ny = y0; ny <= y1; ny++)
                        {
                            int i = ny * w + x;
                            sumR += tmpR[i]; sumG += tmpG[i]; sumB += tmpB[i];
                        }
                        int blurR = sumR / cnt, blurG = sumG / cnt, blurB = sumB / cnt;

                        byte* d   = dst + y * dstRowBytes + x * dstPixBytes;
                        byte* sp  = (byte*)srcImg + y * srcRowBytes + x * srcPixBytes;
                        if (selVal == 255)
                        {
                            d[rIdx] = (byte)blurR;
                            d[gIdx] = (byte)blurG;
                            d[bIdx] = (byte)blurB;
                        }
                        else
                        {
                            d[rIdx] = (byte)Blend8(blurR, sp[rIdx], selVal);
                            d[gIdx] = (byte)Blend8(blurG, sp[gIdx], selVal);
                            d[bIdx] = (byte)Blend8(blurB, sp[bIdx], selVal);
                        }
                    }
                }
            }
        }
        finally
        {
            ArrayPool<int>.Shared.Return(tmpR);
            ArrayPool<int>.Shared.Return(tmpG);
            ArrayPool<int>.Shared.Return(tmpB);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Blend8(int dst, int src, int mask) => ((dst - src) * mask / 255) + src;
}
