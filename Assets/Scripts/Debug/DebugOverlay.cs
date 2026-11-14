using FightTrainer.Core;
using FightTrainer.Game;
using FightTrainer.InputSources;
using UnityEngine;

namespace FightTrainer.DebugTools
{
    /// <summary>
    /// F1 overlay for tuning: body outline (green), the punch's reach line (yellow = startup preview,
    /// red = active), each fighter's state + frame counter, stamina, Counter Window, ring position,
    /// guard angle, and what the Rookie AI decided.
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
            float h = GreyboxGame.BodyHeight;

            for (int i = 0; i < 2; i++)
            {
                Fighter f = sim.fighters[i];
                float r = c.fighterWidth * 0.5f;

                // Body outline at the fighter's spot in the ring.
                Vector3 bl = GreyboxGame.RingToWorld(f.pos, 0f) + new Vector3(-r, 0f, 0f);
                Vector3 tr = GreyboxGame.RingToWorld(f.pos, h) + new Vector3(r, 0f, 0f);
                Outline(cam, bl, tr, new Color(0.3f, 1f, 0.4f, 0.9f));

                // Punch reach as a dotted line at shoulder height.
                AttackPhase phase = f.Phase(c);
                if (phase == AttackPhase.Startup || phase == AttackPhase.Active)
                {
                    Vec2 front, tip;
                    sim.HitSegment(f, out front, out tip);
                    Color hc = phase == AttackPhase.Active ? new Color(1f, 0.2f, 0.2f) : new Color(1f, 0.9f, 0.2f, 0.7f);
                    for (int k = 0; k <= 8; k++)
                    {
                        Vec2 p = front + (tip - front) * (k / 8f);
                        Vector3 sp = cam.WorldToScreenPoint(GreyboxGame.RingToWorld(p, h * 0.75f));
                        GUI.color = hc;
                        GUI.DrawTexture(new Rect(sp.x - 2f, Screen.height - sp.y - 2f, 4f, 4f), GreyboxArt.WhiteTexture);
                    }
                    GUI.color = Color.white;
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
                else if (f.mode == FighterMode.Walk)
                {
                    state += " " + f.walkAction;
                }

                Fighter opp = sim.Opponent(f);
                float guardOff = Vec2.SignedAngle(f.facing, sim.ToOpponent(f));
                string extra = "stamina " + Mathf.RoundToInt(f.stamina) +
                               "  · guard off " + Mathf.RoundToInt(guardOff) + "°" +
                               (f.inCorner ? "  · CORNER" : f.onRopes ? "  · ropes" : "") +
                               (f.counterWindow > 0 ? "  · counter window " + f.counterWindow : "") +
                               (opp.mode == FighterMode.Block && !sim.InsideGuard(opp, f) ? "  · OUTSIDE THEIR GUARD" : "");
                IInputSource src = game.inputs[i];
                RookieAI ai = src as RookieAI;
                string brain = ai != null ? "AI: " + ai.LastDecision + " (" + ai.LastReason + ")" : "you: " + f.attemptedAction;

                Vector3 head = cam.WorldToScreenPoint(GreyboxGame.RingToWorld(f.pos, h + 0.15f));
                GUI.Label(new Rect(head.x - 180, Screen.height - head.y - 60, 360, 60), state + "\n" + extra + "\n" + brain, label);
            }

            GUI.Label(new Rect(10, Screen.height - 110, 500, 22),
                "tick " + sim.tick + "  ·  distance " + sim.Distance.ToString("0.00") + "  ·  phase " + game.Match.phase,
                new GUIStyle(label) { alignment = TextAnchor.MiddleLeft });
        }

        private static void Outline(Camera cam, Vector3 worldMin, Vector3 worldMax, Color color)
        {
            Vector3 a = cam.WorldToScreenPoint(worldMin);
            Vector3 b = cam.WorldToScreenPoint(worldMax);
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
