using System;
using System.Collections.Generic;
using FightTrainer.Core;
using FightTrainer.Rounds;

namespace FightTrainer.Training
{
    /// <summary>
    /// Turns "what's happening right now" into the list of numbers the ML model reads (PDD §5.2),
    /// from one fighter's point of view. Everything is scaled to roughly 0..1 (or -1..1).
    ///
    /// Ring features are expressed relative to the opponent (forward = toward them, side = to my left),
    /// so the model learns "back to the ropes" or "open space on my left", not absolute ring coordinates.
    ///
    /// Categories (opponent situation, previous action) are one-hot encoded.
    /// Version 2: added ring movement features and CircleLeft / CircleRight.
    /// </summary>
    public static class StateEncoder
    {
        public const int Version = 2;

        /// <summary>What the opponent is doing, as the model sees it.</summary>
        public enum OpponentSituation
        {
            Idle, Walk, Dash,
            JabStartup, JabActive, JabRecovery,
            HeavyStartup, HeavyActive, HeavyRecovery,
            Block, Dodge, Stunned
        }

        public const int SituationCount = 12;

        private static string[] names;

        public static string[] FeatureNames
        {
            get
            {
                if (names != null) return names;
                List<string> n = new List<string>
                {
                    // Spacing and resources
                    "distance", "closing_velocity", "lateral_velocity",
                    "own_hp", "opp_hp", "own_stamina", "opp_stamina",
                    "time_since_last_action", "own_actionable", "own_counter_window",
                    // Fight clock and scorecard
                    "round_progress", "rounds_remaining", "round_time_remaining", "scorecard_lead",
                    // Angles: who is lined up with whom
                    "own_guard_offset", "opp_guard_offset", "inside_opp_guard",
                    // Ring position
                    "own_rope_distance", "opp_rope_distance",
                    "own_on_ropes", "own_in_corner", "opp_on_ropes", "opp_in_corner",
                    "own_space_behind", "opp_space_behind",
                    "center_distance", "center_dir_forward", "center_dir_side"
                };
                for (int i = 0; i < SituationCount; i++) n.Add("opp_" + (OpponentSituation)i);
                for (int i = 0; i < FighterActions.Count; i++) n.Add("prev_" + (FighterAction)i);
                names = n.ToArray();
                return names;
            }
        }

        public static int Size { get { return FeatureNames.Length; } }

        public static float[] Encode(FightMatch match, int fighterIndex)
        {
            FightConfig c = match.config;
            FightSim sim = match.sim;
            Fighter me = sim.fighters[fighterIndex];
            Fighter opp = sim.Opponent(me);
            float ring = c.ringHalfSize;
            float diagonal = ring * 2f * 1.4142f;

            Vec2 u = sim.ToOpponent(me);  // forward
            Vec2 left = u.Left;           // my left
            Vec2 relVel = me.Velocity - opp.Velocity;

            float[] v = new float[Size];
            int k = 0;

            // Spacing and resources
            v[k++] = sim.Distance / diagonal;
            v[k++] = Clamp(Vec2.Dot(relVel, u) / c.dashSpeed, -1f, 1f);
            v[k++] = Clamp(Vec2.Dot(relVel, left) / c.dashSpeed, -1f, 1f);
            v[k++] = me.hp / c.maxHp;
            v[k++] = opp.hp / c.maxHp;
            v[k++] = me.stamina / c.maxStamina;
            v[k++] = opp.stamina / c.maxStamina;
            v[k++] = Math.Min(me.ticksSinceLastAction, 120) / 120f;
            v[k++] = me.IsFreelyActionable ? 1f : 0f;
            v[k++] = me.counterWindow > 0 ? 1f : 0f;

            // Fight clock and scorecard
            v[k++] = c.roundCount > 1 ? (match.round - 1) / (float)(c.roundCount - 1) : 0f;
            v[k++] = (c.roundCount - match.round) / (float)Math.Max(1, c.roundCount);
            v[k++] = match.roundTicksLeft / (float)c.RoundTicks;
            v[k++] = Clamp(match.judge.RoundLead(fighterIndex) / (float)Math.Max(1, c.roundCount), -1f, 1f);

            // Angles
            v[k++] = Vec2.SignedAngle(me.facing, u) / 180f;        // how far my guard is off the opponent
            v[k++] = Vec2.SignedAngle(opp.facing, -u) / 180f;      // how far I am off the opponent's guard
            v[k++] = sim.InsideGuard(opp, me) ? 1f : 0f;           // would their block cover my punch?

            // Ring position
            v[k++] = me.DistanceToRopes(c) / ring;
            v[k++] = opp.DistanceToRopes(c) / ring;
            v[k++] = me.onRopes ? 1f : 0f;
            v[k++] = me.inCorner ? 1f : 0f;
            v[k++] = opp.onRopes ? 1f : 0f;
            v[k++] = opp.inCorner ? 1f : 0f;
            v[k++] = SpaceAlong(me.pos, -u, ring) / (ring * 2f);   // room I have to back up
            v[k++] = SpaceAlong(opp.pos, u, ring) / (ring * 2f);   // room they have to back up (can I trap them?)
            float centerDist = me.pos.Length;
            v[k++] = centerDist / (ring * 1.4142f);
            Vec2 toCenter = centerDist > 0.05f ? (-me.pos).Normalized : Vec2.Zero;
            v[k++] = Vec2.Dot(toCenter, u);
            v[k++] = Vec2.Dot(toCenter, left);

            // Opponent situation (one-hot)
            v[k + (int)Situation(opp, c)] = 1f;
            k += SituationCount;

            // My previous action (one-hot)
            v[k + (int)me.lastAction] = 1f;
            k += FighterActions.Count;

            return v;
        }

        /// <summary>Distance from p to the ropes travelling in direction d (d must be a unit vector).</summary>
        public static float SpaceAlong(Vec2 p, Vec2 d, float ring)
        {
            float tx = d.x > 1e-5f ? (ring - p.x) / d.x : d.x < -1e-5f ? (-ring - p.x) / d.x : float.MaxValue;
            float ty = d.y > 1e-5f ? (ring - p.y) / d.y : d.y < -1e-5f ? (-ring - p.y) / d.y : float.MaxValue;
            return Math.Max(0f, Math.Min(tx, ty));
        }

        public static OpponentSituation Situation(Fighter f, FightConfig c)
        {
            switch (f.mode)
            {
                case FighterMode.Walk: return OpponentSituation.Walk;
                case FighterMode.Dash: return OpponentSituation.Dash;
                case FighterMode.Block:
                case FighterMode.Blockstun: return OpponentSituation.Block;
                case FighterMode.Dodge: return OpponentSituation.Dodge;
                case FighterMode.Hitstun:
                case FighterMode.GuardBroken:
                case FighterMode.KO: return OpponentSituation.Stunned;
                case FighterMode.Attack:
                {
                    bool heavy = f.currentAttack == FighterAction.Heavy;
                    switch (f.Phase(c))
                    {
                        case AttackPhase.Startup: return heavy ? OpponentSituation.HeavyStartup : OpponentSituation.JabStartup;
                        case AttackPhase.Active: return heavy ? OpponentSituation.HeavyActive : OpponentSituation.JabActive;
                        default: return heavy ? OpponentSituation.HeavyRecovery : OpponentSituation.JabRecovery;
                    }
                }
            }
            return OpponentSituation.Idle;
        }

        private static float Clamp(float v, float lo, float hi)
        {
            return v < lo ? lo : (v > hi ? hi : v);
        }
    }
}
