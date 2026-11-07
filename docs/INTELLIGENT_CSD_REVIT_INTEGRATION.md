# Intelligent CSD Optimizer - Revit integration status

## What is now integrated

The ALIGN ribbon now includes **CSD Optimize**. The command is read-only and targets Revit 2020 / .NET Framework 4.7.2.

1. Reads native MEP elements in the active view or selection and traverses loaded Revit links visible in the active view. A selected RevitLinkInstance can also be analysed.
2. Supports duct and flex duct, duct fittings/accessories, pipe and flex pipe, pipe fittings/accessories, cable trays/fittings, conduits/fittings, and mechanical/electrical equipment.
3. Reads rectangular, oval and round connector envelopes where available. Round connector diameter is converted to a square section envelope.
4. Uses built-in width/height/diameter parameters as a fallback when connector dimensions are unavailable.
5. Creates candidate arrangements using the existing knowledge-based packing engine.
6. Presents ranked options, clash/clearance/rule metrics, per-element proposed positions, and a separate solver-layout preview.
7. Projects MEPCurve tessellated centreline segments against the active view plane (5 mm centreline tolerance), interpolates crossing points, and uses connector profile envelopes for the route cut markers.
8. Projects fittings, accessories and equipment whose transformed 3D bounding boxes intersect the active view plane; these are explicitly identified as conservative bounding-box envelopes.
9. Reports tested elements, section hits and elements outside the active section plane.
10. Records engineer grades in the current session, reports NDCG@5, and exports the selected candidate to CSV.

## Safety boundary

This increment does **not** move, rotate, resize, disconnect, reconnect, or recreate any Revit MEP element. The window is an analysis/review surface only. Exported positions are solver coordinates in millimetres, not a command to modify model geometry.

The extraction adapter traverses loaded RevitLinkInstance documents, applies each link's total transform to element centres, curve tessellation points, bounding-box corners and connector coordinate frames, and prefixes element identifiers with the link instance identity. The review window now contains two distinct views: (a) an **actual model section projection** using active-view-plane intersections and (b) a **proposed solver arrangement** using abstract packing placements. For MEPCurve routes, tessellated location-curve segments are intersected with the active view plane and connector dimensions provide the displayed profile envelope. Fittings, accessories and equipment use transformed 3D bounding-box envelopes when their boxes cross the plane. Tooltips identify the projection method. The 5 mm tolerance applies to route centreline intersection, not the physical outside face of insulated services. Bounding-box envelopes are conservative and are not exact solid/face intersections. The linked-model collector still uses transformed element-centre crop filtering, so a long linked route whose centre lies outside the crop may be omitted even if a portion enters the view. Unloadable links are skipped. The solver candidate preview remains an optimization proposal, not a relocated or model-validated geometry result. Insulation, lining, slope, access zones, supports, hanger clearances, fire-stopping and maintenance zones are not yet fully modelled.

## Engineering limitations to resolve before construction use

- Improve linked-model extraction with robust visibility/crop-shape filtering, unloaded-link diagnostics, source document version tracking and linked-model performance tests.
- Upgrade route centreline/profile projection to exact solid/face intersections including insulation, lining, non-rectangular profiles and oblique sections; add robust crop-shape filtering for linked routes.
- Add system-specific rules, project profiles, code/standard checks, insulation and fitting envelopes.
- Add multi-section corridor continuity and utility crossover handling.
- Add independent geometry-based clash validation and an engineer-approved application workflow with transaction rollback.
- Persist profiles, assessments and candidate history; current engineer grades are session-only.
- Add automated unit/integration tests and validate with representative Revit 2020 project models.

## How to run

Open a Revit 2020 project, activate the intended section/coordination view, optionally select the native MEP elements to analyse, then choose **BIM TOOLS > ALIGN > CSD Optimize**. Review the alternatives and export a candidate report if required.

This is an engineering-assistance prototype, not an automatically approved coordinated shop drawing. A qualified MEP engineer must validate all clearances, levels, access, maintainability and project requirements.
