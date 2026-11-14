using System;

namespace FightTrainer.Core
{
    /// <summary>
    /// Frame data and timing for one attack. All frame counts are at 60 ticks per second.
    /// startup = visible wind-up, active = can hit, recovery = punishable cool-down.
    /// </summary>
    [Serializable]
    public class MoveData
    {
        public string name = "Move";
        public int startup = 4;
        public int active = 2;
        public int recovery = 8;
        public float damage = 4f;
        public float staminaCost = 3f;
        /// <summary>How far the hit reaches past the front edge of the attacker's body (world units).</summary>
        public float reach = 0.9f;
        /// <summary>Stamina the defender loses when blocking this attack.</summary>
        public float blockStaminaDrain = 4f;
        public int hitstun = 12;
        public int blockstun = 6;
        public float knockback = 0.04f;

        public int TotalFrames { get { return startup + active + recovery; } }
    }

    /// <summary>
    /// Every tunable number for the greybox lives here. It is serializable, so you can tweak it
    /// in the Inspector on the GreyboxGame object while the game is running.
    /// </summary>
    [Serializable]
    public class FightConfig
    {
        public const int TicksPerSecond = 60;

        // Ring / bodies (world units). The ring is a square seen from above, centred on (0, 0).
        public float ringHalfSize = 4f;
        /// <summary>Body diameter (bodies are circles seen from above).</summary>
        public float fighterWidth = 0.8f;
        public float startDistance = 3f;
        /// <summary>Within this distance of a rope, a fighter counts as "on the ropes".</summary>
        public float ropeZone = 0.7f;
        /// <summary>A fighter controls the centre when this much closer to it than the opponent.</summary>
        public float centerControlMargin = 0.3f;

        // Resources
        public float maxHp = 100f;
        public float maxStamina = 100f;

        // Movement
        public float walkForwardSpeed = 0.055f;
        public float walkBackSpeed = 0.045f;
        /// <summary>Sideways speed when circling around the opponent.</summary>
        public float circleSpeed = 0.045f;
        public int dashFrames = 15;
        public float dashSpeed = 0.16f;
        public float dashStaminaCost = 6f;

        // Dodge + Counter Window
        public int dodgeFrames = 20;
        public int dodgeInvulnStart = 3;
        public int dodgeInvulnEnd = 10;
        public float dodgeStaminaCost = 8f;
        public int counterWindowFrames = 15;
        public float counterDamageMultiplier = 1.5f;

        // Turning toward the opponent (degrees per tick). Slow turning while blocking is what lets
        // circling create angles: get outside the guard and the block no longer covers you.
        public float turnRateFree = 8f;
        public float turnRateBlock = 1.0f;
        public float turnRateDodge = 4f;

        // Block
        /// <summary>A block only covers attacks arriving within this many degrees of where the fighter faces.</summary>
        public float blockArcDegrees = 70f;
        public float blockDamageMultiplier = 0.25f;
        public int guardBreakFrames = 40;

        // Stamina regeneration per tick
        public float staminaRegenIdle = 0.30f;
        public float staminaRegenBack = 0.20f;
        public float staminaRegenWalk = 0.10f;

        // Combos
        public int comboMaxHits = 3;
        /// <summary>How many recovery frames after a connected hit still allow chaining.</summary>
        public int cancelWindowRecoveryFrames = 4;

        // Rounds (boxing-style: HP carries over)
        public int roundCount = 3;
        public int roundSeconds = 60;
        public int preRoundTicks = 60;
        public int roundOverTicks = 90;
        public int cornerBreakSeconds = 15;
        /// <summary>Fraction of MISSING stamina recovered between rounds.</summary>
        public float betweenRoundStaminaRecovery = 0.5f;
        public float patchUpHp = 12f;

        // AI decisions happen every N ticks (~0.1s at 6)
        public int decisionInterval = 6;

        public MoveData jab = new MoveData
        {
            name = "Jab", startup = 4, active = 2, recovery = 8,
            damage = 1.5f, staminaCost = 3f, reach = 0.9f, blockStaminaDrain = 4f,
            hitstun = 10, blockstun = 6, knockback = 0.06f
        };

        public MoveData heavy = new MoveData
        {
            name = "Heavy", startup = 12, active = 3, recovery = 20,
            damage = 6f, staminaCost = 10f, reach = 1.15f, blockStaminaDrain = 14f,
            hitstun = 22, blockstun = 12, knockback = 0.08f
        };

        public MoveData Move(FighterAction attack)
        {
            return attack == FighterAction.Heavy ? heavy : jab;
        }

        public int RoundTicks { get { return roundSeconds * TicksPerSecond; } }
    }
}
