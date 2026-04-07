# CSPBridge 内部仕様

---

## 目次

1. [ファイル構成](#1-ファイル構成)
2. [アーキテクチャ](#2-アーキテクチャ)
3. [ビルドオプション](#3-ビルドオプション)
4. [エフェクトの追加方法](#4-エフェクトの追加方法)
5. [C# エフェクト実装ガイド](#5-c-エフェクト実装ガイド)
6. [SDK バインディング](#6-sdk-バインディング)
7. [補足](#7-補足)

---

## 1. ファイル構成

```text
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
│   ├── BridgeFilter.cpp/h    # フィルターコールバック（スタブ）
│   ├── BridgeProperty.cpp/h  # プロパティコールバック（スタブ）
│   ├── dllmain.cpp           # DLL エントリポイント
│   └── pch.cpp/h             # プリコンパイル済みヘッダ
│
├── CSPBridgeEffects/         # C# エフェクトライブラリ
│   ├── CSPBridgeEffects.csproj
│   ├── meson.build
│   ├── Effects/
│   │   ├── EffectTemplate.cs.in   # エフェクトクラスのテンプレート
│   │   ├── EffectMeta.cs.in       # エフェクトメタデータクラスのテンプレート
│   │   ├── EffectHelper.cs        # モジュール・フィルタ初期化ヘルパー
│   │   └── Samples/               # サンプルエフェクト実装
│   │       ├── Blur.cs
│   │       ├── Sharpen.cs
│   │       ├── Mosaic.cs
│   │       └── HSV.cs
│   └── Library/
│       └── SDK/              # TriglavPlugIn SDK の C# バインディング
│
└── CSP_FilterPlugIn/             # TriglavPlugIn SDK（初回 meson setup 時に自動取得）
    └── FilterPlugIn/
        └── TriglavPlugInSDK/ # TriglavPlugIn SDK ヘッダ（C++）
```

---

## 2. アーキテクチャ

### C++ 層（BridgeBase / BridgeCallback）

`BridgeCallback.cpp` の `TriglavPluginCall()` は CSP から呼ばれる唯一のエントリポイントです。`selector` の値に応じて `BridgeBase` のメソッドへディスパッチするだけで、フィルターロジックは持ちません。

`BridgeBase.cpp` は CoreCLR のホスティングを担当します。

1. レジストリから .NET インストールパスを取得
2. `hostfxr.dll` をロード（CSP がすでにロード済みの場合は既存インスタンスを共有）
3. `hostfxr_initialize_for_runtime_config` で CoreCLR を初期化
4. `load_assembly_and_get_function_pointer` で C# の 4 メソッドの関数ポインタを取得
5. 以降はその関数ポインタを呼ぶだけ

複数の `.cpm` が同一 CSP プロセスにロードされた場合、`hostfxr_initialize_for_runtime_config` は `Success_HostAlreadyInitialized (8)` を返します。この場合はセカンダリコンテキストとして既存の CLR を共有します。

### C# 層（CSPBridgeEffects）

各エフェクトクラスは以下の 4 メソッドを `[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]` で公開します。

| メソッド | 役割 |
| --- | --- |
| `ModuleInitialize` | ホストバージョン取得・モジュール ID・モジュール種別の設定 |
| `FilterInitialize` | フィルター名・カテゴリ・パラメータ UI の定義・プロパティコールバックの登録 |
| `FilterTerminate` | GCHandle など確保したリソースの解放 |
| `FilterRun` | ブロック単位のピクセル処理（プレビューループ含む） |

パラメータ定義（スライダーの min/max/default など）、フィルター名、カテゴリ名、ピクセル処理ロジック——これらはすべて C# 側で完結します。C++ 側は関与しません。

### ビルドフロー

```text
effects.json
 └─ meson.build が jq で読み込む
      ├─ per-effect C++ DLL をビルド（-DEFFECT_ID="Blur" などのマクロ付き）
      ├─ EffectMeta.cs.in → {id}Meta.cs を生成（全エフェクト）
      └─ EffectTemplate.cs.in → {id}.cs を生成（custom:true でないエフェクト）
           └─ dotnet build に --property:GeneratedEffectsDir=... を渡してコンパイル
```

---

## 3. ビルドオプション

[`meson_options.txt`](../meson_options.txt) で定義されています。

### plugin_path

ビルド成功後にプラグインファイルを自動コピーするフォルダを指定します。

```text
option('plugin_path',
  type: 'string',
  value: '',
  description: 'ビルド後にプラグインファイルをコピーするフォルダ（空の場合はコピーしない）',
)
```

| 設定例 | コマンド |
| --- | --- |
| 設定なし（コピーしない） | `meson setup build` |
| コピー先を指定 | `meson setup build -Dplugin_path="C:\path\to\plug-in"` |
| コピー先を変更 | `meson setup build --reconfigure -Dplugin_path="新しいパス"` |

コピーされるファイル:

- `{EffectId}.cpm` × エフェクト数
- `CSPBridgeEffects.dll`
- `CSPBridgeEffects.runtimeconfig.json`
- `CSPBridgeEffects.deps.json`

---

## 4. エフェクトの追加方法

[`effects.json`](../effects.json) を編集して追加します。

### テンプレートから自動生成する場合（標準）

```json
{
    "effects": [
        { "id": "Blur", "category": "My Filters", "filterName": "Blur Effect" },
        { "id": "MyNewEffect" }
    ]
}
```

`category` と `filterName` を省略した場合は、デフォルト値（`"Bridge Effects"` / エフェクト ID）が使われます。

### 手書き `.cs` を持つカスタムエフェクトの場合

`"custom": true` を付けると `EffectTemplate.cs.in` からの自動生成をスキップします。手書きの `.cs` ファイルを用意してください。

```json
{
    "effects": [
        { "id": "HSV", "custom": true, "category": "My Filters", "filterName": "HSV Adjustment" }
    ]
}
```

> **namespace の制約**
> `BridgeBase` は実行時に `CSPBridgeEffects.Effects.{id}` という型名でクラスを検索します。
> カスタム `.cs` ファイルのクラスは必ず `namespace CSPBridgeEffects.Effects;` を宣言してください。
> （サブディレクトリに置いても namespace だけ合わせれば問題ありません。`Samples/HSV.cs` が実例です）

`id` の命名規則:

- **英数字とアンダースコアのみ**使用可（C# クラス名・C++ マクロ名として使われるため）
- 先頭は英字

追加後に再設定してビルドします。

```powershell
meson setup build --reconfigure
meson compile -C build
```

---

## 5. C# エフェクト実装ガイド

### 自動生成ファイル

meson は `effects.json` をもとに 2 種類のファイルを生成します。

**`{id}Meta.cs`**（全エフェクト、[`EffectMeta.cs.in`](../CSPBridgeEffects/Effects/EffectMeta.cs.in) から生成）

カテゴリ名とフィルタ名を定数として持つクラスです。

| プレースホルダ | 置換後の値 | 例 |
| --- | --- | --- |
| `@EFFECT_ID@` | effects.json の `id` | `Blur` |
| `@CATEGORY@` | effects.json の `category`（省略時 `"Bridge Effects"`） | `"My Filters"` |
| `@FILTER_NAME@` | effects.json の `filterName`（省略時はエフェクト ID） | `"Blur Effect"` |

**`{id}.cs`**（`"custom": true` でないエフェクトのみ、[`EffectTemplate.cs.in`](../CSPBridgeEffects/Effects/EffectTemplate.cs.in) から生成）

エントリポイントの骨格クラスです。`FilterRun` は空のスケルトンです。

| プレースホルダ | 置換後の値 | 例 |
| --- | --- | --- |
| `@EFFECT_ID@` | effects.json の `id` | `Blur` |
| `@MODULE_ID@` | `com.example.cspbridge.{id小文字}` | `com.example.cspbridge.blur` |

### 共通ヘルパー（EffectHelper）

[`CSPBridgeEffects/Effects/EffectHelper.cs`](../CSPBridgeEffects/Effects/EffectHelper.cs) に共通処理がまとまっています。

| メソッド | シグネチャ | 説明 |
| --- | --- | --- |
| `InitializeModule` | `(TriglavPlugInServer* server, string moduleId)` | ホストバージョン取得・モジュール ID・モジュール種別を設定 |
| `InitializeFilter` | `(TriglavPlugInServer* server, string category, string name, ReadOnlySpan<int> targetKinds)` | カテゴリ名・フィルタ名・プレビュー可否・ターゲット種別を設定 |
| `CreateAsciiString` | `(TriglavPlugInStringService* service, string text)` | ASCII 文字列の `TriglavPlugInStringObject` を作成（使用後に `releaseProc` を呼ぶこと） |

---

## 6. SDK バインディング

`CSPBridgeEffects/Library/SDK/` 以下に TriglavPlugIn SDK の C# バインディングがあります。

| ファイル | 内容 |
| --- | --- |
| `CSPBridgeEffectsLib.cs` | ライブラリエントリクラス（スタブ） |
| `CSPBridgeEffectsLibCallback.cs` | コールバック定義クラス（スタブ） |
| `CSPBridgeEffectsLibType.cs` | 構造体定義（`TriglavPlugInServer` など） |
| `CSPBridgeEffectsLibDefine.cs` | 定数定義（`kTriglavPlugIn...`） |
| `CSPBridgeEffectsLibRecord.cs` | `TriglavPlugInRecordSuite` 構造体 |
| `CSPBridgeEffectsLibService.cs` | `TriglavPlugInServiceSuite` 構造体 |
| `CSPBridgeEffectsLibRecordFunction.cs` | レコード関数のラッパー |

### 構造体レイアウト（Pack=1, 64-bit）

| 構造体 | サイズ |
| --- | --- |
| `TriglavPlugInRecordSuite` | 256 × 8 = 2048 bytes |
| `TriglavPlugInServiceSuite` | 256 × 8 = 2048 bytes |
| `TriglavPlugInServer` | recordSuite + serviceSuite + hostObject = 4104 bytes |

`reserved` フィールドは `fixed long` で定義されています（`IntPtr[]` ではオフセットがずれるため）。

---

## 7. 補足

### Meson で JSON を扱う方法（jq 使用）

本プロジェクトが対象とする Meson バージョン（1.1 以降）では `read_json` や `import('json')` が使えないため、`jq` を使って `effects.json` から値を取り出しています。

`meson.build` での流れ:

1. `find_program('jq')` で jq を検出
2. `run_command()` で `jq -r ".effects[].id" effects.json` を実行
3. 標準出力を改行で分割して `effect_ids` リストを作成
4. `foreach effect_id : effect_ids` でターゲットを生成

### runtimeconfig.json について

C++ の `BridgeBase` は `hostfxr_initialize_for_runtime_config` で CoreCLR を初期化する際に `CSPBridgeEffects.runtimeconfig.json` を必要とします。C# プロジェクトの `<EnableDynamicLoading>true</EnableDynamicLoading>` 設定により、クラスライブラリでも `runtimeconfig.json` が生成されます。

### CSP プラグイン仕様の制約

CSP の TriglavPlugIn SDK は 1 エフェクト = 1 `.cpm` DLL を前提とした設計です。1 つの DLL に複数エフェクトをまとめることは CSP 側の仕様上できません。DLL が複数生成されるのは設計の問題ではなく CSP 仕様の必然です。
