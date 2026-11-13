using System;
using FightTrainer.Core;
using FightTrainer.Rounds;

namespace FightTrainer.InputSources
{
    /// <summary>
    /// The "Rookie AI" fallback brain from PDD §3, used before any training data exists:
    ///   - if close -> jab (sometimes heavy)
    ///   - if low HP -> block more
    ///   - random dodge chance
    /// It decides every config.decisionInterval ticks (~0.1s) and holds that button in between,
    /// the same decision rate the ML brain will use.
    /// </summary>
    public class RookieAI : IInputSource
    {
        public string SourceTag { get { return "rookie"; } }
        public bool IsHuman { get { return false; } }

        /// <summary>Last decision, for the debug overlay.</summary>
        public FighterAction LastDecision { get; private set; }
        public string LastReason { get; private set; }

        private readonly Random rng;
        private int ticksUntilDecision;

        public RookieAI(int seed)
        {
            rng = new Random(seed);
            LastReason = "";
        }

        public FighterAction GetAction(FightMatch match, int fighterIndex)
        {
            if (ticksUntilDecision > 0)
            {
                ticksUntilDecision--;
                return LastDecision;
            }
            ticksUntilDecision = match.config.decisionInterval - 1;
            LastDecision = Decide(match, fighterIndex);
            return LastDecision;
        }

        private FighterAction Decide(FightMatch match, int i)
        {
            FightConfig c = match.config;
            FightSim sim = match.sim;
            Fighter me = sim.fighters[i];
            Fighter opp = sim.Opponent(me);

            float gap = sim.Distance - c.fighterWidth;          // space between the two bodies
            bool inJabRange = gap <= c.jab.reach;
            bool lowHp = me.hp < c.maxHp * 0.35f;
            bool lowStamina = me.stamina < 15f;
            bool oppWindingUp = opp.Phase(c) == AttackPhase.Startup;
            double roll = rng.NextDouble();

            // Random dodge chance when the opponent winds up.
            if (oppWindingUp && inJabRange && roll < 0.12 && me.stamina >= c.dodgeStaminaCost)
                return Pick(FighterAction.Dodge, "random dodge");

            // Under threat -> sometimes block; much more often when HP is low.
            bool threatened = inJabRange && (oppWindingUp || opp.Phase(c) == AttackPhase.Active);
            if (threatened && roll < (lowHp ? 0.65 : 0.30))
                return Pick(FighterAction.Block, lowHp ? "low HP, block" : "threatened, block");

            // Out of gas -> back off to recover.
            if (lowStamina)
                return Pick(FighterAction.Back, "low stamina");

            // Close -> mostly attack, sometimes reset the distance.
            if (inJabRange)
            {
                if (roll < 0.10) return Pick(FighterAction.Heavy, "close, heavy");
                if (roll < 0.50) return Pick(FighterAction.Jab, "close, jab");
                if (roll < 0.75) return Pick(FighterAction.Block, "close, guard up");
                if (roll < 0.88) return Pick(FighterAction.Back, "close, step back");
                return Pick(FighterAction.Idle, "close, wait");
            }

            // Otherwise walk in, occasionally dash.
            if (gap > 2f && roll < 0.05) return Pick(FighterAction.DashIn, "far, dash in");
            return Pick(FighterAction.Forward, "close the distance");
        }

        private FighterAction Pick(FighterAction a, string reason)
        {
            LastReason = reason;
            return a;
        }

        public void OnActionResult(FighterAction action, bool accepted) { }

        public CornerAction ChooseCorner(FightMatch match, int fighterIndex)
        {
            Fighter me = match.sim.fighters[fighterIndex];
            return me.hp < match.config.maxHp * 0.5f ? CornerAction.PatchUp : CornerAction.CatchBreath;
        }
    }
}
