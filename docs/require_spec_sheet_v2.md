# CSP BridgePluginにおけるTypeScript実装の技術的実現可能性

**結論：TypeScriptのトランスパイル方式は、既存のQuickJS-NG統合に対してほぼゼロコストで型安全性を追加できる最も費用対効果の高い選択肢である。** ネイティブTS実行方式（Deno/Bun/Node.js組み込み）はいずれもバイナリサイズが80〜107MBに達し、CSPプラグインとしては非現実的と判断される。トランスパイル方式を採用すればランタイムは従来のQuickJS-NG（約1MB）のままで、開発時にのみTypeScriptの型チェックとIDE補完の恩恵を受けられる。JS・Python・TypeScript・C#の4言語を横断して評価した結果、**TypeScript（トランスパイル方式）がCSP BridgePluginスクリプティングにおける最適解**である。

---

## 方式1：esbuild + QuickJS-NGが実証済みの最適構成

トランスパイル方式の核心は「**開発時はTypeScript、実行時はJavaScript**」というアーキテクチャにある。この構成ではTypeScriptの型情報はビルド時に完全に消去され、QuickJS-NGが実行するのは純粋なJavaScriptである。つまり**ランタイムオーバーヘッドは完全にゼロ**だ。

トランスパイラの選定ではesbuildが明確な最適解となる。Go言語で実装されたesbuildはtscの**約100倍高速**で、典型的なフィルタースクリプトを100ms未満でトランスパイル＋バンドルできる。`--bundle --format=iife --target=es2020`の指定により、外部依存ゼロの単一JSファイルを出力できるため、QuickJS-NGの`JS_Eval()`で直接実行可能だ。一方、esbuildは型チェックを行わないため、CI/IDEでは`tsc --noEmit`を並行実行して型検証を行う二段構成が推奨される。

QuickJS-NGのECMAScript対応度は極めて高い。test262テストスイートのES2023テストをほぼ**100%パス**しており、`?.`（オプショナルチェーニング）、`??`（Null合体）、`async/await`、`Array.at()`、`Object.hasOwn()`、`Array.findLast()`、Change Array by Copy（`.toSorted()`、`.toReversed()`）など主要なES2023機能がすべてサポートされている。esbuildの`--target=es2020`指定であれば完全な互換性が確保できる。唯一の制約は**ECMA-402（`Intl` API）が非サポート**である点だが、フィルター処理では実質的に問題にならない。

### ビルドパイプラインの具体構成

開発ワークフローは以下の通り明快である。

```
.tsファイル作成 → VSCode + TypeScript Language Server（リアルタイム型チェック）
                → esbuild（トランスパイル＋バンドル → 単一.jsファイル出力）
                → tsc --noEmit（CI/ビルド時の厳格型検証）
                → dist/filter.js をC++プラグインDLLに同梱
```

esbuildの`--sourcemap=external`オプションでSource Map v3ファイルを生成できるが、QuickJS-NGはSource Mapをネイティブ消費しない。デバッグビルドではC++ホスト側にVLQデコーダ（約20行のJSで実装可能）を組み込み、JSスタックトレースをTS行番号に逆変換する仕組みが必要となる。

---

## .d.tsファイルによるC++ APIの型付けが開発体験を劇的に改善する

TypeScript導入の最大の実利は、C++が公開するプラグインAPIに対する**完全な型定義**を提供できる点にある。`@types/node`や`@figma/plugin-typings`と同じパターンで、アンビエント宣言ファイル（`.d.ts`）を作成する。

```typescript
// types/clip-studio-plugin.d.ts
declare interface ImageBuffer {
  readonly width: number;
  readonly height: number;
  readonly channels: number;
  getPixel(x: number, y: number, channel: number): number;
  setPixel(x: number, y: number, channel: number, value: number): void;
  getScanline(y: number): Float32Array;
  setScanline(y: number, data: Float32Array): void;
}

declare namespace CSPlugin {
  function getSourceBuffer(): ImageBuffer;
  function getDestBuffer(): ImageBuffer;
  function setProgress(value: number): void;
  function isCancelled(): boolean;
}
```

この宣言により、VSCodeのIntelliSenseが`CSPlugin.`と入力した瞬間に全APIメソッドを候補表示し、引数の型ミスや存在しないプロパティへのアクセスをビルド前に検出する。**JetBrainsの2024年調査によると、型付きコードベースはJSと比較してリグレッションバグ修正に費やす時間が40%少ない**。またTypeScriptの構造的型付け（ダックタイピング）はスクリプティング文脈で特に有用で、ユーザーはインターフェースの明示的実装宣言なしに、正しい形状のオブジェクトを渡すだけでAPIを利用できる。

注意点として、`.d.ts`ファイルはC++ APIの変更に手動で追従させる必要がある。nbindのような自動生成ツール、またはEmscriptenの`--emit-tsd`のようなC++バインディングからの.d.ts自動生成の仕組みを検討すると保守コストを低減できる。

---

## ネイティブTS実行方式はいずれも非現実的

Deno・Bun・Node.jsの3つのネイティブTSランタイムを評価したが、CSPプラグインへの組み込みはいずれも実現困難である。

**Deno**はRust + V8で構築されており、コアライブラリ`deno_core`はRust専用クレートで**C/C++ APIが存在しない**。かつて存在した`libdeno` C APIはDeno 1.0以前に廃止されている。バイナリサイズはWindows x64で**約98〜107MB**。さらに`deno_core`自体はTypeScriptを解釈せず、TS対応は上位レイヤー（`deno_runtime`、CLI）に依存するが、Denoチームは「Denoはライブラリとしての利用を想定していない」と明言している。Rust-C++間のFFIはCRT不一致やスレッドモデル衝突（Tokioの`current_thread`エグゼキュータとC++プラグインのスレッドモデルが競合）のリスクも大きい。

**Bun**はZig + JavaScriptCoreで構築されたモノリシックバイナリで、ライブラリ形式や組み込みAPIは一切提供されていない。Windows x64バイナリは**約91〜105MB**。代替としてJavaScriptCore単体の組み込みを検討しても、WebKitは2024年5月にMSVCサポートを終了し**clang-cl専用**に移行しており、MSVCプロジェクトとの統合は不可能である。

**Node.js**は3選択肢中で唯一の実現可能候補である。公式のC++ Embedder API（`node::InitializeOncePerProcess()`、`CommonEnvironmentSetup::Create()`等）が存在し、MSVCビルドを公式サポートしている。Node.js 22.18+ではSWCベースの型ストリッピングによるネイティブTypeScript実行も安定化した。しかし`node.exe`のサイズは**約88〜95MB**、`libnode.dll`でも推定40〜60MBであり、プラグインとしてのサイズ制約を大幅に超過する。V8ヒープの初期メモリ使用量（4〜8MB）やlibuvイベントループの管理も複雑さを増す。

| ランタイム | バイナリサイズ | C/C++ API | MSVC対応 | 判定 |
|---|---|---|---|---|
| **QuickJS-NG** | **約1MB** | ✅ 純粋C | ✅ 完全 | ✅ 最適 |
| Deno | 約98〜107MB | ❌ なし | ❌ Rust専用 | ❌ 不可 |
| Bun | 約91〜105MB | ❌ なし | ❌ Zig/clang-cl | ❌ 不可 |
| Node.js | 約88〜95MB | ✅ 公式API | ✅ 完全 | ⚠️ サイズ過大 |
| V8単体 | 約30〜50MB | ✅ 公式API | ⚠️ Clang推奨 | ⚠️ ビルド困難 |

---

## TypeScript vs JavaScript：実行時差異ゼロ、開発時差異は劇的

トランスパイル済みTypeScriptとプレーンJavaScriptの**実行時パフォーマンスは完全に同一**である。型情報はコンパイル時に消去され、QuickJS-NGが処理するのは同じJavaScriptコードだ。差異が現れるのは開発時体験においてのみだが、その差は劇的と言える。

TypeScriptは**ビルド前にバグの15〜30%を検出**できるとする研究結果がある。フィルター開発において頻出する「APIの引数順序ミス」「存在しないメソッド呼び出し」「null/undefinedアクセス」といったエラーが、実行前に排除される。Figmaがプラグイン開発でTypeScriptを「強く推奨」している理由もここにある。Figmaは公式型定義パッケージ`@figma/plugin-typings`を提供し、Discriminated Unionを活用してノード型の絞り込みをコンパイル時に実現している。

開発者プールの面でも、2025年8月にTypeScriptは**GitHubの月間コントリビューター数でPythonとJavaScriptの両方を抜き第1位**に達した（約264万人）。Stack Overflow 2025調査でもTypeScriptは**全開発者の43.6%が使用**しており、C#の27.1%を大幅に上回る。JavaScriptからTypeScriptへの移行は「有効なJSはすべて有効なTS」という互換性により、学習曲線はほぼゼロに近い。

既存のQuickJS-NG統合がある状況でTypeScriptを追加するコストは、esbuildビルドステップの追加と.d.tsファイルの作成のみ。**ランタイムの変更は一切不要**であり、プラグインの配布サイズも増加しない。

---

## TypeScript vs C#：プラグインスクリプティングではTSが優位

C#はTypeScriptに対して**JITコンパイルによる20〜50倍の計算性能**、**SIMDサポート（`System.Numerics.Vector<T>`）**、**ImageSharp・SkiaSharp等の成熟した画像処理ライブラリ**という強みを持つ。しかしCSP BridgePluginのアーキテクチャでは、重い画像処理はC++ホスト側で行いスクリプトはオーケストレーション層として機能する設計が前提であり、C#の性能優位は大きく相殺される。

組み込み面では、.NET CoreCLRの埋め込みはQuickJS-NGと比較して桁違いに複雑である。`nethost` → `hostfxr` → `coreclr_initialize`のチェーンが必要で、**一度ロードしたCLRはプロセス終了まで完全にアンロードできない**（.NET 6+の制限）。さらに**1プロセスにつきCLRインスタンスは1つのみ**という制約があり、複数フィルターの独立実行に不向きだ。バイナリサイズもトリミング済みで11MB、フル構成で58〜65MBとQuickJS-NGの約1MBとは比較にならない。

| 比較項目 | TypeScript + QuickJS-NG | C# + .NET CoreCLR |
|---|---|---|
| バイナリサイズ | **約1MB** | 11〜65MB |
| 組み込みコード量 | **約100行のC++** | 数百行（hostfxr chain） |
| 起動時間 | **<300μs** | 200〜500ms |
| クリーンシャットダウン | **✅ 可能** | ❌ 不可（プロセス終了まで残留） |
| 複数インスタンス | **✅ 独立実行可能** | ❌ 1プロセス1CLR |
| 型システム | 構造的型付け（柔軟） | 名義的型付け（厳密） |
| 開発者プール | **43.6%** | 27.1% |

TypeScriptの構造的型付け（ダックタイピング）はスクリプティング文脈ではC#の名義的型付けより自然だ。ユーザーはインターフェースの明示的`implements`宣言なしに、正しいプロパティを持つオブジェクトを渡すだけでよい。Union型（`string | number`）やリテラル型（`"red" | "green" | "blue"`）もC#にはない柔軟性を提供する。

---

## 先行事例が示すトランスパイル方式の実績

TypeScript → JS → 軽量ランタイムというアーキテクチャパターンは複数の大規模プロジェクトで実証済みである。

**Figma**はCSPと最も類似したクリエイティブソフトウェアの事例だ。プラグインはサンドボックス化されたJavaScript環境で実行され、公式TypeScript型定義パッケージ`@figma/plugin-typings`によるフルAPI型付けを提供している。開発者はTSで記述し、webpack/tscでトランスパイルし、出力JSをサンドボックスにロードする。**Cocos Creator**はC++カーネル + TypeScriptスクリプティングのハイブリッドアーキテクチャで、JSB（JavaScript Binding）を介してC++とTypeScriptを橋渡しする。レンダラー・物理演算・シーン管理はC++、ゲームロジックはTypeScriptという責務分離は、CSPのフィルター処理アーキテクチャと完全に一致する。

ゲームエンジン分野では**Godotのgodotjs**プラグインがQuickJSでJavaScript/TypeScriptスクリプティングを実現しており、オーディオフレームワーク**JUCE 8**もC++プラグイン内のスクリプティングにQuickJSを採用した。いずれもQuickJS + トランスパイル済みJSという軽量構成で成功している。

---

## 4言語横断評価：TypeScriptが最高の費用対効果

| 評価軸 | JavaScript | TypeScript | Python | C# |
|---|---|---|---|---|
| バイナリサイズ | **約1MB** | **約1MB** | 30〜55MB | 11〜65MB |
| 組み込み難易度 | ★★★★★ | ★★★★★（同一） | ★★★☆☆ | ★★☆☆☆ |
| 型安全性 | ❌ なし | **✅ 完全**（ビルド時） | ⚠️ 任意（mypy） | ✅ 完全（ビルド+実行時） |
| 計算性能 | 低（インタプリタ） | 低（同一） | 中（NumPy利用時） | **高（JIT+SIMD）** |
| IDE体験 | ⚠️ 限定的 | **★★★★★** | ★★★★☆ | ★★★★★ |
| 開発者プール | **66%** | 43.6% | 53%+ | 27.1% |
| 画像処理エコシステム | 限定的 | 限定的 | **最良** | 良好 |
| 保守コスト | 低 | **極めて低** | 高 | 高 |
| 起動時間 | **<300μs** | **<300μs** | 100〜500ms | 200〜500ms |

**最終推奨：TypeScript（トランスパイル方式）** を第一選択とする。理由は3つに集約される。

第一に、**追加コストが事実上ゼロ**である。既存のQuickJS-NG統合をそのまま活用し、ビルドパイプラインにesbuildを追加するだけで型安全なフィルター開発環境が完成する。ランタイムの変更もバイナリサイズの増加も発生しない。

第二に、**開発体験の改善が最も大きい**。プレーンJavaScriptと比較して「型補完」「引数エラー検出」「リファクタリング支援」が劇的に向上する。C#と同等の型安全性を、C#の1/60以下のバイナリサイズで実現できる。

第三に、**アーキテクチャパターンが実証済み**である。Figma・Cocos Creator・Godot（godotjs）・JUCE 8など、C++ホスト + TypeScript/JSスクリプティングの組み合わせは複数の商用プロダクトで成功を収めている。「TSでオーケストレーション、C++で重い計算」という責務分離が、CSP BridgePluginのフィルター処理に最適に適合する。

計算集約的なピクセル操作についてはQuickJS-NGのインタプリタ性能（V8 JITの2〜3%程度）が制約となるが、これはスキャンライン単位のバルクC++ API（`getScanline()`/`setScanline()`で`Float32Array`を介した一括転送）を設計することで実用上解消できる。フィルターロジック全体をスクリプトでピクセル単位に処理するのではなく、アルゴリズムの制御フローをTypeScriptで記述し、実際のピクセル演算はC++バルク関数に委譲する設計が鍵となる。