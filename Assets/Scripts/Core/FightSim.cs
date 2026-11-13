using System;
using System.Collections.Generic;

namespace FightTrainer.Core
{
    /// <summary>
    /// The fight itself, advanced one fixed tick (1/60s) at a time. Deterministic and Unity-free:
    /// the same inputs always produce the same fight, which keeps frame data exact and ML data clean.
    ///
    /// Order inside a tick:
    ///   1. Each fighter tries to act on its button (accepted or ignored depending on its state)
    ///   2. Fighters advance (frame counters, movement, timers)
    ///   3. Bodies are pushed apart and kept inside the arena
    ///   4. Active attacks are checked against the other fighter (hit / block / dodge)
    ///   5. Facing and stamina are updated
    /// </summary>
    public class FightSim
    {
        public readonly FightConfig config;
        public readonly Fighter[] fighters = { new Fighter(0), new Fighter(1) };
        public int tick;

        /// <summary>Events produced during the most recent tick.</summary>
        public readonly List<FightEvent> tickEvents = new List<FightEvent>();
        /// <summary>Every event this fight (for replays / tagging later).</summary>
        public readonly List<FightEvent> allEvents = new List<FightEvent>();

        public event Action<FightEvent> OnEvent;

        public FightSim(FightConfig config)
        {
            this.config = config;
            ResetFight();
        }

        public Fighter Opponent(Fighter f) { return fighters[1 - f.index]; }

        public float Distance { get { return Math.Abs(fighters[1].x - fighters[0].x); } }

        // ------------------------------------------------------------------ resets

        public void ResetFight()
        {
            tick = 0;
            allEvents.Clear();
            tickEvents.Clear();
            foreach (Fighter f in fighters)
            {
                f.hp = config.maxHp;
                f.stamina = config.maxStamina;
            }
            ResetPositions();
        }

        /// <summary>Back to the starting marks, between rounds. HP is NOT restored (boxing-style).</summary>
        public void ResetPositions()
        {
            for (int i = 0; i < 2; i++)
            {
                Fighter f = fighters[i];
                f.x = (i == 0 ? -1f : 1f) * config.startDistance * 0.5f;
                f.prevX = f.x;
                f.facing = i == 0 ? 1 : -1;
                f.walkVelocity = 0f;
                f.knockVelocity = 0f;
                f.mode = f.IsKO ? FighterMode.KO : FighterMode.Idle;
                f.modeFrame = 0;
                f.modeDuration = 0;
                f.currentAttack = FighterAction.Idle;
                f.attackConnected = false;
                f.attackResolved = false;
                f.attackIsCounter = false;
                f.comboCount = 0;
                f.dodgeSuccess = false;
                f.counterWindow = 0;
                f.lastAction = FighterAction.Idle;
                f.ticksSinceLastAction = 0;
                f.ResetRoundStats();
            }
        }

        // ------------------------------------------------------------------ main tick

        public void Tick(FighterAction action0, FighterAction action1)
        {
            tick++;
            tickEvents.Clear();

            Fighter f0 = fighters[0], f1 = fighters[1];
            f0.prevX = f0.x;
            f1.prevX = f1.x;

            ApplyAction(f0, action0);
            ApplyAction(f1, action1);

            Advance(f0);
            Advance(f1);

            ResolveBodies();
            ResolveHits();

            UpdateFacing(f0);
            UpdateFacing(f1);
            RegenStamina(f0);
            RegenStamina(f1);
        }

        // ------------------------------------------------------------------ 1. actions

        private void ApplyAction(Fighter f, FighterAction a)
        {
            f.attemptedAction = a;
            bool accepted = TryAction(f, a);
            f.actionAccepted = accepted;

            if (accepted && a != FighterAction.Idle && a != f.lastAction)
            {
                f.lastAction = a;
                f.ticksSinceLastAction = 0;
            }
            else
            {
                f.ticksSinceLastAction++;
            }
        }

        private bool TryAction(Fighter f, FighterAction a)
        {
            switch (f.mode)
            {
                case FighterMode.KO:
                case FighterMode.Hitstun:
                case FighterMode.Blockstun:
                case FighterMode.GuardBroken:
                case FighterMode.Dash:
                    return false;

                case FighterMode.Attack:
                    // Only chaining is allowed mid-attack: the previous hit must have LANDED.
                    if (FighterActions.IsAttack(a) && CanChain(f) && f.stamina >= config.Move(a).staminaCost)
                    {
                        StartAttack(f, a, true);
                        return true;
                    }
                    return false;

                case FighterMode.Dodge:
                    // After a successful dodge, the end of the dodge can be cancelled into a punish.
                    if (FighterActions.IsAttack(a) && f.dodgeSuccess && f.modeFrame > config.dodgeInvulnEnd
                        && f.stamina >= config.Move(a).staminaCost)
                    {
                        StartAttack(f, a, false);
                        return true;
                    }
                    return false;
            }

            // Idle / Walk / Block: free to do anything.
            f.walkVelocity = 0f;
            switch (a)
            {
                case FighterAction.Idle:
                    SetMode(f, FighterMode.Idle);
                    return true;

                case FighterAction.Forward:
                case FighterAction.Back:
                {
                    bool forward = a == FighterAction.Forward;
                    float speed = forward ? config.walkForwardSpeed : config.walkBackSpeed;
                    f.walkVelocity = (forward ? f.facing : -f.facing) * speed;
                    if (f.mode != FighterMode.Walk) SetMode(f, FighterMode.Walk);
                    return true;
                }

                case FighterAction.DashIn:
                case FighterAction.DashOut:
                    if (f.stamina < config.dashStaminaCost) { SetMode(f, FighterMode.Idle); return false; }
                    f.stamina -= config.dashStaminaCost;
                    f.dashDir = a == FighterAction.DashIn ? f.facing : -f.facing;
                    SetMode(f, FighterMode.Dash);
                    return true;

                case FighterAction.Jab:
                case FighterAction.Heavy:
                    if (f.stamina < config.Move(a).staminaCost) { SetMode(f, FighterMode.Idle); return false; }
                    StartAttack(f, a, false);
                    return true;

                case FighterAction.Block:
                    if (f.mode != FighterMode.Block) SetMode(f, FighterMode.Block);
                    return true;

                case FighterAction.Dodge:
                    if (f.stamina < config.dodgeStaminaCost) { SetMode(f, FighterMode.Idle); return false; }
                    f.stamina -= config.dodgeStaminaCost;
                    f.dodgeSuccess = false;
                    SetMode(f, FighterMode.Dodge);
                    return true;
            }
            return false;
        }

        private bool CanChain(Fighter f)
        {
            if (!f.attackConnected) return false;
            if (f.comboCount >= config.comboMaxHits) return false;
            if (f.currentAttack == FighterAction.Heavy) return false; // Heavy always ends a chain
            AttackPhase p = f.Phase(config);
            if (p == AttackPhase.Active) return f.attackResolved;
            return p == AttackPhase.Recovery && f.RecoveryFrame(config) <= config.cancelWindowRecoveryFrames;
        }

        private void StartAttack(Fighter f, FighterAction a, bool chain)
        {
            f.stamina -= config.Move(a).staminaCost;
            f.currentAttack = a;
            f.attackConnected = false;
            f.attackResolved = false;
            f.comboCount = chain ? f.comboCount + 1 : 1;
            f.attackIsCounter = f.counterWindow > 0;
            f.counterWindow = 0;
            f.dodgeSuccess = false;
            f.walkVelocity = 0f;
            SetMode(f, FighterMode.Attack);
            Emit(FightEventType.AttackStarted, f.index, -1, a, 0f);
        }

        private static void SetMode(Fighter f, FighterMode mode, int duration = 0)
        {
            f.mode = mode;
            f.modeFrame = 0;
            f.modeDuration = duration;
        }

        // ------------------------------------------------------------------ 2. advance

        private void Advance(Fighter f)
        {
            f.modeFrame++;

            switch (f.mode)
            {
                case FighterMode.Walk:
                    f.x += f.walkVelocity;
                    break;

                case FighterMode.Dash:
                    f.x += f.dashDir * config.dashSpeed;
                    if (f.modeFrame >= config.dashFrames) SetMode(f, FighterMode.Idle);
                    break;

                case FighterMode.Attack:
                {
                    MoveData m = config.Move(f.currentAttack);
                    // Attack finished its active frames without touching anything -> whiff.
                    if (!f.attackResolved && f.modeFrame == m.startup + m.active + 1)
                    {
                        f.attackResolved = true;
                        Emit(FightEventType.Whiff, f.index, -1, f.currentAttack, 0f);
                    }
                    if (f.modeFrame >= m.TotalFrames) SetMode(f, FighterMode.Idle);
                    break;
                }

                case FighterMode.Dodge:
                    if (f.modeFrame >= config.dodgeFrames) SetMode(f, FighterMode.Idle);
                    break;

                case FighterMode.Hitstun:
                case FighterMode.Blockstun:
                case FighterMode.GuardBroken:
                    if (f.modeFrame >= f.modeDuration) SetMode(f, FighterMode.Idle);
                    break;
            }

            // Knockback slides the fighter and decays.
            if (f.knockVelocity != 0f)
            {
                f.x += f.knockVelocity;
                f.knockVelocity *= 0.8f;
                if (Math.Abs(f.knockVelocity) < 0.002f) f.knockVelocity = 0f;
            }

            // The Counter Window only starts counting down once the dodge's invulnerability is over.
            if (f.counterWindow > 0 && !(f.mode == FighterMode.Dodge && f.modeFrame <= config.dodgeInvulnEnd))
                f.counterWindow--;
        }

        // ------------------------------------------------------------------ 3. bodies

        private void ResolveBodies()
        {
            Fighter a = fighters[0], b = fighters[1];
            float minGap = config.fighterWidth;
            float d = b.x - a.x;
            if (Math.Abs(d) < minGap)
            {
                float push = (minGap - Math.Abs(d)) * 0.5f;
                int s = d >= 0f ? 1 : -1;
                a.x -= s * push;
                b.x += s * push;
            }

            float limit = config.arenaHalfWidth - config.fighterWidth * 0.5f;
            foreach (Fighter f in fighters) f.x = Clamp(f.x, -limit, limit);

            // If a wall stopped one fighter, keep them apart by moving the other.
            d = b.x - a.x;
            if (Math.Abs(d) < minGap)
            {
                int s = d >= 0f ? 1 : -1;
                if (Math.Abs(a.x) >= limit - 0.0001f) b.x = a.x + s * minGap;
                else a.x = b.x - s * minGap;
            }
        }

        // ------------------------------------------------------------------ 4. hits

        public struct Box
        {
            public float min, max;
            public Box(float min, float max) { this.min = min; this.max = max; }
            public bool Overlaps(Box o) { return min <= o.max && o.min <= max; }
        }

        public Box Hurtbox(Fighter f)
        {
            float h = config.fighterWidth * 0.5f;
            return new Box(f.x - h, f.x + h);
        }

        /// <summary>The fist's reach in front of the body. Width only; there is no jumping in this game.</summary>
        public Box Hitbox(Fighter f)
        {
            MoveData m = config.Move(f.currentAttack);
            float front = f.x + f.facing * config.fighterWidth * 0.5f;
            float tip = front + f.facing * m.reach;
            return new Box(Math.Min(front, tip), Math.Max(front, tip));
        }

        private readonly List<int> pendingHits = new List<int>(2);

        private void ResolveHits()
        {
            // Collect first, then apply, so simultaneous hits trade fairly regardless of fighter order.
            pendingHits.Clear();
            foreach (Fighter atk in fighters)
            {
                if (atk.mode != FighterMode.Attack || atk.attackResolved) continue;
                if (atk.Phase(config) != AttackPhase.Active) continue;
                Fighter def = Opponent(atk);
                if (def.IsKO) continue;
                if (Hitbox(atk).Overlaps(Hurtbox(def))) pendingHits.Add(atk.index);
            }
            foreach (int i in pendingHits) ApplyHit(fighters[i], fighters[1 - i]);
        }

        private void ApplyHit(Fighter atk, Fighter def)
        {
            MoveData m = config.Move(atk.currentAttack);
            atk.attackResolved = true;

            // Dodged: the attack passes through.
            if (def.IsInvulnerable(config))
            {
                def.dodgeSuccess = true;
                def.dodgesThisRound++;
                def.counterWindow = (config.dodgeInvulnEnd - def.modeFrame) + config.counterWindowFrames;
                Emit(FightEventType.Dodged, def.index, atk.index, atk.currentAttack, 0f);
                return;
            }

            // Blocked: chip damage + stamina drain. A blocked hit does NOT allow chaining.
            if (def.mode == FighterMode.Block || def.mode == FighterMode.Blockstun)
            {
                float chip = m.damage * config.blockDamageMultiplier;
                Damage(atk, def, chip);
                def.blocksThisRound++;
                def.stamina -= m.blockStaminaDrain;
                def.knockVelocity = atk.facing * m.knockback * 0.6f;
                if (def.stamina <= 0f)
                {
                    def.stamina = 0f;
                    SetMode(def, FighterMode.GuardBroken, config.guardBreakFrames);
                    Emit(FightEventType.GuardBreak, atk.index, def.index, atk.currentAttack, chip);
                }
                else
                {
                    SetMode(def, FighterMode.Blockstun, m.blockstun);
                    Emit(FightEventType.Blocked, def.index, atk.index, atk.currentAttack, chip);
                }
                CheckKO(atk, def);
                return;
            }

            // Clean hit.
            float dmg = m.damage * (atk.attackIsCounter ? config.counterDamageMultiplier : 1f);
            Damage(atk, def, dmg);
            atk.attackConnected = true;
            atk.hitsLandedThisRound++;
            def.knockVelocity = atk.facing * m.knockback;
            def.walkVelocity = 0f;
            SetMode(def, FighterMode.Hitstun, m.hitstun);
            Emit(atk.attackIsCounter ? FightEventType.CounterHit : FightEventType.Hit, atk.index, def.index, atk.currentAttack, dmg);
            atk.attackIsCounter = false; // the bonus applies to the first hit only
            CheckKO(atk, def);
        }

        private static void Damage(Fighter atk, Fighter def, float amount)
        {
            float real = Math.Min(amount, def.hp);
            def.hp -= real;
            atk.damageDealtThisRound += real;
        }

        private void CheckKO(Fighter atk, Fighter def)
        {
            if (def.hp > 0f) return;
            def.hp = 0f;
            SetMode(def, FighterMode.KO);
            Emit(FightEventType.KO, atk.index, def.index, atk.currentAttack, 0f);
        }

        // ------------------------------------------------------------------ 5. facing + stamina

        private void UpdateFacing(Fighter f)
        {
            if (f.mode == FighterMode.Attack || f.mode == FighterMode.Dash || f.IsKO) return;
            float d = Opponent(f).x - f.x;
            if (Math.Abs(d) > 0.01f) f.facing = d > 0f ? 1 : -1;
        }

        private void RegenStamina(Fighter f)
        {
            float regen = 0f;
            switch (f.mode)
            {
                case FighterMode.Idle:
                    regen = config.staminaRegenIdle;
                    break;
                case FighterMode.Walk:
                    bool backing = Math.Sign(f.walkVelocity) == -f.facing;
                    regen = backing ? config.staminaRegenBack : config.staminaRegenWalk;
                    break;
            }
            f.stamina = Clamp(f.stamina + regen, 0f, config.maxStamina);
        }

        // ------------------------------------------------------------------ helpers

        private void Emit(FightEventType type, int actor, int target, FighterAction move, float amount)
        {
            FightEvent e = new FightEvent { tick = tick, type = type, actor = actor, target = target, move = move, amount = amount };
            tickEvents.Add(e);
            allEvents.Add(e);
            if (OnEvent != null) OnEvent(e);
        }

        private static float Clamp(float v, float lo, float hi)
        {
            return v < lo ? lo : (v > hi ? hi : v);
        }
    }
}
