using System;
using System.Collections.Generic;
using FightTrainer.Core;

namespace FightTrainer.Rounds
{
    /// <summary>
    /// MVP judging (PDD §4, §13): one judge, simplified 10-point must system.
    ///   1. Whoever dealt more damage in a round wins it 10-9.
    ///   2. If damage is (nearly) even, ring control breaks the tie: the fighter who held the centre
    ///      noticeably longer wins the round.
    ///   3. Otherwise the round is even, 10-10.
    /// Post-MVP: 3 judges, knockdowns (10-8), split / majority decisions.
    /// </summary>
    public class JudgeScorer
    {
        public struct RoundScore
        {
            public int round;
            public int score0;
            public int score1;
            public float damage0;
            public float damage1;
            public int centerTicks0;
            public int centerTicks1;
            public bool decidedByRingControl;
        }

        public readonly List<RoundScore> rounds = new List<RoundScore>();

        /// <summary>Damage difference below which damage alone can't decide a round.</summary>
        public float evenDamageThreshold = 0.5f;

        /// <summary>Share of the round one fighter must out-control the centre by to win a tied round.</summary>
        public float ringControlThreshold = 0.10f;

        public void Reset() { rounds.Clear(); }

        public RoundScore ScoreRound(int round, FightSim sim, int roundTicks)
        {
            Fighter f0 = sim.fighters[0], f1 = sim.fighters[1];
            RoundScore s = new RoundScore
            {
                round = round,
                damage0 = f0.damageDealtThisRound,
                damage1 = f1.damageDealtThisRound,
                centerTicks0 = f0.ticksCenterControlThisRound,
                centerTicks1 = f1.ticksCenterControlThisRound,
                score0 = 10,
                score1 = 10
            };

            if (Math.Abs(s.damage0 - s.damage1) >= evenDamageThreshold)
            {
                if (s.damage0 > s.damage1) s.score1 = 9; else s.score0 = 9;
            }
            else if (Math.Abs(s.centerTicks0 - s.centerTicks1) >= ringControlThreshold * Math.Max(1, roundTicks))
            {
                if (s.centerTicks0 > s.centerTicks1) s.score1 = 9; else s.score0 = 9;
                s.decidedByRingControl = true;
            }

            rounds.Add(s);
            return s;
        }

        public int Total(int fighter)
        {
            int t = 0;
            foreach (RoundScore s in rounds) t += fighter == 0 ? s.score0 : s.score1;
            return t;
        }

        /// <summary>Rounds won minus rounds lost so far, from this fighter's point of view.</summary>
        public int RoundLead(int fighter)
        {
            int lead = 0;
            foreach (RoundScore s in rounds)
            {
                int mine = fighter == 0 ? s.score0 : s.score1;
                int theirs = fighter == 0 ? s.score1 : s.score0;
                if (mine > theirs) lead++;
                else if (theirs > mine) lead--;
            }
            return lead;
        }

        /// <summary>0 or 1 for the winner on the cards, -1 for a draw.</summary>
        public int DecisionWinner()
        {
            int t0 = Total(0), t1 = Total(1);
            if (t0 == t1) return -1;
            return t0 > t1 ? 0 : 1;
        }
    }
}
