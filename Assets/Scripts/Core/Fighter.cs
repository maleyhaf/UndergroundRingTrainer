namespace FightTrainer.Core
{
    public enum FighterMode
    {
        Idle,
        Walk,
        Dash,
        Attack,
        Block,
        Dodge,
        Hitstun,
        Blockstun,
        GuardBroken,
        KO
    }

    public enum AttackPhase
    {
        None,
        Startup,
        Active,
        Recovery
    }

    /// <summary>
    /// All the state for one fighter. Plain C# (no Unity types), so the fight can be simulated
    /// headless for tests and, later, for fast ML training.
    /// </summary>
    public class Fighter
    {
        public readonly int index;

        // Position / movement (world units, 1D along the floor)
        public float x;
        public float prevX;
        public float walkVelocity;
        public float knockVelocity;
        public int facing = 1;          // +1 = facing right, -1 = facing left
        public int dashDir;             // world direction of the current dash

        // Resources
        public float hp;
        public float stamina;

        // State machine
        public FighterMode mode = FighterMode.Idle;
        public int modeFrame;           // frames spent in the current mode
        public int modeDuration;        // used by timed modes (stuns)

        // Attacks / combos
        public FighterAction currentAttack = FighterAction.Idle;
        public bool attackConnected;    // the current attack hit (enables chaining)
        public bool attackResolved;     // the current attack already hit / was blocked / was dodged
        public int comboCount;
        public bool attackIsCounter;    // the current attack started inside the Counter Window

        // Dodge / Counter Window
        public bool dodgeSuccess;
        public int counterWindow;       // ticks left in which a started attack becomes a Counter

        // Bookkeeping for the ML state vector
        public FighterAction lastAction = FighterAction.Idle;
        public int ticksSinceLastAction;
        public FighterAction attemptedAction = FighterAction.Idle;
        public bool actionAccepted;

        // Per-round stats (for judges + corner readout)
        public float damageDealtThisRound;
        public int hitsLandedThisRound;
        public int blocksThisRound;
        public int dodgesThisRound;

        public Fighter(int index)
        {
            this.index = index;
        }

        public float Velocity { get { return x - prevX; } }

        public AttackPhase Phase(FightConfig c)
        {
            if (mode != FighterMode.Attack) return AttackPhase.None;
            MoveData m = c.Move(currentAttack);
            if (modeFrame <= m.startup) return AttackPhase.Startup;
            if (modeFrame <= m.startup + m.active) return AttackPhase.Active;
            return AttackPhase.Recovery;
        }

        /// <summary>Frames into recovery (1 = first recovery frame), or 0 if not in recovery.</summary>
        public int RecoveryFrame(FightConfig c)
        {
            if (Phase(c) != AttackPhase.Recovery) return 0;
            MoveData m = c.Move(currentAttack);
            return modeFrame - m.startup - m.active;
        }

        public bool IsInvulnerable(FightConfig c)
        {
            return mode == FighterMode.Dodge && modeFrame >= c.dodgeInvulnStart && modeFrame <= c.dodgeInvulnEnd;
        }

        public bool IsKO { get { return mode == FighterMode.KO; } }

        /// <summary>Can this fighter freely choose a new action right now?</summary>
        public bool IsFreelyActionable
        {
            get { return mode == FighterMode.Idle || mode == FighterMode.Walk || mode == FighterMode.Block; }
        }

        public void ResetRoundStats()
        {
            damageDealtThisRound = 0f;
            hitsLandedThisRound = 0;
            blocksThisRound = 0;
            dodgesThisRound = 0;
        }
    }
}
