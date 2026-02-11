# HANDOFF

## 1) Project Overview
Minesweeper3D is a 3D Minesweeper implementation with a pure C# gameplay core and a Unity-based UI/presentation layer.

## 2) Repository URL
https://github.com/roblem28/Minesweeper3D

## 3) Local Path
`C:\Users\roble\UnityProjects\MineSweep3D`

## 4) Tech Stack
- Unity 6.3 LTS
- Universal Render Pipeline (URP)
- Unity Input System (new Input System)
- NUnit (for core tests)

## 5) Architecture
- Strong Core/UI separation.
- Core logic is kept engine-agnostic via `.asmdef` boundaries.
- Core assemblies use `noEngineReferences: true`.

## 6) File Map
- **Core (6 files)**: board model, generation, rules, solver, shared types, assembly definition.
- **CoreTests (3 files)**: board, generator, and solver test suites.
- **Unity (4 files)**: scene/input/rendering integration files.
- **AgentBridge (empty)**: reserved integration area.
- **docs (3 files)**: specification, deductions/explainability notes, setup/build workflow.
- **root (1 file)**: top-level setup guidance.

## 7) Current State
- **Phase 1 complete**: 29/29 tests passing.
- **Phase 6 in progress**: ghost-layer 3D rendering.
- **Phase 7 next**.

## 8) Key Design Decisions
- 26-neighbor adjacency in 3D.
- No-guess solvability guarantee.
- Explainable solver behavior.
- First-click safety.
- Flat-array data layout for performance/simplicity.
- Shared materials where feasible for rendering efficiency.

## 9) Known Issues
1. Ghost transparency sorting artifacts.
2. TextMesh vs TextMeshPro inconsistency.
3. HUD still uses OnGUI.
4. Missing Unity-side `.asmdef` in some areas.
5. Uncommitted/local-state risks in active workflow.
6. Input still accepted after game over in some paths.

## 10) Phase Plan
All 7 phases are tracked with status; current emphasis is finishing Phase 6 and moving into Phase 7.

## 11) How to Test
- Use Unity Test Runner for core tests.
- Run manual playtesting for interaction/UX and 3D presentation behavior.

## 12) How to Build / Contribute
- Follow branch naming + PR workflow.
- Use checklist/process in `SETUP.md`.

## 13) References
- `docs/SPEC.md`
- `docs/DEDUCTIONS.md`
- `SETUP.md`

---

## Critical Warning: New Input System
This project uses the **new Input System**. Silent input failures can occur if legacy `Input.*` paths are assumed or project input settings are mismatched. Validate Input System configuration first when debugging “no input” behavior.
