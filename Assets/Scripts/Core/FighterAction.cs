namespace FightTrainer.Core
{
    /// <summary>
    /// The buttons a fighter can press. The player and the AI brain use exactly the same set,
    /// so anything the player does in the gym can be copied by the model 1:1.
    /// Forward / Back / DashIn / DashOut are relative to the opponent, not to the screen.
    /// </summary>
    public enum FighterAction
    {
        Idle = 0,
        Forward = 1,
        Back = 2,
        DashIn = 3,
        DashOut = 4,
        Jab = 5,
        Heavy = 6,
        Block = 7,
        Dodge = 8
    }

    public static class FighterActions
    {
        public const int Count = 9;

        public static bool IsAttack(FighterAction a)
        {
            return a == FighterAction.Jab || a == FighterAction.Heavy;
        }

        /// <summary>
        /// Used when several actions happen inside one decision window: a press (attack, dodge, dash)
        /// outranks a hold (block, walk), which outranks doing nothing.
        /// </summary>
        public static int Priority(FighterAction a)
        {
            switch (a)
            {
                case FighterAction.Jab:
                case FighterAction.Heavy:
                case FighterAction.Dodge:
                case FighterAction.DashIn:
                case FighterAction.DashOut:
                    return 2;
                case FighterAction.Block:
                case FighterAction.Forward:
                case FighterAction.Back:
                    return 1;
                default:
                    return 0;
            }
        }
    }
}
