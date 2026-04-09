// セパラブル Box Blur フィルタ実装
// EffectTemplate.cs.in と同じ 4 エントリポイント構造で実装しています。
using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CSPBridgeEffects.Effects.Kernels;
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

        TriglavPlugInOffscreenObject srcOffscreen, dstOffscreen, selectOffscreen;
        TriglavPlugInRect            selectRect;

        TriglavPlugInFilterRunGetSourceOffscreen(record, &srcOffscreen, host);
        TriglavPlugInFilterRunGetDestinationOffscreen(record, &dstOffscreen, host);
        TriglavPlugInFilterRunGetSelectAreaRect(record, &selectRect, host);
        TriglavPlugInFilterRunGetSelectAreaOffscreen(record, &selectOffscreen, host);

        int rIdx, gIdx, bIdx;
        offscreenSvc->getRGBChannelIndexProc(&rIdx, &gIdx, &bIdx, dstOffscreen);

        // ラムダキャプチャのため参照型ホルダーに状態を格納する。
        // unsafe struct はスタック変数なので直接ラムダにキャプチャできないため、
        // フィールドとしてヒープに移動し、値コピーで受け渡しする。
        var s = new BlurRunState
        {
            srcOffscreen    = srcOffscreen,
            selectOffscreen = selectOffscreen,
            rIdx            = rIdx,
            gIdx            = gIdx,
            bIdx            = bIdx,
            radius          = 3,
        };

        return EffectHelper.RunPreviewLoop(
            pluginServer, dstOffscreen, &selectRect,
            readParameters: (propSvc, propObj) =>
            {
                int v = s.radius;
                propSvc->getIntegerValueProc(&v, propObj, ItemKeyRadius);
                s.radius = v;
            },
            processBlock: (osSvc, dst, blockRect, idx) =>
            {
                ProcessBlock(osSvc, dst, s.srcOffscreen, s.selectOffscreen,
                             blockRect, s.rIdx, s.gIdx, s.bIdx, s.radius);
            });
    }

    // ラムダキャプチャ用状態ホルダー（FilterRun スコープで生存）
    private sealed class BlurRunState
    {
        public TriglavPlugInOffscreenObject srcOffscreen;
        public TriglavPlugInOffscreenObject selectOffscreen;
        public int rIdx, gIdx, bIdx, radius;
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
    // ブロック処理 — BlurKernel への委譲
    // ================================================================

    /// <summary>
    /// 1 ブロック分の Blur 処理を行います。
    /// SDK からポインタを取得し、Span に変換して BlurKernel を呼び出します。
    /// </summary>
    private static void ProcessBlock(
        TriglavPlugInOffscreenService* offscreenSvc,
        TriglavPlugInOffscreenObject   dstOffscreen,
        TriglavPlugInOffscreenObject   srcOffscreen,
        TriglavPlugInOffscreenObject   selectOffscreen,
        TriglavPlugInRect              blockRect,
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

        int size = w * h;
        int[] tmpR = ArrayPool<int>.Shared.Rent(size);
        int[] tmpG = ArrayPool<int>.Shared.Rent(size);
        int[] tmpB = ArrayPool<int>.Shared.Rent(size);
        try
        {
            var srcSpan   = new ReadOnlySpan<byte>(srcImg,   h * srcRowBytes);
            var dstSpan   = new Span<byte>(dstImg,           h * dstRowBytes);
            var alphaSpan = new ReadOnlySpan<byte>(dstAlpha, h * alphaRowBytes);

            // パス 1: 水平方向の Box Blur (src → tmp)
            KernelUtils.HorizontalBoxBlur(
                srcSpan, srcRowBytes, srcPixBytes,
                tmpR, tmpG, tmpB,
                w, h, rIdx, gIdx, bIdx, radius);

            // パス 2: 垂直方向の Box Blur (tmp → dst)
            if (selectOffscreen.value == null)
            {
                BlurKernel.VerticalPass(
                    dstSpan, dstRowBytes, dstPixBytes,
                    alphaSpan, alphaRowBytes, alphaPixBytes,
                    tmpR, tmpG, tmpB,
                    w, h, rIdx, gIdx, bIdx, radius);
            }
            else
            {
                void* selPtr; int selRowBytes, selPixBytes;
                offscreenSvc->getBlockSelectAreaProc(
                    &selPtr, &selRowBytes, &selPixBytes, &outRect, selectOffscreen, &pos);
                if (selPtr == null) return;

                var selSpan = new ReadOnlySpan<byte>(selPtr, h * selRowBytes);
                BlurKernel.VerticalPassWithSelection(
                    dstSpan,   dstRowBytes,   dstPixBytes,
                    srcSpan,   srcRowBytes,   srcPixBytes,
                    alphaSpan, alphaRowBytes, alphaPixBytes,
                    selSpan,   selRowBytes,   selPixBytes,
                    tmpR, tmpG, tmpB,
                    w, h, rIdx, gIdx, bIdx, radius);
            }
        }
        finally
        {
            ArrayPool<int>.Shared.Return(tmpR);
            ArrayPool<int>.Shared.Return(tmpG);
            ArrayPool<int>.Shared.Return(tmpB);
        }
    }
}
