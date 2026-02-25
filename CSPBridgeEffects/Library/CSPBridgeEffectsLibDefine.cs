namespace CSPBridgeEffects.Library.SDK;

/// <summary>
/// Defines for CSPBridgeEffectsLib.
/// </summary>
public static class CSPBridgeEffectsLibDefine
{
    public enum TriglavPlugInBool
    {
        False = 0,
        True = 1,
    }

    public enum TriglavPlugInModuleKind
    {
        Filter = 0x4380,
        FilterActivation = 0x5530,
    }

    public enum TriglavPlugInSelector
    {
        ModuleInitialize = 0x0101,
        ModuleTerminate = 0x0102,
        FilterInitialize = 0x0201,
        FilterRun = 0x0202,
        FilterTerminate = 0x0203,
    }

    public enum TriglavPlugInFilterTargetKind
    {
        RasterLayerGrayAlpha = 0x0101,
        RasterLayerRGBAlpha = 0x0102,
        RasterLayerCMYKAlpha = 0x0103,
        RasterLayerAlpha = 0x0104,
        RasterLayerBinarizationAlpha = 0x0105,
        RasterLayerBinarizationGrayAlpha = 0x0106,
    }

    public enum TriglavPlugInFilterRunProcessResult
    {
        Continue = 0x0101,
        Restart = 0x0102,
        Exit = 0x0103,
    }

    public enum TriglavPlugInFilterRunProcessState
    {
        Start = 0x0101,
        Continue = 0x0102,
        End = 0x0103,
        Abort = 0x0104,
    }

    public enum TriglavPlugInBitmapScanline
    {
        HorizontalLeftTop = 0x10,
        HorizontalRightTop = 0x11,
        HorizontalLeftBottom = 0x12,
        HorizontalRightBottom = 0x13,
        VerticalLeftTop = 0x14,
        VerticalRightTop = 0x15,
        VerticalLeftBottom = 0x16,
        VerticalRightBottom = 0x17,
    }

    public enum TriglavPlugInOffscreenChannelOrder
    {
        Alpha = 0x01,
        GrayAlpha = 0x02,
        RGBAlpha = 0x03,
        CMYKAlpha = 0x04,
        BinarizationAlpha = 0x05,
        BinarizationGrayAlpha = 0x06,
        SelectArea = 0x10,
        Plane = 0x20,
    }

    public enum TriglavPlugInOffscreenCopyMode
    {
        Normal = 0x01,
        Image = 0x02,
        Gray = 0x03,
        Red = 0x04,
        Green = 0x05,
        Blue = 0x06,
        Cyan = 0x07,
        Magenta = 0x08,
        Yellow = 0x09,
        KeyPlate = 0x10,
        Alpha = 0x11,
    }

    public enum TriglavPlugInPropertyPointMinMaxValueKind
    {
        Default = 0x21,
        No = 0x22,
    }

    public enum TriglavPlugInPropertyCallBackResult
    {
        NoModify = 0x01,
        Modify = 0x02,
        Invalid = 0x03,
    }

    public enum TriglavPlugInPropertyValueType
    {
        Void = 0x00,
        Boolean = 0x01,
        Enumeration = 0x02,
        Integer = 0x11,
        Decimal = 0x12,
        Point = 0x21,
        String = 0x31,
    }

    public enum TriglavPlugInPropertyValueKind
    {
        Default = 0x11,
        Pixel = 0x21,
    }

    public enum TriglavPlugInPropertyInputKind
    {
        Hide = 0x10,
        Default = 0x11,
        PushButton = 0x21,
        Canvas = 0x31,
    }

    public enum TriglavPlugInPropertyPointDefaultValueKind
    {
        Default = 0x11,
        CanvasLeftTop = 0x21,
        CanvasRightTop = 0x22,
        CanvasLeftBottom = 0x23,
        CanvasRightBottom = 0x24,
        CanvasCenter = 0x25,
        SelectAreaLeftTop = 0x31,
        SelectAreaRightTop = 0x32,
        SelectAreaLeftBottom = 0x33,
        SelectAreaRightBottom = 0x34,
        SelectAreaCenter = 0x35,
    }

    public enum TriglavPlugInPropertyCallBackNotify
    {
        ValueChanged = 0x11,
        ButtonPushed = 0x21,
        ValueCheck = 0x31,
    }

    public enum TriglavPlugInCallResult
    {
        Failed = -1,
        Success = 0,
    }

    public enum TriglavPlugInAPIResult
    {
        Failed = -1,
        Success = 0,
    }

    public const int kTriglavPlugInBoolTrue = (int)TriglavPlugInBool.True;
    public const int kTriglavPlugInBoolFalse = (int)TriglavPlugInBool.False;

    public const int kTriglavPlugInCallResultSuccess = (int)TriglavPlugInCallResult.Success;
    public const int kTriglavPlugInCallResultFailed = (int)TriglavPlugInCallResult.Failed;

    public const int kTriglavPlugInAPIResultSuccess = (int)TriglavPlugInAPIResult.Success;
    public const int kTriglavPlugInAPIResultFailed = (int)TriglavPlugInAPIResult.Failed;

    public const int kTriglavPlugInModuleKindFilter = (int)TriglavPlugInModuleKind.Filter;
    public const int kTriglavPlugInModuleKindFilterActivation = (int)TriglavPlugInModuleKind.FilterActivation;

#if TRIGLAV_PLUGIN_ACTIVATION
    public const int kTriglavPlugInNeedHostVersion = 3;
#else
    public const int kTriglavPlugInNeedHostVersion = 1;
#endif

#if TRIGLAV_PLUGIN_ACTIVATION
    public const int kTriglavPlugInModuleSwitchKindFilter = kTriglavPlugInModuleKindFilterActivation;
#else
    public const int kTriglavPlugInModuleSwitchKindFilter = kTriglavPlugInModuleKindFilter;
#endif

    public const int kTriglavPlugInSelectorModuleInitialize = (int)TriglavPlugInSelector.ModuleInitialize;
    public const int kTriglavPlugInSelectorModuleTerminate = (int)TriglavPlugInSelector.ModuleTerminate;
    public const int kTriglavPlugInSelectorFilterInitialize = (int)TriglavPlugInSelector.FilterInitialize;
    public const int kTriglavPlugInSelectorFilterRun = (int)TriglavPlugInSelector.FilterRun;
    public const int kTriglavPlugInSelectorFilterTerminate = (int)TriglavPlugInSelector.FilterTerminate;

    public const int kTriglavPlugInFilterTargetKindRasterLayerGrayAlpha = (int)TriglavPlugInFilterTargetKind.RasterLayerGrayAlpha;
    public const int kTriglavPlugInFilterTargetKindRasterLayerRGBAlpha = (int)TriglavPlugInFilterTargetKind.RasterLayerRGBAlpha;
    public const int kTriglavPlugInFilterTargetKindRasterLayerCMYKAlpha = (int)TriglavPlugInFilterTargetKind.RasterLayerCMYKAlpha;
    public const int kTriglavPlugInFilterTargetKindRasterLayerAlpha = (int)TriglavPlugInFilterTargetKind.RasterLayerAlpha;
    public const int kTriglavPlugInFilterTargetKindRasterLayerBinarizationAlpha = (int)TriglavPlugInFilterTargetKind.RasterLayerBinarizationAlpha;
    public const int kTriglavPlugInFilterTargetKindRasterLayerBinarizationGrayAlpha = (int)TriglavPlugInFilterTargetKind.RasterLayerBinarizationGrayAlpha;

    public const int kTriglavPlugInFilterRunProcessResultContinue = (int)TriglavPlugInFilterRunProcessResult.Continue;
    public const int kTriglavPlugInFilterRunProcessResultRestart = (int)TriglavPlugInFilterRunProcessResult.Restart;
    public const int kTriglavPlugInFilterRunProcessResultExit = (int)TriglavPlugInFilterRunProcessResult.Exit;

    public const int kTriglavPlugInFilterRunProcessStateStart = (int)TriglavPlugInFilterRunProcessState.Start;
    public const int kTriglavPlugInFilterRunProcessStateContinue = (int)TriglavPlugInFilterRunProcessState.Continue;
    public const int kTriglavPlugInFilterRunProcessStateEnd = (int)TriglavPlugInFilterRunProcessState.End;
    public const int kTriglavPlugInFilterRunProcessStateAbort = (int)TriglavPlugInFilterRunProcessState.Abort;

    public const int kTriglavPlugInBitmapScanlineHorizontalLeftTop = (int)TriglavPlugInBitmapScanline.HorizontalLeftTop;
    public const int kTriglavPlugInBitmapScanlineHorizontalRightTop = (int)TriglavPlugInBitmapScanline.HorizontalRightTop;
    public const int kTriglavPlugInBitmapScanlineHorizontalLeftBottom = (int)TriglavPlugInBitmapScanline.HorizontalLeftBottom;
    public const int kTriglavPlugInBitmapScanlineHorizontalRightBottom = (int)TriglavPlugInBitmapScanline.HorizontalRightBottom;
    public const int kTriglavPlugInBitmapScanlineVerticalLeftTop = (int)TriglavPlugInBitmapScanline.VerticalLeftTop;
    public const int kTriglavPlugInBitmapScanlineVerticalRightTop = (int)TriglavPlugInBitmapScanline.VerticalRightTop;
    public const int kTriglavPlugInBitmapScanlineVerticalLeftBottom = (int)TriglavPlugInBitmapScanline.VerticalLeftBottom;
    public const int kTriglavPlugInBitmapScanlineVerticalRightBottom = (int)TriglavPlugInBitmapScanline.VerticalRightBottom;

    public const int kTriglavPlugInOffscreenChannelOrderAlpha = (int)TriglavPlugInOffscreenChannelOrder.Alpha;
    public const int kTriglavPlugInOffscreenChannelOrderGrayAlpha = (int)TriglavPlugInOffscreenChannelOrder.GrayAlpha;
    public const int kTriglavPlugInOffscreenChannelOrderRGBAlpha = (int)TriglavPlugInOffscreenChannelOrder.RGBAlpha;
    public const int kTriglavPlugInOffscreenChannelOrderCMYKAlpha = (int)TriglavPlugInOffscreenChannelOrder.CMYKAlpha;
    public const int kTriglavPlugInOffscreenChannelOrderBinarizationAlpha = (int)TriglavPlugInOffscreenChannelOrder.BinarizationAlpha;
    public const int kTriglavPlugInOffscreenChannelOrderBinarizationGrayAlpha = (int)TriglavPlugInOffscreenChannelOrder.BinarizationGrayAlpha;
    public const int kTriglavPlugInOffscreenChannelOrderSelectArea = (int)TriglavPlugInOffscreenChannelOrder.SelectArea;
    public const int kTriglavPlugInOffscreenChannelOrderPlane = (int)TriglavPlugInOffscreenChannelOrder.Plane;

    public const int kTriglavPlugInOffscreenCopyModeNormal = (int)TriglavPlugInOffscreenCopyMode.Normal;
    public const int kTriglavPlugInOffscreenCopyModeImage = (int)TriglavPlugInOffscreenCopyMode.Image;
    public const int kTriglavPlugInOffscreenCopyModeGray = (int)TriglavPlugInOffscreenCopyMode.Gray;
    public const int kTriglavPlugInOffscreenCopyModeRed = (int)TriglavPlugInOffscreenCopyMode.Red;
    public const int kTriglavPlugInOffscreenCopyModeGreen = (int)TriglavPlugInOffscreenCopyMode.Green;
    public const int kTriglavPlugInOffscreenCopyModeBlue = (int)TriglavPlugInOffscreenCopyMode.Blue;
    public const int kTriglavPlugInOffscreenCopyModeCyan = (int)TriglavPlugInOffscreenCopyMode.Cyan;
    public const int kTriglavPlugInOffscreenCopyModeMagenta = (int)TriglavPlugInOffscreenCopyMode.Magenta;
    public const int kTriglavPlugInOffscreenCopyModeYellow = (int)TriglavPlugInOffscreenCopyMode.Yellow;
    public const int kTriglavPlugInOffscreenCopyModeKeyPlate = (int)TriglavPlugInOffscreenCopyMode.KeyPlate;
    public const int kTriglavPlugInOffscreenCopyModeAlpha = (int)TriglavPlugInOffscreenCopyMode.Alpha;

    public const int kTriglavPlugInPropertyValueTypeVoid = (int)TriglavPlugInPropertyValueType.Void;
    public const int kTriglavPlugInPropertyValueTypeBoolean = (int)TriglavPlugInPropertyValueType.Boolean;
    public const int kTriglavPlugInPropertyValueTypeEnumeration = (int)TriglavPlugInPropertyValueType.Enumeration;
    public const int kTriglavPlugInPropertyValueTypeInteger = (int)TriglavPlugInPropertyValueType.Integer;
    public const int kTriglavPlugInPropertyValueTypeDecimal = (int)TriglavPlugInPropertyValueType.Decimal;
    public const int kTriglavPlugInPropertyValueTypePoint = (int)TriglavPlugInPropertyValueType.Point;
    public const int kTriglavPlugInPropertyValueTypeString = (int)TriglavPlugInPropertyValueType.String;

    public const int kTriglavPlugInPropertyValueKindDefault = (int)TriglavPlugInPropertyValueKind.Default;
    public const int kTriglavPlugInPropertyValueKindPixel = (int)TriglavPlugInPropertyValueKind.Pixel;

    public const int kTriglavPlugInPropertyInputKindHide = (int)TriglavPlugInPropertyInputKind.Hide;
    public const int kTriglavPlugInPropertyInputKindDefault = (int)TriglavPlugInPropertyInputKind.Default;
    public const int kTriglavPlugInPropertyInputKindPushButton = (int)TriglavPlugInPropertyInputKind.PushButton;
    public const int kTriglavPlugInPropertyInputKindCanvas = (int)TriglavPlugInPropertyInputKind.Canvas;

    public const int kTriglavPlugInPropertyPointDefaultValueKindDefault = (int)TriglavPlugInPropertyPointDefaultValueKind.Default;
    public const int kTriglavPlugInPropertyPointDefaultValueKindCanvasLeftTop = (int)TriglavPlugInPropertyPointDefaultValueKind.CanvasLeftTop;
    public const int kTriglavPlugInPropertyPointDefaultValueKindCanvasRightTop = (int)TriglavPlugInPropertyPointDefaultValueKind.CanvasRightTop;
    public const int kTriglavPlugInPropertyPointDefaultValueKindCanvasLeftBottom = (int)TriglavPlugInPropertyPointDefaultValueKind.CanvasLeftBottom;
    public const int kTriglavPlugInPropertyPointDefaultValueKindCanvasRightBottom = (int)TriglavPlugInPropertyPointDefaultValueKind.CanvasRightBottom;
    public const int kTriglavPlugInPropertyPointDefaultValueKindCanvasCenter = (int)TriglavPlugInPropertyPointDefaultValueKind.CanvasCenter;
    public const int kTriglavPlugInPropertyPointDefaultValueKindSelectAreaLeftTop = (int)TriglavPlugInPropertyPointDefaultValueKind.SelectAreaLeftTop;
    public const int kTriglavPlugInPropertyPointDefaultValueKindSelectAreaRightTop = (int)TriglavPlugInPropertyPointDefaultValueKind.SelectAreaRightTop;
    public const int kTriglavPlugInPropertyPointDefaultValueKindSelectAreaLeftBottom = (int)TriglavPlugInPropertyPointDefaultValueKind.SelectAreaLeftBottom;
    public const int kTriglavPlugInPropertyPointDefaultValueKindSelectAreaRightBottom = (int)TriglavPlugInPropertyPointDefaultValueKind.SelectAreaRightBottom;
    public const int kTriglavPlugInPropertyPointDefaultValueKindSelectAreaCenter = (int)TriglavPlugInPropertyPointDefaultValueKind.SelectAreaCenter;

    public const int kTriglavPlugInPropertyPointMinMaxValueKindDefault = (int)TriglavPlugInPropertyPointMinMaxValueKind.Default;
    public const int kTriglavPlugInPropertyPointMinMaxValueKindNo = (int)TriglavPlugInPropertyPointMinMaxValueKind.No;

    public const int kTriglavPlugInPropertyCallBackNotifyValueChanged = (int)TriglavPlugInPropertyCallBackNotify.ValueChanged;
    public const int kTriglavPlugInPropertyCallBackNotifyButtonPushed = (int)TriglavPlugInPropertyCallBackNotify.ButtonPushed;
    public const int kTriglavPlugInPropertyCallBackNotifyValueCheck = (int)TriglavPlugInPropertyCallBackNotify.ValueCheck;

    public const int kTriglavPlugInPropertyCallBackResultNoModify = (int)TriglavPlugInPropertyCallBackResult.NoModify;
    public const int kTriglavPlugInPropertyCallBackResultModify = (int)TriglavPlugInPropertyCallBackResult.Modify;
    public const int kTriglavPlugInPropertyCallBackResultInvalid = (int)TriglavPlugInPropertyCallBackResult.Invalid;
}
