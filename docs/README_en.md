# CSPBridge

CSPBridge is a bridge framework for developing **CLIP STUDIO PAINT (CSP)** filter plug-ins in **C#**.

By calling C# code from a C++ plug-in entry point via CoreCLR, you can build CSP plug-ins while leveraging the rich C# ecosystem and SIMD-capable libraries.

---

## Table of Contents

1. [Project Structure](#1-project-structure)
2. [Prerequisites](#2-prerequisites)
3. [Build Steps](#3-build-steps)
4. [meson_options.txt — Build Options](#4-meson_optionstxt--build-options)
5. [How to Add Effects](#5-how-to-add-effects)
6. [C# Effect Implementation Guide](#6-c-effect-implementation-guide)
7. [Notes](#7-notes)
8. [License](#8-license)

---

## 1. Project Structure

```text
CSPBridge/
├── meson.build               # Root build definition
├── meson_options.txt         # Build options (e.g., plugin_path)
├── effects.json              # Effect ID list
├── inst.ps1                  # Dependency installation script
│
├── scripts/                  # Build helper scripts (called by meson)
│   ├── ensure_csp_filterplugin.ps1  # SDK auto-download script
│   ├── copy_to_plugin.py            # Post-build copy script
│   └── make_release_zip.py          # Release ZIP creation script
│
├── CSPBridgeBase/            # Shared C++ bridge implementation
│   ├── BridgeBase.cpp/h      # CoreCLR hosting and function pointer management
│   ├── BridgeCallback.cpp    # Dispatches TriglavPlugIn callbacks
│   ├── dllmain.cpp           # DLL entry point
│   └── pch.cpp/h             # Precompiled headers
│
├── CSPBridgeEffects/         # C# effect library
│   ├── CSPBridgeEffects.csproj
│   ├── meson.build
│   ├── Effects/
│   │   ├── EffectTemplate.cs.in   # Template for effect classes
│   │   ├── EffectHelper.cs        # Module/filter initialization helpers
│   │   └── Samples/               # Sample effect implementations
│   │       └── HSV.cs             # HSV adjustment sample
│   └── Library/
│       └── SDK/              # C# bindings for TriglavPlugIn SDK
│
└── CSP_FilterPlugIn/             # TriglavPlugIn SDK (auto-fetched on first meson setup)
    └── FilterPlugIn/
        └── TriglavPlugInSDK/ # TriglavPlugIn SDK headers (C++)
```

---

## 2. Prerequisites

| Tool | Version | How to get it |
| --- | --- | --- |
| Windows | 10 / 11 (x64) | — |
| Visual Studio | 2022 or later (C++ build tools) | [visualstudio.microsoft.com](https://visualstudio.microsoft.com/) |
| .NET SDK | 10.0 or later | [dot.net](https://dotnet.microsoft.com/) |
| Meson | 1.1 or later | `winget install mesonbuild.meson` |
| Ninja | (bundled with Meson) | Installed automatically with Meson |
| jq | 1.6 or later | `winget install jqlang.jq` |

> **TriglavPlugIn SDK**
> If the `CSP_FilterPlugIn/` folder is missing, `meson setup` automatically runs `ensure_csp_filterplugin.ps1` to download and extract the SDK ZIP. No manual setup is needed.

### Install all at once (`inst.ps1`)

You can install the tools above in one shot using `inst.ps1` in the repository root.

```powershell
powershell -ExecutionPolicy Bypass -File .\inst.ps1
```

After installation, verify in a new terminal:

```powershell
meson --version
dotnet --version
jq --version
```

---

## 3. Build Steps

### 3.1 Initial setup

```powershell
meson setup build
```

To enable **automatic copy to the plug-in folder** for debugging, specify the [`plugin_path` option](#4-meson_optionstxt--build-options).

```powershell
meson setup build -Dplugin_path="C:\path\to\CLIP STUDIO PAINT\plug-in"
```

### 3.2 Build

```powershell
meson compile -C build
```

If the build succeeds, the following files are generated:

| File | Description |
| --- | --- |
| `build/Blur.cpm` | C++ bridge DLL for Blur effect |
| `build/Sharpen.cpm` | C++ bridge DLL for Sharpen effect |
| `build/Mosaic.cpm` | C++ bridge DLL for Mosaic effect |
| `build/HSV.cpm` | C++ bridge DLL for HSV effect |
| `build/CSPBridgeEffects/CSPBridgeEffects.dll` | C# effect library |
| `build/CSPBridgeEffects/CSPBridgeEffects.runtimeconfig.json` | Runtime config required for CoreCLR initialization |
| `build/CSPBridgeEffects/CSPBridgeEffects.deps.json` | Assembly dependency information |
| `build/CSPBridge-v1.0.0.zip` | Release ZIP bundling all output files for distribution |

If `plugin_path` is set, the `.cpm` / `.dll` / `.runtimeconfig.json` / `.deps.json` files are copied automatically to the specified folder after build.

### 3.3 Reconfigure (when options change)

Reconfiguration is required after changing `meson_options.txt` or `meson.build`.

```powershell
meson setup build --reconfigure
```

Same applies when changing `plugin_path`.

```powershell
meson setup build --reconfigure -Dplugin_path="new/path"
```

---

## 4. meson_options.txt — Build Options

Build-time options are defined in [`meson_options.txt`](../meson_options.txt).

### `plugin_path`

Specifies the folder to automatically copy plug-in files after a successful build.

```meson
option('plugin_path',
  type: 'string',
  value: '',
  description: 'Folder to copy plugin files after build (no copy if empty)',
)
```

| Example | Command |
| --- | --- |
| Not set (no copy) | `meson setup build` |
| Set destination | `meson setup build -Dplugin_path="C:\path\to\plug-in"` |
| Change destination | `meson setup build --reconfigure -Dplugin_path="new/path"` |

Copied files:

- `{EffectId}.cpm` × number of effects
- `CSPBridgeEffects.dll`
- `CSPBridgeEffects.runtimeconfig.json`
- `CSPBridgeEffects.deps.json`

---

## 5. How to Add Effects

Add effects by editing [`effects.json`](../effects.json).

### Template-generated effects (standard)

```json
{
    "effects": [
        { "id": "Blur" },
        { "id": "MyNewEffect" }
    ]
}
```

### Custom effects with hand-written `.cs`

Adding `"custom": true` skips auto-generation from `EffectTemplate.cs.in`.
Instead, provide a hand-written `.cs` file for the effect.

> **Namespace requirement**
> `BridgeBase` resolves the type at runtime as `CSPBridgeEffects.Effects.{id}`.
> The class in your custom `.cs` file must declare `namespace CSPBridgeEffects.Effects;`.
> (The file may live in a subdirectory — only the namespace matters. `Samples/HSV.cs` is the reference example.)

```json
{
    "effects": [
        { "id": "HSV", "custom": true }
    ]
}
```

`id` naming rules:

- Use **only alphanumeric characters and underscores** (used for C# class names and C++ macros)
- Must start with a letter

After adding an effect, reconfigure and build:

```powershell
meson setup build --reconfigure
meson compile -C build
```

Meson automatically does the following:

1. Reads `id` values from `effects.json` (using `jq`)
2. Generates `{id}.cs` from `EffectTemplate.cs.in` for effects without `"custom": true` (output to `build/CSPBridgeEffects/`)
3. Builds `{id}.cpm` (C++ bridge DLL)
4. Builds `CSPBridgeEffects.dll` (including generated `.cs` files)

---

## 6. C# Effect Implementation Guide

### Automatic effect class generation

Effect classes are auto-generated by meson using [`CSPBridgeEffects/Effects/EffectTemplate.cs.in`](../CSPBridgeEffects/Effects/EffectTemplate.cs.in). Manual editing is not required.

Placeholders in the template:

| Placeholder | Replaced with | Example |
| --- | --- | --- |
| `@EFFECT_ID@` | `id` in effects.json | `Blur` |
| `@MODULE_ID@` | `com.example.cspbridge.{lowercase id}` | `com.example.cspbridge.blur` |

### Implementing filter processing

For effects that perform actual pixel processing, use `"custom": true` and implement a hand-written `.cs` file. `Samples/HSV.cs` is the reference implementation.

Implement all four entry points:

| Method | Role |
| --- | --- |
| `ModuleInitialize` | Set host version, module ID, and module type |
| `FilterInitialize` | Set category name, filter name, and properties (sliders, etc.) |
| `FilterRun` | Per-block pixel processing (including preview loop) |
| `FilterTerminate` | Release resources such as GCHandle |

> The `FilterRun` in a template-generated `.cs` (without `"custom": true`) is an empty skeleton.
> Switch to `"custom": true` when you need to add pixel processing.

### Common helper (`EffectHelper`)

Common logic is centralized in [`CSPBridgeEffects/Effects/EffectHelper.cs`](../CSPBridgeEffects/Effects/EffectHelper.cs).

| Method | Description |
| --- | --- |
| `InitializeModule(server, moduleId)` | Sets host version, module ID, and module type |
| `InitializeFilter(server, category, name, targetKinds)` | Sets category name, filter name, and target kinds |
| `CreateAsciiString(service, text)` | Creates an ASCII `TriglavPlugInStringObject` |

### SDK bindings

C# bindings for TriglavPlugIn SDK are under `CSPBridgeEffects/Library/SDK/`.

| File | Content |
| --- | --- |
| `CSPBridgeEffectsLibType.cs` | Struct definitions (`TriglavPlugInServer`, etc.) |
| `CSPBridgeEffectsLibDefine.cs` | Constant definitions (`kTriglavPlugIn...`) |
| `CSPBridgeEffectsLibRecord.cs` | `TriglavPlugInRecordSuite` struct |
| `CSPBridgeEffectsLibService.cs` | `TriglavPlugInServiceSuite` struct |
| `CSPBridgeEffectsLibRecordFunction.cs` | Wrappers for record functions |

---

## 7. Notes

### Handling JSON in Meson (using jq)

In Meson 1.10, `read_json` and `import('json')` are unavailable, so this project extracts values from `effects.json` with `jq`.

Flow in `meson.build`:

1. Detect `jq` with `find_program('jq')`
2. Run `jq -r ".effects[].id" effects.json` via `run_command()`
3. Split stdout by newlines to create the `effect_ids` list
4. Generate targets in `foreach effect_id : effect_ids`

### About `runtimeconfig.json`

C++ `BridgeBase` requires `CSPBridgeEffects.runtimeconfig.json` when initializing CoreCLR with `hostfxr_initialize_for_runtime_config`.

Setting `<EnableDynamicLoading>true</EnableDynamicLoading>` in the C# project forces `runtimeconfig.json` generation even for a class library (by default it is generated only for executables).

### Debugging in VSCode

1. Set `plugin_path` and build to copy files into the CSP plug-in folder.
2. Launch CSP, apply the filter, and verify behavior.
3. Use Visual Studio or the VSCode .NET debugger for C# debugging.

---

## 8. License

This repository is distributed under the [MIT License](../LICENSE).
