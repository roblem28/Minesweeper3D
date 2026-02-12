# Night Handover / Turnover Document

## Purpose
This document is intended for Claude Code to continue progress tonight without losing context from today’s work session.

---

## 1) What Was Completed Today

### 1.1 Commits merged into current branch (`work`)
1. `c873373` — **Phase 1: Core board, solver, generator + tests**
   - Established the pure C# core gameplay model (`Board`, `Generator`, `Solver`, rules, types).
   - Added NUnit test coverage for board behavior, generation constraints, and solver deductions.
   - Added baseline project/docs scaffolding.
2. `4a05337` — **Add comprehensive handoff document**
   - Added the initial handoff (`docs/HANDOFF.md`) with architecture, phase status, and known issues.
3. `3978c75` — **Unify OnGUI HUD rendering with board-derived hidden/safe counters**
   - Updated HUD logic so all counters are computed from board state fields.
   - Added/updated board counters and tests to validate these invariants.
4. `48487ac` — merge of PR #2 (**adjacency-counting related fix stream**)
   - Integrates the above branch work into `work`.

### 1.2 Core logic that is in good shape now
- 3D adjacency/cell topology logic is implemented in the core board model.
- Reveal/chord/flag flow and game status transitions are covered by tests.
- Deterministic board generation with first-click safety exclusion exists.
- Rule-based solver (R1/R2) and no-guess validation path are present.
- HUD script now consumes board counters and can render via TextMeshPro labels and/or OnGUI fallback.

---

## 2) Current State of the Scene / Why We Were “Fighting It”

The biggest practical blocker is **Unity scene wiring**, not the core C# game logic.

### 2.1 What appears to be happening
- `Assets/Scenes/SampleScene.unity` currently looks like a near-default URP sample scene (camera/light/volume roots).
- There is no clear, committed gameplay board presenter/driver object in scene YAML right now.
- `GameHUDController` exists, but it is an integration-facing component that expects other systems to call:
  - `CreateBoard(...)`
  - `SingleClickReveal(...)`
  - `DoubleClickChord(...)`
  - `ToggleFlag(...)`
  - `SetSlice(...)`
- If those entry points are not wired from input + board-view controller, scene behavior can look broken or inert.

### 2.2 Why this caused friction
- Core tests pass conceptually, which can make the project look healthy.
- But runtime scene behavior depends on Unity object graph setup and event hookups that are easy to miss.
- Existing known issues also mention OnGUI fallback usage and input/game-over edge cases, adding confusion during manual validation.

---

## 3) Known Open Problems (High Priority Tonight)

1. **Scene playability gap**
   - Need a concrete playable scene flow connecting input → board actions → visuals/HUD.
2. **Scene object wiring / references**
   - `GameHUDController` serialized TMP fields may not be assigned in scene.
   - No guaranteed bootstrapper invokes `CreateBoard(...)` at startup.
3. **Input pipeline uncertainty**
   - Project uses New Input System; misconfiguration can produce “no input” with no obvious errors.
4. **Post-loss input handling**
   - Existing handoff notes that some paths may still accept input after game over.
5. **Visual 3D layer issues**
   - Ghost/transparency/sorting concerns still listed as unresolved in prior handoff.

---

## 4) Recommended Plan for Claude Tonight (Ordered)

### Step A — Establish a reliable playable bootstrap
- Add a small MonoBehaviour bootstrapper (if absent) that:
  - creates board once on `Start`,
  - initializes slice state,
  - safely updates HUD.
- Ensure scene has all required references assigned (HUD TMP labels optional but deterministic).

### Step B — Verify input wiring end-to-end
- Confirm Player Settings and active input actions align with new Input System.
- Ensure clicks/chords/flag toggles invoke `GameHUDController` methods.
- Add temporary debug logs at input boundaries to verify event arrival.

### Step C — Add/restore board presenter in scene
- Ensure there is a board visualization layer that maps `Coord3` to GameObjects.
- On reveal/flag/chord, sync visuals from authoritative board state.
- Prevent stale visual state after flood/chord cascades.

### Step D — Lock game-over state
- Guard interaction paths if `Board.Status != Playing`.
- Verify no reveal/flag/chord mutates state post win/loss.

### Step E — Regression checks
- Re-run existing core tests.
- Do manual scene smoke test:
  - first click always safe,
  - reveal flood behavior,
  - flag/chord behavior,
  - win/loss transitions,
  - HUD counters match board state.

---

## 5) Concrete “Definition of Done” for Tonight

Claude should aim to finish when all are true:
1. Opening `SampleScene` and pressing Play gives a visibly interactive board flow.
2. Left-click reveal, right-click flag, double-click chord (or equivalent mapped actions) work from scene.
3. HUD counters (`Mines`, `Hidden`, `Safe Left`, `Slice`) are accurate during gameplay.
4. Input is ignored after game over.
5. No missing-reference errors in Console on startup or interaction.
6. Core tests remain green.

---

## 6) Files Most Relevant for Claude to Start With

- `Assets/Scripts/GameHUDController.cs`
- `Assets/Core/Board.cs`
- `Assets/Core/Generator.cs`
- `Assets/Core/Solver.cs`
- `Assets/CoreTests/BoardTests.cs`
- `Assets/Scenes/SampleScene.unity`
- `docs/HANDOFF.md`
- `docs/SPEC.md`

---

## 7) Fast Triage Checklist Claude Can Run Immediately

1. Open `SampleScene` and inspect hierarchy for gameplay root objects/scripts.
2. Check for missing script references and unassigned serialized fields.
3. Confirm any input driver script actually calls HUD/controller API.
4. Run one clean play session and capture first Console errors.
5. Fix wiring first, then visual polish.

---

## 8) Important Context Notes

- The **core gameplay model is not the main risk** at this moment; integration/wiring in Unity scene is.
- Keep the Core/UI separation intact (do not pull Unity dependencies into `/Assets/Core`).
- If adding Unity-side scripts, keep them in Unity-facing folders/assemblies and treat `Board` as the source of truth.

