#include "pch.h"
#include "BridgeBase.h"
#include "BridgeFilter.h"
#include "BridgeProperty.h"
#include "TriglavPlugInSDK.h"

static BridgeBase g_bridgeBase;
static const char* kEffectId = EFFECT_ID;

/// <summary>
/// 
/// </summary>
/// <param name="result"></param>
/// <param name="data"></param>
/// <param name="selector"></param>
/// <param name="pluginServer"></param>
/// <param name="reserved"></param>
/// <returns></returns>
void TRIGLAV_PLUGIN_API TriglavPluginCall(
	TriglavPlugInInt* result,
	TriglavPlugInPtr* data,
	TriglavPlugInInt selector,
	TriglavPlugInServer* pluginServer,
	TriglavPlugInPtr reserved
)
{
	if (result == nullptr || pluginServer == nullptr)
	{
		assert(false);
		return;
	}

	switch (selector)
	{
	case kTriglavPlugInSelectorModuleInitialize:
		// プラグインモジュールの初期化
		*result = g_bridgeBase.Initialize(pluginServer);
		if (*result != kTriglavPlugInCallResultSuccess)
		{
			// 初期化に失敗
			assert(false);
		}
		break;

	case kTriglavPlugInSelectorModuleTerminate:
		// プラグインモジュールの終了
		*result = g_bridgeBase.Terminate(pluginServer, data);
		*data = nullptr;
		break;

	case kTriglavPlugInSelectorFilterInitialize:
		// フィルタの初期化
		*result = g_bridgeBase.FilterInitialize(pluginServer, data);
		if (*result != kTriglavPlugInCallResultSuccess)
		{
			// フィルターの初期化に失敗
			assert(false);
		}
		break;

	case kTriglavPlugInSelectorFilterRun:
		// フィルタの実行
		*result = g_bridgeBase.FilterRun(pluginServer, data);
		break;

	case kTriglavPlugInSelectorFilterTerminate:
		// フィルタの終了処理
		*result = g_bridgeBase.FilterTerminate(pluginServer, data);
		break;

	default:
		assert(false);
		break;
	}
}
