# Underground Ring Trainer — notes for Claude

A 2D pixel-art boxing/fighting game where the player is a **trainer**, not a fighter.
The player trains an AI fighter in the gym (Show / Spar / Take Over); in real money fights
the fighter fights on its own and the player only coaches from the corner.
Full design: `Docs/pdd_updated.md` (source of truth; keep it in sync when design changes).

## Project
- Unity 6000.4 (Unity 6.4), URP 2D, **new Input System only** (`activeInputHandler: 1`) —
  use `UnityEngine.InputSystem.Keyboard.current`, never `UnityEngine.Input`.
- Mobile-first eventually; greybox uses keyboard.
- ML later: Python FastAPI + PyTorch MLP over WebSockets (PDD §11). Not built yet.

## Current milestone: Milestone 0 — greybox
Two coloured rectangles fight under the real rules. No art, no ML, no economy, no menus.
Press Play in `SampleScene`: `GreyboxBootstrap` spawns everything automatically.

Done when:
1. [x] Movement + state machine (walk, dash, facing)
2. [x] Jab / Heavy with startup → active → recovery frame data, hit detection
3. [x] Block (stamina drain, guard break), Dodge (invulnerable frames), stamina
4. [x] Combos (max 3, Heavy ends chain, only on hit) + Counter Window (+50% after a dodge)
5. [x] Rookie AI opponent
6. [x] Boxing-style rounds: 3 × 60s, HP carries over, KO anytime, judge decision, corner break
7. [x] Recorder writes (state, button) pairs to `Recordings/*.jsonl`
8. [ ] Playtest in Unity and tune numbers in `FightConfig`

## Architecture rules (keep these)
- **Fixed 60 ticks/second, separate from rendering.** `GreyboxGame.Update` accumulates time and
  calls `FightMatch.Tick` at exactly 1/60s. Frame data means the same thing everywhere.
- **Core is plain C#, no UnityEngine.** `Assets/Scripts/Core`, `Rounds`, `Training`,
  `Input/IInputSource.cs`, `Input/RookieAI.cs` must not reference Unity, so fights can run
  headless (tests, fast ML self-play). Unity-only code lives in `Game/`, `Debug/`, `Input/KeyboardInput.cs`.
- **A fighter doesn't know who presses its buttons.** Everything drives fighters through
  `IInputSource.GetAction` returning one of the 9 `FighterAction`s
  (Idle, Forward, Back, DashIn, DashOut, Jab, Heavy, Block, Dodge). Forward/Back/Dash are
  relative to the opponent. Future sources: `MLBrain`, `ControlArbiter` (Take Over).
- **Deterministic sim.** Same inputs → same fight. AI randomness uses seeded `System.Random`.
- All tunables live in `FightConfig` (serialized, editable in the Inspector at runtime).
- Conservative C# syntax in core files (they're also compiled with Mono `mcs` for tests).

## Folder map
- `Core/` FighterAction, FightConfig (+MoveData), Fighter (state), FightEvent, FightSim (the tick)
- `Rounds/` FightMatch (rounds, KO, corner break, result), JudgeScorer (10-point must)
- `Input/` IInputSource, KeyboardInput, RookieAI
- `Training/` StateEncoder (ML input vector, 34 floats), TrainingRecorder (JSONL)
- `Game/` GreyboxGame (loop + hotkeys), FighterView (box visuals), FightHud (IMGUI HUD), GreyboxArt, GreyboxBootstrap
- `Debug/` DebugOverlay (F1: hitboxes, states, frame counters, AI decisions)

## Controls (greybox)
A/D move (double-tap or Shift = dash) · J jab · K heavy · L block (hold) · Space dodge ·
F1 debug · F2 slow-mo · P pause · R record · Tab AI vs AI · F5 new fight

## Next milestones (from PDD §14)
Python ML server + MLP → Unity↔Python WebSocket → Spar mode → Take Over (ControlArbiter) →
Habit Report → real UI screens → fight loop + money.
