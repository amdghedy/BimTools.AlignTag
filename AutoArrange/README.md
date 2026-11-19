# BIMTools.TagAlign.AutoArrange

**Auto-Arrange Annotation Tags** — Bird-style "Align Tags Automatically" for BIM TOOLS / Autodesk Revit.

Supported Revit versions: **2020 · 2021 · 2022 · 2023 · 2024 · 2025 · 2026**

---

## Commands

| Command class | Button text | What it does |
|---|---|---|
| `AlignTagsAutoCommand` | Align Tags Automatically | Bird-style column stacking: dialog → select → pick lowest/highest head |
| `LeaderTunerCommand` | Leader Tuner | Re-shapes leaders in place (set angle, fixed landing length, or straight) |
| `AlignHeadsCommand` | Align Tag Heads | Align left/centre/right/top/middle/bottom, or distribute; optionally keeps leader shape |

---

## Install (pre-built from CI)

1. Download the ZIP for your Revit year from **GitHub Actions → Artifacts**.
2. Extract and copy to:
```
%ProgramData%\Autodesk\Revit\Addins\<YEAR>\
    BIMTools.TagAlign.AutoArrange.Revit<YEAR>.dll
    BIMTools.TagAlign.AutoArrange.Revit<YEAR>.addin
```
3. Restart Revit.

---

## Build from source

```bat
git clone https://github.com/amdghedy/BimTools.AlignTag.git
cd BimTools.AlignTag\AutoArrange
dotnet build BIMTools.TagAlign.AutoArrange.Revit2024.csproj -c Release
```

No local Revit install required — Revit API is restored from NuGet (`Nice3point.Revit.Api`).

---

## API compatibility notes

- **Revit 2020–2021**: `IndependentTag.LeaderEnd` and `LeaderElbow` are direct properties.
- **Revit 2022+**: Leader access uses `GetLeaderEnd(Reference)` / `SetLeaderEnd(Reference, XYZ)` etc.  
  `Annotations.cs` handles both via reflection so a single source tree compiles across all versions.
- **Revit 2025+**: `System.Windows.Forms.TaskDialog` was added, requiring a disambiguating `using` alias for `Autodesk.Revit.UI.TaskDialog`.
