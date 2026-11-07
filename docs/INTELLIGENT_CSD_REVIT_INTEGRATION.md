# Intelligent CSD Optimizer - Revit integration status

## What is now integrated

The ALIGN ribbon now includes **CSD Optimize**. The command is read-only and targets Revit 2020 / .NET Framework 4.7.2.

1. Reads native MEP elements in the active view or selection and traverses loaded Revit links visible in the active view. A selected RevitLinkInstance can also be analysed.
2. Supports duct and flex duct, duct fittings/accessories, pipe and flex pipe, pipe fittings/accessories, cable trays/fittings, conduits/fittings, and mechanical/electrical equipment.
3. Reads rectangular, oval and round connector envelopes where available. Round connector diameter is converted to a square section envelope.
4. Uses built-in width/height/diameter parameters as a fallback when connector dimensions are unavailable.
5. Creates candidate arrangements using the existing knowledge-based packing engine.
6. Presents ranked options, clash/clearance/rule metrics and per-element proposed positions in a review window.
7. Records engineer grades in the current session, reports NDCG@5, and exports the selected candidate to CSV.

## Safety boundary

This increment does **not** move, rotate, resize, disconnect, reconnect, or recreate any Revit MEP element. The window is an analysis/review surface only. Exported positions are solver coordinates in millimetres, not a command to modify model geometry.

The extraction adapter traverses loaded RevitLinkInstance documents, applies each link's total transform to element centres and connector coordinate frames, and prefixes element identifiers with the link instance identity. When the active view crop is active, linked element centres are filtered against the transformed crop-box bounds. Unloadable links are skipped. Connector envelope dimensions are used as packing rectangles; complex fitting geometry, insulation, lining, slope, access zones, supports, hanger clearances, fire-stopping and maintenance zones are not yet fully modelled.

## Engineering limitations to resolve before construction use

- Improve linked-model extraction with robust visibility/crop-shape filtering, unloaded-link diagnostics, source document version tracking and linked-model performance tests.
- Build a true section-plane projection and route-aware grouping for long ducts/pipes/trays, not just connector-envelope packing.
- Add system-specific rules, project profiles, code/standard checks, insulation and fitting envelopes.
- Add multi-section corridor continuity and utility crossover handling.
- Add independent geometry-based clash validation and engineer-approved application workflow with transaction rollback.
- Persist profiles, assessments and candidate history; current engineer grades are session-only.
- Add automated unit/integration tests and validate with representative Revit 2020 project models.

## How to run

Open a Revit 2020 project, activate the intended section/coordination view, optionally select the native MEP elements to analyse, then choose **BIM TOOLS > ALIGN > CSD Optimize**. Review the alternatives and export a candidate report if required.

This is an engineering-assistance prototype, not an automatically approved coordinated shop drawing. A qualified MEP engineer must validate all clearances, levels, access, maintainability and project requirements.
