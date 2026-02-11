# 3D Minesweeper — Core Specification

## Overview

Engine-agnostic 3D Minesweeper core in pure C#. No Unity dependencies in `/Assets/Core`.

## Grid

- NxNxN cubic grid
- Coordinate system: `Coord3(x, y, z)` where each axis ranges `[0, N-1]`
- Flat index: `x + N * (y + N * z)`

## Adjacency

- **26-neighbor**: all cells where `dx, dy, dz ∈ {-1, 0, +1}` excluding `(0,0,0)`
- Neighbor counts by position:
  - Corner (3 axes at boundary): 7
  - Edge (2 axes at boundary): 11
  - Face (1 axis at boundary): 17
  - Interior (no boundary): 26

## Cell State

```
Hidden → Revealed (via Reveal)
Hidden → Flagged (via ToggleFlag)
Flagged → Hidden (via ToggleFlag)
```

## Reveal Operation

1. If out of bounds → `OutOfBounds`
2. If already revealed → `AlreadyRevealed`
3. If flagged → `Flagged` (no-op, must unflag first)
4. If mine → `Mine`, game status = `Lost`
5. Otherwise → BFS flood-fill:
   - Mark cell revealed
   - If neighbor mine count == 0, enqueue all hidden non-mine neighbors
   - Continue until queue empty
6. After reveal, check win (all safe cells revealed)

## Generator

- Inputs: `size`, `mineCount`, `firstClick`, `seed`
- Exclusion zone: `firstClick` + its 26 neighbors
- Fisher-Yates shuffle on non-excluded indices
- Deterministic: same seed → same board

## Solver

- Reads partial game state (revealed cells, flags, counts)
- Applies rules iteratively
- Returns `List<DeductionStep>` with rule ID, source cells, affected cells, MINE/SAFE

### Rules

| ID | Name | Condition | Inference |
|----|------|-----------|-----------|
| R1 | All-hidden-are-mines | `count - flagged == hidden` | Hidden → MINE |
| R2 | All-remaining-are-safe | `count == flagged` | Hidden → SAFE |

## No-Guess Validation

A board is no-guess solvable if:
1. Reveal first click
2. Repeat: apply solver rules, reveal SAFE cells, flag MINE cells
3. If solver stalls with hidden safe cells remaining → FAIL
4. If all safe cells revealed → PASS
