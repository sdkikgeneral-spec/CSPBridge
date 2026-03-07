# CSPBridge

CLIP STUDIO PAINT (CSP) のフィルタプラグインを **C#** で開発するためのブリッジフレームワークです。

C++ のプラグインエントリポイントから CoreCLR を経由して C# のコードを呼び出すことで、C# の豊富なエコシステムや SIMD 対応ライブラリを活用しながら CSP プラグインを開発できます。

English README: [docs/README_en.md](docs/README_en.md)

---

## 目次

1. [ファイル構成](#1-ファイル構成)
2. [前提条件](#2-前提条件)
3. [ビルド手順](#3-ビルド手順)
4. [meson_options.txt — ビルドオプション](#4-meson_optionstxt--ビルドオプション)
5. [エフェクトの追加方法](#5-エフェクトの追加方法)
6. [C# エフェクト実装ガイド](#6-c-エフェクト実装ガイド)
7. [補足](#7-補足)
8. [ライセンス](#8-ライセンス)

---

## 1. ファイル構成

```
CSPBridge/
├── meson.build               # ルートビルド定義
├── meson_options.txt         # ビルドオプション（plugin_path など）
├── effects.json              # エフェクト ID 一覧
├── inst.ps1                  # 依存ツールインストールスクリプト
│
├── scripts/                  # ビルド補助スクリプト（meson が呼び出す）
│   ├── ensure_csp_filterplugin.ps1  # SDK 自動ダウンロードスクリプト
│   ├── copy_to_plugin.py            # ビルド後コピースクリプト
│   └── make_release_zip.py          # リリース ZIP 作成スクリプト
│
├── CSPBridgeBase/            # C++ ブリッジ共通実装
│   ├── BridgeBase.cpp/h      # CoreCLR ホスティング・関数ポインタ管理
│   ├── BridgeCallback.cpp    # TriglavPlugIn コールバックのディスパッチ
│   ├── dllmain.cpp           # DLL エントリポイント
│   └── pch.cpp/h             # プリコンパイル済みヘッダ
│
├── CSPBridgeEffects/         # C# エフェクトライブラリ
│   ├── CSPBridgeEffects.csproj
│   ├── meson.build
│   ├── Effects/
│   │   ├── EffectTemplate.cs.in   # エフェクトクラスのテンプレート
│   │   ├── EffectHelper.cs        # モジュール・フィルタ初期化ヘルパー
│   │   └── Samples/               # サンプルエフェクト実装
│   │       └── HSV.cs             # HSV 調整サンプル
│   └── Library/
│       └── SDK/              # TriglavPlugIn SDK の C# バインディング
│
└── CSP_FilterPlugIn/             # TriglavPlugIn SDK（初回 meson setup 時に自動取得）
    └── FilterPlugIn/
        └── TriglavPlugInSDK/ # TriglavPlugIn SDK ヘッダ（C++）
```

---

## 2. 前提条件

| ツール | バージョン | 入手方法 |
|---|---|---|
| Windows | 10 / 11 (x64) | — |
| Visual Studio | 2022 以降 (C++ ビルドツール) | [visualstudio.microsoft.com](https://visualstudio.microsoft.com/) |
| .NET SDK | 10.0 以降 | [dot.net](https://dotnet.microsoft.com/) |
| Meson | 1.1 以降 | `winget install mesonbuild.meson` |
| Ninja | （Meson に同梱） | Meson インストール時に自動 |
| jq | 1.6 以降 | `winget install jqlang.jq` |

> **TriglavPlugIn SDK について**
> `CSP_FilterPlugIn/` フォルダが存在しない場合、`meson setup` 実行時に `ensure_csp_filterplugin.ps1` が自動的に SDK ZIP をダウンロード・展開します。手動配置は不要です。

### まとめてインストール（inst.ps1）

リポジトリ直下の `inst.ps1` で上記ツールを一括インストールできます。

```powershell
powershell -ExecutionPolicy Bypass -File .\inst.ps1
```

インストール後、新しいターミナルで確認します。

```powershell
meson --version
dotnet --version
jq --version
```

---

## 3. ビルド手順

### 3.1 初回セットアップ

```powershell
meson setup build
```

デバッグ用に **プラグインフォルダへの自動コピー** を有効にする場合は [`plugin_path` オプション](#4-meson_optionstxt--ビルドオプション) を指定します。

```powershell
meson setup build -Dplugin_path="C:\path\to\CLIP STUDIO PAINT\plug-in"
```

### 3.2 ビルド

```powershell
meson compile -C build
```

ビルドに成功すると次のファイルが生成されます。

| ファイル | 説明 |
|---|---|
| `build/Blur.cpm` | Blur エフェクト用 C++ ブリッジ DLL |
| `build/Sharpen.cpm` | Sharpen エフェクト用 C++ ブリッジ DLL |
| `build/Mosaic.cpm` | Mosaic エフェクト用 C++ ブリッジ DLL |
| `build/HSV.cpm` | HSV エフェクト用 C++ ブリッジ DLL |
| `build/CSPBridgeEffects/CSPBridgeEffects.dll` | C# エフェクトライブラリ |
| `build/CSPBridgeEffects/CSPBridgeEffects.runtimeconfig.json` | CoreCLR 初期化に必要なランタイム設定 |
| `build/CSPBridgeEffects/CSPBridgeEffects.deps.json` | アセンブリ依存関係情報 |
| `build/CSPBridge-v1.0.0.zip` | 配布用リリース ZIP（全出力ファイルをまとめたもの） |

`plugin_path` を指定している場合は、ビルド完了後に `.cpm` / `.dll` / `.runtimeconfig.json` / `.deps.json` が自動的に指定フォルダへコピーされます。

### 3.3 再設定（オプション変更時）

`meson_options.txt` や `meson.build` を変更した後は再設定が必要です。

```powershell
meson setup build --reconfigure
```

`plugin_path` を変更する場合も同様です。

```powershell
meson setup build --reconfigure -Dplugin_path="新しいパス"
```

---

## 4. meson_options.txt — ビルドオプション

[`meson_options.txt`](meson_options.txt) にビルド時のオプションを定義しています。

### plugin_path

ビルド成功後にプラグインファイルを自動コピーするフォルダを指定します。

```
option('plugin_path',
  type: 'string',
  value: '',
  description: 'ビルド後にプラグインファイルをコピーするフォルダ（空の場合はコピーしない）',
)
```

| 設定例 | コマンド |
|---|---|
| 設定なし（コピーしない） | `meson setup build` |
| コピー先を指定 | `meson setup build -Dplugin_path="C:\path\to\plug-in"` |
| コピー先を変更 | `meson setup build --reconfigure -Dplugin_path="新しいパス"` |

コピーされるファイル:

- `{EffectId}.cpm` × エフェクト数
- `CSPBridgeEffects.dll`
- `CSPBridgeEffects.runtimeconfig.json`
- `CSPBridgeEffects.deps.json`

---

## 5. エフェクトの追加方法

エフェクトは [`effects.json`](effects.json) を編集して追加します。

### テンプレートから自動生成する場合（標準）

```json
{
    "effects": [
        { "id": "Blur" },
        { "id": "MyNewEffect" }
    ]
}
```

### 手書き `.cs` を持つカスタムエフェクトの場合

`"custom": true` を付けると、`EffectTemplate.cs.in` からの自動生成をスキップします。
代わりに手書きの `.cs` ファイルを用意してください。

> **namespace の制約**
> `BridgeBase` は実行時に `CSPBridgeEffects.Effects.{id}` という型名でクラスを検索します。
> カスタム `.cs` ファイルのクラスは必ず `namespace CSPBridgeEffects.Effects;` を宣言してください。
> （サブディレクトリに置いても namespace だけ合わせれば問題ありません。`Samples/HSV.cs` が実例です）

```json
{
    "effects": [
        { "id": "HSV", "custom": true }
    ]
}
```

`id` の命名規則:
- **英数字とアンダースコアのみ**使用可（C# クラス名・C++ マクロ名として使われるため）
- 先頭は英字

追加後に再設定してビルドします。

```powershell
meson setup build --reconfigure
meson compile -C build
```

meson が自動的に以下を行います。

1. `effects.json` から `id` を読み取る（`jq` 使用）
2. `"custom": true` でないエフェクトは `EffectTemplate.cs.in` から `{id}.cs` を生成（`build/CSPBridgeEffects/` に出力）
3. `{id}.cpm`（C++ ブリッジ DLL）をビルド
4. `CSPBridgeEffects.dll` をビルド（生成された `.cs` ファイルを含む）

---

## 6. C# エフェクト実装ガイド

### エフェクトクラスの自動生成

エフェクトクラスは [`CSPBridgeEffects/Effects/EffectTemplate.cs.in`](CSPBridgeEffects/Effects/EffectTemplate.cs.in) をテンプレートとして meson が自動生成します。手動での編集は不要です。

テンプレート内のプレースホルダ:

| プレースホルダ | 置換後の値 | 例 |
|---|---|---|
| `@EFFECT_ID@` | effects.json の `id` | `Blur` |
| `@MODULE_ID@` | `com.example.cspbridge.{id小文字}` | `com.example.cspbridge.blur` |

### フィルタ処理の実装

実際のピクセル処理を行うエフェクトには、`"custom": true` を付けて手書き `.cs` ファイルに実装することを推奨します。`Samples/HSV.cs` が完全な実装例です。

以下の 4 つのエントリポイントをすべて実装します。

| メソッド | 役割 |
| --- | --- |
| `ModuleInitialize` | ホストバージョン取得・モジュール ID・種別の設定 |
| `FilterInitialize` | カテゴリ名・フィルタ名・プロパティ（スライダーなど）の設定 |
| `FilterRun` | ブロック単位のピクセル処理（プレビューループ含む） |
| `FilterTerminate` | GCHandle など確保したリソースの解放 |

> テンプレートから生成された `.cs`（`"custom": true` なし）の `FilterRun` は空のスケルトンです。
> ピクセル処理を追加する場合は `"custom": true` に切り替えてください。

### 共通ヘルパー（EffectHelper）

[`CSPBridgeEffects/Effects/EffectHelper.cs`](CSPBridgeEffects/Effects/EffectHelper.cs) に共通処理がまとまっています。

| メソッド | 説明 |
|---|---|
| `InitializeModule(server, moduleId)` | ホストバージョン取得・モジュール ID・モジュール種別を設定 |
| `InitializeFilter(server, category, name, targetKinds)` | カテゴリ名・フィルタ名・ターゲット種別を設定 |
| `CreateAsciiString(service, text)` | ASCII 文字列の `TriglavPlugInStringObject` を作成 |

### SDK バインディング

`CSPBridgeEffects/Library/SDK/` 以下に TriglavPlugIn SDK の C# バインディングがあります。

| ファイル | 内容 |
|---|---|
| `CSPBridgeEffectsLibType.cs` | 構造体定義（`TriglavPlugInServer` など） |
| `CSPBridgeEffectsLibDefine.cs` | 定数定義（`kTriglavPlugIn...`） |
| `CSPBridgeEffectsLibRecord.cs` | `TriglavPlugInRecordSuite` 構造体 |
| `CSPBridgeEffectsLibService.cs` | `TriglavPlugInServiceSuite` 構造体 |
| `CSPBridgeEffectsLibRecordFunction.cs` | レコード関数のラッパー |

---

## 7. 補足

### MesonでJSONを扱う方法（jq使用）

Meson 1.10 では `read_json` や `import('json')` が使えないため、`jq` を使って `effects.json` から値を取り出しています。

`meson.build` での流れ:

1. `find_program('jq')` で jq を検出
2. `run_command()` で `jq -r ".effects[].id" effects.json` を実行
3. 標準出力を改行で分割して `effect_ids` リストを作成
4. `foreach effect_id : effect_ids` でターゲットを生成

### runtimeconfig.json について

C++ の `BridgeBase` は `hostfxr_initialize_for_runtime_config` で CoreCLR を初期化する際に `CSPBridgeEffects.runtimeconfig.json` を必要とします。

C# プロジェクトの `<EnableDynamicLoading>true</EnableDynamicLoading>` を設定することで、クラスライブラリでも `runtimeconfig.json` が生成されます（デフォルトでは実行可能ファイルのみ生成）。

### VSCode でのデバッグ

1. `plugin_path` を設定してビルドし、CSP のプラグインフォルダにファイルをコピーします。
2. CSP を起動してフィルタを適用し、動作を確認します。
3. C# のデバッグは Visual Studio または VSCode の .NET デバッガを使用できます。

---

## 8. ライセンス

このリポジトリは [MIT License](LICENSE) の下で公開されています。
