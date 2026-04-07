using System;
using System.Buffers;
using CSPBridgeEffects.Library.SDK;
using static CSPBridgeEffects.Library.SDK.CSPBridgeEffectsLibDefine;
using static CSPBridgeEffects.Library.SDK.CSPBridgeEffectsLibRecordFunction;

namespace CSPBridgeEffects.Effects;

/// <summary>
/// プレビューの Restart 時にプロパティ値を読み取るコールバック。
/// </summary>
internal unsafe delegate void ReadParametersDelegate(
    TriglavPlugInPropertyService* propertySvc,
    TriglavPlugInPropertyObject   propObj);

/// <summary>
/// 1 ブロック分のピクセル処理を行うコールバック。
/// blockIndex はブロック配列のインデックス。blockRect は値コピーで渡す
/// （ラムダ内で ref キャプチャできない C# の制約への対処）。
/// </summary>
internal unsafe delegate void ProcessBlockDelegate(
    TriglavPlugInOffscreenService* offscreenSvc,
    TriglavPlugInOffscreenObject   dstOffscreen,
    TriglavPlugInRect              blockRect,
    int                            blockIndex);

/// <summary>
/// エフェクト実装の共通ヘルパーメソッドを提供します。
/// </summary>
internal static unsafe class EffectHelper
{
    /// <summary>
    /// ASCII 文字列から TriglavPlugInStringObject を作成します。
    /// 呼び出し元は使用後に releaseProc を呼び出してください。
    /// </summary>
    internal static TriglavPlugInStringObject CreateAsciiString(
        TriglavPlugInStringService* service, string text)
    {
        TriglavPlugInStringObject result = default;
        int len = text.Length;
        Span<sbyte> buf = stackalloc sbyte[len + 1];
        for (int i = 0; i < len; i++)
            buf[i] = (sbyte)text[i];
        buf[len] = 0;
        fixed (sbyte* p = buf)
            service->createWithAsciiStringProc(&result, p, len);
        return result;
    }

    /// <summary>
    /// モジュールを初期化します（ホストバージョン取得・モジュールID設定・モジュール種別設定）。
    /// </summary>
    internal static int InitializeModule(TriglavPlugInServer* server, string moduleId)
    {
        var record  = &server->recordSuite;
        var service = &server->serviceSuite;
        var host    = server->hostObject;

        // ホストバージョン取得（必須）
        int hostVersion = 0;
        record->moduleInitializeRecord->getHostVersionProc(&hostVersion, host);

        // モジュール ID を設定
        var idStr = CreateAsciiString(service->stringService, moduleId);
        record->moduleInitializeRecord->setModuleIDProc(host, idStr);
        service->stringService->releaseProc(idStr);

        // モジュール種別を設定（Activation 有無に応じてマクロで切り替え）
        record->moduleInitializeRecord->setModuleKindProc(host, kTriglavPlugInModuleSwitchKindFilter);

        return kTriglavPlugInCallResultSuccess;
    }

    /// <summary>
    /// フィルタを初期化します（カテゴリ名・フィルタ名・プレビュー可否・ターゲット種別を設定）。
    /// </summary>
    internal static int InitializeFilter(
        TriglavPlugInServer* server,
        string categoryName,
        string filterName,
        ReadOnlySpan<int> targetKinds)
    {
        var record  = &server->recordSuite;
        var service = &server->serviceSuite;
        var host    = server->hostObject;

        // カテゴリ名
        var catStr = CreateAsciiString(service->stringService, categoryName);
        TriglavPlugInFilterInitializeSetFilterCategoryName(record, host, catStr, 0);
        service->stringService->releaseProc(catStr);

        // フィルタ名
        var nameStr = CreateAsciiString(service->stringService, filterName);
        TriglavPlugInFilterInitializeSetFilterName(record, host, nameStr, 0);
        service->stringService->releaseProc(nameStr);

        // プレビュー可能
        TriglavPlugInFilterInitializeSetCanPreview(record, host, kTriglavPlugInBoolTrue);

        // ターゲット種別
        fixed (int* pKinds = targetKinds)
            TriglavPlugInFilterInitializeSetTargetKinds(record, host, pKinds, targetKinds.Length);

        return kTriglavPlugInCallResultSuccess;
    }

    /// <summary>
    /// プレビュー対応のブロック処理ループを実行します。
    /// ブロック矩形リストの取得・ArrayPool 確保・解放、プログレス管理、
    /// Start / Continue / End / Restart / Exit のステート管理をすべて内包します。
    /// Restart 時に readParameters を呼び出し、ブロック処理時に processBlock を呼び出します。
    /// </summary>
    /// <param name="server">プラグインサーバーポインタ。</param>
    /// <param name="dstOffscreen">書き込み先オフスクリーン。</param>
    /// <param name="selectRect">選択矩形（ブロック分割の基準）。</param>
    /// <param name="readParameters">Restart 時にパラメータを読み取るコールバック。</param>
    /// <param name="processBlock">1 ブロック分のピクセル処理コールバック。</param>
    internal static int RunPreviewLoop(
        TriglavPlugInServer*         server,
        TriglavPlugInOffscreenObject dstOffscreen,
        TriglavPlugInRect*           selectRect,
        ReadParametersDelegate       readParameters,
        ProcessBlockDelegate         processBlock)
    {
        if (readParameters == null || processBlock == null)
            return kTriglavPlugInCallResultFailed;

        var record       = &server->recordSuite;
        var host         = server->hostObject;
        var offscreenSvc = server->serviceSuite.offscreenService;
        var propertySvc  = server->serviceSuite.propertyService;

        // Restart 時に渡すプロパティオブジェクトをここで一度取得する。
        // FilterRun 呼び出し元がすでに取得済みの場合もあるが、
        // RunPreviewLoop が自律的に完結するよう再取得する。
        TriglavPlugInPropertyObject propObj;
        TriglavPlugInFilterRunGetProperty(record, &propObj, host);

        // ブロック矩形リストを構築
        int blockCount;
        offscreenSvc->getBlockRectCountProc(&blockCount, dstOffscreen, selectRect);
        var blockRects = ArrayPool<TriglavPlugInRect>.Shared.Rent(blockCount);
        try
        {
            fixed (TriglavPlugInRect* pBlockRects = blockRects)
                for (int i = 0; i < blockCount; i++)
                    offscreenSvc->getBlockRectProc(pBlockRects + i, i, dstOffscreen, selectRect);

            TriglavPlugInFilterRunSetProgressTotal(record, host, blockCount);

            bool restart    = true;
            int  blockIndex = 0;

            while (true)
            {
                if (restart)
                {
                    restart = false;

                    int procResult = kTriglavPlugInFilterRunProcessResultContinue;
                    TriglavPlugInFilterRunProcess(record, &procResult, host,
                        kTriglavPlugInFilterRunProcessStateStart);
                    if (procResult == kTriglavPlugInFilterRunProcessResultExit) break;

                    readParameters(propertySvc, propObj);
                    blockIndex = 0;
                }

                if (blockIndex < blockCount)
                {
                    TriglavPlugInFilterRunSetProgressDone(record, host, blockIndex);
                    // blockRect は値コピーで渡す（ラムダ内での ref キャプチャ不可のため）
                    processBlock(offscreenSvc, dstOffscreen, blockRects[blockIndex], blockIndex);
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
}
