using FightTrainer.Core;
using FightTrainer.Rounds;
using UnityEngine;

namespace FightTrainer.Game
{
    /// <summary>
    /// Greybox HUD (IMGUI, placeholder until the real pixel-art UI): health + stamina bars,
    /// round counter and timer, judge scorecard, round banners, corner break buttons, result screen.
    /// Laid out on a 1280×720 canvas scaled to the window.
    /// </summary>
    public class FightHud : MonoBehaviour
    {
        private GreyboxGame game;
        private GUIStyle center, left, right, big, small, button;

        private const float W = 1280f, H = 720f;

        private void Awake() { game = GetComponent<GreyboxGame>(); }

        private void Styles()
        {
            if (center != null) return;
            center = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 20 };
            center.normal.textColor = Color.white;
            left = new GUIStyle(center) { alignment = TextAnchor.MiddleLeft, fontSize = 18 };
            right = new GUIStyle(center) { alignment = TextAnchor.MiddleRight, fontSize = 18 };
            big = new GUIStyle(center) { fontSize = 56, fontStyle = FontStyle.Bold };
            small = new GUIStyle(center) { fontSize = 14 };
            small.normal.textColor = new Color(1f, 1f, 1f, 0.7f);
            button = new GUIStyle(GUI.skin.button) { fontSize = 20 };
        }

        private void OnGUI()
        {
            if (game == null || game.Match == null) return;
            Styles();
            float scale = Screen.height / H;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            float w = Screen.width / scale; // canvas width adapts to aspect ratio

            FightMatch m = game.Match;
            FightConfig c = game.config;
            Fighter f0 = m.sim.fighters[0], f1 = m.sim.fighters[1];

            // Bars
            float barW = w * 0.36f;
            Bar(new Rect(30, 30, barW, 26), f0.hp / c.maxHp, new Color(0.3f, 0.85f, 0.35f), false);
            Bar(new Rect(w - 30 - barW, 30, barW, 26), f1.hp / c.maxHp, new Color(0.3f, 0.85f, 0.35f), true);
            Bar(new Rect(30, 62, barW * 0.7f, 10), f0.stamina / c.maxStamina, new Color(1f, 0.8f, 0.2f), false);
            Bar(new Rect(w - 30 - barW * 0.7f, 62, barW * 0.7f, 10), f1.stamina / c.maxStamina, new Color(1f, 0.8f, 0.2f), true);
            GUI.Label(new Rect(30, 76, 300, 24), game.displayNames[0] + "  " + Mathf.CeilToInt(f0.hp), left);
            GUI.Label(new Rect(w - 330, 76, 300, 24), Mathf.CeilToInt(f1.hp) + "  " + game.displayNames[1], right);

            // Round + timer
            int secs = Mathf.CeilToInt(m.roundTicksLeft / (float)FightConfig.TicksPerSecond);
            GUI.Label(new Rect(w * 0.5f - 100, 20, 200, 30), "Round " + m.round + " / " + c.roundCount, center);
            GUI.Label(new Rect(w * 0.5f - 100, 46, 200, 40), secs.ToString(), new GUIStyle(center) { fontSize = 34 });

            // Scorecard
            if (m.judge.rounds.Count > 0)
            {
                string card = "";
                foreach (JudgeScorer.RoundScore s in m.judge.rounds) card += "  R" + s.round + " " + s.score0 + "-" + s.score1;
                GUI.Label(new Rect(w * 0.5f - 250, 86, 500, 24), "Card:" + card, small);
            }

            // Phase banners
            switch (m.phase)
            {
                case MatchPhase.PreRound:
                    bool fight = m.phaseTicks > c.preRoundTicks - 20;
                    GUI.Label(new Rect(0, H * 0.35f, w, 80), fight ? "FIGHT!" : "Round " + m.round, big);
                    break;
                case MatchPhase.RoundOver:
                    GUI.Label(new Rect(0, H * 0.35f, w, 80), "End of round " + m.round, big);
                    break;
                case MatchPhase.CornerBreak:
                    CornerBreak(m, w);
                    break;
                case MatchPhase.FightOver:
                    GUI.Label(new Rect(0, H * 0.33f, w, 80), m.ResultText(game.displayNames[0], game.displayNames[1]), new GUIStyle(big) { fontSize = 40 });
                    GUI.Label(new Rect(0, H * 0.33f + 70, w, 30), "Press F5 for a new fight", center);
                    break;
            }

            // Event flashes (COUNTER!, GUARD BREAK, ...)
            Camera cam = Camera.main;
            if (cam != null)
            {
                foreach (GreyboxGame.Flash fl in game.flashes)
                {
                    Vector3 sp = cam.WorldToScreenPoint(new Vector3(fl.x, GreyboxGame.GroundY + GreyboxGame.BodyHeight + 0.6f, 0f));
                    float rise = (45 - fl.ticksLeft) * 0.6f;
                    GUIStyle st = new GUIStyle(center) { fontSize = 26, fontStyle = FontStyle.Bold };
                    st.normal.textColor = fl.color;
                    GUI.Label(new Rect(sp.x / scale - 100, (Screen.height - sp.y) / scale - 20 - rise, 200, 40), fl.text, st);
                }
            }

            // Footer: controls + status
            string status = "";
            if (game.Recorder.IsRecording) status += "<color=#ff5555>● REC " + game.Recorder.SampleCount + " samples</color>   ";
            if (game.Paused) status += "PAUSED   ";
            if (game.SlowMotion) status += "SLOW-MO   ";
            GUIStyle rich = new GUIStyle(small) { richText = true };
            GUI.Label(new Rect(0, H - 56, w, 22), status, rich);
            GUI.Label(new Rect(0, H - 32, w, 22),
                "A/D move (double-tap = dash)  ·  J jab  ·  K heavy  ·  L block  ·  Space dodge  ·  " +
                "F1 debug  ·  F2 slow-mo  ·  P pause  ·  R record  ·  Tab AI vs AI  ·  F5 new fight", small);
        }

        private void CornerBreak(FightMatch m, float w)
        {
            Rect panel = new Rect(w * 0.5f - 300, 170, 600, 330);
            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.DrawTexture(panel, GreyboxArt.WhiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(panel.x, panel.y + 15, panel.width, 36), "Corner break — after round " + m.round, new GUIStyle(center) { fontSize = 26 });

            Fighter a = m.sim.fighters[0], b = m.sim.fighters[1];
            string stats =
                game.displayNames[0] + ":  hits " + a.hitsLandedThisRound + " · blocks " + a.blocksThisRound + " · dodges " + a.dodgesThisRound + "\n" +
                game.displayNames[1] + ":  hits " + b.hitsLandedThisRound + " · blocks " + b.blocksThisRound + " · dodges " + b.dodgesThisRound;
            GUI.Label(new Rect(panel.x, panel.y + 60, panel.width, 60), stats, new GUIStyle(center) { fontSize = 17 });

            if (m.judge.rounds.Count > 0)
            {
                JudgeScorer.RoundScore last = m.judge.rounds[m.judge.rounds.Count - 1];
                string verdict = last.score0 > last.score1 ? game.displayNames[0] + " probably won that round"
                               : last.score1 > last.score0 ? game.displayNames[1] + " probably won that round"
                               : "That round looked even";
                GUI.Label(new Rect(panel.x, panel.y + 125, panel.width, 30), verdict, center);
            }

            if (game.inputs[0].IsHuman)
            {
                if (m.cornerChoice[0] == CornerAction.None)
                {
                    if (GUI.Button(new Rect(panel.x + 50, panel.y + 175, 235, 60), "Patch Up\n+" + game.config.patchUpHp + " HP", button))
                        m.SetCornerChoice(0, CornerAction.PatchUp);
                    if (GUI.Button(new Rect(panel.x + 315, panel.y + 175, 235, 60), "Catch Breath\nfull stamina", button))
                        m.SetCornerChoice(0, CornerAction.CatchBreath);
                }
                else
                {
                    GUI.Label(new Rect(panel.x, panel.y + 190, panel.width, 30), "You chose: " + m.cornerChoice[0], center);
                }
            }

            int secs = Mathf.CeilToInt(m.CornerTicksLeft / (float)FightConfig.TicksPerSecond);
            GUI.Label(new Rect(panel.x, panel.y + 270, panel.width, 30), "Next round in " + secs + "s", small);
        }

        private static void Bar(Rect r, float fill, Color color, bool fromRight)
        {
            fill = Mathf.Clamp01(fill);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(r, GreyboxArt.WhiteTexture);
            GUI.color = color;
            float fw = r.width * fill;
            GUI.DrawTexture(new Rect(fromRight ? r.xMax - fw : r.x, r.y, fw, r.height), GreyboxArt.WhiteTexture);
            GUI.color = Color.white;
        }
    }
}
