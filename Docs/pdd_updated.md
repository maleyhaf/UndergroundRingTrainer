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

## Platform
**PC first** (Windows), played with **keyboard or controller**. A mobile port may come later, but it is not a goal for now (see §7 and §11 for why).

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

## Perspective — Square Ring, Two Views
* The fight takes place on a **square boxing ring** and is simulated in 2D **from above**: fighters move toward, away from, *and around* each other
* **Main view:** KOF-style **side view with depth**: fighters further back in the ring stand higher on screen and are drawn behind
* **Minimap:** a small **top-down view of the ring** (bottom-right) showing both fighters, which way their guard faces, the ropes and corners, and live ring-control readouts *(in the MVP; see §6.10 and §15.1 for the full design rationale)*

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
* Average fight: 90–240 seconds (many fights end early by KO). *Greybox measurement: Rookie AI vs Rookie AI averages ~1 minute; trained fighters are expected to fight longer.*
* Maximum: 5 rounds × 60s = 300 seconds (plus corner breaks)

## Hitbox / Collision Model — "Classic Fighter Lite"
* Simple 2D shapes, seen from above (NOT physics-based): bodies are circles; a punch is a line from the front of the body out to its reach
* Frame-based attack windows
* Simple per-frame collision checks
* Each attack has: **startup frames → active hit frames → recovery frames**

> Physics-based collision was rejected: it destabilizes ML training, makes behavior hard to debug, and creates unpredictable movement that breaks the learning loop.

## Moveset — The Buttons

**Key principle:** every fighter has the same fixed moveset from day one. Training never creates or unlocks moves — it teaches **when** to use them. The fighter's brain "presses" the exact same buttons the player uses (11 in total, see §5.3), so whatever the player does with the controls is directly copyable.

**All movement is relative to the opponent**, not the screen. The player presses screen directions; the game translates them into the buttons below depending on where the opponent stands. That's also what gets recorded, so the fighter learns "circle left", not "press W".

Frame data at 60 fps (startup = visible wind-up; recovery = punishable cool-down). Values are starting points for tuning.

| Button | Move | Startup → Active → Recovery | Damage | Stamina | Role |
|---|---|---|---|---|---|
| Jab | Light attack | 4 → 2 → 8 | Low | Low | Fast poke, interrupts, starts combos |
| Heavy | Hook / cross | 12 → 3 → 20 | High | High | Big damage, can cause knockdowns, very punishable on whiff |
| Block | Guard (hold) | Instant | ~75% damage reduction | Each blocked hit drains stamina | Safe defense; **guard break** when stamina hits 0 |
| Dodge | Slip / sway | 8 invulnerable frames, 20 total | — | Medium | Avoid a hit, opens the Counter Window |
| Forward | Walk toward the opponent | — | — | — | Close distance, cut off the ring |
| Back | Walk away from the opponent | — | — | Slow regen | Create space, recover |
| Circle Left / Circle Right | Sidestep around the opponent, keeping the same distance | — | — | — | Find angles, escape the ropes, stay off the line of fire |
| Dash In / Dash Out | Burst toward / away (double-tap a direction) | 15 frames | — | Medium | Fast distance change |
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

## Ring & Movement (2D)

### The Ring
* Square ring, **8 × 8 units** (fighters are 0.8 units wide), with ropes on all four sides; the ropes stop movement
* **On the ropes:** within 0.7 units of a rope. **In a corner:** near two ropes at once
* Fighters start each round on opposite sides of the centre, 3 units apart

### Circling, Facing & Angles
* **Circling** keeps the same distance to the opponent, moving around them like real boxers
* Fighters **turn to face their opponent automatically, at a limited speed**:
  * Free (standing, walking): fast turn (8°/tick)
  * **Blocking: slow turn (1°/tick)**
  * Dodging: medium turn
  * Attacking, dashing or stunned: no turning. Punches are aimed where the fighter faced when the punch started
* **Block covers ±70°** in front of the fighter. A hit from outside that arc goes **around the guard** and lands for full damage (**Angle Hit**, flashed "ANGLE!")
* Result: circling around someone who is turtling gets you outside their guard in about **1 second**, which is a teachable, visible skill. Stepping off the line during an opponent's wind-up can also make their punch miss

### Ring Metrics (tracked per round)
| Metric | Meaning | Used by |
|---|---|---|
| Centre control time | Time spent closer to the ring centre than the opponent (by 0.3+ units) | Judges (ring control), corner break, minimap |
| Time on the ropes | Time within the rope zone | Corner break, minimap, ML input |
| Time in a corner | Time near two ropes | Corner break, minimap, ML input |
| Angle hits | Hits that got around a block from the side | Corner break, replays, Habit Report |

## Combat Depth
Focused on: **Timing, Spacing, Angles, Reaction** — NOT move complexity.

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
* **Ring & angle features (2D ring):**
  * Sideways (circling) velocity relative to the opponent
  * How far my guard is turned away from the opponent, how far I am off *their* guard, and whether their block would cover my punch
  * Distance to the ropes (me and opponent), on-the-ropes and in-a-corner flags (me and opponent)
  * Room behind me / room behind the opponent (can I back up? can I trap them?)
  * Distance to the ring centre and which way the centre is (forward / to my left)

All ring features are **relative to the opponent** (forward = toward them), so a fighter learns "my back is to the ropes" rather than absolute ring coordinates. Current encoding (v2): **51 numbers** per decision, including one-hot opponent situation (12) and previous action (11).

## 5.3 Action Space (Output)
The model outputs one of the **same 11 buttons** the player uses (see §4 Moveset):
* Idle
* Forward / Back
* Circle Left / Circle Right
* Dash In / Dash Out
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
* Input layer: state vector (~50 values, see §5.2)
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

> Why this matters: pure behavior cloning can never be better than the player's own demonstrations (which will always be a bit imperfect). Feedback and RL are what let a fighter eventually **outgrow its trainer** — a natural late-game story beat.

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
* "Escape the corner" × 8 reps (start trapped, get back to the centre)
* "Cut off the ring" × 8 reps (opponent keeps circling away)

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

### Gym Controls (PC — keyboard or controller)

| Action | Keyboard | Controller |
|---|---|---|
| Move around the ring | W A S D (or arrow keys) | Left stick or D-pad |
| Dash | Double-tap direction, or Shift + direction | Double-tap direction, or RB / R1 + direction |
| Minimap size (normal / large / off) | M | View / Select |
| Jab | J | X / Square |
| Heavy | K | Y / Triangle |
| Block (hold) | L | LB / L1 |
| Dodge | Space | A / Cross |
| Take Over (hold) | F | RT / R2 |
| Spar feedback 👎 / 👍 | Q / E | LT / L2 and RT / R2 (Spar only) |

* Up/down move toward the back/front of the ring. The game turns the pressed direction into Forward / Back / Circle Left / Circle Right depending on where the opponent is (whichever matches the pressed direction best).
* Exact bindings are starting points, finalized in playtesting; all keys are rebindable in Settings.
* Top HUD: both health bars, stamina, current mode badge, Drill name + rep counter, recording indicator
* The greybox (Milestone 0) already uses the keyboard column above.

### What Each Move Teaches — and the Best Mode for It

| Move | What the player is really teaching | Best mode |
|---|---|---|
| Jab | When to poke vs. when to wait | Show (free play) |
| Heavy | Only throw it when the opponent is open — not into a block | Take Over (grab control on wild heavies) |
| Combos | Chain only when the first hit lands | Show |
| Block | Block when an attack is coming, not constantly (it drains stamina) | Spar (👎 when turtling, 👍 on a real block) |
| Dodge + Counter | Dodge heavies, then punish in the Counter Window | Drill: "Opponent throws heavies" |
| Movement / Dash | Spacing — stay just out of range, dash in when the opponent is recovering | Show + Take Over |
| Circling | Get outside a turtling opponent's guard; step off the line of a heavy | Show + Spar (👍 on an Angle Hit) |
| Ring position | Don't get trapped on the ropes or in a corner; cut off an opponent who runs | Drills: "Escape the corner", "Cut off the ring" |
| Idle / Back | Recover stamina instead of panicking | Spar (👍 when it backs off at low stamina) |

## 6.10 The Ring Minimap

A small **top-down view of the ring**, shown in fights, the gym and replays (bottom-right corner; toggle normal / large / hidden).

### What It Shows
* The ring: ropes, corner posts, centre mark, and the shaded rope zone
* Both fighters as dots, with a short line showing **which way their guard faces** (brighter while blocking)
* The attacking fighter's dot flashes red on active punch frames
* Live readouts under the map: each fighter's ring status (**open / ropes / CORNER**) and **centre control %** for the round

### Why It Matters
* The side view can't show depth clearly. When fighters circle, the minimap makes it obvious who has the angle and who is trapped
* It makes the ring metrics *visible*, which supports the core goal: the player can see their fighter learned to escape the corner or cut off the ring
* Later: the same view powers ring-position review in replays and the Habit Report (e.g. *"Spends 40% less time on the ropes"*)

## 6.11 Teaching Example

### Example Teaching Loop
1. **Real fight:** fighter loses round 2 by throwing Heavy into the opponent's block repeatedly. Player tags those moments ❌ Bad in the replay.
2. **Gym — Show (Drill: "Opponent blocking"):** player does 10 reps — jabs until the bot drops guard, *then* Heavy.
3. **Gym — Take Over:** fighter spars alone; when it throws Heavy into a block, player holds Take Over, jabs instead, releases.
4. **Gym — Spar:** player controls the bot and keeps guard up; fighter jabs patiently → 👍.
5. **Retrain → Habit Report:** *"Throws Heavy into a block 60% less often."*
6. **Next real fight:** fighter waits, lands a Heavy on an opening — replay tags it *"Learned in Drill: Opponent blocking."*

---

# 7. 🖥️ UI / UX Design

## Platform: PC First (Unity)
* Windows first; 16:9 layout, scales to common resolutions
* **Keyboard or controller** for fighting; menus work with mouse, keyboard, or controller
* Minimal HUD, clear readable layout
* On-screen button prompts switch automatically between keyboard and controller icons

> **Why PC first (decided Nov 2026):**
> 1. **Teaching by playing needs precise input.** Show mode is the heart of the game, and the fighter copies how the player plays. Keyboard and controller give cleaner demonstrations than touch, so fighters learn better.
> 2. **The ML brain can run locally.** On PC, the Python ML server runs on the same machine as the game, with no internet, hosting costs or lag (see §11).
> 3. **Fighting-game audience.** Players of this genre mostly play on PC and console with keyboards and controllers.
>
> A mobile port stays possible later: the core fight code has no platform dependencies, and the brain can be moved inside the game (see §11, "Future: Mobile Port").

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
* Arena view: fighter + sim opponent, with the ring minimap
* Keyboard / controller fight controls, with on-screen button prompts
* Spar: 👍 / 👎 live feedback buttons
* Take Over: hold-to-control key / trigger, with a clear "YOU HAVE CONTROL" indicator
* End of session → retrain → Habit Report

## 7.6 Fight Screen
* Health bars (top) — carry over between rounds, round counter (e.g., "Round 2 / 3"), round timer
* **Ring minimap** (bottom-right): top-down ring, both fighters and their guard direction, ropes/corner status, centre control % (see §6.10)
* Knockdown count animation
* Fighter names
* Small decision indicators (icons)
* Shout buttons (2 per round, greyed out when used)
* Minimal UI — focus on the fight

## 7.7 Corner Break Screen
* Last-round readout per fighter: hits (and how many came from angles), blocks, dodges, **centre-control time, time on the ropes, time in a corner** + estimated scorecard (noting when ring control decided the round)
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
* Unity 6 (2D, URP) — **PC (Windows) build first**
* New Input System (keyboard + controller from the same action bindings)

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

### Running Locally on PC
* The Python server runs **on the player's own machine**, started automatically by the game and connected over `localhost`
* No internet connection, no server hosting costs, and no network lag
* Packaging (shipping Python + PyTorch with the game) to be solved before release; during development it runs from a terminal

### Future: Mobile Port (not planned yet)
* Phones can't run the Python server alongside the game, so a mobile version would run the trained model **inside Unity** (Unity's Inference Engine, formerly Sentis), exported from PyTorch via ONNX
* Training could stay on PC or a server; only the brain's decisions need to run on the phone
* Touch controls would need their own design (see the earlier landscape thumb layout idea: movement on the left, a 2×2 action grid on the right)

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
* Combat system with **boxing-style rounds**: 3 × 60s rounds, HP carries over, KO anytime, simple judge decision (single scorer: round winner by damage dealt; if damage is even, **ring control** breaks the tie) if no KO
* **2D ring movement**: square ring with ropes and corners, circling, turn rates, guard arc and Angle Hits, ring metrics (centre control, ropes, corners)
* No knockdowns in MVP (HP 0 = KO)
* Gym: **Show** (free play), **Spar** (player fights their fighter, 👍/👎 feedback), **Take Over** (basic hold-to-control)
* Simple behavior cloning + Take Over corrections (MLP via Python backend)
* Rookie AI fallback before training data exists

### UI
* Basic hub screen
* Gym screen (3 modes)
* Fight screen with round counter and **ring minimap** (basic, §6.10)
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
* Ring polish beyond the basics: real depth-sorted sprites with turn animations, minimap ring review in replays, ring-control lines in the Habit Report, "power hand" side bias

---

# 14. 📌 Build Execution Order

1. Basic fighter movement + the moveset (frame data, combos, Counter Window, stamina)
2. Round system (timer, HP carry-over, KO, simple decision scoring)
3. 2D ring movement (circling, turning, guard arc, ropes/corners, ring metrics) + ring minimap
4. Simple rule-based AI opponent (with basic ring sense)
5. Show mode recording
6. Python ML server + basic MLP
7. Unity ↔ Python WebSocket connection
8. Spar mode (player controls opponent, fighter AI, 👍/👎)
9. Take Over mode (ControlArbiter hand-off)
10. Habit Report
11. UI screens (hub, gym, fight, corner break, post-fight)
12. Fight loop + money system

*Status (Nov 2026): steps 1–5 are built in the Milestone 0 greybox.*

---

# 15. 🔭 Future Features & Design Notes

## 15.1 Top-Down Ring View — Circling & Ring Generalship

> **Status (Nov 2026): decided and pulled into the MVP.** Option B below was chosen: the fight is simulated in 2D, shown in the side view with depth, plus a top-down ring minimap. A basic version is built in the greybox (see §4 "Ring & Movement", §6.10). This section keeps the original reasoning and the later polish.

### Why
A big part of real boxing happens before a punch is thrown: circling the opponent, watching their habits, cutting off the ring, trapping them on the ropes or in a corner, and stepping off-line to create angles. The side view only allows moving forward and back. A **top-down view of the ring** keeps the game 2D while adding that second axis of movement.

### What It Adds
* **A real ring:** a square ring with ropes and four corners replaces the left/right walls.
* **Circling:** fighters can move around each other, not just toward and away. They still always face their opponent.
* **Ring generalship:** cornering an opponent, escaping the ropes, cutting off the ring when the opponent runs, and controlling the centre.
* **Angles:** punches land in a forward cone, so stepping to the side and then punching can catch an opponent who is only defending straight ahead.
* **Judging:** "Ring control" (already a judging criterion in §4) becomes measurable: time spent controlling the centre vs. time spent trapped on the ropes.

### How It Fits the Trainer Loop
It adds a whole new layer of habits to teach, all through the existing gym modes:
* *"Circle away from his power hand."*
* *"Don't get trapped in the corner."*
* *"Cut off the ring when he keeps running."*
* New Drills, e.g. "Escape the corner" and "Cut off the ring"
* New Habit Report lines, e.g. *"Spends 40% less time on the ropes"*

### What Changes Technically
* **Fight sim:** positions become 2D (x, y). The opponent's relative position becomes distance + angle, and hit detection uses 2D shapes (a cone or arc in front of the fighter) instead of a line.
* **Action space:** adds **Circle Left** and **Circle Right** (sidestep around the opponent while facing them): 9 → 11 buttons. Forward / Back / Dash stay relative to the opponent, as they are now.
* **ML state vector:** adds own distance to the ropes and the nearest corner, the opponent's distance to the ropes, the angle around the opponent, and sideways velocity.
* **Training data:** recordings made before the 2D ring (version 1) are not compatible with the new inputs and buttons. The recorder now writes **version 2**, and old v1 files should be discarded.
* **What already helps:** the fight logic is plain C# with no Unity dependency, every action is already relative to the opponent, and the sim is deterministic. Moving from 1D to 2D is a contained change to the core, not a rewrite of the game.

### Presentation — Decided: Option B
How the top-down view sits alongside the KOF-style side view has a big effect on the art workload. **Option B was chosen** (Nov 2026):

| Option | Description | Trade-off |
|---|---|---|
| A. Top-down only | The whole fight is shown from above | Loses the KOF side-view look the art direction is built around |
| B. Two views of one fight | The fight is simulated in 2D; the main screen stays side-view, plus a top-down ring view (toggle or mini-map) showing positions | Keeps the KOF look; top-down art can be simple tokens. The side view must handle fighters moving "into" the screen |
| C. Side-view camera that rotates | The fight is simulated top-down; the camera always stays side-on to the two fighters (like modern 3D boxing games) | Most authentic feel, but effectively 2.5D, with the heaviest art and animation cost |
| D. Top-down for tactics only | Side view for fights; top-down used in the gym and replays to teach and review positioning | Cheapest, but circling isn't part of the live fight |

### Scope & Timing
* **In the MVP (basic):** 2D ring sim, circling, turning, guard arc + Angle Hits, ropes/corners, ring metrics, ring-control tiebreak in judging, minimap, Rookie AI ring sense.
* **Later (polish):** pixel-art sprites that handle depth well (facing toward / away from the camera, turn animations), minimap ring review in replays, ring-control lines in the Habit Report, ring-position Drills, "power hand" side bias, ring control in 3-judge scoring.
* **Sprite implication:** because fighters can face into and out of the screen, each fighter needs at least side-facing plus three-quarter (toward / away) poses. Plan this before full sprite production.

---

# 16. 🧠 Final Note

This project succeeds if:

> The player can *see* their fighter improve and say:
> "That behavior came from how I trained them."

If that feeling exists, everything else is a bonus.

---

END OF DOCUMENT
