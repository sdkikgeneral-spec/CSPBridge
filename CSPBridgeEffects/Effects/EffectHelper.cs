using System;
using CSPBridgeEffects.Library.SDK;
using static CSPBridgeEffects.Library.SDK.CSPBridgeEffectsLibDefine;
using static CSPBridgeEffects.Library.SDK.CSPBridgeEffectsLibRecordFunction;

namespace CSPBridgeEffects.Effects;

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
}
