# CSPBridge v2 仕様書 — フレームワーク成熟化

---

## 目次

1. [概要](#1-概要)
2. [v2 の位置づけ](#2-v2-の位置づけ)
3. [v2.0 アーキテクチャ整備](#3-v20-アーキテクチャ整備)
4. [v2.1 パフォーマンス基盤](#4-v21-パフォーマンス基盤)
5. [ロードマップ](#5-ロードマップ)
6. [確認事項](#6-確認事項)

---

## 1. 概要

CSPBridge v2 は言語変更やエフェクト拡充ではなく、**フレームワークとしての成熟化**を目的とする。

CSPBridge を基盤とする下流プラグインが増える前提で、エフェクト開発の生産性・保守性・性能を底上げする。

### v2 で実現すること

- 新規エフェクトの `FilterRun` を **10 行以下**で書けるようにする
- ピクセル処理ロジックを `unsafe`/SDK 依存から分離し、**ユニットテスト可能**にする
- `EffectTemplate.cs.in` から生成したコードが**そのまま動作する**テンプレートにする
- 下流プラグインが意識せずに**並列処理・SIMD の恩恵**を受けられるようにする

### v2 でやらないこと

- エフェクトの追加（下流プラグインの責務）
- 言語変更（C# + CoreCLR を維持）
- C++ 側の変更（ディスパッチャとして薄く保つ方針を維持）

---

## 2. v2 の位置づけ

```text
┌─────────────────────────────────────────┐
│  下流プラグイン（エフェクト実装）          │  ← 各種エフェクトはここで開発
│  DustRemove / LineBoost / RadialBlur ... │
├─────────────────────────────────────────┤
│  CSPBridge v2（フレームワーク）           │  ← 本仕様書の対象
│  EffectHelper / RunPreviewLoop /         │
│  テスト基盤 / パフォーマンスユーティリティ  │
├─────────────────────────────────────────┤
│  CSPBridge v1（基盤層）                  │
│  SDK バインディング / CoreCLR ホスト /    │
│  ビルドパイプライン                       │
└─────────────────────────────────────────┘
```

v1 の基盤層（SDK バインディング、CoreCLR ホスト、meson ビルドパイプライン）には手を入れない。v2 はその上に共通ユーティリティ層を追加する。

---

## 3. v2.0 アーキテクチャ整備

### 3.1 プレビューループの共通化（RunPreviewLoop）

#### 現状の問題

Blur / Sharpen / Mosaic / HSV の 4 エフェクトはそれぞれ `FilterRun` 内に同一構造のプレビューループを持っている。コピペ率は **約 85〜95%**。変動部分はパラメータ読み取りと `ProcessBlock` 呼び出しのみ。

```csharp
// 現行: 全エフェクトに重複する約 50 行のパターン
while (true)
{
    if (restart)
    {
        // Start ステート → パラメータ読み取り → blockIndex = 0
    }
    if (blockIndex < blockCount)
    {
        // ProcessBlock → UpdateDestinationOffscreenRect → blockIndex++
    }
    {
        // Continue/End ステート → Restart/Exit 判定
    }
}
```

エフェクトが増えるほどコード重複が累積し、ループ制御に変更が生じた場合に全エフェクトの修正が必要になる。

#### 設計

`EffectHelper` に `RunPreviewLoop` メソッドを追加する。

```csharp
// EffectHelper.cs に追加
internal static int RunPreviewLoop(
    TriglavPlugInServer*       server,
    TriglavPlugInOffscreenObject dstOffscreen,
    TriglavPlugInRect*         selectRect,
    ReadParametersDelegate     readParameters,
    ProcessBlockDelegate       processBlock)
```

**デリゲート定義:**

```csharp
/// <summary>
/// プレビューの Restart 時にプロパティ値を読み取るコールバック。
/// </summary>
internal unsafe delegate void ReadParametersDelegate(
    TriglavPlugInPropertyService* propertySvc,
    TriglavPlugInPropertyObject   propObj);

/// <summary>
/// 1 ブロック分のピクセル処理を行うコールバック。
/// blockIndex はブロック配列のインデックス。ブロック矩形は値コピーで渡す
/// （ラムダ内で ref キャプチャできない C# の制約への対処）。
/// </summary>
internal unsafe delegate void ProcessBlockDelegate(
    TriglavPlugInOffscreenService* offscreenSvc,
    TriglavPlugInOffscreenObject   dstOffscreen,
    TriglavPlugInRect              blockRect,
    int blockIndex);
```

**`RunPreviewLoop` の責務:**

1. ブロック矩形リストの取得（`getBlockRectCountProc` / `getBlockRectProc`）
2. `ArrayPool<TriglavPlugInRect>` の確保・解放
3. プログレス管理（`SetProgressTotal` / `SetProgressDone`）
4. `while(true)` プレビューループ（Start / Continue / End / Restart / Exit）
5. Restart 時に `readParameters` を呼び出し
6. ブロック処理時に `processBlock` を呼び出し
7. `UpdateDestinationOffscreenRect` の呼び出し

#### 適用後の Blur.FilterRun イメージ

```csharp
[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
public static int FilterRun(TriglavPlugInServer* pluginServer, void** data)
{
    var record       = &pluginServer->recordSuite;
    var offscreenSvc = pluginServer->serviceSuite.offscreenService;
    var propertySvc  = pluginServer->serviceSuite.propertyService;

    if (TriglavPlugInGetFilterRunRecord(record) == null
        || offscreenSvc == null || propertySvc == null)
        return kTriglavPlugInCallResultFailed;

    var host = pluginServer->hostObject;

    TriglavPlugInPropertyObject  propObj;
    TriglavPlugInOffscreenObject srcOffscreen, dstOffscreen, selectOffscreen;
    TriglavPlugInRect            selectRect;

    TriglavPlugInFilterRunGetProperty(record, &propObj, host);
    TriglavPlugInFilterRunGetSourceOffscreen(record, &srcOffscreen, host);
    TriglavPlugInFilterRunGetDestinationOffscreen(record, &dstOffscreen, host);
    TriglavPlugInFilterRunGetSelectAreaRect(record, &selectRect, host);
    TriglavPlugInFilterRunGetSelectAreaOffscreen(record, &selectOffscreen, host);

    int rIdx, gIdx, bIdx;
    offscreenSvc->getRGBChannelIndexProc(&rIdx, &gIdx, &bIdx, dstOffscreen);

    int radius = 3;

    return EffectHelper.RunPreviewLoop(
        pluginServer, dstOffscreen, &selectRect,
        readParameters: (propSvc, prop) =>
        {
            propSvc->getIntegerValueProc(&radius, prop, ItemKeyRadius);
        },
        processBlock: (osSvc, dst, blockRect, idx) =>
        {
            ProcessBlock(osSvc, dst, srcOffscreen, selectOffscreen,
                         blockRect, rIdx, gIdx, bIdx, radius);
        });
}
```

プレビューループの約 50 行が消え、エフェクト固有のロジック（パラメータ読み取り + ProcessBlock 呼び出し）だけが残る。

#### 既存エフェクトへの適用

4 つのサンプルエフェクトを段階的にリファクタリングする。動作の変更がないことを確認するため、リファクタリング前後で CSP 上の実機テストを行う。

---

### 3.2 ピクセルロジックの分離（テスタビリティ）

#### 現状の問題

`ProcessBlock` メソッドは `TriglavPlugInOffscreenService*` や `void*` ポインタに直接依存しており、ユニットテストが不可能。

#### 設計

ピクセル処理の純粋計算部分をカーネルメソッドとして切り出す。

```csharp
// BlurKernel.cs（新規）
namespace CSPBridgeEffects.Effects.Kernels;

/// <summary>
/// Blur のピクセル処理カーネル。SDK 非依存でテスト可能。
/// </summary>
internal static class BlurKernel
{
    /// <summary>
    /// 水平方向の Box Blur（src → tmp）。
    /// </summary>
    internal static void HorizontalPass(
        ReadOnlySpan<byte> src, int srcStride, int srcPixBytes,
        Span<int> tmpR, Span<int> tmpG, Span<int> tmpB,
        int w, int h, int rIdx, int gIdx, int bIdx, int radius)
    {
        // 現行 Blur.ProcessBlock のパス 1 と同じロジック
    }

    /// <summary>
    /// 垂直方向の Box Blur（tmp → dst）。
    /// </summary>
    internal static void VerticalPass(
        Span<byte> dst, int dstStride, int dstPixBytes,
        ReadOnlySpan<byte> alpha, int alphaStride, int alphaPixBytes,
        ReadOnlySpan<int> tmpR, ReadOnlySpan<int> tmpG, ReadOnlySpan<int> tmpB,
        int w, int h, int rIdx, int gIdx, int bIdx, int radius)
    {
        // 現行 Blur.ProcessBlock のパス 2（選択範囲なし）と同じロジック
    }

    /// <summary>
    /// 垂直方向の Box Blur（選択範囲付き、tmp → dst）。
    /// </summary>
    internal static void VerticalPassWithSelection(
        Span<byte> dst, int dstStride, int dstPixBytes,
        ReadOnlySpan<byte> src, int srcStride, int srcPixBytes,
        ReadOnlySpan<byte> alpha, int alphaStride, int alphaPixBytes,
        ReadOnlySpan<byte> selection, int selStride, int selPixBytes,
        ReadOnlySpan<int> tmpR, ReadOnlySpan<int> tmpG, ReadOnlySpan<int> tmpB,
        int w, int h, int rIdx, int gIdx, int bIdx, int radius)
    {
        // 現行 Blur.ProcessBlock のパス 2（選択範囲あり）と同じロジック
    }
}
```

**ProcessBlock の役割:**

1. SDK の `getBlockImageProc` / `getBlockAlphaProc` でポインタを取得
2. ポインタを `Span<byte>` に変換
3. カーネルメソッドを呼び出し

これにより `BlurKernel` は `Span<byte>` のみに依存し、xUnit でテストできる。

#### テストの例

```csharp
[Fact]
public void HorizontalPass_Radius1_AveragesNeighbors()
{
    // 3x1 の入力: [10, 20, 30]（R チャンネルのみ）
    byte[] src = [10, 20, 30];
    int[] tmpR = new int[3];
    // ...
    BlurKernel.HorizontalPass(src, 3, 1, tmpR, ..., w: 3, h: 1, rIdx: 0, ..., radius: 1);
    Assert.Equal(15, tmpR[0]);  // (10+20)/2
    Assert.Equal(20, tmpR[1]);  // (10+20+30)/3
    Assert.Equal(25, tmpR[2]);  // (20+30)/2
}
```

#### ディレクトリ構成

```text
CSPBridgeEffects/
├── Effects/
│   ├── EffectHelper.cs          # RunPreviewLoop 追加
│   ├── Kernels/                 # 新規: SDK 非依存カーネル
│   │   ├── BlurKernel.cs
│   │   ├── SharpenKernel.cs
│   │   ├── MosaicKernel.cs
│   │   └── HsvKernel.cs
│   └── Samples/                 # ProcessBlock → Kernel 呼び出しに変更
│       ├── Blur.cs
│       ├── Sharpen.cs
│       ├── Mosaic.cs
│       └── HSV.cs

CSPBridgeEffects.Tests/          # 新規: xUnit テストプロジェクト
├── CSPBridgeEffects.Tests.csproj
├── BlurKernelTests.cs
├── SharpenKernelTests.cs
├── MosaicKernelTests.cs
├── HsvKernelTests.cs
└── KernelUtilsTests.cs
```

---

### 3.3 テンプレートの改善（EffectTemplate.cs.in）

#### 現状の問題

現行テンプレートの `FilterRun` は `Start → End` を呼ぶだけのスタブで、実際のプレビューループやパラメータ読み取りがない。テンプレートから生成したコードはそのままでは「何もしないエフェクト」にしかならない。

#### 設計

v2 の `RunPreviewLoop` を活用し、テンプレートから生成したコードが**パラメータ付きの動作するエフェクト**になるようにする。

```csharp
// EffectTemplate.cs.in（v2 改訂版）
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CSPBridgeEffects.Library.SDK;
using static CSPBridgeEffects.Library.SDK.CSPBridgeEffectsLibDefine;
using static CSPBridgeEffects.Library.SDK.CSPBridgeEffectsLibRecordFunction;

namespace CSPBridgeEffects.Effects;

public static unsafe class @EFFECT_ID@
{
    private static readonly int[] s_targetKinds =
    [
        kTriglavPlugInFilterTargetKindRasterLayerRGBAlpha,
        kTriglavPlugInFilterTargetKindRasterLayerGrayAlpha,
    ];

    // ================================================================
    // エントリポイント
    // ================================================================

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    public static int ModuleInitialize(TriglavPlugInServer* pluginServer)
        => EffectHelper.InitializeModule(pluginServer, "@MODULE_ID@");

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    public static int FilterInitialize(TriglavPlugInServer* pluginServer, void** data)
    {
        int rc = EffectHelper.InitializeFilter(
            pluginServer, @EFFECT_ID@Meta.Category, @EFFECT_ID@Meta.FilterName, s_targetKinds);
        if (rc != kTriglavPlugInCallResultSuccess) return rc;

        // TODO: パラメータを追加する場合はここに propSvc->addItemProc() を記述

        // 重要: GCHandle.Alloc は SetPropertyCallBack より前に行うこと。
        // SetPropertyCallBack に *data を渡すため、先に GCHandle を確保しておく必要がある。
        var handle = GCHandle.Alloc(new object());
        *data = (void*)GCHandle.ToIntPtr(handle);

        var record  = &pluginServer->recordSuite;
        var service = &pluginServer->serviceSuite;
        var host    = pluginServer->hostObject;

        // TODO: プロパティを使う場合は以下のコメントを解除
        // TriglavPlugInPropertyObject propObj;
        // service->propertyService->createProc(&propObj);
        // TriglavPlugInFilterInitializeSetProperty(record, host, propObj);
        // TriglavPlugInFilterInitializeSetPropertyCallBack(record, host, &PropertyCallback, *data);
        // service->propertyService->releaseProc(propObj);

        return kTriglavPlugInCallResultSuccess;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    public static int FilterTerminate(TriglavPlugInServer* pluginServer, void** data)
    {
        if (data != null && *data != null)
        {
            GCHandle.FromIntPtr((IntPtr)(*data)).Free();
            *data = null;
        }
        return kTriglavPlugInCallResultSuccess;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    public static int FilterRun(TriglavPlugInServer* pluginServer, void** data)
    {
        var record       = &pluginServer->recordSuite;
        var offscreenSvc = pluginServer->serviceSuite.offscreenService;
        var propertySvc  = pluginServer->serviceSuite.propertyService;

        if (TriglavPlugInGetFilterRunRecord(record) == null
            || offscreenSvc == null || propertySvc == null)
            return kTriglavPlugInCallResultFailed;

        var host = pluginServer->hostObject;

        TriglavPlugInOffscreenObject dstOffscreen;
        TriglavPlugInRect            selectRect;

        TriglavPlugInFilterRunGetDestinationOffscreen(record, &dstOffscreen, host);
        TriglavPlugInFilterRunGetSelectAreaRect(record, &selectRect, host);

        return EffectHelper.RunPreviewLoop(
            pluginServer, dstOffscreen, &selectRect,
            readParameters: (propSvc, propObj) =>
            {
                // TODO: パラメータを読み取る
            },
            processBlock: (osSvc, dst, blockRect, idx) =>
            {
                // TODO: ピクセル処理を実装する
            });
    }

    // ================================================================
    // プロパティコールバック
    // ================================================================

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void PropertyCallback(
        int* result, TriglavPlugInPropertyObject propObj,
        int itemKey, int notify, void* callbackData)
    {
        *result = notify == kTriglavPlugInPropertyCallBackNotifyValueChanged
            ? kTriglavPlugInPropertyCallBackResultModify
            : kTriglavPlugInPropertyCallBackResultNoModify;
    }
}
```

#### 改善点

| 項目 | v1（現行） | v2 |
| --- | --- | --- |
| FilterRun | Start → End のスタブ（2 行） | `RunPreviewLoop` 呼び出し付き骨格 |
| FilterTerminate | `return Success` のみ | GCHandle 解放処理付き |
| FilterInitialize | `InitializeFilter` のみ | プロパティ追加のコメント例付き |
| PropertyCallback | なし | 標準的なコールバック実装付き |

---

## 4. v2.1 パフォーマンス基盤

### 4.1 Parallel.For によるブロック並列化

#### 前提条件

SDK の `getBlockImageProc` / `updateDestinationOffscreenRect` がマルチスレッドから呼べるかの確認が必要（[6. 確認事項](#6-確認事項) 参照）。確認が取れた場合のみ着手する。

#### 設計

`RunPreviewLoop` に並列実行オプションを追加する。

```csharp
internal static int RunPreviewLoop(
    TriglavPlugInServer*           server,
    TriglavPlugInOffscreenObject   dstOffscreen,
    TriglavPlugInRect*             selectRect,
    ReadParametersDelegate         readParameters,
    ProcessBlockDelegate           processBlock,
    bool                           enableParallel = false)  // v2.1 追加
```

`enableParallel = true` の場合、ブロック処理ループを `Parallel.For` に置き換える。

```csharp
if (enableParallel)
{
    Parallel.For(0, blockCount, i =>
    {
        // blockRects[i] は値コピーで渡す（ラムダ内で ref キャプチャ不可のため）
        processBlock(offscreenSvc, dstOffscreen, blockRects[i], i);
    });
    // 全ブロック完了後にまとめて UpdateDestinationOffscreenRect
}
```

**注意点:**

- `ArrayPool<int>.Shared.Rent` はスレッドセーフだが、per-スレッドのバッファ競合に注意
- Restart シグナル受信時の安全な中断には `CancellationToken` パターンが必要
- プログレス更新（`SetProgressDone`）はアトミックカウンタで管理

#### 下流プラグインへの影響

`RunPreviewLoop` の引数に `enableParallel: true` を追加するだけ。ProcessBlock の実装を変更する必要はない（ただしスレッドセーフである必要がある）。

---

### 4.2 SIMD 最適化

#### 対象

Blur の水平パスのみに限定する。現行のスカラーループ:

```csharp
for (int nx = x0; nx <= x1; nx++)
{
    byte* p = src + y * srcRowBytes + nx * srcPixBytes;
    sumR += p[rIdx]; sumG += p[gIdx]; sumB += p[bIdx];
}
```

#### 設計方針

- `System.Runtime.Intrinsics.X86.Avx2` を使用
- SIMD 非対応環境ではスカラーにフォールバック
- カーネルメソッド内に実装するため、下流プラグインは SIMD を意識しない

```csharp
// BlurKernel.cs 内
internal static void HorizontalPass(...)
{
    if (Avx2.IsSupported && w >= 32)
        HorizontalPassAvx2(...);
    else
        HorizontalPassScalar(...);
}
```

#### 期待効果

水平パスは連続メモリアクセスが中心のため、SIMD の恩恵が大きい。`Vector256<byte>` で 32 ピクセルを同時処理することで **4〜8x のスループット改善**が期待できる。

ただし実装の複雑さが跳ね上がるため、v2.1 の後半で実測ベンチマーク結果を見てから着手を判断する。

---

## 5. ロードマップ

### v2.0 — アーキテクチャ整備

| 状態 | タスク | 概要 | 依存 |
| --- | --- | --- | --- |
| [x] | `RunPreviewLoop` 実装 | EffectHelper にプレビューループ共通化メソッドを追加 | なし |
| [x] | 既存エフェクトのリファクタリング | Blur → Sharpen → Mosaic → HSV の順で適用 | RunPreviewLoop |
| [x] | カーネル分離 | `BlurKernel` 等を `Kernels/` に切り出し | なし |
| [x] | xUnit テスト導入 | `CSPBridgeEffects.Tests` プロジェクト作成 | カーネル分離 |
| [x] | テンプレート改善 | `EffectTemplate.cs.in` を v2 版に更新 | RunPreviewLoop |

### v2.1 — パフォーマンス基盤

| タスク | 概要 | 依存 |
| --- | --- | --- |
| SDK スレッドセーフ確認 | `getBlockImageProc` 等のマルチスレッド呼び出し可否を検証 | なし |
| `Parallel.For` 対応 | `RunPreviewLoop` に並列実行オプション追加 | SDK 確認 |
| ベンチマーク基盤 | カーネルの処理時間計測の仕組み | カーネル分離 |
| SIMD 試験導入 | Blur 水平パスのみ `Avx2` で実装 | ベンチマーク |

---

## 6. 確認事項

v2 着手前に以下を確認する必要がある。

### 6.1 SDK のスレッドセーフ性

`Parallel.For` でブロックを並列処理した場合、SDK 側の以下の関数が複数スレッドから呼ばれることになる。CSP 側がこれを許容するか確認が必要。

- `getBlockImageProc`
- `getBlockAlphaProc`
- `getBlockSelectAreaProc`
- `updateDestinationOffscreenRect` (※これは処理後の通知なので、直列化が必要な可能性が高い)

### 6.2 Restart シグナルの安全な中断

プレビューの `Restart` シグナルは UI スレッドから来る。`Parallel.For` で並列処理中に Restart を受け取った場合、処理中のブロックをどう安全に中断するか。

### 6.3 GrayAlpha での getRGBChannelIndexProc の戻り値

Blur / Sharpen / Mosaic は GrayAlpha レイヤーをターゲットに含めている。GrayAlpha の場合 `rIdx == gIdx == bIdx` になるという理解で正しいか。カーネル分離時のテストケース設計に影響する。
