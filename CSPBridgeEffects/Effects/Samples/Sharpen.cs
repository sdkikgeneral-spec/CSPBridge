// Unsharp Mask シャープンフィルタ実装
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
/// Sharpen フィルタ。Unsharp Mask（元画像 + Strength × (元画像 − ぼかし画像)）でエッジを強調します。
/// Strength: 効果の強さ（0〜200 %）、Radius: ぼかし半径（1〜10 px）。
/// </summary>
public static unsafe class Sharpen
{
    private const int ItemKeyStrength = 1;
    private const int ItemKeyRadius   = 2;

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
        => EffectHelper.InitializeModule(pluginServer, "com.example.cspbridge.sharpen");

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    public static int FilterInitialize(TriglavPlugInServer* pluginServer, void** data)
    {
        int rc = EffectHelper.InitializeFilter(pluginServer, SharpenMeta.Category, SharpenMeta.FilterName, s_targetKinds);
        if (rc != kTriglavPlugInCallResultSuccess)
            return rc;

        var record  = &pluginServer->recordSuite;
        var service = &pluginServer->serviceSuite;
        var host    = pluginServer->hostObject;
        var propSvc = service->propertyService;

        TriglavPlugInPropertyObject propObj;
        propSvc->createProc(&propObj);

        // Strength スライダー: 0 〜 200 %（デフォルト 100）
        var strengthLabel = EffectHelper.CreateAsciiString(service->stringService, "Strength");
        propSvc->addItemProc(propObj, ItemKeyStrength,
            kTriglavPlugInPropertyValueTypeInteger,
            kTriglavPlugInPropertyValueKindDefault,
            kTriglavPlugInPropertyInputKindDefault,
            strengthLabel, (sbyte)'s');
        propSvc->setIntegerValueProc(propObj,        ItemKeyStrength, 100);
        propSvc->setIntegerDefaultValueProc(propObj, ItemKeyStrength, 100);
        propSvc->setIntegerMinValueProc(propObj,     ItemKeyStrength, 0);
        propSvc->setIntegerMaxValueProc(propObj,     ItemKeyStrength, 200);
        service->stringService->releaseProc(strengthLabel);

        // Radius スライダー: 1 〜 10（デフォルト 2）
        var radiusLabel = EffectHelper.CreateAsciiString(service->stringService, "Radius");
        propSvc->addItemProc(propObj, ItemKeyRadius,
            kTriglavPlugInPropertyValueTypeInteger,
            kTriglavPlugInPropertyValueKindDefault,
            kTriglavPlugInPropertyInputKindDefault,
            radiusLabel, (sbyte)'r');
        propSvc->setIntegerValueProc(propObj,        ItemKeyRadius, 2);
        propSvc->setIntegerDefaultValueProc(propObj, ItemKeyRadius, 2);
        propSvc->setIntegerMinValueProc(propObj,     ItemKeyRadius, 1);
        propSvc->setIntegerMaxValueProc(propObj,     ItemKeyRadius, 10);
        service->stringService->releaseProc(radiusLabel);

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
        var s = new SharpenRunState
        {
            srcOffscreen    = srcOffscreen,
            selectOffscreen = selectOffscreen,
            rIdx            = rIdx,
            gIdx            = gIdx,
            bIdx            = bIdx,
            strength        = 100,
            radius          = 2,
        };

        return EffectHelper.RunPreviewLoop(
            pluginServer, dstOffscreen, &selectRect,
            readParameters: (propSvc, propObj) =>
            {
                int vs = s.strength, vr = s.radius;
                propSvc->getIntegerValueProc(&vs, propObj, ItemKeyStrength);
                propSvc->getIntegerValueProc(&vr, propObj, ItemKeyRadius);
                s.strength = vs;
                s.radius   = vr;
            },
            processBlock: (osSvc, dst, blockRect, idx) =>
            {
                ProcessBlock(osSvc, dst, s.srcOffscreen, s.selectOffscreen,
                             ref blockRect, s.rIdx, s.gIdx, s.bIdx, s.strength, s.radius);
            });
    }

    // ラムダキャプチャ用状態ホルダー（FilterRun スコープで生存）
    private sealed class SharpenRunState
    {
        public TriglavPlugInOffscreenObject srcOffscreen;
        public TriglavPlugInOffscreenObject selectOffscreen;
        public int rIdx, gIdx, bIdx, strength, radius;
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
    // ブロック処理 — SharpenKernel への委譲
    //   dst = clamp(src + strength/100 × (src − blur(src)), 0, 255)
    // ================================================================

    /// <summary>
    /// 1 ブロック分の Sharpen 処理を行います。
    /// SDK からポインタを取得し、Span に変換して SharpenKernel を呼び出します。
    /// </summary>
    private static void ProcessBlock(
        TriglavPlugInOffscreenService* offscreenSvc,
        TriglavPlugInOffscreenObject   dstOffscreen,
        TriglavPlugInOffscreenObject   srcOffscreen,
        TriglavPlugInOffscreenObject   selectOffscreen,
        ref TriglavPlugInRect          blockRect,
        int rIdx, int gIdx, int bIdx, int strength, int radius)
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

            // パス 1: 水平方向 Box Blur (src → tmp)
            SharpenKernel.HorizontalPass(
                srcSpan, srcRowBytes, srcPixBytes,
                tmpR, tmpG, tmpB,
                w, h, rIdx, gIdx, bIdx, radius);

            // パス 2: 垂直方向 Box Blur → Unsharp Mask (tmp → dst)
            if (selectOffscreen.value == null)
            {
                SharpenKernel.VerticalPassUnsharp(
                    dstSpan,   dstRowBytes,   dstPixBytes,
                    srcSpan,   srcRowBytes,   srcPixBytes,
                    alphaSpan, alphaRowBytes, alphaPixBytes,
                    tmpR, tmpG, tmpB,
                    w, h, rIdx, gIdx, bIdx, radius, strength);
            }
            else
            {
                void* selPtr; int selRowBytes, selPixBytes;
                offscreenSvc->getBlockSelectAreaProc(
                    &selPtr, &selRowBytes, &selPixBytes, &outRect, selectOffscreen, &pos);
                if (selPtr == null) return;

                var selSpan = new ReadOnlySpan<byte>(selPtr, h * selRowBytes);
                SharpenKernel.VerticalPassUnsharpWithSelection(
                    dstSpan,   dstRowBytes,   dstPixBytes,
                    srcSpan,   srcRowBytes,   srcPixBytes,
                    alphaSpan, alphaRowBytes, alphaPixBytes,
                    selSpan,   selRowBytes,   selPixBytes,
                    tmpR, tmpG, tmpB,
                    w, h, rIdx, gIdx, bIdx, radius, strength);
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
