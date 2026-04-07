# CSPBridge

A bridge framework for developing **CLIP STUDIO PAINT (CSP)** filter plug-ins in **C#**.

Japanese README: [../README.md](../README.md)

---

## Overview

CSP plug-ins must be implemented as C++ DLLs (`.cpm`). CSPBridge provides a thin, shared C++ dispatcher layer so that all actual filter logic can be written in **C#**.

```text
CSP
 └─ {EffectId}.cpm  (C++ / TriglavPlugIn SDK)
      └─ BridgeBase — initializes hostfxr → CoreCLR
           └─ CSPBridgeEffects.dll  (C#)
                └─ CSPBridgeEffects.Effects.{EffectId}
                     ├─ ModuleInitialize  — module ID and kind setup
                     ├─ FilterInitialize  — parameter UI definition and callback registration
                     ├─ FilterTerminate   — resource cleanup
                     └─ FilterRun         — block-based pixel processing
```

- **Adding an effect only requires editing `effects.json`** — no C++ changes needed.
- All filter logic and parameter definitions are implemented on the C# side.
- Per the CSP specification, one `.cpm` DLL is generated per effect. When multiple `.cpm` files are loaded in the same process, they share a single CoreCLR instance.

---

## Prerequisites

| Tool | Version | How to get it |
| --- | --- | --- |
| Windows | 10 / 11 (x64) | — |
| Visual Studio | 2022 or later (C++ build tools) | [visualstudio.microsoft.com](https://visualstudio.microsoft.com/) |
| .NET SDK | 10.0 or later | [dotnet.microsoft.com](https://dotnet.microsoft.com/) |
| Meson | 1.1 or later | `winget install mesonbuild.meson` |
| jq | 1.6 or later | `winget install jqlang.jq` |

To install all tools at once:

```powershell
powershell -ExecutionPolicy Bypass -File .\inst.ps1
```

---

## Build

```powershell
meson setup build
meson compile -C build
```

To automatically copy files to the CSP plug-in folder:

```powershell
meson setup build -Dplugin_path="C:\path\to\CLIP STUDIO PAINT\plug-in"
meson compile -C build
```

---

## Documentation

- [Internal Specification (spec_sheet.md)](spec_sheet.md) — Architecture, effect implementation guide, SDK bindings

---

## License

[MIT License](../LICENSE)
