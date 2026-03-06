// サンプル: HSV フィルタ
// CSP_FilterPlugIn/FilterPlugIn/Source/HSV/PIHSVMain.cpp を C# に移植したものです。
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
        int rc = EffectHelper.InitializeFilter(pluginServer, "Bridge Effects", "HSV", targets);
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

        TriglavPlugInPropertyObject  propObj;
        TriglavPlugInOffscreenObject dstOffscreen, selectOffscreen;
        TriglavPlugInRect            selectRect;

        TriglavPlugInFilterRunGetProperty(record, &propObj, host);
        TriglavPlugInFilterRunGetDestinationOffscreen(record, &dstOffscreen, host);
        TriglavPlugInFilterRunGetSelectAreaRect(record, &selectRect, host);
        TriglavPlugInFilterRunGetSelectAreaOffscreen(record, &selectOffscreen, host);

        int rIdx, gIdx, bIdx;
        offscreenSvc->getRGBChannelIndexProc(&rIdx, &gIdx, &bIdx, dstOffscreen);

        // ブロック矩形リストを構築
        int blockCount;
        offscreenSvc->getBlockRectCountProc(&blockCount, dstOffscreen, &selectRect);
        var blockRects = ArrayPool<TriglavPlugInRect>.Shared.Rent(blockCount);
        try
        {
        fixed (TriglavPlugInRect* pBlockRects = blockRects)
        {
            for (int i = 0; i < blockCount; i++)
                offscreenSvc->getBlockRectProc(pBlockRects + i, i, dstOffscreen, &selectRect);
        }

        TriglavPlugInFilterRunSetProgressTotal(record, host, blockCount);

        // ---- プレビュー対応メインループ ----
        bool restart    = true;
        int  blockIndex = 0;
        int  hFilter = 0, sFilter = 0, vFilter = 0;

        while (true)
        {
            if (restart)
            {
                restart = false;

                int procResult = kTriglavPlugInFilterRunProcessResultContinue;
                TriglavPlugInFilterRunProcess(record, &procResult, host,
                    kTriglavPlugInFilterRunProcessStateStart);
                if (procResult == kTriglavPlugInFilterRunProcessResultExit) break;

                // 現在のパラメータを読み取り内部スケールに変換
                int h, s, v;
                propertySvc->getIntegerValueProc(&h, propObj, ItemKeyHue);
                propertySvc->getIntegerValueProc(&s, propObj, ItemKeySaturation);
                propertySvc->getIntegerValueProc(&v, propObj, ItemKeyValue);

                if (h != 0 || s != 0 || v != 0)
                {
                    blockIndex = 0;
                    int hue = h < 0 ? h + 360 : h;
                    hFilter = hue * HsvHFilterMax / 360;
                    sFilter = s   * HsvSFilterMax / 100;
                    vFilter = v   * HsvVFilterMax / 100;
                }
                else
                {
                    // 全パラメータ 0: 処理不要、選択範囲全体をそのまま更新
                    blockIndex = blockCount;
                    TriglavPlugInFilterRunUpdateDestinationOffscreenRect(record, host, &selectRect);
                }
            }

            if (blockIndex < blockCount)
            {
                TriglavPlugInFilterRunSetProgressDone(record, host, blockIndex);
                ProcessBlock(offscreenSvc, dstOffscreen, selectOffscreen,
                             ref blockRects[blockIndex],
                             rIdx, gIdx, bIdx, hFilter, sFilter, vFilter);
                fixed (TriglavPlugInRect* pBlockRects = blockRects)
                {
                    TriglavPlugInRect* pRect = pBlockRects + blockIndex;
                    TriglavPlugInFilterRunUpdateDestinationOffscreenRect(record, host, pRect);
                }
                blockIndex++;
            }

            {
                int procResult = kTriglavPlugInFilterRunProcessResultContinue;
                int procState  = blockIndex < blockCount
                    ? kTriglavPlugInFilterRunProcessStateContinue
                    : kTriglavPlugInFilterRunProcessStateEnd;
                if (procState == kTriglavPlugInFilterRunProcessStateEnd)
                    TriglavPlugInFilterRunSetProgressDone(record, host, blockIndex);
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
    // ブロック処理（選択範囲の有無で分岐）
    // ================================================================

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

        if (selectOffscreen.value == null)
        {
            // 選択範囲なし: アルファ > 0 の全ピクセルを処理
            byte* imgY   = (byte*)dstImg;
            byte* alphaY = (byte*)dstAlpha;
            for (int y = blockRect.top; y < blockRect.bottom; y++)
            {
                byte* imgX   = imgY;
                byte* alphaX = alphaY;
                for (int x = blockRect.left; x < blockRect.right; x++)
                {
                    if (*alphaX > 0)
                        ApplyHsv(imgX, rIdx, gIdx, bIdx, hFilter, sFilter, vFilter);
                    imgX   += dstPixBytes;
                    alphaX += alphaPixBytes;
                }
                imgY   += dstRowBytes;
                alphaY += alphaRowBytes;
            }
        }
        else
        {
            // 選択範囲あり: 選択値 255 でフル適用、1〜254 でマスク合成
            void* selPtr; int selRowBytes, selPixBytes;
            offscreenSvc->getBlockSelectAreaProc(
                &selPtr, &selRowBytes, &selPixBytes, &tempRect, selectOffscreen, &pos);
            if (selPtr == null) return;

            byte* imgY   = (byte*)dstImg;
            byte* alphaY = (byte*)dstAlpha;
            byte* selY   = (byte*)selPtr;
            for (int y = blockRect.top; y < blockRect.bottom; y++)
            {
                byte* imgX   = imgY;
                byte* alphaX = alphaY;
                byte* selX   = selY;
                for (int x = blockRect.left; x < blockRect.right; x++)
                {
                    if (*alphaX > 0)
                    {
                        if (*selX == 255)
                            ApplyHsv(imgX, rIdx, gIdx, bIdx, hFilter, sFilter, vFilter);
                        else if (*selX != 0)
                            ApplyHsvMask(imgX, rIdx, gIdx, bIdx, *selX, hFilter, sFilter, vFilter);
                    }
                    imgX   += dstPixBytes;
                    alphaX += alphaPixBytes;
                    selX   += selPixBytes;
                }
                imgY   += dstRowBytes;
                alphaY += alphaRowBytes;
                selY   += selRowBytes;
            }
        }
    }

    // ================================================================
    // HSV 数学処理（PIHSVFilter.h の C# 移植）
    // ================================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ApplyHsv(byte* pixel, int r, int g, int b,
        int hFilter, int sFilter, int vFilter)
    {
        RgbToHsv(out uint uH, out uint uS, out uint uV, pixel[r], pixel[g], pixel[b]);
        HsvFilter(ref uH, ref uS, ref uV, hFilter, sFilter, vFilter);
        HsvToRgb(out uint uR, out uint uG, out uint uB, uH, uS, uV);
        pixel[r] = (byte)uR;
        pixel[g] = (byte)uG;
        pixel[b] = (byte)uB;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ApplyHsvMask(byte* pixel, int r, int g, int b, byte mask,
        int hFilter, int sFilter, int vFilter)
    {
        RgbToHsv(out uint uH, out uint uS, out uint uV, pixel[r], pixel[g], pixel[b]);
        HsvFilter(ref uH, ref uS, ref uV, hFilter, sFilter, vFilter);
        HsvToRgb(out uint uR, out uint uG, out uint uB, uH, uS, uV);
        pixel[r] = (byte)Blend8((int)uR, pixel[r], mask);
        pixel[g] = (byte)Blend8((int)uG, pixel[g], mask);
        pixel[b] = (byte)Blend8((int)uB, pixel[b], mask);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Blend8(int dst, int src, int mask)
        => ((dst - src) * mask / 255) + src;

    /// <summary>
    /// RGB (各 0〜255) を HSV に変換します。
    /// H: 0〜6*32768, S: 0〜65536, V: 0〜255
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void RgbToHsv(out uint ruH, out uint ruS, out uint ruV,
        uint r, uint g, uint b)
    {
        uint uMax, uMin;
        if (r >= g && r >= b)
        {
            uMax = r;
            uMin = g >= b ? b : g;
        }
        else if (g >= b)   // g > r (前段で除外済み), g >= b → G が最大
        {
            uMax = g;
            uMin = r >= b ? b : r;
        }
        else               // b が最大
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
    private static void HsvToRgb(out uint ruR, out uint ruG, out uint ruB,
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
    private static void HsvFilter(ref uint ruH, ref uint ruS, ref uint ruV,
        int hFilter, int sFilter, int vFilter)
    {
        const int HMax = 6 * 32768;
        const int SMax = 65536;
        const int VMax = 255;

        int nH = (int)ruH;
        int nS = (int)ruS;
        int nV = (int)ruV;

        // 色相: 加算して 0〜HMax に折り返す
        if (hFilter != 0)
        {
            nH += hFilter;
            if (nH >= HMax) nH -= HMax;
        }

        // 明度: 正で増加（上限 VMax に近づく）、負で減少
        if (vFilter != 0)
        {
            if (vFilter > 0)
            {
                nV += (int)(((uint)(VMax - nV) * (uint)vFilter) >> 15);
                if (nV > VMax) nV = VMax;
                nS -= (int)(((uint)nS * (uint)vFilter) >> 15);
                if (nS < 0) nS = 0;
            }
            else
            {
                nV -= (int)(((uint)nV * (uint)(-vFilter)) >> 15);
                if (nV < 0) nV = 0;
            }
        }

        // 彩度: 有彩色のみ処理
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
                    // V の実際の増加量に比例して S を補正
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
