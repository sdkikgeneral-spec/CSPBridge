project('CSPBridge', 'cpp', 'cs',
  version: '1.0.0',
  default_options: ['cpp_std=c++17'])

# JSON を読み取る
project_dir = meson.project_source_root()
fs = import('fs')

csp_filterplugin_dir = project_dir / 'CSP_FilterPlugIn'
csp_filterplugin_sdk_dir = csp_filterplugin_dir / 'FilterPlugIn' / 'TriglavPlugInSDK'
csp_filterplugin_sdk_rel = 'CSP_FilterPlugIn/FilterPlugIn/TriglavPlugInSDK'
if not fs.is_dir(csp_filterplugin_dir)
  message('CSP_FilterPlugIn が見つからないため、SDK ZIP を自動取得します。')

  pwsh = find_program('pwsh', required: false)
  if not pwsh.found()
    pwsh = find_program('powershell', required: false)
  endif
  if not pwsh.found()
    error('PowerShell (pwsh または powershell) が見つかりません。CSP_FilterPlugIn を手動配置するか、PowerShell をインストールしてください。')
  endif

  csp_filterplugin_zip_url = 'https://vd.clipstudio.net/clipcontent/paint/sdk/FilterPlugIn20210827.zip'
  run_command(
    pwsh,
    '-NoProfile',
    '-ExecutionPolicy', 'Bypass',
    '-File', project_dir / 'scripts' / 'ensure_csp_filterplugin.ps1',
    '-ProjectDir', project_dir,
    '-ZipUrl', csp_filterplugin_zip_url,
    check: true
  )
endif

if not fs.is_dir(csp_filterplugin_sdk_dir)
  error('TriglavPlugIn SDK ヘッダが見つかりません: ' + csp_filterplugin_sdk_dir)
endif

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

# C# (CSPBridgeEffects) ビルド設定
subdir('CSPBridgeEffects')

# Windows用C++ DLLビルド設定
if host_machine.system() == 'windows'
  # CSPBridgeBase ソースコード
  cspbridge_sources = [
    'CSPBridgeBase/BridgeBase.cpp',
    'CSPBridgeBase/BridgeCallback.cpp',
    'CSPBridgeBase/dllmain.cpp',
    'CSPBridgeBase/pch.cpp',
  ]

  py3 = import('python').find_installation()

  cspbridge_libs = []
  foreach effect_id : effect_ids

    # エフェクトIDごとにブリッジ DLL をビルドする
    # C# DLL は実行時に CoreCLR 経由でロードするためリンク依存はない
    cspbridge_lib = shared_library(
        effect_id,
        cspbridge_sources,
        include_directories: [
            'CSPBridgeBase',
            csp_filterplugin_sdk_rel,
        ],
        cpp_args: [
            '/D_WINDOWS',
            '/DCSPBRIDGEBASE_EXPORTS',
            '/D_USRDLL',
            '-DEFFECT_ID="' + effect_id + '"'
        ],
        name_suffix: 'cpm',
        install: true,
        install_dir: get_option('libdir'))
    cspbridge_libs += cspbridge_lib
  endforeach

  # ---- リリース ZIP 作成 ----
  zip_inputs = cspbridge_libs + [
    cspbridgeeffects_cs_target[0],  # CSPBridgeEffects.dll
    cspbridgeeffects_cs_target[1],  # CSPBridgeEffects.runtimeconfig.json
    cspbridgeeffects_cs_target[2],  # CSPBridgeEffects.deps.json
  ]
  custom_target(
    'release_zip',
    input:   zip_inputs,
    output:  ['CSPBridge-v' + meson.project_version() + '.zip', 'release_zip.stamp'],
    command: [
      py3,
      project_dir / 'scripts' / 'make_release_zip.py',
      '@OUTPUT0@',
      '@OUTPUT1@',
      '@INPUT@',
    ],
    build_by_default: true,
  )

  # デバッグ用：plugin_path が指定されていればビルド成功後にファイルをコピー
  plugin_path = get_option('plugin_path')
  if plugin_path != ''
    custom_target(
      'copy_to_plugin_path',
      input: cspbridge_libs + [cspbridgeeffects_cs_target[0], cspbridgeeffects_cs_target[1], cspbridgeeffects_cs_target[2]],
      output: 'copy_to_plugin_path.stamp',
      command: [
        py3,
        project_dir / 'scripts' / 'copy_to_plugin.py',
        plugin_path,
        '@OUTPUT@',
        '@INPUT@',
      ],
      build_by_default: true,
    )
  endif

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
