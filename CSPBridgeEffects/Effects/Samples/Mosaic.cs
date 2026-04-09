// モザイク（ピクセル化）フィルタ実装
// EffectTemplate.cs.in と同じ 4 エントリポイント構造で実装しています。
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CSPBridgeEffects.Effects.Kernels;
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
        int rc = EffectHelper.InitializeFilter(pluginServer, MosaicMeta.Category, MosaicMeta.FilterName, s_targetKinds);
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

        // 現状は managed 側の状態保持が不要だが、ライフサイクル対称性のためハンドルは維持する。
        // FilterTerminate で解放すること。
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

        TriglavPlugInOffscreenObject dstOffscreen, selectOffscreen;
        TriglavPlugInRect            selectRect;

        TriglavPlugInFilterRunGetDestinationOffscreen(record, &dstOffscreen, host);
        TriglavPlugInFilterRunGetSelectAreaRect(record, &selectRect, host);
        TriglavPlugInFilterRunGetSelectAreaOffscreen(record, &selectOffscreen, host);

        int rIdx, gIdx, bIdx;
        offscreenSvc->getRGBChannelIndexProc(&rIdx, &gIdx, &bIdx, dstOffscreen);

        // ラムダキャプチャのため参照型ホルダーに状態を格納する。
        var s = new MosaicRunState
        {
            selectOffscreen = selectOffscreen,
            rIdx            = rIdx,
            gIdx            = gIdx,
            bIdx            = bIdx,
            cellSize        = 16,
        };

        return EffectHelper.RunPreviewLoop(
            pluginServer, dstOffscreen, &selectRect,
            readParameters: (propSvc, propObj) =>
            {
                int v = s.cellSize;
                propSvc->getIntegerValueProc(&v, propObj, ItemKeySize);
                if (v < 2) v = 2;
                s.cellSize = v;
            },
            processBlock: (osSvc, dst, blockRect, idx) =>
            {
                ProcessBlock(osSvc, dst, s.selectOffscreen,
                             blockRect, s.rIdx, s.gIdx, s.bIdx, s.cellSize);
            });
    }

    // ラムダキャプチャ用状態ホルダー（FilterRun スコープで生存）
    private sealed class MosaicRunState
    {
        public TriglavPlugInOffscreenObject selectOffscreen;
        public int rIdx, gIdx, bIdx, cellSize;
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
    // ブロック処理 — MosaicKernel への委譲
    // ================================================================

    /// <summary>
    /// 1 ブロック分のモザイク処理を行います。
    /// SDK からポインタを取得し、Span に変換して MosaicKernel を呼び出します。
    /// </summary>
    private static void ProcessBlock(
        TriglavPlugInOffscreenService* offscreenSvc,
        TriglavPlugInOffscreenObject   dstOffscreen,
        TriglavPlugInOffscreenObject   selectOffscreen,
        TriglavPlugInRect              blockRect,
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

        var dstSpan   = new Span<byte>(dstImg,           h * dstRowBytes);
        var alphaSpan = new ReadOnlySpan<byte>(dstAlpha, h * alphaRowBytes);

        if (selectOffscreen.value == null)
        {
            MosaicKernel.Process(
                dstSpan,   dstRowBytes,   dstPixBytes,
                alphaSpan, alphaRowBytes, alphaPixBytes,
                w, h,
                blockRect.left, blockRect.top, blockRect.right, blockRect.bottom,
                rIdx, gIdx, bIdx, cellSize);
        }
        else
        {
            void* selPtr; int selRowBytes, selPixBytes;
            offscreenSvc->getBlockSelectAreaProc(
                &selPtr, &selRowBytes, &selPixBytes, &outRect, selectOffscreen, &pos);
            if (selPtr == null) return;

            var selSpan = new ReadOnlySpan<byte>(selPtr, h * selRowBytes);
            MosaicKernel.ProcessWithSelection(
                dstSpan,   dstRowBytes,   dstPixBytes,
                alphaSpan, alphaRowBytes, alphaPixBytes,
                selSpan,   selRowBytes,   selPixBytes,
                w, h,
                blockRect.left, blockRect.top, blockRect.right, blockRect.bottom,
                rIdx, gIdx, bIdx, cellSize);
        }
    }
}
