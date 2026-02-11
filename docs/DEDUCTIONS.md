# 3D Minesweeper — Deduction Rules Reference

## Rule R1: All-Hidden-Are-Mines

**When:** A revealed cell's mine count minus its flagged neighbor count equals its hidden neighbor count.

**Then:** All hidden neighbors are mines.

**Example (2D slice for clarity):**
```
[ 2 ][ ? ]
[ ? ][ F ]
```
Cell `[2]` has count=2, flagged=1, hidden=2... wait, that doesn't match.
Correct: count=2, flagged=1, hidden=1 → remaining mines = 1 = hidden count → the 1 hidden cell is a mine.

**3D extension:** Same logic, but neighbors span all 26 directions.

---

## Rule R2: All-Remaining-Are-Safe

**When:** A revealed cell's mine count equals its flagged neighbor count.

**Then:** All remaining hidden neighbors are safe (can be revealed).

**Example (2D slice for clarity):**
```
[ 1 ][ ? ]
[ F ][ ? ]
```
Cell `[1]` has count=1, flagged=1 → all mines accounted for → remaining hidden neighbors are safe.

---

## Solver Loop

```
while game not won:
    steps = SolveStep(board)
    if steps is empty:
        STALL → board requires guessing → no-guess validation FAILS
    for each step:
        if MINE: flag the cell
        if SAFE: reveal the cell
```

## Future Rules (Not Yet Implemented)

These can be added to increase solver power:

- **R3: Subset/superset rule** — If cell A's hidden neighbors are a subset of cell B's hidden neighbors, and `countB - flaggedB - (countA - flaggedA) == |B_hidden \ A_hidden|`, then the difference set are all mines.
- **R4: Constraint propagation** — Gaussian elimination over mine-count constraints.
- **R5: Backtracking** — Assume a cell is MINE/SAFE, check for contradiction. (Still deterministic, not guessing.)

Adding R3-R5 dramatically increases the percentage of boards that pass no-guess validation at higher mine densities.
