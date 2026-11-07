# ALIGN Intelligent CSD Optimization — Engineering Scope

## Reference and intent
Inspired by Albany Tam's 2016 article on a knowledge-based BIM system for intelligent Combined Services Drawing arrangement. The article describes rule-based service packing, generation of alternative arrangements, expert grading, and ranking using Normalized Discounted Cumulative Gain (NDCG), developed in an Arup/HKUST research collaboration. This repository is an independent implementation; it does not reproduce proprietary source code or claim equivalence.

## Existing baseline
- Revit 2020, .NET Framework 4.7.2, x64.
- Existing ALIGN ribbon, annotation tools, leader tools, annotation QA, and MEP tag organizer.
- Existing MEP organizer is an annotation-layout heuristic, not a model-routing optimizer.
- Linked-tag support does not imply linked model elements can be edited.

## Initial knowledge-based engine
The Revit-independent KnowledgeBasedCsdEngine accepts service envelopes in section-plane millimetres. It generates deterministic alternatives using different engineering orderings and scores section boundaries, clashes, clearances, configurable discipline-pair separation, preferred elevations, fixed-item movement, compactness, access, and electrical/wet-service ordering preferences.

It exposes NDCG@k for expert assessments and a conservative calibration method that adjusts only compactness weight. This is not an ANN and must not be described as trained neural intelligence.

## Engineering boundary
The initial solver is a 2D section packing engine, not a full 3D routing engine. It does not create elbows, tees, reducers, transitions, slopes, supports, fabrication parts, or modify Revit model geometry. A candidate is an engineering recommendation, not proof of constructability or code compliance.

## Implementation gates
1. Revit extraction adapter for true section-plane geometry, connectors, dimensions, slopes, insulation, fittings, system identity, linked transforms, fixed objects and keep-out zones.
2. Rule/profile editor for project standards, clearances, discipline rules, hierarchy, access zones and profile versioning.
3. Candidate preview UI with section graphics, side-by-side alternatives, penalty explanations, manual override and expert grading.
4. Revit-safe apply workflow with explicit movable-element selection, preview, clash re-check, rollback and linked-element read-only handling.
5. Multi-section corridor solver with continuity constraints and transition zones.
6. Unit and Revit integration tests for packing, fixed objects, boundaries, clashes, clearances, NDCG, repeatability and Revit 2020 API compatibility.

## Safety and QA
Never auto-move linked elements. Never silently modify native MEP geometry. Do not label a candidate clash-free unless geometry checks prove it. Discipline leads must review project rules. The source model remains unchanged until a separately validated apply command is implemented.
