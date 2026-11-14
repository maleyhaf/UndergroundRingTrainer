namespace FightTrainer.Core
{
    /// <summary>
    /// The buttons a fighter can press. The player and the AI brain use exactly the same set,
    /// so anything the player does in the gym can be copied by the model 1:1.
    ///
    /// Every movement is relative to the opponent, not the screen:
    ///   Forward / Back        = toward / away from the opponent
    ///   CircleLeft / Right    = sidestep around the opponent while facing them
    ///   DashIn / DashOut      = burst toward / away from the opponent
    ///
    /// (v2: CircleLeft / CircleRight added for 2D ring movement; numbering changed from v1.)
    /// </summary>
    public enum FighterAction
    {
        Idle = 0,
        Forward = 1,
        Back = 2,
        CircleLeft = 3,
        CircleRight = 4,
        DashIn = 5,
        DashOut = 6,
        Jab = 7,
        Heavy = 8,
        Block = 9,
        Dodge = 10
    }

    public static class FighterActions
    {
        public const int Count = 11;

        public static bool IsAttack(FighterAction a)
        {
            return a == FighterAction.Jab || a == FighterAction.Heavy;
        }

        public static bool IsWalk(FighterAction a)
        {
            return a == FighterAction.Forward || a == FighterAction.Back
                || a == FighterAction.CircleLeft || a == FighterAction.CircleRight;
        }

        /// <summary>
        /// Used when several actions happen inside one decision window: a press (attack, dodge, dash)
        /// outranks a hold (block, walk, circle), which outranks doing nothing.
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
                case FighterAction.CircleLeft:
                case FighterAction.CircleRight:
                    return 1;
                default:
                    return 0;
            }
        }
    }
}
