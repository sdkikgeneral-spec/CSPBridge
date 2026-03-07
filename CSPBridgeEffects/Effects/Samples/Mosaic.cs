// モザイク（ピクセル化）フィルタ実装
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
/// Mosaic フィルタ。Size × Size ピクセルのセルを均一色で塗り潰すことでモザイクを作成します。
/// セルはキャンバス座標基準でグリッド整列するため、ブロック境界をまたいでも継ぎ目が出ません。
/// </summary>
public static unsafe class Mosaic
{
    private const int ItemKeySize = 1;

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
        => EffectHelper.InitializeModule(pluginServer, "com.example.cspbridge.mosaic");

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    public static int FilterInitialize(TriglavPlugInServer* pluginServer, void** data)
    {
        int rc = EffectHelper.InitializeFilter(pluginServer, "Bridge Effects", "Mosaic", s_targetKinds);
        if (rc != kTriglavPlugInCallResultSuccess)
            return rc;

        var record  = &pluginServer->recordSuite;
        var service = &pluginServer->serviceSuite;
        var host    = pluginServer->hostObject;
        var propSvc = service->propertyService;

        TriglavPlugInPropertyObject propObj;
        propSvc->createProc(&propObj);

        // Size スライダー: 2 〜 64（デフォルト 16）
        var label = EffectHelper.CreateAsciiString(service->stringService, "Size");
        propSvc->addItemProc(propObj, ItemKeySize,
            kTriglavPlugInPropertyValueTypeInteger,
            kTriglavPlugInPropertyValueKindDefault,
            kTriglavPlugInPropertyInputKindDefault,
            label, (sbyte)'z');
        propSvc->setIntegerValueProc(propObj,        ItemKeySize, 16);
        propSvc->setIntegerDefaultValueProc(propObj, ItemKeySize, 16);
        propSvc->setIntegerMinValueProc(propObj,     ItemKeySize, 2);
        propSvc->setIntegerMaxValueProc(propObj,     ItemKeySize, 64);
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
        TriglavPlugInOffscreenObject dstOffscreen, selectOffscreen;
        TriglavPlugInRect            selectRect;

        TriglavPlugInFilterRunGetProperty(record, &propObj, host);
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
            int  cellSize   = 16;

            while (true)
            {
                if (restart)
                {
                    restart = false;

                    int procResult = kTriglavPlugInFilterRunProcessResultContinue;
                    TriglavPlugInFilterRunProcess(record, &procResult, host,
                        kTriglavPlugInFilterRunProcessStateStart);
                    if (procResult == kTriglavPlugInFilterRunProcessResultExit) break;

                    propertySvc->getIntegerValueProc(&cellSize, propObj, ItemKeySize);
                    if (cellSize < 2) cellSize = 2;
                    blockIndex = 0;
                }

                if (blockIndex < blockCount)
                {
                    TriglavPlugInFilterRunSetProgressDone(record, host, blockIndex);
                    ProcessBlock(offscreenSvc, dstOffscreen, selectOffscreen,
                                 ref blockRects[blockIndex], rIdx, gIdx, bIdx, cellSize);
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
    // ブロック処理 — セル単位ピクセル化
    // ================================================================

    private static void ProcessBlock(
        TriglavPlugInOffscreenService* offscreenSvc,
        TriglavPlugInOffscreenObject   dstOffscreen,
        TriglavPlugInOffscreenObject   selectOffscreen,
        ref TriglavPlugInRect          blockRect,
        int rIdx, int gIdx, int bIdx, int cellSize)
    {
        var pos = new TriglavPlugInPoint { x = blockRect.left, y = blockRect.top };
        TriglavPlugInRect outRect;

        void* dstImg;   int dstRowBytes,   dstPixBytes;
        void* dstAlpha; int alphaRowBytes,  alphaPixBytes;

        offscreenSvc->getBlockImageProc(&dstImg,   &dstRowBytes,   &dstPixBytes,   &outRect, dstOffscreen, &pos);
        offscreenSvc->getBlockAlphaProc(&dstAlpha, &alphaRowBytes, &alphaPixBytes, &outRect, dstOffscreen, &pos);

        if (dstImg == null || dstAlpha == null) return;

        int w = blockRect.right  - blockRect.left;
        int h = blockRect.bottom - blockRect.top;
        if (w <= 0 || h <= 0) return;

        byte* dst   = (byte*)dstImg;
        byte* alpha = (byte*)dstAlpha;

        // セルグリッドはグローバル座標に整列させる（ブロック間で継ぎ目なし）
        // cellLeft/cellTop: ブロックの左上を含む最初のセル左上（グローバル座標）
        int cellLeft = blockRect.left / cellSize * cellSize;
        int cellTop  = blockRect.top  / cellSize * cellSize;

        if (selectOffscreen.value == null)
        {
            // --- 選択範囲なし ---
            for (int cy = cellTop; cy < blockRect.bottom; cy += cellSize)
            {
                for (int cx = cellLeft; cx < blockRect.right; cx += cellSize)
                {
                    // セルとブロックの交差領域（ローカル座標）
                    int lx0 = (cx > blockRect.left  ? cx : blockRect.left)  - blockRect.left;
                    int ly0 = (cy > blockRect.top   ? cy : blockRect.top)   - blockRect.top;
                    int lx1 = (cx + cellSize < blockRect.right  ? cx + cellSize : blockRect.right)  - blockRect.left;
                    int ly1 = (cy + cellSize < blockRect.bottom ? cy + cellSize : blockRect.bottom) - blockRect.top;

                    // 非透明ピクセルの平均色を計算
                    long sumR = 0, sumG = 0, sumB = 0;
                    int  cnt  = 0;
                    for (int ly = ly0; ly < ly1; ly++)
                    {
                        for (int lx = lx0; lx < lx1; lx++)
                        {
                            if (alpha[ly * alphaRowBytes + lx * alphaPixBytes] == 0) continue;
                            byte* p = dst + ly * dstRowBytes + lx * dstPixBytes;
                            sumR += p[rIdx]; sumG += p[gIdx]; sumB += p[bIdx];
                            cnt++;
                        }
                    }
                    if (cnt == 0) continue;

                    byte avgR = (byte)(sumR / cnt);
                    byte avgG = (byte)(sumG / cnt);
                    byte avgB = (byte)(sumB / cnt);

                    // セル内を平均色で塗り潰す
                    for (int ly = ly0; ly < ly1; ly++)
                    {
                        for (int lx = lx0; lx < lx1; lx++)
                        {
                            if (alpha[ly * alphaRowBytes + lx * alphaPixBytes] == 0) continue;
                            byte* d = dst + ly * dstRowBytes + lx * dstPixBytes;
                            d[rIdx] = avgR;
                            d[gIdx] = avgG;
                            d[bIdx] = avgB;
                        }
                    }
                }
            }
        }
        else
        {
            // --- 選択範囲あり ---
            void* selPtr; int selRowBytes, selPixBytes;
            offscreenSvc->getBlockSelectAreaProc(
                &selPtr, &selRowBytes, &selPixBytes, &outRect, selectOffscreen, &pos);
            if (selPtr == null) return;

            byte* sel = (byte*)selPtr;

            for (int cy = cellTop; cy < blockRect.bottom; cy += cellSize)
            {
                for (int cx = cellLeft; cx < blockRect.right; cx += cellSize)
                {
                    int lx0 = (cx > blockRect.left  ? cx : blockRect.left)  - blockRect.left;
                    int ly0 = (cy > blockRect.top   ? cy : blockRect.top)   - blockRect.top;
                    int lx1 = (cx + cellSize < blockRect.right  ? cx + cellSize : blockRect.right)  - blockRect.left;
                    int ly1 = (cy + cellSize < blockRect.bottom ? cy + cellSize : blockRect.bottom) - blockRect.top;

                    // 非透明ピクセルの平均色（選択範囲を問わず全体を集計）
                    long sumR = 0, sumG = 0, sumB = 0;
                    int  cnt  = 0;
                    for (int ly = ly0; ly < ly1; ly++)
                    {
                        for (int lx = lx0; lx < lx1; lx++)
                        {
                            if (alpha[ly * alphaRowBytes + lx * alphaPixBytes] == 0) continue;
                            byte* p = dst + ly * dstRowBytes + lx * dstPixBytes;
                            sumR += p[rIdx]; sumG += p[gIdx]; sumB += p[bIdx];
                            cnt++;
                        }
                    }
                    if (cnt == 0) continue;

                    byte avgR = (byte)(sumR / cnt);
                    byte avgG = (byte)(sumG / cnt);
                    byte avgB = (byte)(sumB / cnt);

                    // 選択値に応じてマスク合成しながら塗り潰す
                    for (int ly = ly0; ly < ly1; ly++)
                    {
                        for (int lx = lx0; lx < lx1; lx++)
                        {
                            if (alpha[ly * alphaRowBytes + lx * alphaPixBytes] == 0) continue;

                            byte selVal = sel[ly * selRowBytes + lx * selPixBytes];
                            if (selVal == 0) continue;

                            byte* d = dst + ly * dstRowBytes + lx * dstPixBytes;
                            if (selVal == 255)
                            {
                                d[rIdx] = avgR;
                                d[gIdx] = avgG;
                                d[bIdx] = avgB;
                            }
                            else
                            {
                                d[rIdx] = (byte)Blend8(avgR, d[rIdx], selVal);
                                d[gIdx] = (byte)Blend8(avgG, d[gIdx], selVal);
                                d[bIdx] = (byte)Blend8(avgB, d[bIdx], selVal);
                            }
                        }
                    }
                }
            }
        }
    }

    private static int Blend8(int dst, int src, int mask) => ((dst - src) * mask / 255) + src;
}
