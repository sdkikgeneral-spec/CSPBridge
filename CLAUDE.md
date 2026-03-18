# CSPBridge — Claude 向け注意事項

このファイルは Claude が誤った前提で提案しないための正誤集です。
コードを読む前に必ず確認してください。

## Claude の役割

あなたはアプリケーションアーキテクトです。また画像処理を得意とするプログラマです。

---

## ドキュメント運用ルール

- README を更新するときは日本語版（`README.md`）と英語版（`docs/README_en.md`）の両方を更新する

---

## コーディングルール

- Microsoft のコーディング規約を参考にすること
- 固有名詞を変数名に使わない
- 変数名は英語
- コメントは各関数・クラスのヘッダに付ける

---

## アーキテクチャの正確な理解

### C++ 側はほぼ純粋なディスパッチャ

`BridgeCallback.cpp` は `TriglavPluginCall()` の `switch(selector)` で
`g_bridgeBase.FilterInitialize()` などを呼ぶだけ。

`BridgeBase.cpp` は CoreCLR を初期化して C# の関数ポインタを取得し、
それを呼び出すだけのホスティングコード。

`BridgeFilter.cpp` / `BridgeProperty.cpp` はほぼ空ファイル。

**C++ 側にフィルターロジックは一切ない。**

### フィルターのすべては C# 側に実装されている

パラメータ定義（スライダーの min/max/default など）、フィルター名、
カテゴリ名、ターゲット種別、ピクセル処理ロジック、プロパティコールバック——
これらはすべて `Blur.cs` などの C# クラス内で完結している。

C# は `TriglavPlugInServer*` ポインタを直接受け取り、
`[UnmanagedCallersOnly]` + `unsafe` で SDK の C API を直接呼ぶ。

**「パラメータが C++ と C# に分散している」は誤り。C# 側だけにある。**

### CoreCLR は CSP プロセス内で共有される

複数の .cpm DLL が同一 CSP プロセスにロードされた場合、
`BridgeBase::Initialize()` は `hostfxr_initialize_for_runtime_config` の
戻り値 8（`Success_HostAlreadyInitialized`）を検出し、
既存の CLR インスタンスを再利用するセカンダリコンテキストを使う。

**「複数 .cpm を読み込むと CoreCLR が複数起動する」は誤り。**

---

## CSP プラグイン仕様の制約（変更不可）

### 1 エフェクト = 1 .cpm DLL は CSP 側の仕様

TriglavPlugIn SDK の仕様上、CSP は各 .cpm を独立したプラグインとして扱う。
1 つの .cpm に複数エフェクトをまとめる「Single Host DLL」の発想は
CSP の仕様が許していないため実現不可能。

**DLL が増殖するのは設計の問題ではなく CSP 仕様の必然。**

---

## 正しいビルド・生成フロー

```
effects.json (id リスト)
    └─ meson.build が jq で読んで per-effect C++ DLL をビルド
          ├─ C++ コンパイル時に -DEFFECT_ID="Blur" などのマクロを渡す
          └─ C# .cs ファイルを EffectTemplate.cs.in から configure_file() で生成
               └─ build/CSPBridgeEffects/<Id>.cs として出力
                     └─ dotnet build に --property:GeneratedEffectsDir=... を渡してコンパイル
```

手書きの per-effect .cs は `CSPBridgeEffects/Effects/Samples/` 以下にある
`Blur.cs` / `Sharpen.cs` / `Mosaic.cs` / `HSV.cs`（サンプル実装）。

テンプレート生成の対象は `EffectTemplate.cs.in` から生成される汎用スタブ。

---

## C# エフェクトの構造（Blur.cs が基準）

各エフェクトクラスは 4 つのエントリポイントを持つ：

| メソッド | 役割 |
|---|---|
| `ModuleInitialize` | モジュール ID 設定・モジュール種別設定 |
| `FilterInitialize` | フィルター名・パラメータ・コールバック登録 |
| `FilterTerminate` | data ポインタ（GCHandle）の解放 |
| `FilterRun` | 実際のピクセル処理 |

すべて `[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]` 付き。

パラメータ登録は `FilterInitialize` 内で `propSvc->addItemProc()` 等を直接呼ぶ。

---

## よくある誤った提案（しないこと）

| 誤った提案 | 理由 |
|---|---|
| 「Single Host DLL にまとめよう」 | CSP 仕様上 1 effect = 1 .cpm が必須 |
| 「パラメータ定義を C++ 側に移そう」 | すでに C# 側に完結しており問題ない |
| 「複数 .cpm の CoreCLR 競合を解消しよう」 | BridgeBase が既に適切に共有処理している |
| 「C++ 側にロジックを追加しよう」 | C++ はディスパッチャとして薄く保つ設計 |
