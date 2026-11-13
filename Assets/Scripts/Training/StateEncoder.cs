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
    /// Categories (opponent situation, previous action) are one-hot encoded, so the vector is
    /// longer than the PDD's 10-20 "values" but carries the same information.
    /// </summary>
    public static class StateEncoder
    {
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
                    "distance", "relative_velocity",
                    "own_hp", "opp_hp", "own_stamina", "opp_stamina",
                    "time_since_last_action", "own_actionable", "own_counter_window",
                    "round_progress", "rounds_remaining", "round_time_remaining", "scorecard_lead"
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

            float[] v = new float[Size];
            int k = 0;

            // Distance and closing speed (positive = getting closer).
            v[k++] = sim.Distance / (c.arenaHalfWidth * 2f);
            float closing = (me.Velocity * me.facing) - (opp.Velocity * me.facing);
            v[k++] = Clamp(closing / c.dashSpeed, -1f, 1f);

            v[k++] = me.hp / c.maxHp;
            v[k++] = opp.hp / c.maxHp;
            v[k++] = me.stamina / c.maxStamina;
            v[k++] = opp.stamina / c.maxStamina;

            v[k++] = Math.Min(me.ticksSinceLastAction, 120) / 120f;
            v[k++] = me.IsFreelyActionable ? 1f : 0f;
            v[k++] = me.counterWindow > 0 ? 1f : 0f;

            v[k++] = c.roundCount > 1 ? (match.round - 1) / (float)(c.roundCount - 1) : 0f;
            v[k++] = (c.roundCount - match.round) / (float)Math.Max(1, c.roundCount);
            v[k++] = match.roundTicksLeft / (float)c.RoundTicks;
            v[k++] = Clamp(match.judge.RoundLead(fighterIndex) / (float)Math.Max(1, c.roundCount), -1f, 1f);

            v[k + (int)Situation(opp, c)] = 1f;
            k += SituationCount;

            v[k + (int)me.lastAction] = 1f;
            k += FighterActions.Count;

            return v;
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
