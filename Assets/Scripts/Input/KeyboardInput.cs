using FightTrainer.Core;
using FightTrainer.Rounds;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FightTrainer.InputSources
{
    /// <summary>
    /// You, on the keyboard (greybox controls; touch controls come later).
    ///   A / D  or  Left / Right : walk back / forward   (double-tap, or Shift + direction = dash)
    ///   J : Jab     K : Heavy     L (hold) : Block     Space : Dodge
    ///
    /// Presses (attacks, dodge, dash) are buffered for a few ticks so pressing slightly early still
    /// works, e.g. queueing the next jab of a combo while the first one is still landing.
    /// </summary>
    public class KeyboardInput : IInputSource
    {
        public string SourceTag { get { return "show"; } }
        public bool IsHuman { get { return true; } }

        private const float DoubleTapWindow = 0.25f;
        private const int BufferTicks = 6;

        private FighterAction bufferedPress = FighterAction.Idle; // Jab, Heavy, Dodge, or DashIn as "dash" marker
        private int bufferedDashWorldDir;
        private int bufferLeft;
        private FighterAction lastResolvedPress = FighterAction.Idle;

        private float lastLeftTap = -10f;
        private float lastRightTap = -10f;

        /// <summary>Call every rendered frame (Update) so no key press is missed between ticks.</summary>
        public void Poll()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            float t = Time.unscaledTime;
            bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;

            if (kb.jKey.wasPressedThisFrame) Buffer(FighterAction.Jab, 0);
            if (kb.kKey.wasPressedThisFrame) Buffer(FighterAction.Heavy, 0);
            if (kb.spaceKey.wasPressedThisFrame) Buffer(FighterAction.Dodge, 0);

            if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame)
            {
                if (shift || t - lastLeftTap <= DoubleTapWindow) Buffer(FighterAction.DashIn, -1);
                lastLeftTap = t;
            }
            if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame)
            {
                if (shift || t - lastRightTap <= DoubleTapWindow) Buffer(FighterAction.DashIn, +1);
                lastRightTap = t;
            }
        }

        public void ClearBuffer()
        {
            bufferLeft = 0;
        }

        private void Buffer(FighterAction press, int dashWorldDir)
        {
            bufferedPress = press;
            bufferedDashWorldDir = dashWorldDir;
            bufferLeft = BufferTicks;
        }

        public FighterAction GetAction(FightMatch match, int fighterIndex)
        {
            Fighter f = match.sim.fighters[fighterIndex];

            if (bufferLeft > 0)
            {
                bufferLeft--;
                FighterAction a = bufferedPress;
                if (a == FighterAction.DashIn)
                    a = bufferedDashWorldDir == f.facing ? FighterAction.DashIn : FighterAction.DashOut;
                lastResolvedPress = a;
                return a;
            }

            Keyboard kb = Keyboard.current;
            if (kb == null) return FighterAction.Idle;

            if (kb.lKey.isPressed) return FighterAction.Block;

            int dir = 0;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) dir -= 1;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) dir += 1;
            if (dir != 0) return dir == f.facing ? FighterAction.Forward : FighterAction.Back;

            return FighterAction.Idle;
        }

        public void OnActionResult(FighterAction action, bool accepted)
        {
            // Once the buffered press actually happened, stop repeating it.
            if (accepted && bufferLeft > 0 && action == lastResolvedPress) bufferLeft = 0;
        }

        public CornerAction ChooseCorner(FightMatch match, int fighterIndex)
        {
            return CornerAction.None; // chosen with the corner-break buttons
        }
    }
}
