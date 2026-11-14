# 🥊 Underground Ring Trainer

**A 2D pixel-art boxing game where you don't fight — you *train* the fighter.**

You coach an AI boxer in an underground fight circuit. In the gym, you teach it by actually fighting: showing it how you'd box, sparring against it, and grabbing control when it's about to make a mistake. On fight night it steps into the ring alone, and you watch from the corner to see if your training holds up.

The fighter's brain is a small neural network that learns from **how you play**. Every habit it has (good or bad) came from you.

> *"That behavior came from how I trained them."*
> If a player can say that after a fight, the game works.

---

## Why I'm Building This

I'm passionate about machine learning, and especially about what it can do for **user experience**: software that adapts to the person using it instead of making every person adapt to the software.

Most games and apps treat AI as a fixed opponent or a hidden system. I want to flip that: make the learning **visible and personal**, so the user is actively shaping a model and can *see* the results of their choices. This game is my way of exploring that idea in a form that's fun and immediate. A fighting game is a great test bed, because every decision is fast, readable, and has clear consequences.

What I want to learn and show with this project:
- How to turn **imitation learning** and **human feedback** into a core game mechanic, not just a backend feature
- How to design interfaces that make a model's learning **understandable** to non-technical players (Habit Reports, "Learned From" tags, the ring minimap)
- How an experience can become **adaptive**: every player ends up with a different fighter, because every player teaches differently

---

## The Game Loop

```
Pick a fight  →  Train in the gym  →  Fight night (AI fights alone)  →  Review the replay  →  back to the gym
```

1. **Pick a fight:** opponent style, entry fee, prize money, risk.
2. **Train in the gym** (this is where *you* play):
   * **Show:** you control your fighter; it records how you box.
   * **Spar:** you fight *against* your fighter and hit 👍 / 👎 as it reacts.
   * **Take Over:** your fighter spars on its own; hold a button to take control when it's about to mess up.
3. **Fight night:** boxing-style rounds (damage carries over, KO anytime, judges decide if it goes the distance). You can't control the fighter. You coach from the corner between rounds.
4. **Review:** tag moments in the replay as Good / Bad / Mistake, read the Habit Report, then go back and train again.

Win money and reputation, recruit up to three fighters, and climb from **Bronze → Elite → Underground King**.

### Ring & Movement
The fight happens on a square ring simulated in 2D from above, so fighters can **circle** each other, trap opponents on the ropes and in corners, and get **angles** around a blocking opponent's guard. It's shown in a KOF-style side view with depth, plus a **top-down minimap** of the ring.

### Art Direction
A darker take on classic *King of Fighters* pixel art, set in a grimy underground arena with neon accents. *(Not started yet; the current build is a greybox.)*

---

## How the ML Works

### 1. The fighter has the same controller you do
The fighter's brain "presses" the same **11 buttons** a player uses:
`Idle · Forward · Back · Circle Left · Circle Right · Dash In · Dash Out · Jab · Heavy · Block · Dodge`

All movement is relative to the opponent, so whatever you do in the gym can be copied 1:1.

### 2. It sees the fight as numbers
Every **0.1 seconds**, the game describes the situation from the fighter's point of view as **51 numbers**:
* Spacing: distance, closing speed, sideways (circling) speed
* Resources: both fighters' health and stamina
* What the opponent is doing (jab wind-up, heavy active, blocking, dodging, ...)
* Angles: is my guard on them, am I outside their guard?
* Ring position: how close the ropes are, room behind me and behind them, corners, centre
* The fight clock and scorecard (am I ahead on the cards?)

### 3. It learns from you in three ways

| Gym mode | What's recorded | ML technique |
|---|---|---|
| **Show** | (situation → button you pressed) | **Behavior cloning:** supervised learning to copy your choices |
| **Take Over** | Your corrections, exactly in the situations where the AI was about to fail | **HG-DAgger:** fixes the main weakness of pure imitation (small mistakes compounding into situations it never saw) |
| **Spar** | Your 👍 / 👎 attached to its recent actions | **Learning from human feedback** (TAMER-style): reweights what it does |
| **Replay tags** | Good / Bad / Mistake on fight moments | Dataset reweighting |

### 4. The model
A small **neural network (MLP)** in **PyTorch**: 51 inputs → two hidden layers (32–64 neurons) → a probability for each of the 11 buttons. It runs in a local **Python (FastAPI) server** that the Unity game talks to over **WebSockets**. It retrains in batches after each gym session or fight, never mid-fight, so behavior stays stable.

### 5. Making learning visible
* **Habit Report:** after every retrain, the model is tested on fixed situations and the changes are shown in plain language: *"Now blocks heavy attacks 40% more often."*
* **"Learned From" tags:** replays show where a behavior came from: *"Learned in Drill #3."*
* **Rookie AI baseline:** before any training, fighters use a rule-based brain ([`Docs/rookie_ai.md`](Docs/rookie_ai.md)), so the game is playable from minute one and you can see your fighter grow beyond it.

### Later: Reinforcement Learning
Imitation can only be as good as the trainer. Post-MVP, reinforcement learning (rewards for damage, rounds won, successful defense, plus your 👍/👎) lets a fighter eventually **outgrow its trainer**.

---

## Tech Stack

* **Engine:** Unity 6 (6000.4), URP 2D, new Input System
* **Platform:** PC first (Windows), keyboard or controller
* **ML:** Python + PyTorch, served locally with FastAPI over WebSockets *(planned)*
* **Architecture:**
  * The fight simulation is **plain C# with no Unity dependency**, deterministic, at a fixed **60 ticks per second**, so fights can run headless for tests and fast ML training
  * Every fighter is driven through one interface (`IInputSource`): keyboard, Rookie AI, and later the ML brain and Take Over are all just different "button pressers"

---

## Milestones

### ✅ Milestone 0 — Greybox (current)
Two coloured rectangles boxing under the real rules. No art, ML, money or menus.

- [x] Movement and fighter state machine
- [x] Jab / Heavy with frame data (startup → active → recovery) and hit detection
- [x] Block (stamina drain, guard break), Dodge (invulnerable frames), stamina
- [x] Combos (max 3 hits, only on hit) and the Counter Window (+50% after a dodge)
- [x] Rookie AI opponent with basic ring sense
- [x] Boxing-style rounds: 3 × 60s, HP carries over, KO, judge decision, corner break
- [x] 2D ring: circling, guard angles, ropes and corners, ring-control metrics, top-down minimap
- [x] Training recorder: writes (situation, button) pairs to `Recordings/*.jsonl`
- [ ] Playtest and tune the numbers

### 🔜 Milestone 1 — The Brain
- [ ] Python ML server + MLP trained on recorded Show sessions
- [ ] Unity ↔ Python WebSocket connection; the fighter fights with its trained model

### 🔜 Milestone 2 — The Gym
- [ ] Spar mode (you vs your fighter, 👍 / 👎 feedback)
- [ ] Take Over mode (hold to take control)
- [ ] Habit Report after each retrain

### 🔜 Milestone 3 — The MVP Loop
- [ ] Hub, gym, fight, corner break and post-fight screens
- [ ] Fight selection, money, one fight tier

### 🔭 After the MVP
Drills, Shouts from the corner, "Learned From" replay tags, Brain View, multiple fighters, injuries, knockdowns and 3-judge scorecards, reinforcement learning, pixel-art sprites in the KOF-inspired style.

---

## Running the Greybox

1. Open the project in **Unity 6000.4** or later.
2. Open `Assets/Scenes/SampleScene` and press **Play**. Everything sets itself up.

| Key | Action |
|---|---|
| W A S D / arrows | Move around the ring (double-tap or Shift = dash) |
| J / K | Jab / Heavy |
| L (hold) | Block |
| Space | Dodge |
| M | Minimap size (normal / large / hidden) |
| R | Start / stop recording training data |
| Tab | Hand your side to the AI (watch AI vs AI) |
| F1 / F2 / P | Debug overlay / slow motion / pause |
| F5 | New fight |

All fight numbers live in `FightConfig` and can be tuned live in the Inspector on the `GreyboxGame` object.

---

## Project Layout

```
Assets/Scripts/
  Core/       Fight simulation: moves, fighters, hits, ring (plain C#)
  Rounds/     Boxing rounds, KO, corner break, judging
  Input/      Who presses the buttons: keyboard, Rookie AI (later: ML brain)
  Training/   ML input encoding (51 features) and the training-data recorder
  Game/       Unity side: visuals, HUD, minimap
  Debug/      F1 overlay (hitboxes, frame data, AI decisions)
Docs/
  pdd_updated.md   Full product design document
  rookie_ai.md     Rookie AI behavior reference
```

---

## Docs
* **[Product Design Document](Docs/pdd_updated.md):** the full design: combat, ML, gym modes, UI, economy, roadmap
* **[Rookie AI](Docs/rookie_ai.md):** how the baseline fighter brain works
