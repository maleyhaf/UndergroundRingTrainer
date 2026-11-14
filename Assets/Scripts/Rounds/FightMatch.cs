using System;
using FightTrainer.Core;

namespace FightTrainer.Rounds
{
    public enum MatchPhase
    {
        PreRound,
        Fighting,
        RoundOver,
        CornerBreak,
        FightOver
    }

    public enum CornerAction
    {
        None,
        PatchUp,     // recover a little HP
        CatchBreath  // recover extra stamina
    }

    public enum FightResultType
    {
        None,
        KO,
        Decision,
        Draw
    }

    /// <summary>
    /// Boxing-style rounds around the FightSim (PDD §4):
    /// fixed number of rounds, HP carries over, KO ends the fight at any time,
    /// the judge decides if it goes the distance, corner break between rounds.
    /// </summary>
    public class FightMatch
    {
        public readonly FightConfig config;
        public readonly FightSim sim;
        public readonly JudgeScorer judge = new JudgeScorer();

        public MatchPhase phase;
        public int round;               // 1-based
        public int roundTicksLeft;
        public int phaseTicks;          // ticks spent in the current phase

        public readonly CornerAction[] cornerChoice = new CornerAction[2];

        public FightResultType resultType;
        public int winner = -1;         // 0 / 1, or -1 for none / draw
        public int koRound;

        public event Action<MatchPhase> OnPhaseChanged;

        public FightMatch(FightConfig config)
        {
            this.config = config;
            sim = new FightSim(config);
        }

        public int CornerTicksLeft
        {
            get { return Math.Max(0, config.cornerBreakSeconds * FightConfig.TicksPerSecond - phaseTicks); }
        }

        public void StartFight()
        {
            sim.ResetFight();
            judge.Reset();
            round = 1;
            resultType = FightResultType.None;
            winner = -1;
            koRound = 0;
            BeginRound();
        }

        /// <summary>Advance one tick. Fighter actions are only used while the round is live.</summary>
        public void Tick(FighterAction action0, FighterAction action1)
        {
            phaseTicks++;
            switch (phase)
            {
                case MatchPhase.PreRound:
                    if (phaseTicks >= config.preRoundTicks) SetPhase(MatchPhase.Fighting);
                    break;

                case MatchPhase.Fighting:
                    sim.Tick(action0, action1);
                    roundTicksLeft--;
                    if (sim.fighters[0].IsKO || sim.fighters[1].IsKO)
                    {
                        EndByKO();
                    }
                    else if (roundTicksLeft <= 0)
                    {
                        judge.ScoreRound(round, sim, config.RoundTicks);
                        SetPhase(MatchPhase.RoundOver);
                    }
                    break;

                case MatchPhase.RoundOver:
                    if (phaseTicks >= config.roundOverTicks)
                    {
                        if (round >= config.roundCount) EndByDecision();
                        else
                        {
                            cornerChoice[0] = CornerAction.None;
                            cornerChoice[1] = CornerAction.None;
                            SetPhase(MatchPhase.CornerBreak);
                        }
                    }
                    break;

                case MatchPhase.CornerBreak:
                    bool bothChose = cornerChoice[0] != CornerAction.None && cornerChoice[1] != CornerAction.None;
                    if (bothChose || CornerTicksLeft <= 0)
                    {
                        ApplyCorner();
                        round++;
                        BeginRound();
                    }
                    break;
            }
        }

        public void SetCornerChoice(int fighter, CornerAction choice)
        {
            if (phase == MatchPhase.CornerBreak) cornerChoice[fighter] = choice;
        }

        private void BeginRound()
        {
            sim.ResetPositions();
            roundTicksLeft = config.RoundTicks;
            SetPhase(MatchPhase.PreRound);
        }

        private void ApplyCorner()
        {
            foreach (Fighter f in sim.fighters)
            {
                // Everyone gets partial stamina back between rounds.
                f.stamina += (config.maxStamina - f.stamina) * config.betweenRoundStaminaRecovery;
                switch (cornerChoice[f.index])
                {
                    case CornerAction.PatchUp:
                        f.hp = Math.Min(config.maxHp, f.hp + config.patchUpHp);
                        break;
                    case CornerAction.CatchBreath:
                        f.stamina = config.maxStamina;
                        break;
                }
            }
        }

        private void EndByKO()
        {
            resultType = FightResultType.KO;
            winner = sim.fighters[0].IsKO ? 1 : 0;
            koRound = round;
            SetPhase(MatchPhase.FightOver);
        }

        private void EndByDecision()
        {
            winner = judge.DecisionWinner();
            resultType = winner < 0 ? FightResultType.Draw : FightResultType.Decision;
            SetPhase(MatchPhase.FightOver);
        }

        private void SetPhase(MatchPhase p)
        {
            phase = p;
            phaseTicks = 0;
            if (OnPhaseChanged != null) OnPhaseChanged(p);
        }

        public string ResultText(string name0, string name1)
        {
            switch (resultType)
            {
                case FightResultType.KO:
                    return (winner == 0 ? name0 : name1) + " wins by KO in round " + koRound;
                case FightResultType.Decision:
                    return (winner == 0 ? name0 : name1) + " wins by decision " + judge.Total(winner) + "-" + judge.Total(1 - winner);
                case FightResultType.Draw:
                    return "Draw " + judge.Total(0) + "-" + judge.Total(1) + " (rematch)";
            }
            return "";
        }
    }
}
