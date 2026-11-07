# BimTools.Align

**BIM TOOLS – Tag Align Suite for Autodesk Revit 2020**

[![Build BimTools.Align](https://github.com/amdghedy/BimTools.AlignTag/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/amdghedy/BimTools.AlignTag/actions/workflows/dotnet-desktop.yml)

Professional IndependentTag alignment, arrange, and leader tools under the **BIM TOOLS** ribbon tab.

---

## Download built DLL (CI artifact)

Latest verified **Release | x64** build:

| Item | Link |
|------|------|
| **Actions run** | [Build #15 – success](https://github.com/amdghedy/BimTools.AlignTag/actions/runs/42070975304) |
| **Artifact (ZIP)** | [BimTools.Align-Release](https://github.com/amdghedy/BimTools.AlignTag/actions/runs/42070975304#artifacts) |
| **Direct artifact API** | [Download ZIP](https://github.com/amdghedy/BimTools.AlignTag/actions/artifacts/13436626443/zip) |

> You must be logged into GitHub to download the artifact.  
> Open the run page → **Artifacts** → **BimTools.Align-Release**.

The CI workflow validates that the release DLL and Revit add-in manifest both exist and are non-empty before publishing the artifact.

Artifact contents:
- `BimTools.Align.dll`
- `BimTools.Align.addin`
- `Resources/**` (when resource files are present)

---

## Requirements

- Autodesk Revit **2020** (x64)
- .NET Framework 4.7.2 (build only)
- Visual Studio 2019/2022 (optional – CI builds on every push)

## Install

1. Download the artifact ZIP from the link above.
2. Copy files to:

```
%APPDATA%\Autodesk\Revit\Addins\2020\
    BimTools.Align.dll
    BimTools.Align.addin
```

3. Restart Revit.
4. Ribbon: **BIM TOOLS → ALIGN / ARRANGE / TAG TOOLS**

Optional icons (if present in your local package):

```
%APPDATA%\Autodesk\Revit\Addins\2020\Resources\Icons\
```

---

## Build from source

Revit API is restored from **NuGet** (no local Revit install required to compile):

| Package | Version |
|---------|---------|
| `Nice3point.Revit.Api.RevitAPI` | `2020.2.90` |
| `Nice3point.Revit.Api.RevitAPIUI` | `2020.2.90` |

```bat
git clone https://github.com/amdghedy/BimTools.AlignTag.git
cd BimTools.AlignTag
msbuild BimTools.Align.sln /t:Restore /p:Configuration=Release /p:Platform=x64
msbuild BimTools.Align.sln /t:Rebuild /p:Configuration=Release /p:Platform=x64
```

Output: `bin\x64\Release\BimTools.Align.dll`

CI workflow: `.github/workflows/dotnet-desktop.yml` (runs on pushes and pull requests targeting `main`).

---

## Tools

### ALIGN
Left · Center · Right · Top · Middle · Bottom · Dist H · Dist V · To Line

### ARRANGE
Stack (uncross) · Left Col · Right Col · Untangle · Flip · Parallel · Eq Offset · Match Len · Minimize

### TAG TOOLS
Horiz · Vert · To Ref · Straighten · Orient · Sel Cross · Pin · Unpin · Settings · About

---

## License

Internal BIM TOOLS project.
