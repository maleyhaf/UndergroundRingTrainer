using FightTrainer.Core;
using FightTrainer.Game;
using FightTrainer.InputSources;
using UnityEngine;

namespace FightTrainer.DebugTools
{
    /// <summary>
    /// F1 overlay for tuning: hurtboxes (green), hitboxes (yellow = startup preview, red = active),
    /// each fighter's state + frame counter, stamina, Counter Window, and what the Rookie AI decided.
    /// </summary>
    public class DebugOverlay : MonoBehaviour
    {
        private GreyboxGame game;
        private GUIStyle label;

        private void Awake() { game = GetComponent<GreyboxGame>(); }

        private void OnGUI()
        {
            if (game == null || game.Match == null || !game.showDebug) return;
            Camera cam = Camera.main;
            if (cam == null) return;

            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.LowerCenter };
                label.normal.textColor = Color.white;
            }

            FightSim sim = game.Match.sim;
            FightConfig c = game.config;
            float top = GreyboxGame.GroundY + GreyboxGame.BodyHeight;

            for (int i = 0; i < 2; i++)
            {
                Fighter f = sim.fighters[i];

                FightSim.Box hurt = sim.Hurtbox(f);
                Outline(cam, hurt.min, hurt.max, GreyboxGame.GroundY, top, new Color(0.3f, 1f, 0.4f, 0.9f));

                AttackPhase phase = f.Phase(c);
                if (phase == AttackPhase.Startup || phase == AttackPhase.Active)
                {
                    FightSim.Box hit = sim.Hitbox(f);
                    Color hc = phase == AttackPhase.Active ? new Color(1f, 0.2f, 0.2f) : new Color(1f, 0.9f, 0.2f, 0.6f);
                    float y0 = GreyboxGame.GroundY + GreyboxGame.BodyHeight * 0.6f;
                    Outline(cam, hit.min, hit.max, y0, y0 + 0.5f, hc);
                }

                string state = f.mode.ToString().ToUpper();
                if (f.mode == FighterMode.Attack)
                {
                    MoveData m = c.Move(f.currentAttack);
                    state = m.name.ToUpper() + " " + phase.ToString().ToLower() + " " + f.modeFrame + "/" + m.TotalFrames +
                            (f.comboCount > 1 ? "  combo " + f.comboCount : "") + (f.attackIsCounter ? "  COUNTER" : "");
                }
                else if (f.modeDuration > 0)
                {
                    state += " " + f.modeFrame + "/" + f.modeDuration;
                }
                else if (f.mode == FighterMode.Dodge)
                {
                    state += " " + f.modeFrame + "/" + c.dodgeFrames + (f.IsInvulnerable(c) ? " INVULN" : "");
                }

                string extra = "stamina " + Mathf.RoundToInt(f.stamina);
                if (f.counterWindow > 0) extra += "  · counter window " + f.counterWindow;
                IInputSource src = game.inputs[i];
                RookieAI ai = src as RookieAI;
                string brain = ai != null ? "AI: " + ai.LastDecision + " (" + ai.LastReason + ")" : "you: " + f.attemptedAction;

                Vector3 sp = cam.WorldToScreenPoint(new Vector3(f.x, top + 0.15f, 0f));
                GUI.Label(new Rect(sp.x - 160, Screen.height - sp.y - 60, 320, 60), state + "\n" + extra + "\n" + brain, label);
            }

            GUI.Label(new Rect(10, Screen.height - 110, 400, 22),
                "tick " + sim.tick + "  ·  distance " + sim.Distance.ToString("0.00") + "  ·  phase " + game.Match.phase,
                new GUIStyle(label) { alignment = TextAnchor.MiddleLeft });
        }

        private static void Outline(Camera cam, float xMin, float xMax, float yMin, float yMax, Color color)
        {
            Vector3 a = cam.WorldToScreenPoint(new Vector3(xMin, yMin, 0f));
            Vector3 b = cam.WorldToScreenPoint(new Vector3(xMax, yMax, 0f));
            Rect r = Rect.MinMaxRect(a.x, Screen.height - b.y, b.x, Screen.height - a.y);
            const float t = 2f;
            GUI.color = color;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), GreyboxArt.WhiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), GreyboxArt.WhiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), GreyboxArt.WhiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), GreyboxArt.WhiteTexture);
            GUI.color = Color.white;
        }
    }
}
