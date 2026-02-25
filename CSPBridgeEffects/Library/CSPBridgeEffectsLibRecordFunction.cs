namespace CSPBridgeEffects.Library.SDK;

/// <summary>
/// TriglavPlugInRecordFunction.h のマクロを C# で再現するラッパーです。
/// </summary>
/// <remarks>
/// TRIGLAV_PLUGIN_ACTIVATION の有無に応じて呼び出し先レコードを切り替え、
/// マクロ同等の引数展開（hostPermission 自動取得を含む）を提供します。
/// </remarks>
public static unsafe class CSPBridgeEffectsLibRecordFunction
{
#if TRIGLAV_PLUGIN_ACTIVATION
    // Activation 分岐: RecordFunction.h の Activation マクロ相当
    /// <summary>
    /// フィルタ初期化レコードを取得します（Activation）。
    /// </summary>
    public static TriglavPlugInFilterActivationInitializeRecord* TriglavPlugInGetFilterInitializeRecord(TriglavPlugInRecordSuite* record)
        => record->filterActivationInitializeRecord;

    /// <summary>
    /// フィルタカテゴリ名を設定します（Activation）。
    /// </summary>
    public static int TriglavPlugInFilterInitializeSetFilterCategoryName(TriglavPlugInRecordSuite* record, TriglavPlugInHostObject hostObject, TriglavPlugInStringObject filterCategoryName, sbyte accessKey)
    {
        var filterInitializeRecord = record->filterActivationInitializeRecord;
        return filterInitializeRecord->setFilterCategoryNameProc(hostObject, filterInitializeRecord->getHostPermissionProc(hostObject), filterCategoryName, accessKey);
    }

    /// <summary>
    /// フィルタ名を設定します（Activation）。
    /// </summary>
    public static int TriglavPlugInFilterInitializeSetFilterName(TriglavPlugInRecordSuite* record, TriglavPlugInHostObject hostObject, TriglavPlugInStringObject filterName, sbyte accessKey)
    {
        var filterInitializeRecord = record->filterActivationInitializeRecord;
        return filterInitializeRecord->setFilterNameProc(hostObject, filterInitializeRecord->getHostPermissionProc(hostObject), filterName, accessKey);
    }

    /// <summary>
    /// プレビュー可否を設定します（Activation）。
    /// </summary>
    public static int TriglavPlugInFilterInitializeSetCanPreview(TriglavPlugInRecordSuite* record, TriglavPlugInHostObject hostObject, byte canPreview)
    {
        var filterInitializeRecord = record->filterActivationInitializeRecord;
        return filterInitializeRecord->setCanPreviewProc(hostObject, filterInitializeRecord->getHostPermissionProc(hostObject), canPreview);
    }

    /// <summary>
    /// 空白画像使用可否を設定します（Activation）。
    /// </summary>
    public static int TriglavPlugInFilterInitializeSetUseBlankImage(TriglavPlugInRecordSuite* record, TriglavPlugInHostObject hostObject, byte useBlankImage)
    {
        var filterInitializeRecord = record->filterActivationInitializeRecord;
        return filterInitializeRecord->setUseBlankImageProc(hostObject, filterInitializeRecord->getHostPermissionProc(hostObject), useBlankImage);
    }

    /// <summary>
    /// 対象レイヤー種別を設定します（Activation）。
    /// </summary>
    public static int TriglavPlugInFilterInitializeSetTargetKinds(TriglavPlugInRecordSuite* record, TriglavPlugInHostObject hostObject, int* targetKinds, int targetKindCount)
    {
        var filterInitializeRecord = record->filterActivationInitializeRecord;
        return filterInitializeRecord->setTargetKindsProc(hostObject, filterInitializeRecord->getHostPermissionProc(hostObject), targetKinds, targetKindCount);
    }

    /// <summary>
    /// プロパティオブジェクトを設定します（Activation）。
    /// </summary>
    public static int TriglavPlugInFilterInitializeSetProperty(TriglavPlugInRecordSuite* record, TriglavPlugInHostObject hostObject, TriglavPlugInPropertyObject propertyObject)
    {
        var filterInitializeRecord = record->filterActivationInitializeRecord;
        return filterInitializeRecord->setPropertyProc(hostObject, filterInitializeRecord->getHostPermissionProc(hostObject), propertyObject);
    }

    /// <summary>
    /// プロパティコールバックを設定します（Activation）。
    /// </summary>
    public static int TriglavPlugInFilterInitializeSetPropertyCallBack(
        TriglavPlugInRecordSuite* record,
        TriglavPlugInHostObject hostObject,
        delegate* unmanaged[Cdecl]<int*, TriglavPlugInPropertyObject, int, int, void*, void> propertyCallBackProc,
        void* data)
    {
        var filterInitializeRecord = record->filterActivationInitializeRecord;
        return filterInitializeRecord->setPropertyCallBackProc(hostObject, filterInitializeRecord->getHostPermissionProc(hostObject), propertyCallBackProc, data);
    }

    /// <summary>
    /// フィルタ実行レコードを取得します（Activation）。
    /// </summary>
    public static TriglavPlugInFilterActivationRunRecord* TriglavPlugInGetFilterRunRecord(TriglavPlugInRecordSuite* record)
        => record->filterActivationRunRecord;

    /// <summary>
    /// 実行時プロパティを取得します（Activation）。
    /// </summary>
    public static int TriglavPlugInFilterRunGetProperty(TriglavPlugInRecordSuite* record, TriglavPlugInPropertyObject* propertyObject, TriglavPlugInHostObject hostObject)
    {
        var filterRunRecord = record->filterActivationRunRecord;
        return filterRunRecord->getPropertyProc(propertyObject, hostObject, filterRunRecord->getHostPermissionProc(hostObject));
    }

    /// <summary>
    /// キャンバス幅を取得します（Activation）。
    /// </summary>
    public static int TriglavPlugInFilterRunGetCanvasWidth(TriglavPlugInRecordSuite* record, int* width, TriglavPlugInHostObject hostObject)
    {
        var filterRunRecord = record->filterActivationRunRecord;
        return filterRunRecord->getCanvasWidthProc(width, hostObject, filterRunRecord->getHostPermissionProc(hostObject));
    }

    /// <summary>
    /// キャンバス高さを取得します（Activation）。
    /// </summary>
    public static int TriglavPlugInFilterRunGetCanvasHeight(TriglavPlugInRecordSuite* record, int* height, TriglavPlugInHostObject hostObject)
    {
        var filterRunRecord = record->filterActivationRunRecord;
        return filterRunRecord->getCanvasHeightProc(height, hostObject, filterRunRecord->getHostPermissionProc(hostObject));
    }

    /// <summary>
    /// キャンバス解像度を取得します（Activation）。
    /// </summary>
    public static int TriglavPlugInFilterRunGetCanvasResolution(TriglavPlugInRecordSuite* record, double* resolution, TriglavPlugInHostObject hostObject)
    {
        var filterRunRecord = record->filterActivationRunRecord;
        return filterRunRecord->getCanvasResolutionProc(resolution, hostObject, filterRunRecord->getHostPermissionProc(hostObject));
    }

    /// <summary>
    /// レイヤー原点を取得します（Activation）。
    /// </summary>
    public static int TriglavPlugInFilterRunGetLayerOrigin(TriglavPlugInRecordSuite* record, TriglavPlugInPoint* layerOrigin, TriglavPlugInHostObject hostObject)
    {
        var filterRunRecord = record->filterActivationRunRecord;
        return filterRunRecord->getLayerOriginProc(layerOrigin, hostObject, filterRunRecord->getHostPermissionProc(hostObject));
    }

    /// <summary>
    /// レイヤーマスク選択状態を取得します（Activation）。
    /// </summary>
    public static int TriglavPlugInFilterRunIsLayerMaskSelected(TriglavPlugInRecordSuite* record, byte* selected, TriglavPlugInHostObject hostObject)
    {
        var filterRunRecord = record->filterActivationRunRecord;
        return filterRunRecord->isLayerMaskSelectedProc(selected, hostObject, filterRunRecord->getHostPermissionProc(hostObject));
    }

    /// <summary>
    /// アルファロック状態を取得します（Activation）。
    /// </summary>
    public static int TriglavPlugInFilterRunIsAlphaLocked(TriglavPlugInRecordSuite* record, byte* locked, TriglavPlugInHostObject hostObject)
    {
        var filterRunRecord = record->filterActivationRunRecord;
        return filterRunRecord->isAlphaLockedProc(locked, hostObject, filterRunRecord->getHostPermissionProc(hostObject));
    }

    /// <summary>
    /// ソースオフスクリーンを取得します（Activation）。
    /// </summary>
    public static int TriglavPlugInFilterRunGetSourceOffscreen(TriglavPlugInRecordSuite* record, TriglavPlugInOffscreenObject* offscreenObject, TriglavPlugInHostObject hostObject)
    {
        var filterRunRecord = record->filterActivationRunRecord;
        return filterRunRecord->getSourceOffscreenProc(offscreenObject, hostObject, filterRunRecord->getHostPermissionProc(hostObject));
    }

    /// <summary>
    /// デスティネーションオフスクリーンを取得します（Activation）。
    /// </summary>
    public static int TriglavPlugInFilterRunGetDestinationOffscreen(TriglavPlugInRecordSuite* record, TriglavPlugInOffscreenObject* offscreenObject, TriglavPlugInHostObject hostObject)
    {
        var filterRunRecord = record->filterActivationRunRecord;
        return filterRunRecord->getDestinationOffscreenProc(offscreenObject, hostObject, filterRunRecord->getHostPermissionProc(hostObject));
    }

    /// <summary>
    /// 選択範囲矩形を取得します（Activation）。
    /// </summary>
    public static int TriglavPlugInFilterRunGetSelectAreaRect(TriglavPlugInRecordSuite* record, TriglavPlugInRect* selectAreaRect, TriglavPlugInHostObject hostObject)
    {
        var filterRunRecord = record->filterActivationRunRecord;
        return filterRunRecord->getSelectAreaRectProc(selectAreaRect, hostObject, filterRunRecord->getHostPermissionProc(hostObject));
    }

    /// <summary>
    /// 選択範囲オフスクリーンの有無を取得します（Activation）。
    /// </summary>
    public static int TriglavPlugInFilterRunHasSelectAreaOffscreen(TriglavPlugInRecordSuite* record, byte* hasSelectAreaOffscreen, TriglavPlugInHostObject hostObject)
    {
        var filterRunRecord = record->filterActivationRunRecord;
        return filterRunRecord->hasSelectAreaOffscreenProc(hasSelectAreaOffscreen, hostObject, filterRunRecord->getHostPermissionProc(hostObject));
    }

    /// <summary>
    /// 選択範囲オフスクリーンを取得します（Activation）。
    /// </summary>
    public static int TriglavPlugInFilterRunGetSelectAreaOffscreen(TriglavPlugInRecordSuite* record, TriglavPlugInOffscreenObject* offscreenObject, TriglavPlugInHostObject hostObject)
    {
        var filterRunRecord = record->filterActivationRunRecord;
        return filterRunRecord->getSelectAreaOffscreenProc(offscreenObject, hostObject, filterRunRecord->getHostPermissionProc(hostObject));
    }

    /// <summary>
    /// 更新矩形を通知します（Activation）。
    /// </summary>
    public static int TriglavPlugInFilterRunUpdateDestinationOffscreenRect(TriglavPlugInRecordSuite* record, TriglavPlugInHostObject hostObject, TriglavPlugInRect* updateRect)
    {
        var filterRunRecord = record->filterActivationRunRecord;
        return filterRunRecord->updateDestinationOffscreenRectProc(hostObject, filterRunRecord->getHostPermissionProc(hostObject), updateRect);
    }

    /// <summary>
    /// メイン色を取得します（Activation）。
    /// </summary>
    public static int TriglavPlugInFilterRunGetMainColor(TriglavPlugInRecordSuite* record, TriglavPlugInRGBColor* mainColor, byte* mainAlpha, TriglavPlugInHostObject hostObject)
    {
        var filterRunRecord = record->filterActivationRunRecord;
        return filterRunRecord->getMainColorProc(mainColor, mainAlpha, hostObject, filterRunRecord->getHostPermissionProc(hostObject));
    }

    /// <summary>
    /// サブ色を取得します（Activation）。
    /// </summary>
    public static int TriglavPlugInFilterRunGetSubColor(TriglavPlugInRecordSuite* record, TriglavPlugInRGBColor* subColor, byte* subAlpha, TriglavPlugInHostObject hostObject)
    {
        var filterRunRecord = record->filterActivationRunRecord;
        return filterRunRecord->getSubColorProc(subColor, subAlpha, hostObject, filterRunRecord->getHostPermissionProc(hostObject));
    }

    /// <summary>
    /// 描画色を取得します（Activation）。
    /// </summary>
    public static int TriglavPlugInFilterRunGetDrawColor(TriglavPlugInRecordSuite* record, TriglavPlugInRGBColor* drawColor, byte* drawAlpha, TriglavPlugInHostObject hostObject)
    {
        var filterRunRecord = record->filterActivationRunRecord;
        return filterRunRecord->getDrawColorProc(drawColor, drawAlpha, hostObject, filterRunRecord->getHostPermissionProc(hostObject));
    }

    /// <summary>
    /// フィルタ処理を実行します（Activation）。
    /// </summary>
    public static int TriglavPlugInFilterRunProcess(TriglavPlugInRecordSuite* record, int* result, TriglavPlugInHostObject hostObject, int processState)
    {
        var filterRunRecord = record->filterActivationRunRecord;
        return filterRunRecord->processProc(result, hostObject, filterRunRecord->getHostPermissionProc(hostObject), processState);
    }

    /// <summary>
    /// 進捗総数を設定します（Activation）。
    /// </summary>
    public static int TriglavPlugInFilterRunSetProgressTotal(TriglavPlugInRecordSuite* record, TriglavPlugInHostObject hostObject, int progressTotal)
    {
        var filterRunRecord = record->filterActivationRunRecord;
        return filterRunRecord->setProgressTotalProc(hostObject, filterRunRecord->getHostPermissionProc(hostObject), progressTotal);
    }

    /// <summary>
    /// 進捗完了数を設定します（Activation）。
    /// </summary>
    public static int TriglavPlugInFilterRunSetProgressDone(TriglavPlugInRecordSuite* record, TriglavPlugInHostObject hostObject, int progressDone)
    {
        var filterRunRecord = record->filterActivationRunRecord;
        return filterRunRecord->setProgressDoneProc(hostObject, filterRunRecord->getHostPermissionProc(hostObject), progressDone);
    }
#else
    /// <summary>
    /// フィルタ初期化レコードを取得します（非Activation）。
    /// </summary>
    public static TriglavPlugInFilterInitializeRecord* TriglavPlugInGetFilterInitializeRecord(TriglavPlugInRecordSuite* record)
        => record->filterInitializeRecord;

    /// <summary>
    /// フィルタカテゴリ名を設定します。
    /// </summary>
    public static int TriglavPlugInFilterInitializeSetFilterCategoryName(TriglavPlugInRecordSuite* record, TriglavPlugInHostObject hostObject, TriglavPlugInStringObject filterCategoryName, sbyte accessKey)
        => record->filterInitializeRecord->setFilterCategoryNameProc(hostObject, filterCategoryName, accessKey);

    /// <summary>
    /// フィルタ名を設定します。
    /// </summary>
    public static int TriglavPlugInFilterInitializeSetFilterName(TriglavPlugInRecordSuite* record, TriglavPlugInHostObject hostObject, TriglavPlugInStringObject filterName, sbyte accessKey)
        => record->filterInitializeRecord->setFilterNameProc(hostObject, filterName, accessKey);

    /// <summary>
    /// プレビュー可否を設定します。
    /// </summary>
    public static int TriglavPlugInFilterInitializeSetCanPreview(TriglavPlugInRecordSuite* record, TriglavPlugInHostObject hostObject, byte canPreview)
        => record->filterInitializeRecord->setCanPreviewProc(hostObject, canPreview);

    /// <summary>
    /// 空白画像使用可否を設定します。
    /// </summary>
    public static int TriglavPlugInFilterInitializeSetUseBlankImage(TriglavPlugInRecordSuite* record, TriglavPlugInHostObject hostObject, byte useBlankImage)
        => record->filterInitializeRecord->setUseBlankImageProc(hostObject, useBlankImage);

    /// <summary>
    /// 対象レイヤー種別を設定します。
    /// </summary>
    public static int TriglavPlugInFilterInitializeSetTargetKinds(TriglavPlugInRecordSuite* record, TriglavPlugInHostObject hostObject, int* targetKinds, int targetKindCount)
        => record->filterInitializeRecord->setTargetKindsProc(hostObject, targetKinds, targetKindCount);

    /// <summary>
    /// プロパティオブジェクトを設定します。
    /// </summary>
    public static int TriglavPlugInFilterInitializeSetProperty(TriglavPlugInRecordSuite* record, TriglavPlugInHostObject hostObject, TriglavPlugInPropertyObject propertyObject)
        => record->filterInitializeRecord->setPropertyProc(hostObject, propertyObject);

    /// <summary>
    /// プロパティコールバックを設定します。
    /// </summary>
    public static int TriglavPlugInFilterInitializeSetPropertyCallBack(
        TriglavPlugInRecordSuite* record,
        TriglavPlugInHostObject hostObject,
        delegate* unmanaged[Cdecl]<int*, TriglavPlugInPropertyObject, int, int, void*, void> propertyCallBackProc,
        void* data)
        => record->filterInitializeRecord->setPropertyCallBackProc(hostObject, propertyCallBackProc, data);

    /// <summary>
    /// フィルタ実行レコードを取得します（非Activation）。
    /// </summary>
    public static TriglavPlugInFilterRunRecord* TriglavPlugInGetFilterRunRecord(TriglavPlugInRecordSuite* record)
        => record->filterRunRecord;

    /// <summary>
    /// 実行時プロパティを取得します。
    /// </summary>
    public static int TriglavPlugInFilterRunGetProperty(TriglavPlugInRecordSuite* record, TriglavPlugInPropertyObject* propertyObject, TriglavPlugInHostObject hostObject)
        => record->filterRunRecord->getPropertyProc(propertyObject, hostObject);

    /// <summary>
    /// キャンバス幅を取得します。
    /// </summary>
    public static int TriglavPlugInFilterRunGetCanvasWidth(TriglavPlugInRecordSuite* record, int* width, TriglavPlugInHostObject hostObject)
        => record->filterRunRecord->getCanvasWidthProc(width, hostObject);

    /// <summary>
    /// キャンバス高さを取得します。
    /// </summary>
    public static int TriglavPlugInFilterRunGetCanvasHeight(TriglavPlugInRecordSuite* record, int* height, TriglavPlugInHostObject hostObject)
        => record->filterRunRecord->getCanvasHeightProc(height, hostObject);

    /// <summary>
    /// キャンバス解像度を取得します。
    /// </summary>
    public static int TriglavPlugInFilterRunGetCanvasResolution(TriglavPlugInRecordSuite* record, double* resolution, TriglavPlugInHostObject hostObject)
        => record->filterRunRecord->getCanvasResolutionProc(resolution, hostObject);

    /// <summary>
    /// レイヤー原点を取得します。
    /// </summary>
    public static int TriglavPlugInFilterRunGetLayerOrigin(TriglavPlugInRecordSuite* record, TriglavPlugInPoint* layerOrigin, TriglavPlugInHostObject hostObject)
        => record->filterRunRecord->getLayerOriginProc(layerOrigin, hostObject);

    /// <summary>
    /// レイヤーマスク選択状態を取得します。
    /// </summary>
    public static int TriglavPlugInFilterRunIsLayerMaskSelected(TriglavPlugInRecordSuite* record, byte* selected, TriglavPlugInHostObject hostObject)
        => record->filterRunRecord->isLayerMaskSelectedProc(selected, hostObject);

    /// <summary>
    /// アルファロック状態を取得します。
    /// </summary>
    public static int TriglavPlugInFilterRunIsAlphaLocked(TriglavPlugInRecordSuite* record, byte* locked, TriglavPlugInHostObject hostObject)
        => record->filterRunRecord->isAlphaLockedProc(locked, hostObject);

    /// <summary>
    /// ソースオフスクリーンを取得します。
    /// </summary>
    public static int TriglavPlugInFilterRunGetSourceOffscreen(TriglavPlugInRecordSuite* record, TriglavPlugInOffscreenObject* offscreenObject, TriglavPlugInHostObject hostObject)
        => record->filterRunRecord->getSourceOffscreenProc(offscreenObject, hostObject);

    /// <summary>
    /// デスティネーションオフスクリーンを取得します。
    /// </summary>
    public static int TriglavPlugInFilterRunGetDestinationOffscreen(TriglavPlugInRecordSuite* record, TriglavPlugInOffscreenObject* offscreenObject, TriglavPlugInHostObject hostObject)
        => record->filterRunRecord->getDestinationOffscreenProc(offscreenObject, hostObject);

    /// <summary>
    /// 選択範囲矩形を取得します。
    /// </summary>
    public static int TriglavPlugInFilterRunGetSelectAreaRect(TriglavPlugInRecordSuite* record, TriglavPlugInRect* selectAreaRect, TriglavPlugInHostObject hostObject)
        => record->filterRunRecord->getSelectAreaRectProc(selectAreaRect, hostObject);

    /// <summary>
    /// 選択範囲オフスクリーンの有無を取得します。
    /// </summary>
    public static int TriglavPlugInFilterRunHasSelectAreaOffscreen(TriglavPlugInRecordSuite* record, byte* hasSelectAreaOffscreen, TriglavPlugInHostObject hostObject)
        => record->filterRunRecord->hasSelectAreaOffscreenProc(hasSelectAreaOffscreen, hostObject);

    /// <summary>
    /// 選択範囲オフスクリーンを取得します。
    /// </summary>
    public static int TriglavPlugInFilterRunGetSelectAreaOffscreen(TriglavPlugInRecordSuite* record, TriglavPlugInOffscreenObject* offscreenObject, TriglavPlugInHostObject hostObject)
        => record->filterRunRecord->getSelectAreaOffscreenProc(offscreenObject, hostObject);

    /// <summary>
    /// 更新矩形を通知します。
    /// </summary>
    public static int TriglavPlugInFilterRunUpdateDestinationOffscreenRect(TriglavPlugInRecordSuite* record, TriglavPlugInHostObject hostObject, TriglavPlugInRect* updateRect)
        => record->filterRunRecord->updateDestinationOffscreenRectProc(hostObject, updateRect);

    /// <summary>
    /// メイン色を取得します。
    /// </summary>
    public static int TriglavPlugInFilterRunGetMainColor(TriglavPlugInRecordSuite* record, TriglavPlugInRGBColor* mainColor, byte* mainAlpha, TriglavPlugInHostObject hostObject)
        => record->filterRunRecord->getMainColorProc(mainColor, mainAlpha, hostObject);

    /// <summary>
    /// サブ色を取得します。
    /// </summary>
    public static int TriglavPlugInFilterRunGetSubColor(TriglavPlugInRecordSuite* record, TriglavPlugInRGBColor* subColor, byte* subAlpha, TriglavPlugInHostObject hostObject)
        => record->filterRunRecord->getSubColorProc(subColor, subAlpha, hostObject);

    /// <summary>
    /// 描画色を取得します。
    /// </summary>
    public static int TriglavPlugInFilterRunGetDrawColor(TriglavPlugInRecordSuite* record, TriglavPlugInRGBColor* drawColor, byte* drawAlpha, TriglavPlugInHostObject hostObject)
        => record->filterRunRecord->getDrawColorProc(drawColor, drawAlpha, hostObject);

    /// <summary>
    /// フィルタ処理を実行します。
    /// </summary>
    public static int TriglavPlugInFilterRunProcess(TriglavPlugInRecordSuite* record, int* result, TriglavPlugInHostObject hostObject, int processState)
        => record->filterRunRecord->processProc(result, hostObject, processState);

    /// <summary>
    /// 進捗総数を設定します。
    /// </summary>
    public static int TriglavPlugInFilterRunSetProgressTotal(TriglavPlugInRecordSuite* record, TriglavPlugInHostObject hostObject, int progressTotal)
        => record->filterRunRecord->setProgressTotalProc(hostObject, progressTotal);

    /// <summary>
    /// 進捗完了数を設定します。
    /// </summary>
    public static int TriglavPlugInFilterRunSetProgressDone(TriglavPlugInRecordSuite* record, TriglavPlugInHostObject hostObject, int progressDone)
        => record->filterRunRecord->setProgressDoneProc(hostObject, progressDone);
#endif
}
