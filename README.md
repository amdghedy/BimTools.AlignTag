# BimTools.Align

**BIM TOOLS – Tag Align Suite for Autodesk Revit 2020**

[![Build BimTools.Align](https://github.com/amdghedy/BimTools.AlignTag/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/amdghedy/BimTools.AlignTag/actions/workflows/dotnet-desktop.yml)

Professional Revit tag alignment, arrange, and leader tools under the **BIM TOOLS** ribbon tab. Includes a universal alignment workflow for supported annotation tag classes.

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

### UNIVERSAL ALIGN
Left · Center · Right · Top · Middle · Bottom · Dist H · Dist V · All View · Match H · Match V · Pinned · Unpinned · Invert

The **TAG SELECTION** panel provides Pinned, Unpinned and Invert selection filters scoped to supported tags in the active view.

Universal alignment detects supported annotation elements through their writable `TagHeadPosition` API property instead of requiring `IndependentTag`. It supports IndependentTag-based family tags and the Revit RoomTag, AreaTag and SpaceTag classes when the active Revit API exposes that property. Alignment and distribution are calculated in the active view's right/up coordinate system, so rotated plans and sections are handled correctly. Pinned elements are skipped. Select tags first, or pick them when prompted. **All View** selects every supported tag in the active view so it can be used as the input for the alignment commands. **Match H** and **Match V** use the first selected tag as the master and align the remaining tag heads to its view-horizontal or view-vertical coordinate.

The universal commands intentionally move tag heads only. They do not claim to normalize leader geometry for spatial tags or to align dimensions, text notes, symbols, or arbitrary model elements.

### ANNOTATION MANAGER

The first Annotation Manager layer adds geometry-based operations for selected **TextNote**, **Dimension**, and supported tag elements:

- Align H / Align V: align annotation bounding-box centres using active-view axes.
- Dist H / Dist V: evenly distribute annotation centres along the active-view horizontal or vertical axis.
- Audit: inspect visible annotation bounds and report potential 2D overlaps and elements without usable view bounds.
- Resolve: calculate a collision-reduction move plan, show a confirmation preview, then apply only after the user confirms. Pinned elements are excluded.

Collision clearance is configurable in BIM TOOLS → TAG TOOLS → Settings and defaults to 2 mm. The preview lists affected element IDs and proposed move distances; the audit report lists detected overlap pairs. The audit is a geometric screening tool, not a semantic checker: bounding-box intersections can include intentional overlaps and cannot prove that leaders, dimension witness lines, text readability, or plotted output are correct. Re-run Audit after applying a plan and visually review the view. Every move is performed in a Revit transaction with per-element failure isolation.

The manager uses Revit view-specific bounding boxes and ElementTransformUtils.MoveElement; it does not rewrite dimension references or text content. Leader elbow/endpoint optimization, MEP-host-aware placement, configurable rule profiles, persistent audit reports and a richer graphical preview remain planned work and are not claimed as complete in this phase.
### INTELLIGENT CSD OPTIMIZATION ENGINE — FOUNDATION

The feature branch introduces a Revit-independent 2D section-packing engine for generating and scoring candidate MEP service arrangements. It evaluates section boundaries, physical overlaps, clearances, configurable discipline-pair separation rules, preferred elevations, fixed-item movement, compactness and access. Expert candidate grades can be evaluated using NDCG@k; the current calibration helper adjusts a ranking weight conservatively from explicit assessments.

**Current limitation:** this is an optimization-engine foundation, not yet a complete Revit CSD optimizer. Revit connector/geometry extraction, a project rule/profile editor, graphical candidate preview, assessment persistence, multi-section corridor continuity and a safe native-model apply workflow remain to be implemented. The engine does not move Revit elements, create fittings, route services in 3D or edit linked models. Candidate outputs are engineering recommendations, not approved construction layouts or code-compliance checks.

Engineering scope and implementation gates: [INTELLIGENT_CSD_ENGINE.md](docs/INTELLIGENT_CSD_ENGINE.md).

### MEP TAG ORGANIZER

**MEP Organize** works on selected MEP tags, or all supported MEP tags in the active view when nothing is selected. It recognizes duct, flex-duct, pipe, flex-pipe, cable-tray and conduit tags (including common fittings), skips pinned tags, sorts tag heads by host location in active-view coordinates, spaces the heads into side-based columns, and attempts to set orthogonal leader elbows while retaining each tag-to-host reference. A preview is shown before applying changes. **MEP Select** selects eligible tags; **MEP Audit** reports eligible, pinned and leaderless tag counts. The organizer is a deterministic layout heuristic: it reduces leader crossings but cannot guarantee a collision-free result for every dense or multi-reference view. Review the result in Revit 2020 after applying; no model geometry or MEP system parameters are changed.

### ARRANGE
Stack (uncross) · Left Col · Right Col · Untangle · Flip · Parallel · Eq Offset · Match Len · Minimize

### TAG TOOLS
Horiz · Vert · To Ref · Straighten · Orient · Sel Cross · Pin · Unpin · Settings · About

---

## License

Internal BIM TOOLS project.
