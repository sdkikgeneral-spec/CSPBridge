// サンプル: HSV フィルタ
// CSP_FilterPlugIn/FilterPlugIn/Source/HSV/PIHSVMain.cpp を C# に移植したものです。
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
/// HSV フィルタのサンプル実装。
/// 色相・彩度・明度を調整します。RGBAlpha レイヤーのみ対応。
/// <para>元実装: PIHSVMain.cpp / PIHSVFilter.h (CELSYS Inc.)</para>
/// </summary>
public static unsafe class HSV
{
    // ---- プロパティアイテムキー ----
    private const int ItemKeyHue        = 1;
    private const int ItemKeySaturation = 2;
    private const int ItemKeyValue      = 3;

    // ---- ユーザー入力 → 内部スケール変換係数（PIHSVMain.cpp と一致） ----
    // 色相: deg(-180~180) → hue*HsvHFilterMax/360  内部値 (0 ~ 6*32768-1)
    // 彩度: pct(-100~100) → sat*HsvSFilterMax/100  内部値 (-32768 ~ 32768)
    // 明度: pct(-100~100) → val*HsvVFilterMax/100  内部値 (-32768 ~ 32768)
    private const int HsvHFilterMax = 6 * 32768;
    private const int HsvSFilterMax = 32768;
    private const int HsvVFilterMax = 32768;

    // ================================================================
    // プラグインエントリポイント
    // BridgeBase が load_assembly_and_get_function_pointer で取得する
    // ================================================================

    /// <summary>モジュール初期化。モジュール ID とモジュール種別を設定します。</summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    public static int ModuleInitialize(TriglavPlugInServer* pluginServer)
        => EffectHelper.InitializeModule(pluginServer, "A9EE0802-84E7-4847-87D8-9EBAC916EEE4");

    /// <summary>フィルタ初期化。色相・彩度・明度スライダーを作成します。</summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    public static int FilterInitialize(TriglavPlugInServer* pluginServer, void** data)
    {
        // カテゴリ名・フィルタ名・プレビュー可・ターゲット（RGB のみ）を設定
        int[] targets = [kTriglavPlugInFilterTargetKindRasterLayerRGBAlpha];
        int rc = EffectHelper.InitializeFilter(pluginServer, HSVMeta.Category, HSVMeta.FilterName, targets);
        if (rc != kTriglavPlugInCallResultSuccess)
            return rc;

        var record  = &pluginServer->recordSuite;
        var service = &pluginServer->serviceSuite;
        var host    = pluginServer->hostObject;
        var propSvc = service->propertyService;

        TriglavPlugInPropertyObject propObj;
        propSvc->createProc(&propObj);

        // 色相スライダー: -180 〜 +180
        var hueLabel = EffectHelper.CreateAsciiString(service->stringService, "Hue");
        propSvc->addItemProc(propObj, ItemKeyHue,
            kTriglavPlugInPropertyValueTypeInteger,
            kTriglavPlugInPropertyValueKindDefault,
            kTriglavPlugInPropertyInputKindDefault,
            hueLabel, (sbyte)'h');
        propSvc->setIntegerValueProc(propObj,        ItemKeyHue, 0);
        propSvc->setIntegerDefaultValueProc(propObj, ItemKeyHue, 0);
        propSvc->setIntegerMinValueProc(propObj,     ItemKeyHue, -180);
        propSvc->setIntegerMaxValueProc(propObj,     ItemKeyHue,  180);
        service->stringService->releaseProc(hueLabel);

        // 彩度スライダー: -100 〜 +100
        var satLabel = EffectHelper.CreateAsciiString(service->stringService, "Saturation");
        propSvc->addItemProc(propObj, ItemKeySaturation,
            kTriglavPlugInPropertyValueTypeInteger,
            kTriglavPlugInPropertyValueKindDefault,
            kTriglavPlugInPropertyInputKindDefault,
            satLabel, (sbyte)'s');
        propSvc->setIntegerValueProc(propObj,        ItemKeySaturation, 0);
        propSvc->setIntegerDefaultValueProc(propObj, ItemKeySaturation, 0);
        propSvc->setIntegerMinValueProc(propObj,     ItemKeySaturation, -100);
        propSvc->setIntegerMaxValueProc(propObj,     ItemKeySaturation,  100);
        service->stringService->releaseProc(satLabel);

        // 明度スライダー: -100 〜 +100
        var valLabel = EffectHelper.CreateAsciiString(service->stringService, "Value");
        propSvc->addItemProc(propObj, ItemKeyValue,
            kTriglavPlugInPropertyValueTypeInteger,
            kTriglavPlugInPropertyValueKindDefault,
            kTriglavPlugInPropertyInputKindDefault,
            valLabel, (sbyte)'v');
        propSvc->setIntegerValueProc(propObj,        ItemKeyValue, 0);
        propSvc->setIntegerDefaultValueProc(propObj, ItemKeyValue, 0);
        propSvc->setIntegerMinValueProc(propObj,     ItemKeyValue, -100);
        propSvc->setIntegerMaxValueProc(propObj,     ItemKeyValue,  100);
        service->stringService->releaseProc(valLabel);

        // 状態オブジェクトを GCHandle で保持し void** data に格納
        // 現状は managed 側の状態保持が不要だが、ライフサイクル対称性のためハンドルは維持する。
        var handle = GCHandle.Alloc(new object());
        *data = (void*)GCHandle.ToIntPtr(handle);

        // プロパティとコールバックをホストに登録
        TriglavPlugInFilterInitializeSetProperty(record, host, propObj);
        TriglavPlugInFilterInitializeSetPropertyCallBack(record, host, &PropertyCallback, *data);

        propSvc->releaseProc(propObj);
        return kTriglavPlugInCallResultSuccess;
    }

    /// <summary>フィルタ終了。GCHandle を解放します。</summary>
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

    /// <summary>フィルタ実行。ブロック単位で HSV 変換を行います。</summary>
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
        // 全パラメータ 0 のときは processBlock 内でピクセル処理をスキップする。
        var s = new HsvRunState
        {
            selectOffscreen = selectOffscreen,
            rIdx            = rIdx,
            gIdx            = gIdx,
            bIdx            = bIdx,
            skipProcessing  = true,
        };

        return EffectHelper.RunPreviewLoop(
            pluginServer, dstOffscreen, &selectRect,
            readParameters: (propSvc, propObj) =>
            {
                int h = 0, sat = 0, v = 0;
                propSvc->getIntegerValueProc(&h,   propObj, ItemKeyHue);
                propSvc->getIntegerValueProc(&sat, propObj, ItemKeySaturation);
                propSvc->getIntegerValueProc(&v,   propObj, ItemKeyValue);

                if (h != 0 || sat != 0 || v != 0)
                {
                    int hue = h < 0 ? h + 360 : h;
                    s.hFilter       = hue * HsvHFilterMax / 360;
                    s.sFilter       = sat * HsvSFilterMax / 100;
                    s.vFilter       = v   * HsvVFilterMax / 100;
                    s.skipProcessing = false;
                }
                else
                {
                    // 全パラメータ 0: ピクセル処理不要
                    s.hFilter = s.sFilter = s.vFilter = 0;
                    s.skipProcessing = true;
                }
            },
            processBlock: (osSvc, dst, blockRect, idx) =>
            {
                if (s.skipProcessing) return;
                ProcessBlock(osSvc, dst, s.selectOffscreen,
                             ref blockRect, s.rIdx, s.gIdx, s.bIdx,
                             s.hFilter, s.sFilter, s.vFilter);
            });
    }

    // ラムダキャプチャ用状態ホルダー（FilterRun スコープで生存）
    private sealed class HsvRunState
    {
        public TriglavPlugInOffscreenObject selectOffscreen;
        public int rIdx, gIdx, bIdx;
        public int hFilter, sFilter, vFilter;
        public bool skipProcessing;
    }

    // ================================================================
    // プロパティコールバック
    // 値変更を通知して FilterRun の再実行を促す
    // ================================================================

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void PropertyCallback(
        int*                        result,
        TriglavPlugInPropertyObject propObj,
        int                         itemKey,
        int                         notify,
        void*                       callbackData)
    {
        *result = notify == kTriglavPlugInPropertyCallBackNotifyValueChanged
            ? kTriglavPlugInPropertyCallBackResultModify
            : kTriglavPlugInPropertyCallBackResultNoModify;
    }

    // ================================================================
    // ブロック処理 — HsvKernel への委譲
    // ================================================================

    /// <summary>
    /// 1 ブロック分の HSV 処理を行います。
    /// SDK からポインタを取得し、Span に変換して HsvKernel を呼び出します。
    /// </summary>
    private static void ProcessBlock(
        TriglavPlugInOffscreenService* offscreenSvc,
        TriglavPlugInOffscreenObject   dstOffscreen,
        TriglavPlugInOffscreenObject   selectOffscreen,
        ref TriglavPlugInRect          blockRect,
        int rIdx, int gIdx, int bIdx,
        int hFilter, int sFilter, int vFilter)
    {
        var pos = new TriglavPlugInPoint { x = blockRect.left, y = blockRect.top };
        TriglavPlugInRect tempRect;

        void* dstImg;   int dstRowBytes,   dstPixBytes;
        void* dstAlpha; int alphaRowBytes, alphaPixBytes;
        offscreenSvc->getBlockImageProc(&dstImg,   &dstRowBytes,   &dstPixBytes,   &tempRect, dstOffscreen, &pos);
        offscreenSvc->getBlockAlphaProc(&dstAlpha, &alphaRowBytes, &alphaPixBytes, &tempRect, dstOffscreen, &pos);

        if (dstImg == null || dstAlpha == null) return;

        int w = blockRect.right  - blockRect.left;
        int h = blockRect.bottom - blockRect.top;
        if (w <= 0 || h <= 0) return;

        var dstSpan   = new Span<byte>(dstImg,           h * dstRowBytes);
        var alphaSpan = new ReadOnlySpan<byte>(dstAlpha, h * alphaRowBytes);

        if (selectOffscreen.value == null)
        {
            HsvKernel.Process(
                dstSpan,   dstRowBytes,   dstPixBytes,
                alphaSpan, alphaRowBytes, alphaPixBytes,
                w, h, rIdx, gIdx, bIdx,
                hFilter, sFilter, vFilter);
        }
        else
        {
            void* selPtr; int selRowBytes, selPixBytes;
            offscreenSvc->getBlockSelectAreaProc(
                &selPtr, &selRowBytes, &selPixBytes, &tempRect, selectOffscreen, &pos);
            if (selPtr == null) return;

            var selSpan = new ReadOnlySpan<byte>(selPtr, h * selRowBytes);
            HsvKernel.ProcessWithSelection(
                dstSpan,   dstRowBytes,   dstPixBytes,
                alphaSpan, alphaRowBytes, alphaPixBytes,
                selSpan,   selRowBytes,   selPixBytes,
                w, h, rIdx, gIdx, bIdx,
                hFilter, sFilter, vFilter);
        }
    }
}
