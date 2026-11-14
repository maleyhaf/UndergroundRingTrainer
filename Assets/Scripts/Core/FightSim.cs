using System;
using System.Collections.Generic;

namespace FightTrainer.Core
{
    /// <summary>
    /// The fight itself, advanced one fixed tick (1/60s) at a time. Deterministic and Unity-free:
    /// the same inputs always produce the same fight, which keeps frame data exact and ML data clean.
    ///
    /// The fight happens on a square ring seen from above (2D positions). Fighters can move toward,
    /// away from, and around each other. They turn to face their opponent at a limited speed; turning
    /// is slow while blocking, so circling around a blocker can get outside their guard.
    ///
    /// Order inside a tick:
    ///   1. Each fighter tries to act on its button (accepted or ignored depending on its state)
    ///   2. Fighters advance (frame counters, movement, timers)
    ///   3. Bodies are pushed apart and kept inside the ropes
    ///   4. Active attacks are checked against the other fighter (hit / block / angle / dodge)
    ///   5. Facing, stamina and ring-position stats are updated
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

        public float Distance { get { return Vec2.Distance(fighters[0].pos, fighters[1].pos); } }

        /// <summary>Unit direction from a fighter toward its opponent.</summary>
        public Vec2 ToOpponent(Fighter f) { return (Opponent(f).pos - f.pos).Normalized; }

        private float Radius { get { return config.fighterWidth * 0.5f; } }

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
                f.pos = new Vec2((i == 0 ? -1f : 1f) * config.startDistance * 0.5f, 0f);
                f.prevPos = f.pos;
                f.facing = new Vec2(i == 0 ? 1f : -1f, 0f);
                f.walkAction = FighterAction.Idle;
                f.knockVelocity = Vec2.Zero;
                f.dashDir = Vec2.Zero;
                f.mode = FighterMode.Idle; // a KO ends the fight, so a reset always clears it
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
                f.onRopes = false;
                f.inCorner = false;
                f.ResetRoundStats();
            }
        }

        // ------------------------------------------------------------------ main tick

        public void Tick(FighterAction action0, FighterAction action1)
        {
            tick++;
            tickEvents.Clear();

            Fighter f0 = fighters[0], f1 = fighters[1];
            f0.prevPos = f0.pos;
            f1.prevPos = f1.pos;

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
            UpdateRingStats();
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
            switch (a)
            {
                case FighterAction.Idle:
                    SetMode(f, FighterMode.Idle);
                    return true;

                case FighterAction.Forward:
                case FighterAction.Back:
                case FighterAction.CircleLeft:
                case FighterAction.CircleRight:
                    f.walkAction = a;
                    if (f.mode != FighterMode.Walk) SetMode(f, FighterMode.Walk);
                    return true;

                case FighterAction.DashIn:
                case FighterAction.DashOut:
                    if (f.stamina < config.dashStaminaCost) { SetMode(f, FighterMode.Idle); return false; }
                    f.stamina -= config.dashStaminaCost;
                    f.dashDir = a == FighterAction.DashIn ? ToOpponent(f) : -ToOpponent(f);
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
            // The punch is aimed where the fighter is facing when it starts and stays locked there.
            // A target that circles away during the wind-up can slip out of its path.
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
                    Walk(f);
                    break;

                case FighterMode.Dash:
                    f.pos = f.pos + f.dashDir * config.dashSpeed;
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
            if (f.knockVelocity.x != 0f || f.knockVelocity.y != 0f)
            {
                f.pos = f.pos + f.knockVelocity;
                f.knockVelocity = f.knockVelocity * 0.8f;
                if (f.knockVelocity.Length < 0.002f) f.knockVelocity = Vec2.Zero;
            }

            // The Counter Window only starts counting down once the dodge's invulnerability is over.
            if (f.counterWindow > 0 && !(f.mode == FighterMode.Dodge && f.modeFrame <= config.dodgeInvulnEnd))
                f.counterWindow--;
        }

        private void Walk(Fighter f)
        {
            // Directions are measured from where the opponent stood at the start of this tick.
            Vec2 target = Opponent(f).prevPos;
            Vec2 offset = target - f.pos;
            float dist = offset.Length;
            Vec2 u = offset.Normalized;

            switch (f.walkAction)
            {
                case FighterAction.Forward:
                    f.pos = f.pos + u * config.walkForwardSpeed;
                    break;
                case FighterAction.Back:
                    f.pos = f.pos - u * config.walkBackSpeed;
                    break;
                case FighterAction.CircleLeft:
                case FighterAction.CircleRight:
                {
                    // Sidestep, then pull back onto the same circle so circling keeps the distance.
                    Vec2 side = f.walkAction == FighterAction.CircleLeft ? u.Left : -u.Left;
                    Vec2 moved = f.pos + side * config.circleSpeed;
                    f.pos = target - (target - moved).Normalized * dist;
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ 3. bodies

        private void ResolveBodies()
        {
            Fighter a = fighters[0], b = fighters[1];
            float limit = config.ringHalfSize - Radius;

            for (int iteration = 0; iteration < 3; iteration++)
            {
                Vec2 d = b.pos - a.pos;
                float dist = d.Length;
                if (dist < config.fighterWidth)
                {
                    Vec2 n = dist > 1e-5f ? d * (1f / dist) : new Vec2(1f, 0f);
                    float push = (config.fighterWidth - dist) * 0.5f;
                    a.pos = a.pos - n * push;
                    b.pos = b.pos + n * push;
                }
                a.pos = ClampToRing(a.pos, limit);
                b.pos = ClampToRing(b.pos, limit);
            }
        }

        private static Vec2 ClampToRing(Vec2 p, float limit)
        {
            return new Vec2(Clamp(p.x, -limit, limit), Clamp(p.y, -limit, limit));
        }

        // ------------------------------------------------------------------ 4. hits

        /// <summary>The punch as a line from the front of the body out to the fist's full reach.</summary>
        public void HitSegment(Fighter f, out Vec2 front, out Vec2 tip)
        {
            MoveData m = config.Move(f.currentAttack);
            front = f.pos + f.facing * Radius;
            tip = front + f.facing * m.reach;
        }

        public bool AttackReaches(Fighter atk, Fighter def)
        {
            Vec2 front, tip;
            HitSegment(atk, out front, out tip);
            return Vec2.DistanceToSegment(def.pos, front, tip) <= Radius;
        }

        /// <summary>Is the attacker inside the defender's guard arc (so a block would cover the hit)?</summary>
        public bool InsideGuard(Fighter def, Fighter atk)
        {
            Vec2 toAtk = (atk.pos - def.pos).Normalized;
            return Math.Abs(Vec2.SignedAngle(def.facing, toAtk)) <= config.blockArcDegrees;
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
                if (AttackReaches(atk, def)) pendingHits.Add(atk.index);
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

            bool blocking = def.mode == FighterMode.Block || def.mode == FighterMode.Blockstun;
            bool angled = blocking && !InsideGuard(def, atk);

            // Blocked: chip damage + stamina drain. A blocked hit does NOT allow chaining.
            if (blocking && !angled)
            {
                float chip = m.damage * config.blockDamageMultiplier;
                Damage(atk, def, chip);
                def.blocksThisRound++;
                def.stamina -= m.blockStaminaDrain;
                def.knockVelocity = atk.facing * (m.knockback * 0.6f);
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

            // Clean hit (including one that got around the guard from the side).
            float dmg = m.damage * (atk.attackIsCounter ? config.counterDamageMultiplier : 1f);
            Damage(atk, def, dmg);
            atk.attackConnected = true;
            atk.hitsLandedThisRound++;
            if (angled) atk.angleHitsThisRound++;
            def.knockVelocity = atk.facing * m.knockback;
            SetMode(def, FighterMode.Hitstun, m.hitstun);
            FightEventType type = atk.attackIsCounter ? FightEventType.CounterHit
                                : angled ? FightEventType.AngleHit
                                : FightEventType.Hit;
            Emit(type, atk.index, def.index, atk.currentAttack, dmg);
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

        // ------------------------------------------------------------------ 5. facing, stamina, ring stats

        private void UpdateFacing(Fighter f)
        {
            float rate;
            switch (f.mode)
            {
                case FighterMode.Idle:
                case FighterMode.Walk: rate = config.turnRateFree; break;
                case FighterMode.Block:
                case FighterMode.Blockstun: rate = config.turnRateBlock; break;
                case FighterMode.Dodge: rate = config.turnRateDodge; break;
                default: return; // attacking, dashing, stunned, KO: no turning
            }
            if (Distance < 0.01f) return;
            f.facing = Vec2.RotateTowards(f.facing, ToOpponent(f), rate);
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
                    regen = f.walkAction == FighterAction.Back ? config.staminaRegenBack : config.staminaRegenWalk;
                    break;
            }
            f.stamina = Clamp(f.stamina + regen, 0f, config.maxStamina);
        }

        private void UpdateRingStats()
        {
            float zone = config.ropeZone;
            float edge = config.ringHalfSize - Radius;
            foreach (Fighter f in fighters)
            {
                bool nearX = edge - Math.Abs(f.pos.x) < zone;
                bool nearY = edge - Math.Abs(f.pos.y) < zone;
                f.onRopes = nearX || nearY;
                f.inCorner = nearX && nearY;
                if (f.onRopes) f.ticksOnRopesThisRound++;
                if (f.inCorner) f.ticksInCornerThisRound++;
            }

            float c0 = fighters[0].pos.Length, c1 = fighters[1].pos.Length;
            if (c0 + config.centerControlMargin < c1) fighters[0].ticksCenterControlThisRound++;
            else if (c1 + config.centerControlMargin < c0) fighters[1].ticksCenterControlThisRound++;
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
