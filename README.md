# Overview

CSPBridge is an open-source core component of an education-oriented platform,
designed to bridge AI inference, data processing, and downstream evaluation pipelines.

This repository focuses on the architectural specification and reference implementation,
with an emphasis on practical deployment, reproducibility, and long-term maintainability.

# CSPBridge

CLIP STUDIO PAINT (CSP) のフィルタプラグインを **C#** で開発するためのブリッジフレームワークです。

English README: [docs/README_en.md](docs/README_en.md)

---

## オーバービュー

CSP のプラグインは C++ の DLL（`.cpm`）として実装する必要があります。CSPBridge はその C++ 層を薄いディスパッチャとして共通化し、実際のフィルター処理を **C#** で書けるようにするフレームワークです。

```text
CSP
 └─ {EffectId}.cpm  (C++ / TriglavPlugIn SDK)
      └─ BridgeBase — hostfxr → CoreCLR を初期化
           └─ CSPBridgeEffects.dll  (C#)
                └─ CSPBridgeEffects.Effects.{EffectId}
                     ├─ FilterInitialize  — パラメータ UI の定義
                     └─ FilterRun         — ピクセル処理
```

- **エフェクトの追加は `effects.json` を編集するだけ**。C++ コードの変更は不要です。
- フィルターのロジック・パラメータ定義はすべて C# 側に実装します。
- CSP の仕様上、エフェクト 1 個につき `.cpm` DLL が 1 個生成されます。複数の `.cpm` が同一プロセスにロードされた場合も CoreCLR インスタンスは共有されます。

---

## 前提条件

| ツール | バージョン | 入手方法 |
|---|---|---|
| Windows | 10 / 11 (x64) | — |
| Visual Studio | 2022 以降 (C++ ビルドツール) | [visualstudio.microsoft.com](https://visualstudio.microsoft.com/) |
| .NET SDK | 10.0 以降 | [dotnet.microsoft.com](https://dotnet.microsoft.com/) |
| Meson | 1.1 以降 | `winget install mesonbuild.meson` |
| jq | 1.6 以降 | `winget install jqlang.jq` |

依存ツールを一括インストールする場合:

```powershell
powershell -ExecutionPolicy Bypass -File .\inst.ps1
```

---

## ビルド

```powershell
meson setup build
meson compile -C build
```

CSP のプラグインフォルダへ自動コピーする場合:

```powershell
meson setup build -Dplugin_path="C:\path\to\CLIP STUDIO PAINT\plug-in"
meson compile -C build
```

---

## ドキュメント

- [内部仕様 (docs/spec_sheet.md)](docs/spec_sheet.md) — アーキテクチャ詳細・エフェクト実装ガイド・SDK バインディング

---

## ライセンス

[MIT License](LICENSE)
