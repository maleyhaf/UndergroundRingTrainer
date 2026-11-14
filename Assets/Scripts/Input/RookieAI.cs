using System;
using FightTrainer.Core;
using FightTrainer.Rounds;
using FightTrainer.Training;

namespace FightTrainer.InputSources
{
    /// <summary>
    /// The "Rookie AI" fallback brain from PDD §3, used before any training data exists:
    ///   - if close -> jab (sometimes heavy)
    ///   - if low HP -> block more
    ///   - random dodge chance
    /// plus basic ring sense (2D ring movement):
    ///   - circles while looking for an opening, holding one direction for a while
    ///   - escapes toward the centre when its back is to the ropes or it's stuck in a corner
    ///   - walks straight in to cut off an opponent who is trapped on the ropes
    /// It decides every config.decisionInterval ticks (~0.1s) and holds that button in between,
    /// the same decision rate the ML brain will use. Full reference: Docs/rookie_ai.md
    /// </summary>
    public class RookieAI : IInputSource
    {
        public string SourceTag { get { return "rookie"; } }
        public bool IsHuman { get { return false; } }

        /// <summary>Last decision, for the debug overlay.</summary>
        public FighterAction LastDecision { get; private set; }
        public string LastReason { get; private set; }

        /// <summary>Room behind (world units) below which the Rookie feels trapped against the ropes.</summary>
        public float trappedSpace = 1.0f;

        private readonly Random rng;
        private int ticksUntilDecision;
        private bool circleLeft;

        public RookieAI(int seed)
        {
            rng = new Random(seed);
            LastReason = "";
            circleLeft = rng.NextDouble() < 0.5;
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
            Vec2 u = sim.ToOpponent(me);

            float gap = sim.Distance - c.fighterWidth;          // space between the two bodies
            bool inJabRange = gap <= c.jab.reach;
            bool lowHp = me.hp < c.maxHp * 0.35f;
            bool lowStamina = me.stamina < 15f;
            bool oppWindingUp = opp.Phase(c) == AttackPhase.Startup;
            bool trapped = me.inCorner || StateEncoder.SpaceAlong(me.pos, -u, c.ringHalfSize) < trappedSpace;
            bool oppTrapped = opp.inCorner || StateEncoder.SpaceAlong(opp.pos, u, c.ringHalfSize) < trappedSpace;
            double roll = rng.NextDouble();

            // Random dodge chance when the opponent winds up.
            if (oppWindingUp && inJabRange && roll < 0.12 && me.stamina >= c.dodgeStaminaCost)
                return Pick(FighterAction.Dodge, "random dodge");

            // Under threat -> sometimes block; much more often when HP is low.
            bool threatened = inJabRange && (oppWindingUp || opp.Phase(c) == AttackPhase.Active);
            if (threatened && roll < (lowHp ? 0.65 : 0.30))
                return Pick(FighterAction.Block, lowHp ? "low HP, block" : "threatened, block");

            // Out of gas -> get away to recover (around, not back, if the ropes are behind).
            if (lowStamina)
                return trapped ? Pick(CircleTowardCenter(me, u), "low stamina, slip off the ropes")
                               : Pick(FighterAction.Back, "low stamina");

            // Close -> mostly attack, sometimes reset the distance or circle.
            if (inJabRange)
            {
                if (roll < 0.10) return Pick(FighterAction.Heavy, "close, heavy");
                if (roll < 0.50) return Pick(FighterAction.Jab, "close, jab");
                if (roll < 0.70) return Pick(FighterAction.Block, "close, guard up");
                if (roll < 0.82) return trapped ? Pick(CircleTowardCenter(me, u), "close, escape ropes") : Pick(Circle(), "close, circle");
                if (roll < 0.90) return trapped ? Pick(CircleTowardCenter(me, u), "close, escape ropes") : Pick(FighterAction.Back, "close, step back");
                return Pick(FighterAction.Idle, "close, wait");
            }

            // Back to the ropes -> work back toward the centre.
            if (trapped && roll < 0.70)
                return Pick(CircleTowardCenter(me, u), "escape the ropes");

            // Opponent trapped -> cut off the ring and walk them down.
            if (oppTrapped)
            {
                if (gap > 2f && roll < 0.08) return Pick(FighterAction.DashIn, "cut off, dash in");
                return Pick(FighterAction.Forward, "cut off the ring");
            }

            // Otherwise stalk: walk in or circle, occasionally dash.
            if (gap > 2f && roll < 0.05) return Pick(FighterAction.DashIn, "far, dash in");
            if (roll < 0.60) return Pick(FighterAction.Forward, "close the distance");
            if (roll < 0.92) return Pick(Circle(), "circle, look for an opening");
            return Pick(FighterAction.Idle, "wait");
        }

        /// <summary>Circle in the current direction, occasionally switching (so it doesn't jitter).</summary>
        private FighterAction Circle()
        {
            if (rng.NextDouble() < 0.15) circleLeft = !circleLeft;
            return circleLeft ? FighterAction.CircleLeft : FighterAction.CircleRight;
        }

        /// <summary>Circle in whichever direction leads back toward the centre of the ring.</summary>
        private FighterAction CircleTowardCenter(Fighter me, Vec2 u)
        {
            float side = Vec2.Dot(-me.pos, u.Left);
            circleLeft = side >= 0f;
            return circleLeft ? FighterAction.CircleLeft : FighterAction.CircleRight;
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
