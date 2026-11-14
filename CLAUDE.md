# Underground Ring Trainer — notes for Claude

A 2D pixel-art boxing/fighting game where the player is a **trainer**, not a fighter.
The player trains an AI fighter in the gym (Show / Spar / Take Over); in real money fights
the fighter fights on its own and the player only coaches from the corner.
Full design: `Docs/pdd_updated.md` (source of truth; keep it in sync when design changes).
Rookie AI reference: `Docs/rookie_ai.md` (keep in sync with `Input/RookieAI.cs`).

## Project
- Unity 6000.4 (Unity 6.4), URP 2D, **new Input System only** (`activeInputHandler: 1`) —
  use `UnityEngine.InputSystem.Keyboard.current`, never `UnityEngine.Input`.
- **PC first** (Windows, keyboard + controller). Mobile is not a goal for now; if it ever happens, the
  brain moves inside Unity (Inference Engine) instead of the Python server (PDD §7, §11).
- ML later: Python FastAPI + PyTorch MLP over WebSockets (PDD §11). Not built yet.

## Current milestone: Milestone 0 — greybox
Two coloured rectangles fight under the real rules on a square ring (2D, seen from above),
shown in a side view with depth plus a top-down minimap. No art, no ML, no economy, no menus.
Press Play in `SampleScene`: `GreyboxBootstrap` spawns everything automatically.

Done when:
1. [x] Movement + state machine (walk, dash, facing)
2. [x] Jab / Heavy with startup → active → recovery frame data, hit detection
3. [x] Block (stamina drain, guard break), Dodge (invulnerable frames), stamina
4. [x] Combos (max 3, Heavy ends chain, only on hit) + Counter Window (+50% after a dodge)
5. [x] Rookie AI opponent
6. [x] Boxing-style rounds: 3 × 60s, HP carries over, KO anytime, judge decision, corner break
7. [x] Recorder writes (state, button) pairs to `Recordings/*.jsonl` (format v2)
8. [x] 2D ring: circling, turn rates, guard arc + Angle Hits, ropes/corners, ring metrics, minimap
9. [ ] Playtest in Unity and tune numbers in `FightConfig`

## Architecture rules (keep these)
- **Fixed 60 ticks/second, separate from rendering.** `GreyboxGame.Update` accumulates time and
  calls `FightMatch.Tick` at exactly 1/60s. Frame data means the same thing everywhere.
- **Core is plain C#, no UnityEngine.** `Assets/Scripts/Core`, `Rounds`, `Training`,
  `Input/IInputSource.cs`, `Input/RookieAI.cs` must not reference Unity, so fights can run
  headless (tests, fast ML self-play). Unity-only code lives in `Game/`, `Debug/`, `Input/KeyboardInput.cs`.
- **A fighter doesn't know who presses its buttons.** Everything drives fighters through
  `IInputSource.GetAction` returning one of the 11 `FighterAction`s
  (Idle, Forward, Back, CircleLeft, CircleRight, DashIn, DashOut, Jab, Heavy, Block, Dodge).
  All movement is relative to the opponent; KeyboardInput translates WASD screen directions. Future sources: `MLBrain`, `ControlArbiter` (Take Over).
- **2D ring, ring coordinates:** `Vec2` positions seen from above (x right, y toward the back of
  the ring). The side view maps them with `GreyboxGame.RingToWorld`. Changing actions or
  `StateEncoder` features means bumping `StateEncoder.Version` (recordings carry it).
- **Deterministic sim.** Same inputs → same fight. AI randomness uses seeded `System.Random`.
- All tunables live in `FightConfig` (serialized, editable in the Inspector at runtime).
- Conservative C# syntax in core files (they're also compiled with Mono `mcs` for tests).

## Folder map
- `Core/` FighterAction, FightConfig (+MoveData), Fighter (state), FightEvent, FightSim (the tick), Vec2
- `Rounds/` FightMatch (rounds, KO, corner break, result), JudgeScorer (10-point must, ring-control tiebreak)
- `Input/` IInputSource, KeyboardInput, RookieAI
- `Training/` StateEncoder (ML input vector v2, 51 floats), TrainingRecorder (JSONL)
- `Game/` GreyboxGame (loop + hotkeys + ring scenery), FighterView (box visuals with depth), FightHud (IMGUI HUD + minimap), GreyboxArt, GreyboxBootstrap
- `Debug/` DebugOverlay (F1: hitboxes, states, frame counters, AI decisions)

## Controls (greybox)
WASD move around the ring (double-tap or Shift = dash) · J jab · K heavy · L block (hold) · Space dodge ·
M minimap · F1 debug · F2 slow-mo · P pause · R record · Tab AI vs AI · F5 new fight

## Next milestones (from PDD §14)
Python ML server + MLP → Unity↔Python WebSocket → Spar mode → Take Over (ControlArbiter) →
Habit Report → real UI screens → fight loop + money.
