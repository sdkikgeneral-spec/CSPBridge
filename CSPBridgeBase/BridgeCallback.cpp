#include "pch.h"
#include "BridgeBase.h"
#include "TriglavPlugInSDK.h"

static BridgeBase g_bridgeBase;

/// <summary>
/// 
/// </summary>
/// <param name="result"></param>
/// <param name="data"></param>
/// <param name="selector"></param>
/// <param name="pluginServer"></param>
/// <param name="reserved"></param>
/// <returns></returns>
void TRIGLAV_PLUGIN_API TriglavPluginCall(TriglavPlugInInt* result, TriglavPlugInPtr* data, TriglavPlugInInt selector, TriglavPlugInServer* pluginServer, TriglavPlugInPtr reserved)
{
	*result = kTriglavPlugInCallResultFailed;
	if (pluginServer == nullptr)
	{
		assert(false);
		return;
	}

	switch (selector)
	{
	case kTriglavPlugInSelectorModuleInitialize:
		// プラグインモジュールの初期化
		if (g_bridgeBase.Initialize(pluginServer))
		{
			*result = kTriglavPlugInCallResultSuccess;
		}
		else
		{
			// 初期化に失敗
			assert(false);
		}
		break;

	case kTriglavPlugInSelectorModuleTerminate:
		// プラグインモジュールの終了
		g_bridgeBase.Terminate(pluginServer, data);
		*result = kTriglavPlugInCallResultSuccess;
		break;

	case kTriglavPlugInSelectorFilterInitialize:
		break;

	case kTriglavPlugInSelectorFilterRun:
		break;

	case kTriglavPlugInSelectorFilterTerminate:
		break;

	default:
		assert(false);
		break;
	}
}
