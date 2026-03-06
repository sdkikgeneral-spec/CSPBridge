# C# (CSPBridgeEffects) ビルド設定
#
# effects.json から読み込んだ effect_ids（親 meson.build で定義）をもとに、
# EffectTemplate.cs.in からエフェクトごとの .cs ファイルを生成してから
# dotnet build を実行します。

dotnet = find_program('dotnet', required: true)

cs_build_config = 'Release'
if get_option('buildtype') == 'debug' or get_option('buildtype') == 'debugoptimized'
  cs_build_config = 'Debug'
endif

# ---- カスタム実装を持つエフェクト ID を取得 (テンプレート生成をスキップ) ----
# effects.json の { "custom": true } フラグで識別する。
custom_ids_result = run_command(
  jq,
  '-r',
  '.effects[] | select(.custom == true) | .id',
  project_dir / 'effects.json',
  check: true
)
custom_ids_raw = custom_ids_result.stdout().strip()
custom_ids = []
if custom_ids_raw != ''
  custom_ids = custom_ids_raw.split('\n')
endif

# ---- テンプレートからエフェクト .cs ファイルを生成 ----
# configure_file は meson setup 時に実行され、出力は meson ビルドディレクトリ以下に置かれる。
# 出力先: <build_dir>/CSPBridgeEffects/<EffectId>.cs
# custom_ids に含まれるエフェクトは手書き .cs を持つため生成をスキップする。
foreach effect_id : effect_ids
  if effect_id not in custom_ids
    configure_file(
      input:         'Effects/EffectTemplate.cs.in',
      output:        effect_id + '.cs',
      configuration: {
        'EFFECT_ID': effect_id,
        'MODULE_ID': 'com.example.cspbridge.' + effect_id.to_lower(),
      },
    )
  endif
endforeach

# 生成 .cs ファイルが置かれるディレクトリ（dotnet build へ渡す）
generated_effects_dir = meson.current_build_dir() + '/'

# ---- C# DLL をビルド ----
# --property:GeneratedEffectsDir で生成ファイルのパスを .csproj に伝える。
# 出力: CSPBridgeEffects.dll, CSPBridgeEffects.runtimeconfig.json
cspbridgeeffects_cs_target = custom_target(
  'cspbridgeeffects_cs_build',
  input:  'CSPBridgeEffects.csproj',
  output: ['CSPBridgeEffects.dll', 'CSPBridgeEffects.runtimeconfig.json', 'CSPBridgeEffects.deps.json'],
  command: [
    dotnet,
    'build',
    '@INPUT0@',
    '-c', cs_build_config,
    '-nologo',
    '-o', '@OUTDIR@',
    '--property:GeneratedEffectsDir=' + generated_effects_dir,
  ],
  build_by_default:   true,
  build_always_stale: true,
  install:            true,
  install_dir:        get_option('libdir'),
)
