namespace FightTrainer.Core
{
    public enum FightEventType
    {
        AttackStarted,
        Hit,
        CounterHit,
        /// <summary>A hit that landed on a blocking fighter from outside their guard (circled around it).</summary>
        AngleHit,
        Blocked,
        GuardBreak,
        Dodged,
        Whiff,
        KO
    }

    /// <summary>
    /// Something notable that happened in the fight. These become the "action events" on the
    /// replay timeline that the player tags Good / Bad / Mistake (PDD §6.8).
    /// </summary>
    public struct FightEvent
    {
        public int tick;
        public FightEventType type;
        public int actor;       // who did it (attacker / dodger)
        public int target;      // who it happened to (-1 if none)
        public FighterAction move;
        public float amount;    // damage dealt, if any

        public override string ToString()
        {
            return "[" + tick + "] F" + actor + " " + type + " " + move + (amount > 0f ? " " + amount.ToString("0.0") : "");
        }
    }
}
