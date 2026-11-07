# Project clearance profile editor

The ALIGN ribbon command **Clearance Profiles** opens a project-specific editor for reusable maintenance and access envelope definitions.

## Included in this increment
- Maintenance, operational, left/right side access, overhead, underfloor, equipment-removal, electrical working-space and custom zone types.
- Independent left, right, below and above dimensions in millimetres.
- Optional discipline and engineer notes.
- Add, update, remove, validate and explicitly save profiles.
- Local project-specific JSON persistence under the current Windows user's AppData folder. No project model elements or Revit parameters are changed.
- Schema versioning and defensive loading. A malformed profile file is not overwritten automatically.

## Important engineering boundary
The supplied profiles are editable project criteria, not code-certified values. The included defaults are illustrative starting points and must be checked against project specifications, equipment manufacturer requirements and applicable codes.

This increment does not yet construct 3D zone solids, compare zones against measured corridor sections, report clearance violations, or modify the model. Those are separate subsequent stages. The command is read-only with respect to the Revit document.
