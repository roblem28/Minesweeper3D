# 3D Minesweeper — Setup Guide (Shell-First)

## Prerequisites

- Git installed
- GitHub CLI (`gh`) installed — https://cli.github.com/
- Unity Hub + Unity LTS (6000.x or 2022.3.x) installed
- .NET SDK 8+ (for running NUnit tests outside Unity)

---

## Step 1 — Create GitHub Repo

```powershell
# From your projects directory
mkdir Minesweeper3D
cd Minesweeper3D
git init
gh repo create Minesweeper3D --public --source=. --push
```

## Step 2 — Create Unity Project via CLI

```powershell
# Option A: Unity Hub CLI (if available)
# Find your Unity editor path first
& "C:\Program Files\Unity Hub\Unity Hub.exe" -- --headless create -projectPath . -template com.unity.template.urp-blank

# Option B: Just create via Unity Hub GUI
# File > New Project > 3D (URP) > set location to your Minesweeper3D folder
# Then come back to shell
```

**ASSUMPTION:** You have Unity LTS installed. If not, install via Unity Hub first.

## Step 3 — Create Folder Structure

```powershell
# Run from repo root (inside the Unity project)
New-Item -ItemType Directory -Force -Path @(
    "Assets/Core",
    "Assets/CoreTests",
    "Assets/Unity",
    "Assets/AgentBridge",
    "docs"
)
```

## Step 4 — Drop the Generated Files

Copy the files I'm generating into these paths:

```
Assets/Core/Core.asmdef
Assets/Core/Types.cs
Assets/Core/Board.cs
Assets/Core/Rules.cs
Assets/Core/Generator.cs
Assets/Core/Solver.cs
Assets/CoreTests/CoreTests.asmdef
Assets/CoreTests/BoardTests.cs
Assets/CoreTests/GeneratorTests.cs
Assets/CoreTests/SolverTests.cs
docs/SPEC.md
docs/DEDUCTIONS.md
```

## Step 5 — Assembly Definitions

The `.asmdef` files are included in the generated output. They ensure:
- `Core` compiles with zero Unity dependencies
- `CoreTests` references Core + NUnit

## Step 6 — Verify Tests in Unity

```
Unity Editor > Window > General > Test Runner > EditMode > Run All
```

## Step 7 — First Commit + Branch Workflow

```powershell
git checkout -b feat/core-board
git add -A
git commit -m "Phase 1: Core board implementation + tests"
git push -u origin feat/core-board
gh pr create --title "Phase 1: Core Board" --body "See PR checklist in description"
```

## Step 8 — Feeding Claude Code (Phase by Phase)

After merging each PR, start the next branch:

```powershell
git checkout main
git pull
git checkout -b feat/generator  # or whatever the next phase branch is
```

Then paste the phase prompt from the execution plan into Claude Code.

---

## PR Checklist (copy into every PR description)

```markdown
## PR Checklist
- [ ] No Unity types (`UnityEngine`, `MonoBehaviour`, `Vector3`) in `/Assets/Core`
- [ ] NUnit tests added/updated in `/Assets/CoreTests`
- [ ] All tests pass locally (Test Runner > EditMode)
- [ ] `docs/SPEC.md` updated if API changed
- [ ] `docs/DEDUCTIONS.md` updated if solver rules changed
- [ ] No compiler warnings
- [ ] Branch named `feat/<phase-name>`
```
