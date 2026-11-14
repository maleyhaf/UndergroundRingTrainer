using FightTrainer.Core;
using FightTrainer.Rounds;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FightTrainer.InputSources
{
    /// <summary>
    /// You, on the keyboard (greybox controls).
    ///   W A S D  or  arrow keys : move around the ring (up = toward the back of the ring)
    ///   double-tap a direction, or Shift + direction : dash
    ///   J : Jab     K : Heavy     L (hold) : Block     Space : Dodge
    ///
    /// You press screen directions; they are translated into the fighter's opponent-relative buttons
    /// (Forward, Back, Circle Left, Circle Right, Dash In, Dash Out) depending on where the opponent is.
    /// That's what gets recorded, so the model learns "circle left", not "press W".
    ///
    /// Presses (attacks, dodge, dash) are buffered for a few ticks so pressing slightly early still works.
    /// </summary>
    public class KeyboardInput : IInputSource
    {
        public string SourceTag { get { return "show"; } }
        public bool IsHuman { get { return true; } }

        private const float DoubleTapWindow = 0.25f;
        private const int BufferTicks = 6;

        private enum Press { None, Jab, Heavy, Dodge, Dash }

        private Press bufferedPress = Press.None;
        private Vec2 bufferedDashDir;
        private int bufferLeft;
        private FighterAction lastResolvedPress = FighterAction.Idle;

        private readonly float[] lastTap = { -10f, -10f, -10f, -10f }; // left, right, up, down

        /// <summary>Call every rendered frame (Update) so no key press is missed between ticks.</summary>
        public void Poll()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            float t = Time.unscaledTime;
            bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;

            if (kb.jKey.wasPressedThisFrame) Buffer(Press.Jab, Vec2.Zero);
            if (kb.kKey.wasPressedThisFrame) Buffer(Press.Heavy, Vec2.Zero);
            if (kb.spaceKey.wasPressedThisFrame) Buffer(Press.Dodge, Vec2.Zero);

            Tap(0, kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame, new Vec2(-1f, 0f), shift, t);
            Tap(1, kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame, new Vec2(1f, 0f), shift, t);
            Tap(2, kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame, new Vec2(0f, 1f), shift, t);
            Tap(3, kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame, new Vec2(0f, -1f), shift, t);
        }

        private void Tap(int slot, bool pressed, Vec2 dir, bool shift, float t)
        {
            if (!pressed) return;
            if (shift || t - lastTap[slot] <= DoubleTapWindow) Buffer(Press.Dash, dir);
            lastTap[slot] = t;
        }

        public void ClearBuffer()
        {
            bufferLeft = 0;
        }

        private void Buffer(Press press, Vec2 dashDir)
        {
            bufferedPress = press;
            bufferedDashDir = dashDir;
            bufferLeft = BufferTicks;
        }

        public FighterAction GetAction(FightMatch match, int fighterIndex)
        {
            Fighter f = match.sim.fighters[fighterIndex];
            Vec2 u = match.sim.ToOpponent(f);

            if (bufferLeft > 0)
            {
                bufferLeft--;
                FighterAction a;
                switch (bufferedPress)
                {
                    case Press.Jab: a = FighterAction.Jab; break;
                    case Press.Heavy: a = FighterAction.Heavy; break;
                    case Press.Dodge: a = FighterAction.Dodge; break;
                    default: a = Vec2.Dot(bufferedDashDir, u) > 0f ? FighterAction.DashIn : FighterAction.DashOut; break;
                }
                lastResolvedPress = a;
                return a;
            }

            Keyboard kb = Keyboard.current;
            if (kb == null) return FighterAction.Idle;

            if (kb.lKey.isPressed) return FighterAction.Block;

            Vec2 dir = Vec2.Zero;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) dir.x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) dir.x += 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) dir.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) dir.y -= 1f;
            if (dir.x == 0f && dir.y == 0f) return FighterAction.Idle;

            return ToRelativeMove(dir.Normalized, u);
        }

        /// <summary>Pick the opponent-relative move closest to the direction being pressed.</summary>
        public static FighterAction ToRelativeMove(Vec2 dir, Vec2 toOpponent)
        {
            float forward = Vec2.Dot(dir, toOpponent);
            float left = Vec2.Dot(dir, toOpponent.Left);
            if (Mathf.Abs(forward) >= Mathf.Abs(left))
                return forward > 0f ? FighterAction.Forward : FighterAction.Back;
            return left > 0f ? FighterAction.CircleLeft : FighterAction.CircleRight;
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
