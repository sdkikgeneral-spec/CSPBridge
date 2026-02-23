project('CSPBridge', 'cpp', 'cs',
  version: '1.0.0',
  default_options: ['cpp_std=c++17'])

# JSON を読み取る
project_dir = meson.project_source_root()
jq = find_program('jq', required: false)
if not jq.found()
  error('jq が見つかりません。Windows では `winget install jqlang.jq` を実行してから、meson setup build --reconfigure を再実行してください。')
endif
effects_result = run_command(
  jq,
  '-r',
  '.effects[].id',
  project_dir / 'effects.json',
  check: true
)
effect_ids = effects_result.stdout().strip().split('\n')
if effect_ids.length() == 1 and effect_ids[0] == ''
  effect_ids = []
endif

# Windows用C++ DLLビルド設定
if host_machine.system() == 'windows'
  # CSPBridgeBase ソースコード
  cspbridge_sources = [
    'CSPBridgeBase/BridgeBase.cpp',
    'CSPBridgeBase/BridgeCallback.cpp',
    'CSPBridgeBase/dllmain.cpp',
    'CSPBridgeBase/pch.cpp',
  ]

  foreach effect_id : effect_ids
    
    # 共通ブリッジ DLL の依存関係
    cspbridge_lib = shared_library(
        effect_id,
        cspbridge_sources,
        include_directories: [
            'CSPBridgeBase',
            '../CSP_FilterPlugIn/FilterPlugIn/TriglavPlugInSDK'
        ],
        cpp_args: [
            '/D_WINDOWS',
            '/DCSPBRIDGEBASE_EXPORTS',
            '/D_USRDLL',
            '-DEFFECT_ID="' + effect_id + '"'
        ],
        name_suffix: 'cpm',
        install: true)
  endforeach

  # ヘッダーファイルのインストール
  install_headers(
    'CSPBridgeBase/BridgeBase.h',
    'CSPBridgeBase/framework.h',
    'CSPBridgeBase/pch.h',
    'CSPBridgeBase/resource.h',
    subdir: 'CSPBridge')
else
  error('This project supports Windows only')
endif
