using FightTrainer.Core;
using FightTrainer.Rounds;

namespace FightTrainer.InputSources
{
    /// <summary>
    /// "Who is pressing this fighter's buttons?" A fighter never knows or cares.
    ///
    /// Milestone 0 sources: KeyboardInput (you) and RookieAI (rule-based fallback brain).
    /// Later: MLBrain (the trained model) and a ControlArbiter that switches between the AI and
    /// the player for Take Over mode. Show / Spar / Take Over are just different wiring of these.
    /// </summary>
    public interface IInputSource
    {
        /// <summary>Short tag stored with recorded data, e.g. "show", "rookie", "model".</summary>
        string SourceTag { get; }

        /// <summary>True if a human is behind this source (gets the corner-break buttons).</summary>
        bool IsHuman { get; }

        /// <summary>Called once per fight tick. Return the button to press this tick.</summary>
        FighterAction GetAction(FightMatch match, int fighterIndex);

        /// <summary>Told after each tick whether the fighter actually did the action (lets buffers clear).</summary>
        void OnActionResult(FighterAction action, bool accepted);

        /// <summary>Corner break choice. Humans return None and choose through the UI instead.</summary>
        CornerAction ChooseCorner(FightMatch match, int fighterIndex);
    }
}
