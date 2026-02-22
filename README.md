# 1. プロジェクトの説明
このプロジェクトは、CLIP STUDIO PAINT（以下CSP）のプラグインをC#で開発するためのベースを作るプロジェクトです。
C++での開発は、現代において敷居が一段あがってしまう雰囲気があります。
C#で開発する場合、特にメモリの解放処理忘れなどが簡略化されます。
また、C#だとSIMDも扱えるため、C++に近づける速度も期待できます。
（速度面でいくとnativeAOTでビルドするという手もあるが、それはまた別で解説します）

# 2. 構成
CSPプラグインは、C++にて開発されるため、一工夫がいります。

# 3. MesonでJSONを扱う方法（jq使用）
Meson 1.10 では `read_json` や `import('json')` が使えないため、`jq` を使って `effects.json` から値を取り出します。

## 3.1 Windowsでjqをインストール
このリポジトリ直下にある `inst.ps1` でインストールできます。

```powershell
powershell -ExecutionPolicy Bypass -File .\inst.ps1
```

直接インストールする場合は、PowerShell で以下を実行します。

```powershell
winget install jqlang.jq
```

インストール後、新しいターミナルで確認します。

```powershell
jq --version
```

## 3.2 Meson側の考え方
`meson.build` では次の流れで JSON を利用します。

1. `find_program('jq')` で jq を検出
2. `run_command()` で `jq -r ".effects[].id" effects.json` を実行
3. 標準出力を改行で分割して `effect_ids` を作成
4. `foreach effect_id : effect_ids` でターゲット生成

## 3.3 例: effects.json

```json
{
	"effects": [
		{ "id": "Blur" },
		{ "id": "Sharpen" },
		{ "id": "Mosaic" }
	]
}
```

## 3.4 再設定コマンド
`meson.build` を変更したあとは再設定を実行します。

```powershell
meson setup build --reconfigure
```