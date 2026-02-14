# 3D Minesweeper — Handoff Document

## 1. Project Overview

A 3D Minesweeper game built as a Unity application with a pure C# core engine. The player navigates an NxNxN cubic grid (default 6x6x6), revealing cells, flagging mines, and using 26-directional adjacency counts to deduce mine locations. The game includes a deterministic solver that can verify boards are solvable without guessing, producing explainable step-by-step deduction traces.

The architecture strictly separates engine-agnostic game logic (Core) from Unity presentation (UI), enabling the Core to be tested, solved, and validated with zero Unity dependencies.

## 2. Repo Location

**GitHub:** https://github.com/roblem28/Minesweeper3D
**Local path:** `C:\Users\roble\UnityProjects\MineSweep3D`

## 3. Tech Stack

| Component | Version |
|-----------|---------|
| Unity | 6.3 LTS (6000.3.7f1) |
| Render Pipeline | URP (Universal Render Pipeline) |
| Language | C# |
| Input | Unity Input System (new) — `activeInputHandler: 1` |
| Testing | NUnit via Unity Test Framework |
| .NET | SDK 8+ (for out-of-editor testing) |

**Critical note:** The project uses the **new Input System only**. All input code must use `UnityEngine.InputSystem` (`Mouse.current`, `Keyboard.current`). Legacy `UnityEngine.Input` calls silently return 0/false.

## 4. Architecture

### Core / UI Separation

```
Assets/
├── Core/          Pure C# game logic — NO Unity dependencies
│   └── Core.asmdef        noEngineReferences: true
├── CoreTests/     NUnit tests — editor-only
│   └── CoreTests.asmdef   references: [Core], noEngineReferences: true
├── Unity/         MonoBehaviour scripts — depends on Core + UnityEngine
├── AgentBridge/   (Planned) AI agent integration layer
└── docs/          Specifications and documentation
```

### Assembly Definition Boundaries

- **Core.asmdef** — `noEngineReferences: true` enforces that no `UnityEngine`, `MonoBehaviour`, or `Vector3` types can leak into game logic. This is the hard architectural boundary. If it compiles, it's clean.
- **CoreTests.asmdef** — Editor-only (`includePlatforms: ["Editor"]`), references Core + `nunit.framework.dll`, `defineConstraints: ["UNITY_INCLUDE_TESTS"]`.
- **Unity/** — No `.asmdef` yet (compiles into Assembly-CSharp). References Core via the auto-reference mechanism. All MonoBehaviours live here.

### Data Flow

```
User Input → GameController → Board/Generator (Core) → SliceController → CellView (visual update)
                                    ↑
                              Solver/Rules (Core) — reads board state, returns DeductionSteps
```

UI scripts translate input into Core API calls. No game logic exists outside `/Assets/Core`.

## 5. File Map

### Assets/Core/ — Pure C# Game Engine

| File | Description |
|------|-------------|
| `Core.asmdef` | Assembly definition — `Minesweeper3D.Core`, `noEngineReferences: true` |
| `Types.cs` | `Coord3` struct, `CellState`/`RevealResult`/`GameStatus` enums, `DeductionStep` class |
| `Board.cs` | NxNxN grid with flat-array storage, 26-adjacency, BFS flood-fill reveal, flag toggle, win/loss detection |
| `Generator.cs` | Seeded board generation via Fisher-Yates shuffle with first-click safety (excludes click + 26 neighbors) |
| `Rules.cs` | R1 (all-hidden-are-mines) and R2 (all-remaining-are-safe) deduction rule implementations |
| `Solver.cs` | Iterative solver: `SolveStep` (one pass), `SolveFull` (complete solve), `ValidateNoGuess` (no-guess check) |

### Assets/CoreTests/ — NUnit Tests (29 tests, all passing)

| File | Description |
|------|-------------|
| `CoreTests.asmdef` | Assembly definition — editor-only, references Core + NUnit |
| `BoardTests.cs` | 16 tests: adjacency counts (corner/edge/face/center), reveal, flood-fill, flagging, bounds, win detection |
| `GeneratorTests.cs` | 5 tests: first-click safety, mine count, deterministic seeds, seed variance |
| `SolverTests.cs` | 10 tests: R1/R2 deductions, full solve, step format, no-guess validation |

### Assets/Unity/ — Unity UI Layer (Phase 6)

| File | Description |
|------|-------------|
| `CellView.cs` | Renders one cell as a cube. Two modes: Active (opaque, collider enabled) and Ghost (transparent, collider disabled). Shared materials + `MaterialPropertyBlock` for per-cell colors. Billboard `TextMesh` labels for counts. URP + Standard shader support. |
| `SliceController.cs` | Creates all NxNxN cells in a 3D grid. One slice is active (opaque, interactive), others are transparent ghosts. Scroll wheel changes active slice. |
| `CameraController.cs` | Orbit/zoom camera. Middle-mouse drag to orbit, Ctrl+scroll to zoom. Starts at isometric angle (45° azimuth, 30° elevation). |
| `GameController.cs` | Main orchestrator. First click → `Generator.Generate()` → `Board.Reveal()`. Left click = reveal, right click = flag via `Physics.Raycast` (only hits active slice). `OnGUI` HUD with slice indicator, status, restart button. |

### Assets/AgentBridge/ — (Empty, planned for Phase 7)

Will contain the AI agent integration layer for programmatic board interaction.

### docs/ — Documentation

| File | Description |
|------|-------------|
| `SPEC.md` | Core specification: grid, adjacency, cell states, reveal algorithm, generator, solver, no-guess validation |
| `DEDUCTIONS.md` | Deduction rules reference: R1/R2 with examples, solver loop pseudocode, future rules R3-R5 |
| `HANDOFF.md` | This document |

### Root Files

| File | Description |
|------|-------------|
| `SETUP.md` | Shell-first setup guide: prerequisites, Unity project creation, folder structure, PR workflow, checklist |

## 6. Current State

### Done

- **Phase 1: Core Engine** — `Board.cs`, `Types.cs`, `Generator.cs`, `Solver.cs`, `Rules.cs` — fully implemented and tested. 29/29 NUnit tests passing. Committed on `main` as `e9bb40e`.

### In Progress

- **Phase 6: Unity UI** — Branch `feat/unity-ui` (commit `5b48e44` + uncommitted changes). The rendering works with the ghost-layer Rubik's cube design:
  - All NxNxN cubes rendered in 3D space
  - Active slice: opaque cubes with colored count labels
  - Ghost slices: transparent cubes (alpha 0.12) via URP transparent material
  - Ghost colliders disabled so raycasts only hit active slice
  - Scroll wheel changes slice, middle-drag orbits, Ctrl+scroll zooms
  - **Needs polish:** The ghost transparency may need tuning depending on URP settings. The `TextMesh` billboard labels work but could be upgraded to TextMeshPro for better rendering. The `OnGUI` HUD is functional but basic.

### Not Started

- **Phase 2-5:** Were folded into Phase 1 (core engine covers board, generator, solver, rules, tests)
- **Phase 7: Agent Bridge** — AI agent integration layer in `Assets/AgentBridge/`

## 7. Key Design Decisions

### 26-Adjacency
Every cell connects to up to 26 neighbors (all `dx, dy, dz ∈ {-1, 0, +1}` excluding `(0,0,0)`). This is the natural 3D extension of 2D Minesweeper's 8-adjacency. Neighbor counts by position: corner=7, edge=11, face=17, interior=26.

### No-Guess Guarantee
The `Solver.ValidateNoGuess()` method can verify that a board is solvable purely through logical deduction from the first click. This ensures fair gameplay — the player never needs to guess.

### Explainable Solver
Every deduction returns a `DeductionStep` with: the rule used (`R1`/`R2`), the source cell(s) that drove the deduction, the affected cells, and whether they're inferred as MINE or SAFE. This enables hint systems and step-by-step tutorials.

### First-Click Safety
`Generator.Generate()` guarantees the first click and all 26 of its neighbors are mine-free. This gives the player a safe opening area for initial deductions.

### Flat Array Indexing
The 3D grid is stored as flat arrays (`bool[]`, `CellState[]`, `int[]`) indexed by `x + N * (y + N * z)`. This avoids jagged array overhead and enables efficient iteration.

### Shared Materials + MaterialPropertyBlock
The UI uses only 2 material instances (opaque + ghost transparent) shared across all 216 cells. Per-cell colors are applied via `MaterialPropertyBlock`, which avoids material instance explosion and enables dynamic batching.

## 8. Known Issues

1. **Ghost transparency:** The URP transparent material setup (`_Surface=1`, `_SrcBlend=SrcAlpha`, `_DstBlend=OneMinusSrcAlpha`, `_ZWrite=0`) works but may have sorting artifacts when viewed from certain angles. Z-write is disabled for ghosts which can cause visual popping.
2. **TextMesh rendering:** `TextMesh` (legacy) is used for cell labels. At steep camera angles or far zoom, text may appear blurry or clip. Consider upgrading to TextMeshPro.
3. **OnGUI HUD:** Uses immediate-mode `OnGUI` for the status bar. Functional but not styled. Should migrate to Unity UI Toolkit or Canvas-based UI for production.
4. **No `.asmdef` for Unity folder:** The Unity scripts compile into the default `Assembly-CSharp`. Adding a `Unity.asmdef` with a Core reference would be cleaner.
5. **Uncommitted UI changes:** The `feat/unity-ui` branch has uncommitted changes on top of `5b48e44` (ghost-layer rewrite with 3D grid, transparent materials, new Input System).
6. **No restart on slice change after game over:** The restart button works, but the game doesn't prevent scroll/orbit input after game over.

## 9. Phase Plan

| Phase | Name | Status | Description |
|-------|------|--------|-------------|
| 1 | Core Engine | **Done** | Board, Types, Generator, Solver, Rules + 29 NUnit tests |
| 2 | Generator | **Done** (merged into Phase 1) | Seeded generation with first-click safety |
| 3 | Solver | **Done** (merged into Phase 1) | R1/R2 rules, SolveStep, SolveFull, ValidateNoGuess |
| 4 | Tests | **Done** (merged into Phase 1) | BoardTests, GeneratorTests, SolverTests |
| 5 | Documentation | **Done** (merged into Phase 1) | SPEC.md, DEDUCTIONS.md, SETUP.md |
| 6 | Unity UI | **In Progress** | CellView, SliceController, CameraController, GameController — ghost-layer 3D rendering |
| 7 | Agent Bridge | **Not Started** | AI agent integration in Assets/AgentBridge/ — programmatic board interaction API |

## 10. How to Test

### Unity Test Runner (NUnit)
```
Unity Editor → Window → General → Test Runner → EditMode → Run All
```
Expects 29/29 passing. Tests cover board logic, generator safety, solver correctness.

### Manual Play Testing
1. Open Unity project
2. Open `Assets/Scenes/SampleScene.unity`
3. Create empty GameObject → Add Component → `GameController`
4. Hit Play
5. Left-click a cell to start → board generates → flood-fill reveals safe area
6. Scroll wheel to change Z-slice
7. Right-click to flag suspected mines
8. Middle-drag to orbit, Ctrl+scroll to zoom
9. Check Console for `[MineSweeper3D]` debug logs on every action

## 11. How to Build

### Branch Naming Convention
```
feat/<phase-name>
```
Examples: `feat/core-board`, `feat/unity-ui`, `feat/agent-bridge`

### PR Workflow
```powershell
git checkout main && git pull
git checkout -b feat/<phase-name>
# ... make changes ...
git add <specific files>
git commit -m "Phase N: description"
git push -u origin feat/<phase-name>
gh pr create --title "Phase N: Title" --body "..."
```

### PR Checklist (from SETUP.md)
```
- [ ] No Unity types (UnityEngine, MonoBehaviour, Vector3) in /Assets/Core
- [ ] NUnit tests added/updated in /Assets/CoreTests
- [ ] All tests pass locally (Test Runner > EditMode)
- [ ] docs/SPEC.md updated if API changed
- [ ] docs/DEDUCTIONS.md updated if solver rules changed
- [ ] No compiler warnings
- [ ] Branch named feat/<phase-name>
```

## 12. References

- **[SPEC.md](./SPEC.md)** — Core engine specification (grid, adjacency, reveal, generator, solver)
- **[DEDUCTIONS.md](./DEDUCTIONS.md)** — Deduction rules R1/R2 with examples, solver loop, future rules R3-R5
- **[SETUP.md](../SETUP.md)** — Shell-first project setup guide and PR workflow
