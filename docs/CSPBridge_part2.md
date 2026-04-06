# CLIP STUDIO PAINTのフィルタプラグインをC#で書けるようにした「CSPBridge」を公開しました（後編）

## 実装の詳細

前編ではCSPBridgeの概要とセットアップ方法を紹介しました。後編では、実装の核心部分であるCoreCLRホスティングとC#エフェクトの実装について詳しく解説します。

### CoreCLRホスティングの実装

CSPBridgeの最も重要な部分は、C++からCoreCLRを動的にロードし、C#の関数ポインタを取得するホスティングコードです。これにより、C++プラグインの制約を突破してC#のエコシステムを活用できます。

#### BridgeBase.cppの構造

`BridgeBase.cpp`は以下のステップでCoreCLRを初期化します：

1. **hostfxr.dllのロード**: .NETインストール先から最新のhostfxr.dllをロード。CSPプロセスが既に.NETを使用している場合、既存インスタンスを共有。

2. **ホスティングAPIの呼び出し**: `hostfxr_initialize_for_runtime_config`でCoreCLRを初期化。成功するとホストコンテキストが得られる。

3. **デリゲートの取得**: `hostfxr_get_runtime_delegate`で`load_assembly_and_get_function_pointer`デリゲートを取得。

4. **マネージド関数のロード**: C#アセンブリから`[UnmanagedCallersOnly]`属性付きメソッドのポインタを取得。

```cpp
// BridgeBase::Initialize() の抜粋
const auto hostfxr_init = reinterpret_cast<hostfxr_initialize_fn>(
    ::GetProcAddress(m_hHostfxrLib, "hostfxr_initialize_for_runtime_config"));

int rc = hostfxr_init(configPath.c_str(), &initParams, &hostContext);
```

#### 複数プラグインのCoreCLR共有

CSPBridgeの設計では、複数の.cpm DLLが同一プロセスにロードされた場合、CoreCLRインスタンスを共有します。これによりメモリ効率が向上し、競合を回避します。

```cpp
// hostfxr_initialize_for_runtime_config の戻り値処理
if (rc == 0) {
    // 新規初期化成功（プライマリホスト）
    m_isPrimaryHost = true;
} else if (rc == 8) { // Success_HostAlreadyInitialized
    // セカンダリコンテキスト（既存CLRを共有）
    m_isPrimaryHost = false;
}
```

### C#エフェクトの実装

C#側では、TriglavPlugIn SDKのC APIを直接呼び出すunsafeコードを使用します。各エフェクトクラスは4つのエントリポイントを持ちます。

#### エフェクトクラスの構造

```csharp
public static unsafe class Blur
{
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    public static int ModuleInitialize(TriglavPlugInServer* pluginServer)
        => EffectHelper.InitializeModule(pluginServer, "com.example.cspbridge.blur");

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    public static int FilterInitialize(TriglavPlugInServer* pluginServer, void** data)
    {
        // フィルタの初期化処理
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    public static int FilterRun(TriglavPlugInServer* pluginServer, void** data)
    {
        // 実際のピクセル処理
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    public static int FilterTerminate(TriglavPlugInServer* pluginServer, void** data)
    {
        // リソース解放
    }
}
```

#### ピクセル処理の実装例（Blur.cs）

BlurエフェクトはセパラブルBox Blurを実装しています。水平パスと垂直パスに分けて処理することで効率的なぼかしを実現します。

```csharp
private static void ProcessBlock(/* ... */)
{
    // 水平方向のBox Blur
    for (int y = 0; y < h; y++)
    {
        for (int x = 0; x < w; x++)
        {
            // 半径radius分のピクセルを平均化
            int sumR = 0, sumG = 0, sumB = 0;
            int x0 = Math.Max(0, x - radius);
            int x1 = Math.Min(w - 1, x + radius);
            int cnt = x1 - x0 + 1;
            for (int nx = x0; nx <= x1; nx++)
            {
                // RGB値の取得と加算
            }
            tmpR[y * w + x] = sumR / cnt;
            // G, Bも同様
        }
    }

    // 垂直方向のBox Blur（tmp → dst）
    // 省略...
}
```

### ビルドシステムの自動化

Meson + Ninja + jqの組み合わせで、effects.jsonから自動的にC++ DLLとC#クラスを生成します。

#### effects.jsonの処理

```json
{
    "effects": [
        { "id": "Blur", "custom": true, "category": "Bridge Effects", "filterName": "Blur" }
    ]
}
```

Mesonはjqを使ってこのJSONを解析し、エフェクトIDごとにビルドターゲットを作成します。

#### テンプレート生成

`EffectTemplate.cs.in`から`configure_file()`で具体的なエフェクトクラスを生成：

```csharp
// 生成されるBlur.cs（テンプレートベース）
public static class Blur
{
    // ...
}
```

カスタム実装が必要なエフェクトは`custom: true`フラグでテンプレート生成をスキップし、手書きコードを使用します。

### 苦労した点

#### CoreCLRホスティングの複雑さ

- **hostfxr.dllの共有**: CSPプロセスが既に.NETを使用している場合、hostfxrのグローバル状態を考慮する必要があった。
- **構造体のオフセット**: C#とC++間で構造体のメモリレイアウトを一致させるため、pack(1)などの属性を慎重に使用。
- **例外処理**: SEH (Structured Exception Handling) を使用してマネージドコードの例外を適切に処理。

#### メモリ管理の課題

- **ポインタのライフサイクル**: C#のGCとC++のポインタを安全に連携させるため、GCHandleを使用。
- **ブロック処理**: CSPのオフスクリーンAPIはブロック単位で処理するため、効率的なメモリバッファ管理が必要。

#### ビルドシステムの統合

- **MesonのJSON処理**: MesonにJSON処理機能がないため、jqを外部ツールとして使用。
- **クロスコンパイル**: C#とC++のビルドを1つのMesonプロジェクトで統合。

## まとめ

CSPBridgeは、C++プラグインの制約を克服し、C#の生産性とエコシステムを活用できるように設計されています。CoreCLRホスティングにより、.NETの豊富なライブラリを使用した高度な画像処理が可能になります。

サンプルとして実装したBlur, Sharpen, Mosaic, HSVエフェクトは、CSPBridgeの可能性を示すものです。

興味のある方はGitHubリポジトリをチェックしてください。コントリビューションも歓迎します！

https://github.com/sdkikgeneral-spec/CSPBridge