using System;
using System.Collections.Generic;
using FightTrainer.Core;

namespace FightTrainer.Rounds
{
    /// <summary>
    /// MVP judging (PDD §13): one judge, simplified 10-point must system.
    /// Whoever dealt more damage in a round wins it 10-9; an even round is 10-10.
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
        }

        public readonly List<RoundScore> rounds = new List<RoundScore>();

        /// <summary>Damage difference below which a round is scored even.</summary>
        public float evenThreshold = 0.5f;

        public void Reset() { rounds.Clear(); }

        public RoundScore ScoreRound(int round, FightSim sim)
        {
            float d0 = sim.fighters[0].damageDealtThisRound;
            float d1 = sim.fighters[1].damageDealtThisRound;
            RoundScore s = new RoundScore { round = round, damage0 = d0, damage1 = d1, score0 = 10, score1 = 10 };
            if (Math.Abs(d0 - d1) >= evenThreshold)
            {
                if (d0 > d1) s.score1 = 9; else s.score0 = 9;
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
