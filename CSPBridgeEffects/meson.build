# C# (CSPBridgeEffects) ビルド設定

dotnet = find_program('dotnet', required: true)

cs_build_config = 'Release'
if get_option('buildtype') == 'debug' or get_option('buildtype') == 'debugoptimized'
  cs_build_config = 'Debug'
endif

custom_target(
  'cspbridgeeffects_cs_build',
  input: 'CSPBridgeEffects.csproj',
  output: 'CSPBridgeEffects.dll',
  command: [
    dotnet,
    'build',
    '@INPUT0@',
    '-c', cs_build_config,
    '-nologo',
    '-o', '@OUTDIR@',
  ],
  build_by_default: true,
  build_always_stale: true,
)
