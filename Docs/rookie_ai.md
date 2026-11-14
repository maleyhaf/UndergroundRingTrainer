# 🥊 Rookie AI — Design & Behavior Reference

Companion to the PDD (`pdd_updated.md`, §3 "Pre-Training Behavior"). Describes the rule-based fallback brain as implemented in the Milestone 0 greybox, **updated for 2D ring movement** (circling, ropes, corners).

* **Code:** `Assets/Scripts/Input/RookieAI.cs`
* **Type:** Hand-written rules + randomness. **No machine learning.**
* **Implements:** `IInputSource`, so it presses the same 11 buttons as the player and, later, the ML brain

---

# 1. 🎯 Purpose

The Rookie AI is the brain a fighter uses **before any training data exists**. It does three jobs:

1. **Makes the game playable from minute one.** A brand-new fighter already fights back.
2. **Is the baseline personality.** The ML fighter starts as this Rookie and gradually turns into a copy of the player's training.
3. **Is the greybox opponent.** In Milestone 0 it controls the grey fighter, and it can also control the player's side (Tab = AI vs AI).

PDD §3 asks for three behaviors, and all three are present, plus basic ring sense:

| PDD rule | Implementation |
|---|---|
| If close → light attack | In Jab range it jabs 40% of the time (plus 10% Heavy) |
| If low HP → block more | Block chance under threat rises from 30% to 65% below 35% HP |
| Random dodge chance | 12% chance to dodge when the opponent winds up in range |
| *Ring sense (added)* | Circles while stalking, escapes the ropes toward the centre, cuts off a trapped opponent |

---

# 2. ⏱️ How It Decides

* **Decision rate:** once every `config.decisionInterval` ticks (default **6 ticks = 0.1s**), the same rate the ML brain will use.
* **Between decisions** it keeps holding the button it chose.
* **One random roll per decision.** A single number from 0 to 1 is rolled, and every rule checks it against its own threshold. Because the rules share the roll, they interact (see §5 for the real odds).
* **Sticky circling:** it remembers which way it was circling and only switches direction 15% of the time, so it circles smoothly instead of jittering left-right.
* **Deterministic:** randomness comes from a seeded `System.Random`, so the same seed and the same fight produce the same choices.

## What It Looks At

| Input | Definition |
|---|---|
| `gap` | Space between the two bodies (distance between centres minus body width) |
| `inJabRange` | `gap ≤ jab.reach` (0.9 units) |
| `lowHp` | Own HP below **35%** of max |
| `lowStamina` | Own stamina below **15** |
| `oppWindingUp` | Opponent is in the **startup** frames of an attack |
| `threatened` | In Jab range **and** the opponent is in startup **or active** frames |
| `trapped` | In a corner, **or** less than **1.0 unit** of room behind it (straight away from the opponent) before the ropes |
| `oppTrapped` | The same test for the opponent: cornered, or ropes right behind them |

---

# 3. 📋 Rules (Checked in Order — First Match Wins)

| # | Condition | Action | Roll threshold | Debug label |
|---|---|---|---|---|
| 1 | Opponent winding up, in range, stamina ≥ dodge cost (8) | **Dodge** | roll < 0.12 | "random dodge" |
| 2 | Threatened | **Block** | roll < 0.30 (normal) / < 0.65 (low HP) | "threatened, block" / "low HP, block" |
| 3 | Stamina < 15 | **Back**, or **circle toward centre** if trapped | always | "low stamina" / "low stamina, slip off the ropes" |
| 4a | In Jab range | **Heavy** | roll < 0.10 | "close, heavy" |
| 4b | In Jab range | **Jab** | roll < 0.50 | "close, jab" |
| 4c | In Jab range | **Block** (guard up) | roll < 0.70 | "close, guard up" |
| 4d | In Jab range | **Circle** (sticky direction), or **circle toward centre** if trapped | roll < 0.82 | "close, circle" / "close, escape ropes" |
| 4e | In Jab range | **Back** (step back), or **circle toward centre** if trapped | roll < 0.90 | "close, step back" / "close, escape ropes" |
| 4f | In Jab range | **Idle** (wait) | otherwise | "close, wait" |
| 5 | Out of range, trapped | **Circle toward centre** | roll < 0.70 | "escape the ropes" |
| 6a | Out of range, opponent trapped, gap > 2 | **Dash In** | roll < 0.08 | "cut off, dash in" |
| 6b | Out of range, opponent trapped | **Forward** | otherwise | "cut off the ring" |
| 7a | Out of range, gap > 2 | **Dash In** | roll < 0.05 | "far, dash in" |
| 7b | Out of range | **Forward** | roll < 0.60 | "close the distance" |
| 7c | Out of range | **Circle** (sticky direction) | roll < 0.92 | "circle, look for an opening" |
| 7d | Out of range | **Idle** | otherwise | "wait" |

**"Circle toward centre"** picks Circle Left or Circle Right, whichever side the ring centre is on (relative to the opponent), so circling carries it off the ropes.

The debug label is shown above the fighter with **F1** on, e.g. `AI: CircleLeft (escape the ropes)`.

---

# 4. 🛋️ Corner Break Choice

Between rounds:

* HP below **50%** → **Patch Up** (+12 HP)
* Otherwise → **Catch Breath** (full stamina)

---

# 5. 🎲 Effective Odds (Because Rules Share One Roll)

Checking the same roll against each threshold in order means the real chance of each action depends on the situation. Assumes enough stamina to dodge and stamina ≥ 15.

## Close Range (in Jab range, not trapped)

| Situation | Dodge | Block | Jab | Heavy | Circle | Back | Idle |
|---|---|---|---|---|---|---|---|
| Opponent neutral | — | 20% | 40% | 10% | 12% | 8% | 10% |
| Opponent winding up | 12% | 38% | 20% | 0% | 12% | 8% | 10% |
| Opponent winding up, **low HP** | 12% | 58% | 0% | 0% | 12% | 8% | 10% |
| Opponent's hit active | — | 50% | 20% | 0% | 12% | 8% | 10% |
| Opponent's hit active, **low HP** | — | 70% | 0% | 0% | 12% | 8% | 10% |

**Trapped at close range:** the Circle and Back slices (20% together) both become "circle toward centre"; everything else is unchanged.

## Out of Range

| Situation | Forward | Circle | Dash In | Idle |
|---|---|---|---|---|
| Normal stalking (gap > 2) | 55% | 32% | 5% | 8% |
| Normal stalking (gap ≤ 2) | 60% | 32% | — | 8% |
| Opponent trapped (gap > 2) | 92% | — | 8% | — |
| **Itself trapped** | — | 70% toward centre + 22% sticky | — | 8% |

## Measured in Greybox Fights (Rookie vs Rookie)
* Button mix: ~31% Forward, ~23% Back, **~17% circling**, ~12% Jab, ~8% Block, ~6% Idle, small amounts of Heavy, Dash and Dodge
* Spends **~20% of the time on the ropes**
* Fights last **~1 minute** on average and end by KO
* Almost never lands an **Angle Hit**, since it doesn't deliberately circle around a blocker (see §7)

**Takeaways:**
* The Rookie **never throws a Heavy while you're attacking**, because the low rolls that would pick Heavy are already used up by Dodge/Block.
* At **low HP** it stops jabbing back into your attacks entirely and turtles, which matches the "panics under pressure" hidden trait in PDD §3.
* When **you're on the ropes**, it stops circling and walks straight at you: it cuts off the ring.

---

# 6. ✨ Why It Feels Smarter Than It Is

* **The fight rules do some of its thinking.** Holding Jab for 0.1s means that whenever a jab lands, the next one chains automatically (combos only chain on hit). It "knows" to combo only on a hit without any rule for it.
* **It guards in advance instead of reacting.** A Jab's 4-frame startup is faster than its 0.1s reaction time, so blocking on reaction can't work. Raising its guard 20% of the time at close range is what makes it feel defensive.
* **Sticky circling looks deliberate.** Holding one circling direction for a while reads as "working an angle", even though it's random.
* **Ring escapes look like ring IQ.** Circling toward the centre when trapped, and walking straight in when you're trapped, is exactly what a real boxer does.
* **Randomness hides the pattern**, and **it manages stamina**, backing off (or slipping sideways off the ropes) when tired.

---

# 7. 🩹 Known Weaknesses (Exploitable — Useful for Playtesting)

* **Weak dodging:** only a 12% dodge chance, so a Heavy thrown from just outside Jab range usually lands.
* **Turtles in place:** when it blocks, it doesn't step away. **Circle around it for about a second** and you'll be outside its guard (watch for "OUTSIDE THEIR GUARD" in the F1 overlay), then land an Angle Hit.
* **Doesn't hunt angles itself:** its circling is random, so it rarely uses the guard arc against you.
* **Escapes are predictable:** when trapped, it almost always circles toward the centre, so you can cut that side off.
* **Never punishes on purpose:** after you whiff a Heavy, it only counters if its roll happens to say Jab. It doesn't use the Counter Window deliberately.
* **No round or scorecard awareness:** it fights the same way whether it's winning or losing on the cards, and doesn't play for ring control in close rounds.

---

# 8. 🎛️ Tuning Knobs

Thresholds in `RookieAI.cs`:

| Knob | Current | Effect |
|---|---|---|
| Low-HP threshold | 35% | When it starts turtling |
| Low-stamina threshold | 15 | When it backs off |
| Dodge chance | 0.12 | How often it dodges windups |
| Block under threat | 0.30 / 0.65 | Defensiveness (normal / low HP) |
| Close-range split | 0.10 / 0.50 / 0.70 / 0.82 / 0.90 | Heavy / Jab / Guard / Circle / Back / Wait mix |
| `trappedSpace` | 1.0 units | How close the ropes must be behind it to feel trapped |
| Escape chance when trapped (out of range) | 0.70 | How urgently it leaves the ropes |
| Stalking split | 0.60 / 0.92 | Forward / Circle / Wait mix |
| Circle direction switch | 0.15 per decision | Smooth vs. erratic circling |
| Dash-in chance | 0.05 (0.08 when cutting off) | How often it rushes in |
| Corner Patch Up threshold | 50% HP | When it heals vs. recovers stamina |

Shared knobs in `FightConfig` (Inspector-editable at runtime) that strongly affect how the Rookie plays: `decisionInterval` (reaction speed), `turnRateBlock` and `blockArcDegrees` (how exposed its turtling is), `circleSpeed`, `ropeZone`, plus the move frame data.

---

# 9. 📝 Design Notes & Open Questions

* **"Beat the Rookie" check.** The trained fighter starts as this Rookie and has to visibly outgrow it, or players won't feel their training mattered (PDD §15). Once the ML brain exists, measure trained-vs-Rookie win rates and ring metrics (time on the ropes, centre control). If training doesn't clearly win, weaken the Rookie.
* **Reuse as sparring archetypes (PDD §6.4).** Moving the thresholds into a serializable profile would give the gym's three sim opponents almost for free:
  * **Aggressive rusher:** high Jab/Heavy, low guard, more dash-ins, always cuts off the ring
  * **Defensive counter:** high guard and dodge, deliberate counters in the Counter Window, circles away from the ropes early
  * **Balanced fighter:** the current Rookie
* **Ring-position Drills.** Two simple Rookie variants would make good Drill opponents: one that runs (always circles away, for "Cut off the ring") and one that pins you (always walks forward, for "Escape the corner").
* **Archetype bias (PDD §3).** Aggressive and Defensive recruits could start from different Rookie profiles, giving each new fighter a distinct baseline personality before any training.
* **Difficulty scaling.** Lowering `decisionInterval` (faster reactions), raising dodge/punish odds, and letting it deliberately circle a blocker are simple ways to make tougher preset arena opponents.
