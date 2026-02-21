project('CSPBridge', 'cpp',
  version: '1.0.0',
  default_options: ['cpp_std=c++17'])

# Windows用C++ DLLビルド設定
if host_machine.system() == 'windows'
  # CSPBridgeBase DLLライブラリ
  cspbridge_lib = library('CSPBridgeBase',
    'CSPBridgeBase/BridgeBase.cpp',
    'CSPBridgeBase/BridgeCallback.cpp',
    'CSPBridgeBase/dllmain.cpp',
    'CSPBridgeBase/pch.cpp',
    include_directories: [
      'CSPBridgeBase',
      '../CSP_FilterPlugIn/FilterPlugIn/TriglavPlugInSDK'
    ],
    cpp_args: [
      '/D_WINDOWS',
      '/DCSPBRIDGEBASE_EXPORTS',
      '/D_USRDLL'
    ],
    install: true)

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
