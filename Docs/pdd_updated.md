# 🧠🎮 ML Fighter Trainer Game — Product Design Document

---

# 1. 🧭 Overview

## Concept

A 2D pixel-art competitive fighting simulator where the player acts as a **trainer**, not a fighter. The player trains AI-controlled fighters in a hands-on gym — demonstrating moves, sparring against their own fighter, and taking over mid-spar to correct mistakes — then sends them into underground arena matches where they fight on their own to earn money and progress.

---

## Core Pillars

1. **Indirect Control in Real Fights** — The player never controls their fighter in a real (money) match. The real fight is the exam: it shows whether the training worked. The player's only influence during a real fight is from the corner (Shouts and between-round adjustments, see §6.6).
2. **Hands-On Training** — The gym is where the player actually plays. Every training mode involves real fighting action (see §6).
3. **Visible Learning** — Fighters evolve based on training decisions, and the game shows *where* each behavior came from.
4. **High-Stakes Economy** — Money, injuries, and risk create tension.
5. **Emergent Behavior** — Fighters develop unique styles.

---

# 2. 🎮 Core Gameplay Loop

### Step 1: Select Fight
* View opponent style, entry cost, reward, risk level

### Step 2: Prepare (The Gym)
* Choose **one** fighter to enter (only one fighter per fight)
* Train hands-on: Show, Spar, Take Over (see §6)
* Set strategy bias (see §6.5)

### Step 3: Fight (Autonomous, from the Corner)
* Fighter fights on its own, round by round (boxing-style: damage carries over, KO anytime, judges decide if it goes the distance)
* Player coaches from the corner: limited Shouts during rounds, adjustments between rounds (see §6.6)

### Step 4: Outcome
* Win → money + reputation
* Loss → injury + lost entry fee

### Step 5: Analyze
* Replay system
* Tag mistakes
* Review Habit Report and "Learned From" tags
* Go back to the gym and adjust

---

# 3. 🧍 Fighter System

## Roster
* Start with 1 fighter
* Unlock up to 3 total
* Only **one** fighter is entered per fight — the roster lets the player rotate fighters (e.g., while one recovers from injury) and pick the best match-up for each opponent

## Fighter Data Model

### Core Stats
* Aggression (0–1)
* Defense (0–1)
* Reaction Speed (0–1)
* Fight IQ (derived from training)
* Stamina

### Hidden Traits (Emergent)
* Overcommits
* Hesitates
* Panics under pressure

## Archetypes (Initial Bias)

### Aggressive
* Faster attack learning
* Poor defensive learning
* Cheaper recruit cost (less stable)

### Defensive
* Strong block/dodge tendencies
* Slower offense learning
* More expensive recruit cost (more reliable)

## Pre-Training Behavior ("Rookie AI")
Before any player demonstrations are recorded, each fighter runs a rule-based fallback brain:
* If close → light attack
* If low HP → block more
* Random dodge chance

This acts as a **baseline fighter personality** and ensures the game is playable immediately before any ML data is collected.

## Early Habits Stick
Inspired by Black & White's creature: behaviors trained early and reinforced repeatedly become harder to change later. Older, heavily reinforced data keeps more weight in the dataset, so the player's first training choices shape the fighter's long-term personality.

## Health & Injury
* Post-fight damage persists
* Injury severity is set by how much HP the fighter ended the fight with and how many knockdowns they suffered (boxing-style rounds make this a direct link)
* Injury reduces performance
* Recovery costs money/time *(excluded from MVP)*

---

# 4. 🥊 Combat System (2D Pixel)

## Perspective
* Side-view 2D arena

## Fight Format — Boxing-Style Rounds
* Fights are a **fixed number of rounds**, scaled by tier:
  * Low tier: 3 rounds
  * Mid tier: 4 rounds
  * High tier: 5 rounds
* Each round: **60-second timer**
* **HP carries over between rounds** — damage taken in round 1 is still there in round 3
* Stamina recovers partially (not fully) between rounds, so an exhausting round has a cost later
* A **corner break** happens between rounds (see §6.6), where the corner can recover a little HP or stamina

> Why boxing-style: carried-over damage makes every exchange matter, gives the fight a story arc (an early lead can be lost, a hurt fighter has to survive), and ties naturally into the injury system — the damage a fighter ends the fight with decides how badly they're hurt.

## Win Condition
* **KO:** Opponent HP reaches 0 at any point → instant win, fight ends
* **Decision:** If all rounds finish with both fighters standing, the judges' scorecards decide (see Round Scoring)
* **Draw:** If the scorecards are tied → automatic rematch triggered (rematch is a new fight; damage from the drawn fight counts toward injury)

## Round Scoring (Judges)
* Each round is scored separately by 3 judges using a simplified **10-point must system**:
  * Round winner gets 10, loser gets 9
  * A **knockdown** in a round costs the downed fighter an extra point (10–8)
* A judge picks the round winner from, in order of weight:
  1. Clean hits landed
  2. Damage dealt
  3. Effective defense (blocks/dodges of real attacks)
  4. Ring control / aggression score
* Fight result on a decision: **Unanimous** (3–0), **Split** (2–1), or **Majority** (2–0 with one judge scoring it even)

## Knockdowns
* When HP drops below a knockdown threshold for the first time in a round (e.g., a big heavy hit at low HP), the fighter is knocked down instead of KO'd
* The downed fighter gets back up after a short count with a small HP buffer, costing them a point on the scorecard
* **3 knockdowns in one round = KO** (three-knockdown rule)
* *(Knockdowns can be simplified or cut for the MVP — see §13)*

## Fight Duration
* Average fight: 90–240 seconds (many fights end early by KO)
* Maximum: 5 rounds × 60s = 300 seconds (plus corner breaks)

## Hitbox / Collision Model — "Classic Fighter Lite"
* 2D rectangular hitboxes (NOT physics-based)
* Frame-based attack windows
* Simple per-frame collision checks
* Each attack has: **startup frames → active hit frames → recovery frames**

> Physics-based collision was rejected: it destabilizes ML training, makes behavior hard to debug, and creates unpredictable movement that breaks the learning loop.

## Moveset — The 8 Buttons

**Key principle:** every fighter has the same fixed moveset from day one. Training never creates or unlocks moves — it teaches **when** to use them. The fighter's brain "presses" the exact same 8 buttons the player uses (see §5.3), so whatever the player does with the controls is directly copyable.

Frame data at 60 fps (startup = visible wind-up; recovery = punishable cool-down). Values are starting points for tuning.

| Button | Move | Startup → Active → Recovery | Damage | Stamina | Role |
|---|---|---|---|---|---|
| Jab | Light attack | 4 → 2 → 8 | Low | Low | Fast poke, interrupts, starts combos |
| Heavy | Hook / cross | 12 → 3 → 20 | High | High | Big damage, can cause knockdowns, very punishable on whiff |
| Block | Guard (hold) | Instant | ~75% damage reduction | Each blocked hit drains stamina | Safe defense; **guard break** when stamina hits 0 |
| Dodge | Slip / sway | 8 invulnerable frames, 20 total | — | Medium | Avoid a hit, opens the Counter Window |
| Forward | Walk in | — | — | — | Close distance |
| Back | Walk out | — | — | Slow regen | Create space, recover |
| Dash | Burst in/out (double-tap direction) | 15 frames | — | Medium | Fast distance change |
| Idle | Stand ready | — | — | Fast regen | Wait, bait, recover |

### Combos
* Combos are **not separate moves**. Pressing Jab or Heavy during the previous hit's active/early-recovery frames (the cancel window) chains into the next attack.
* Max chain: 3 hits (e.g., Jab → Jab → Heavy). Heavy always ends a chain.
* The fighter learns combos the same way it learns everything else — from seeing the player chain them in the right situations.

### Counter Window
* After a **successful dodge** (an attack whiffs during the invulnerable frames), any attack started within ~0.25s (15 frames) deals **+50% damage**.
* Purpose: makes "dodge, then punish" a clearly teachable, clearly visible skill — the kind of moment that creates the "I taught it that" feeling.
* A small flash/sound cue on a Counter hit makes it readable in fights and replays.

### Special
* Signature move *(post-MVP)*

## Stamina
* Stamina gates blocking, dodging, dashing, and attacking
* Blocked hits drain stamina; at 0, the guard breaks and the fighter is briefly stunned
* Running out of stamina forces the fighter into a vulnerable state
* Idle and Back regenerate stamina; partial recovery between rounds

## Combat Depth
Focused on: **Timing, Spacing, Reaction** — NOT move complexity.

---

# 5. 🧠 ML System Design

## 5.1 Model Overview
Each fighter has an independent model:
* Input: game state vector
* Output: discrete action decision

## 5.2 State Representation (Input Vector)
Per decision step:
* Distance to opponent
* Relative velocity
* Opponent action (encoded)
* Own health %
* Opponent health %
* Time since last action
* Previous action
* Stamina
* Current round / rounds remaining / round time remaining
* Estimated scorecard lead (rounds up / down) — lets the fighter learn to protect a lead or chase a KO when behind

## 5.3 Action Space (Output)
The model outputs one of the **same 8 buttons** the player uses (see §4 Moveset):
* Idle
* Move forward / Move back
* Dash
* Jab (light attack)
* Heavy attack
* Block
* Dodge

### Decision Rate
* The model makes a decision roughly every **~0.1s (6 frames)**
* While the fighter is locked in an animation (startup/active/recovery), decisions are ignored — **except** during combo cancel windows and the Counter Window, where a new attack can be queued
* In Show/Take Over mode, the player's button press is recorded at the same rate alongside the current state, so player data and model output line up 1:1

## 5.4 Model Architecture — Small MLP (Neural Network)

**Architecture:**
* Input layer: state vector (10–20 values)
* Hidden layers: 2 layers, 32–64 neurons each
* Output: probability distribution over actions (softmax)
* Framework: **PyTorch**

> Decision trees were rejected as too shallow for behavior learning. The MLP handles continuous state inputs, is stable for behavior cloning, and runs efficiently via the Python backend.

## 5.5 Learning Methods

### Phase 1 — MVP: Behavior Cloning + Corrections
* Show-mode demonstrations recorded as state→action pairs
* Take Over corrections added to the same dataset (DAgger-style, see §5.6)
* Supervised learning on the combined dataset

### Phase 2 — Post-MVP: Reinforcement Learning
Reward signals:
* Damage dealt (+)
* Damage taken (-)
* Round won on the scorecard (+)
* Knockdown scored (+) / suffered (−)
* Successful dodge/block (+)
* Player feedback from Spar mode (Good/Bad presses) as an extra reward signal

> Why this matters: pure behavior cloning can never be better than the player's own demonstrations (which will be imperfect on touch controls). Feedback and RL are what let a fighter eventually **outgrow its trainer** — a natural late-game story beat.

## 5.6 Training Data Sources

| Source | Gym Mode | What's recorded | Research basis |
|---|---|---|---|
| Demonstration | Show | Player controls fighter → state/action pairs | Behavior cloning |
| Correction | Take Over | States where the AI was about to fail + the player's corrective actions | HG-DAgger (human-gated DAgger) |
| Live feedback | Spar | Good/Bad presses attached to the fighter's recent actions | TAMER (learning from human approval/disapproval) |
| Replay tags | Post-fight | Good / Bad / Mistake on action events | Dataset reweighting |

### Why Take Over is in the MVP
Behavior cloning alone suffers from **compounding errors**: once the fighter makes a mistake, it lands in situations the player never demonstrated, and it falls apart. Take Over fixes exactly this — the player corrects the fighter *in the situations the fighter actually gets itself into*.

### Data Volume Note
A 15-second free-play demo at one decision every ~100 ms is only ~150 samples. Drills and Take Over generate far more *useful* samples per minute than free play, so the UX should steer players toward them.

## 5.7 Model Update Cycle & Trigger
1. Data is queued during gym sessions and fights
2. **Trigger:** Model updates after every gym session end OR fight end (batch update — NOT real-time, for stability)
3. Store updated dataset per fighter (each sample tagged with its source session, for "Learned From" tracking)
4. Retrain model on new batch
5. Generate Habit Report (see §6.7)
6. Deploy updated policy

## 5.8 Action Weighting — Preventing Demo Abuse
Raw player input is not trusted blindly. Each training session tracks:
* Frequency of each action
* Context in which actions were taken

The ML system then:
* Downweights repetitive/spammed patterns
* Normalizes the behavior distribution

A **Diversity Reward** is applied: training data from sessions where the player uses multiple actions and reacts to opponent state changes is weighted more heavily.

## 5.9 Feedback Tagging — ML Effect

| Tag | ML Effect |
|-----|-----------|
| ✅ Good | Increases probability weight for that action in similar states |
| ❌ Bad | Decreases probability of that action in similar states |
| ⚠️ Mistake | Adds event to "avoid pattern" dataset |

Tags (post-fight replay tags and live Spar presses) modify **training dataset weights** — not instant rule changes. They feed into the next retrain cycle.

---

# 6. 🧪 The Gym — Training System (Core UX)

The gym is where the player gets real fighting action. It has three modes, all using the same KOF-style controls.

## 6.1 Show Mode — *You are the fighter*

### Flow
* Button: "Show"
* Player directly controls their fighter against a sim opponent
* Session lasts ~15–30s

### Drills
Instead of only free play, the player can pick a **Drill**: a set-up scenario repeated several times, e.g.:
* "Opponent dashes in from mid-range" × 10 reps
* "Opponent throws heavies when you're low on stamina" × 8 reps
* "Opponent backs into the corner" × 10 reps

Drills target the exact situations the fighter is weak in and produce much denser training data than free play.

### Data Captured
* State → action pairs (filtered through action weighting, §5.8)

## 6.2 Spar Mode — *You fight your fighter*

### Flow
* Button: "Spar"
* The **player controls the sparring opponent**; the player's fighter is driven by its AI
* Session lasts one round (60s)

### Why it exists
This is the most "game-like" mode: the player is genuinely fighting. It also lets the player **test what they taught** — if they trained the fighter to dodge heavy attacks, they throw heavies at it and see.

### Live Feedback
* Two buttons during Spar: **👍 Good** / **👎 Bad**
* A press applies to the fighter's last ~1 second of actions (credit assignment window)
* Feeds the dataset as weighted feedback (TAMER-style)

### Data Captured
* Fighter's state/action pairs + attached live feedback
* The player's own inputs (as the opponent) are NOT used to train the player's fighter

## 6.3 Take Over Mode — *Grab control when it messes up*

### Flow
* Button: "Take Over"
* The fighter spars on its own (AI-driven) against a sim opponent
* When the player sees a mistake coming, they **hold** the Take Over button to take control
* **Release** to hand control back to the AI
* Session lasts ~30–60s

### Data Captured
* Every state/action during player-controlled stretches, weighted higher than Show data (these are corrections in situations the AI actually reaches)
* The moment of takeover is logged as a soft "the AI was about to do the wrong thing here" signal

## 6.4 Training Sim Opponent

The sim opponent in Show and Take Over uses generated archetypes:
* Aggressive rusher
* Defensive counter
* Balanced fighter

Additionally, **preset arena fighters** are reused across real fights, providing consistency for learning and increasing difficulty over time.

## 6.5 Strategy Bias (Pre-Fight Tuning)

Strategy bias is a **pre-fight parameter** that temporarily shifts action probabilities. It does NOT permanently modify the fighter's learned behavior.

| Strategy | Effect |
|----------|--------|
| Aggressive Plan | +attack probability, −block probability |
| Defensive Plan | +block/dodge probability, −attack probability |
| Counter Style | Increases reaction-based action weighting |

Think of it as a **temporary personality overlay** applied on top of the fighter's trained model.

## 6.6 The Corner — Coaching During Real Fights

Pillar 1 holds: the player never controls the fighter in a real fight. Instead they coach from the corner.

### Shouts (during a round)
* Limited to **2 per round**
* Each Shout applies a short-lived strategy bias (~4 seconds) using the §6.5 system
* Examples: **"Press!"** (+attack), **"Back off!"** (+move back / block), **"Watch the heavy!"** (+dodge vs. heavy attacks)
* Shouts are NOT training data — they don't change the model

### Corner Break (between rounds)
* ~15-second screen between rounds
* Shows a quick 3-stat readout for the last round (e.g., hits landed, blocks, stamina) and the player's best guess at the scorecard ("You probably won that round")
* Player can switch the Strategy Bias for the next round
* Player picks **one** corner action:
  * **Patch Up** — recover a small amount of HP
  * **Catch Breath** — recover extra stamina
  * **Fire Them Up** — a temporary aggression boost for the first 15s of the next round
* Because damage carries over, the corner choice is a real decision: patch up a hurt fighter, or push for the knockout while ahead?

## 6.7 Visible Learning Features

### Habit Report (after every retrain)
* Before and after retraining, the model is run on a fixed set of test situations
* The differences are shown as plain-language changes, e.g. *"Now dodges heavy attacks 40% more often"* / *"Stopped dashing in when low on stamina"*

### "Learned From" Tags
* Every training sample remembers which gym session it came from
* In replays, when the fighter does something notable, a small tag shows its origin: *"Learned in Drill #3"* / *"From your Take Over on Day 4"*
* This is the feature that most directly creates the core feeling: *"That behavior came from how I trained them."*

### Brain View *(post-MVP)*
* Optional overlay showing action-probability bars above the fighter during Spar, Take Over and replays

## 6.8 Feedback Mode (Post-Fight Replay)

* Timeline of the fight, per **action event** (not frame-by-frame), split by round
* Events include: attack started, attack hit, dodge executed, block triggered, counter hit, guard break
* Click a moment → assign tag: **Good / Bad / Mistake**

## 6.9 Teaching the Moveset

### Gym Controls (mobile, landscape)
* **Left thumb:** ◀ Back / ▶ Forward (double-tap = Dash)
* **Right thumb:** 2×2 grid — Block, Dodge (top) / Jab, Heavy (bottom)
* Top HUD: both health bars, stamina, current mode badge, Drill name + rep counter, recording indicator

### What Each Move Teaches — and the Best Mode for It

| Move | What the player is really teaching | Best mode |
|---|---|---|
| Jab | When to poke vs. when to wait | Show (free play) |
| Heavy | Only throw it when the opponent is open — not into a block | Take Over (grab control on wild heavies) |
| Combos | Chain only when the first hit lands | Show |
| Block | Block when an attack is coming, not constantly (it drains stamina) | Spar (👎 when turtling, 👍 on a real block) |
| Dodge + Counter | Dodge heavies, then punish in the Counter Window | Drill: "Opponent throws heavies" |
| Movement / Dash | Spacing — stay just out of range, dash in when the opponent is recovering | Show + Take Over |
| Idle / Back | Recover stamina instead of panicking | Spar (👍 when it backs off at low stamina) |

### Example Teaching Loop
1. **Real fight:** fighter loses round 2 by throwing Heavy into the opponent's block repeatedly. Player tags those moments ❌ Bad in the replay.
2. **Gym — Show (Drill: "Opponent blocking"):** player does 10 reps — jabs until the bot drops guard, *then* Heavy.
3. **Gym — Take Over:** fighter spars alone; when it throws Heavy into a block, player holds Take Over, jabs instead, releases.
4. **Gym — Spar:** player controls the bot and keeps guard up; fighter jabs patiently → 👍.
5. **Retrain → Habit Report:** *"Throws Heavy into a block 60% less often."*
6. **Next real fight:** fighter waits, lands a Heavy on an opening — replay tags it *"Learned in Drill: Opponent blocking."*

---

# 7. 🖥️ UI / UX Design

## Platform: Mobile-First (Unity)
* Large touch-friendly buttons
* Minimal HUD
* Simplified layout

## 7.1 Main Menu
* Continue
* New Game
* Settings

## 7.2 Hub Screen (Core Screen)
* Fighter roster (cards)
* Current money
* Available fights
* Gym button

## 7.3 Fighter Card UI
* Name
* Record (W/L)
* Style / Archetype
* Health bar
* Traits

## 7.4 Fight Selection Screen
* Opponent style
* Risk level
* Entry fee
* Reward
* Pick **one** fighter to enter

## 7.5 Gym Screen
* Mode select: **Show / Spar / Take Over**
* Drill picker (Show mode)
* Arena view: fighter + sim opponent
* On-screen fight controls (touch)
* Spar: 👍 / 👎 live feedback buttons
* Take Over: large hold-to-control button
* End of session → retrain → Habit Report

## 7.6 Fight Screen
* Health bars (top) — carry over between rounds, round counter (e.g., "Round 2 / 3"), round timer
* Knockdown count animation
* Fighter names
* Small decision indicators (icons)
* Shout buttons (2 per round, greyed out when used)
* Minimal UI — focus on the fight

## 7.7 Corner Break Screen
* Last-round 3-stat readout + estimated scorecard
* Corner action picker (Patch Up / Catch Breath / Fire Them Up)
* Strategy Bias switcher
* Auto-continues after ~15s

## 7.8 Post-Fight Screen
* Result (KO / Decision — Unanimous / Split / Majority / Draw)
* Judges' scorecards, round by round
* Money gained/lost
* Replay timeline (action-event based) with "Learned From" tags
* Behavior tagging

---

# 8. 💸 Economy System

## Starting Setup
* Starting money: **$500**
* Fighter recruit #1: Free
* Fighter recruit #2: $3,000
* Fighter recruit #3: $8,000

## Fight Earnings

| Tier | Entry Fee Range | Reward Range |
|------|----------------|--------------|
| Low  | Low            | $200–$500    |
| Mid  | Medium         | $800–$2,000  |
| High | High           | $3,000–$8,000|

## Recovery Costs
* Minor injury: $100–$300
* Major injury: $500–$1,500

## Fighter Recruit Cost Logic
* Cost is randomized within archetype-based ranges
* Hidden trait modifiers affect price
* Aggressive fighters are cheaper (less stable)
* Defensive fighters are more expensive (more reliable)

## Broke State — "Debt Mode"
There is **no game over** if the player runs out of money. Instead:
* Locked to low-tier fights only
* Reduced training efficiency
* Reputation penalty applies

## Death Spiral Prevention
1. Low-tier fallback fights are **always** available
2. A **Comeback Bonus** slightly increases low-tier rewards during a losing streak
3. Cheap training options always remain accessible

---

# 9. 📈 Progression System

## Reputation
* Increases with wins
* Unlocks harder fights, better recruitable fighters, and new Drills
* Progression ladder: **Bronze → Elite → Underground King**

## End State
No hard ending. The long-term goal is:
> Build 3 elite fighters and dominate the high-tier underground circuit.

## Fighter Growth
* Improves via accumulated training data
* No traditional stat leveling — growth is entirely ML-driven

---

# 10. 🎨 Visual & Style Direction

## Reference
* **A darker take on classic King of Fighters pixel art** — detailed sprites, strong silhouettes, side-view arcade framing — relocated to an underground fighting arena. *(Full art direction to be detailed separately.)*

## Art Style
* 2D pixel art
* Minimal frames (readable animations)

## Animation Principles
* Clear anticipation frames
* Fast readability
* Distinct silhouettes

## Environment
* Dark underground arenas
* Neon accents
* Small crowds

---

# 11. 🧱 Technical Architecture

## Engine
* Unity (2D) — Mobile-first build

## ML Bridge — Option B: Python Backend (FastAPI)

### Unity (Client)
* Renders game
* Sends game state per decision step
* Receives action decision

### Python Server (FastAPI)
* Model inference (PyTorch MLP)
* Training updates (batch, post-session)
* Dataset storage per fighter
* Habit Report generation

### Communication
* WebSockets (real-time during fights, Spar and Take Over)

## Core Unity Scripts

| Script | Responsibility |
|--------|---------------|
| FighterController.cs | State machine, executes actions |
| AIController.cs | Sends state to ML server, receives action |
| ControlArbiter.cs | Decides who drives the fighter each frame (AI or player) — handles Take Over hand-off |
| TrainingRecorder.cs | Records Show, Spar and Take Over sessions, tags samples with source + session ID |
| FeedbackInput.cs | Live 👍/👎 presses and credit-assignment window |
| RoundManager.cs | Rounds, timers, HP carry-over, knockdowns, corner break |
| JudgeScorer.cs | Per-round scoring (10-point must), decision result |
| CornerController.cs | Shouts and between-round bias changes |
| GameManager.cs | Controls game loop |
| UIManager.cs | Handles screen transitions |

## Data Storage
* JSON per fighter dataset (e.g., `fighter_1_data.json`)
* Each sample stores: state, action, source (show / takeover / spar / replay-tag), session ID, weight
* Stored locally, synced to Python server

---

# 12. 🧠 Data Flow

## During Show / Take Over
Player input → action weighting filter → stored as state/action dataset (with source + session ID)

## During Spar
Fighter AI acts → player presses 👍/👎 → feedback attached to recent fighter actions → stored

## During Real Fight
Game state → WebSocket → Python ML server → action (+ any active Shout bias) → Unity animation

## After Session / Fight
Replay → player tags events → dataset weights updated → retrain → Habit Report → deploy

---

# 13. 🚀 MVP Scope (2–3 Weeks)

## Goal
One fighter. One loop. Visible learning. Real fighting action in the gym.

## Included in MVP

### Core
* 1 fighter
* 1 opponent type (rule-based aggressive archetype)
* Combat system with **boxing-style rounds**: 3 × 60s rounds, HP carries over, KO anytime, simple judge decision (single scorer, round winner by damage dealt) if no KO
* No knockdowns in MVP (HP 0 = KO)
* Gym: **Show** (free play), **Spar** (player fights their fighter, 👍/👎 feedback), **Take Over** (basic hold-to-control)
* Simple behavior cloning + Take Over corrections (MLP via Python backend)
* Rookie AI fallback before training data exists

### UI
* Basic hub screen
* Gym screen (3 modes)
* Fight screen with round counter
* Simple corner break (Strategy Bias switch + Patch Up / Catch Breath choice)
* Post-fight summary
* Basic Habit Report (text list of top changes)

### ML
* State → action mapping
* Post-session batch retraining
* Source + session tagging on every sample
* No RL yet

### Economy
* Basic money system ($500 start)
* 1 fight tier

## Excluded from MVP
* Multiple fighters
* Reinforcement learning
* Drills (Show mode is free play only in MVP)
* Knockdowns, 3-judge scorecards, split/majority decisions, Fire Them Up
* Variable round counts by tier
* Shouts
* "Learned From" tags in replays
* Brain View
* Injury system
* Advanced UI polish
* Multiple arenas

---

# 14. 📌 Build Execution Order

1. Basic fighter movement + the 8-button moveset (frame data, combos, Counter Window, stamina)
2. Round system (timer, HP carry-over, KO, simple decision scoring)
3. Simple rule-based AI opponent
4. Show mode recording
5. Python ML server + basic MLP
6. Unity ↔ Python WebSocket connection
7. Spar mode (player controls opponent, fighter AI, 👍/👎)
8. Take Over mode (ControlArbiter hand-off)
9. Habit Report
10. UI screens (hub, gym, fight, corner break, post-fight)
11. Fight loop + money system

---

# 15. 🧠 Final Note

This project succeeds if:

> The player can *see* their fighter improve and say:
> "That behavior came from how I trained them."

If that feeling exists, everything else is a bonus.

---

END OF DOCUMENT
