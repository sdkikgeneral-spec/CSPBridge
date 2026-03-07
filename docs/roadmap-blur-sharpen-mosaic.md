# Blur / Sharpen / Mosaic 実装ロードマップ

## 概要

`effects.json` に定義された Blur・Sharpen・Mosaic エフェクトに実際のピクセル処理を実装する。
HSV (`custom: true`) と同じパターンで、各エフェクトを独立した C# ファイルとして手書き実装する。

---

## 全体フロー（実装後）

```
meson setup build
  └─ effects.json を読み込み
  └─ custom: true → テンプレート生成をスキップ（Blur/Sharpen/Mosaic も対象）
  └─ C++ DLL をエフェクトごとにビルド（Blur.cpm, Sharpen.cpm, Mosaic.cpm）

dotnet build CSPBridgeEffects.csproj
  └─ Effects/Samples/Blur.cs     ← 手書き実装
  └─ Effects/Samples/Sharpen.cs  ← 手書き実装
  └─ Effects/Samples/Mosaic.cs   ← 手書き実装
  └─ Effects/EffectHelper.cs     ← 共通ヘルパー（既存）
  └─ CSPBridgeEffects.dll を出力

CSP がプラグインをロード
  └─ Blur.cpm  → BridgeBase::Initialize → load_assembly_and_get_function_pointer
                     → CSPBridgeEffects.Effects.Blur::ModuleInitialize / FilterInitialize / FilterRun / FilterTerminate
  └─ Sharpen.cpm → 同上 (Sharpen クラス)
  └─ Mosaic.cpm  → 同上 (Mosaic クラス)
```

---

## フェーズ 1: effects.json の変更

**ファイル**: `effects.json`

Blur・Sharpen・Mosaic に `"custom": true` を追加する。
これにより meson の `configure_file()` によるテンプレート生成がスキップされ、
手書き実装ファイルが唯一のソースになる。

```json
{
    "effects": [
        { "id": "Blur",    "custom": true },
        { "id": "Sharpen", "custom": true },
        { "id": "Mosaic",  "custom": true },
        { "id": "HSV",     "custom": true }
    ]
}
```

**影響範囲**:
- `build/CSPBridgeEffects/Blur.cs` 等の自動生成ファイルが以後生成されなくなる
- C++ DLL のビルドには影響なし（EFFECT_ID マクロは引き続き meson が設定）

---

## フェーズ 2: Blur 実装

**ファイル**: `CSPBridgeEffects/Effects/Samples/Blur.cs`

### エフェクト仕様

| 項目 | 内容 |
|------|------|
| アルゴリズム | セパラブル Box Blur（水平パス → 垂直パス） |
| ターゲット | RGBAlpha / GrayAlpha レイヤー |
| パラメータ | Radius: 1 〜 20（デフォルト 3） |
| プレビュー | 対応 |
| 選択範囲 | 対応（マスク合成） |

### 処理フロー

```
FilterRun
  └─ プロパティから Radius 取得
  └─ ブロックリストを構築
  └─ プレビューループ
       └─ ProcessBlock (ブロックごと)
            └─ getBlockImageProc でピクセルデータ取得
            └─ ブロック全体をバイト配列にコピー（元データ保存）
            └─ HorizontalPass: 各行で前後 Radius ピクセルの移動平均
            └─ VerticalPass:   各列で前後 Radius ピクセルの移動平均
            └─ アルファ値が 0 のピクセルはスキップ
            └─ 選択範囲がある場合はマスク合成
```

### セパラブル Box Blur のポイント

- 2 パスに分けることで計算量を O(r²) → O(r) に削減
- 元ピクセルを `ArrayPool<byte>` にコピーしてから書き戻す（in-place 問題を回避）
- ブロック境界でのクランプ（境界ピクセルで端値を繰り返す）

---

## フェーズ 3: Sharpen 実装

**ファイル**: `CSPBridgeEffects/Effects/Samples/Sharpen.cs`

### エフェクト仕様

| 項目 | 内容 |
|------|------|
| アルゴリズム | Unsharp Mask（Box Blur + 差分強調） |
| ターゲット | RGBAlpha / GrayAlpha レイヤー |
| パラメータ | Strength: 0 〜 200 % （デフォルト 100） |
|             | Radius:   1 〜 10（ブラー半径、デフォルト 2） |
| プレビュー | 対応 |
| 選択範囲 | 対応（マスク合成） |

### 処理フロー

```
FilterRun
  └─ プロパティから Strength / Radius 取得
  └─ ブロックリストを構築
  └─ プレビューループ
       └─ ProcessBlock (ブロックごと)
            └─ 元ピクセルをコピー（srcBuffer）
            └─ Box Blur を srcBuffer に適用 → blurBuffer
            └─ 各ピクセル:
                 sharpened = clamp(src + Strength/100 * (src - blur), 0, 255)
            └─ 選択範囲がある場合はマスク合成
```

### Unsharp Mask の式

```
out = src + strength * (src - blur)
    = (1 + strength) * src - strength * blur
```

- `strength = Strength / 100.0` (0.0 〜 2.0)
- 結果は [0, 255] にクランプ

---

## フェーズ 4: Mosaic 実装

**ファイル**: `CSPBridgeEffects/Effects/Samples/Mosaic.cs`

### エフェクト仕様

| 項目 | 内容 |
|------|------|
| アルゴリズム | ピクセル化（セル内の平均色で塗り潰し） |
| ターゲット | RGBAlpha / GrayAlpha レイヤー |
| パラメータ | Size: 2 〜 64（モザイクセルのピクセルサイズ、デフォルト 16） |
| プレビュー | 対応 |
| 選択範囲 | 対応（マスク合成） |

### 処理フロー

```
FilterRun
  └─ プロパティから Size 取得
  └─ ブロックリストを構築
  └─ プレビューループ
       └─ ProcessBlock (ブロックごと)
            └─ ブロック内をセル（Size × Size）に分割
            └─ 各セルについて:
                 1. アルファ > 0 のピクセルの R/G/B 合計と個数を集計
                 2. 平均色を算出（透明ピクセルは除外）
                 3. セル内の全ピクセルに平均色を書き込む
            └─ 選択範囲がある場合はマスク合成
```

### セル境界の扱い

- セルはブロック左上 (blockRect.left, blockRect.top) を基点とする
- セルがブロック境界をはみ出す場合はクランプ（端の不完全セルも正常処理）
- モザイク適用後、元のアルファ値は変更しない

---

## フェーズ 5: ビルド検証

### 確認手順

```
# 1. meson reconfigure（effects.json の変更を反映）
cd build
meson setup .. --reconfigure

# 2. ビルド
ninja

# 3. 出力物確認
ls <libdir>/
# → Blur.cpm, Sharpen.cpm, Mosaic.cpm, HSV.cpm
# → CSPBridgeEffects.dll, CSPBridgeEffects.runtimeconfig.json
```

### 確認チェックリスト

- [ ] `meson setup --reconfigure` が警告なく完了する
- [ ] `ninja` で C++ DLL 3本がビルドされる
- [ ] `dotnet build` が警告なく完了する
- [ ] CSP にプラグインをコピーして Blur が適用できる
- [ ] CSP にプラグインをコピーして Sharpen が適用できる
- [ ] CSP にプラグインをコピーして Mosaic が適用できる
- [ ] プレビューが各エフェクトで動作する
- [ ] 選択範囲ありの場合にマスク合成が正しく動作する
- [ ] GrayAlpha レイヤーでも正常動作する

---

## ファイル変更一覧

| 操作 | ファイル | 内容 |
|------|----------|------|
| 変更 | `effects.json` | Blur/Sharpen/Mosaic に `"custom": true` を追加 |
| 新規 | `CSPBridgeEffects/Effects/Samples/Blur.cs` | セパラブル Box Blur 実装 |
| 新規 | `CSPBridgeEffects/Effects/Samples/Sharpen.cs` | Unsharp Mask 実装 |
| 新規 | `CSPBridgeEffects/Effects/Samples/Mosaic.cs` | ピクセル化実装 |
| 変更なし | `CSPBridgeEffects/Effects/EffectHelper.cs` | 共通ヘルパー（流用） |
| 変更なし | `CSPBridgeEffects/Effects/EffectTemplate.cs.in` | 将来の新規エフェクト用テンプレート（流用） |
| 変更なし | `CSPBridgeBase/` | C++ ブリッジ層（変更不要） |
| 変更なし | `meson.build` / `CSPBridgeEffects/meson.build` | ビルド定義（変更不要） |

---

## 依存関係・制約

- `ArrayPool<byte>` を使うため `System.Buffers` が必要（net10.0 で標準利用可）
- ブロック境界をまたぐブラーは未対応（境界でクランプ）。ブロック間の継ぎ目に微細なアーティファクトが生じる可能性があるが、CSP のブロックサイズ（通常 256px 以上）に対してデフォルト Radius（3px）は十分小さい
- GrayAlpha レイヤーはチャンネル数が異なるため、`getRGBChannelIndexProc` で取得したインデックスを使い RGB チャンネルを正しく識別する（HSV.cs と同じアプローチ）

---

## 参考

- `CSPBridgeEffects/Effects/Samples/HSV.cs` — 実装パターンの参照元
- `CSPBridgeEffects/Effects/EffectHelper.cs` — `InitializeModule` / `InitializeFilter` / `CreateAsciiString`
- `CSPBridgeEffects/Library/CSPBridgeEffectsLibRecordFunction.cs` — SDK ヘルパー関数
- `CSPBridgeEffects/Library/CSPBridgeEffectsLibRecord.cs` — SDK 構造体定義
