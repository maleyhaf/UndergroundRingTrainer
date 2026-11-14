using FightTrainer.Core;
using FightTrainer.Rounds;
using UnityEngine;

namespace FightTrainer.Game
{
    /// <summary>
    /// Greybox HUD (IMGUI, placeholder until the real pixel-art UI): health + stamina bars,
    /// round counter and timer, judge scorecard, round banners, corner break buttons, result screen,
    /// and the top-down ring minimap.
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
                    Vector3 sp = cam.WorldToScreenPoint(GreyboxGame.RingToWorld(fl.pos, GreyboxGame.BodyHeight + 0.6f));
                    float rise = (45 - fl.ticksLeft) * 0.6f;
                    GUIStyle st = new GUIStyle(center) { fontSize = 26, fontStyle = FontStyle.Bold };
                    st.normal.textColor = fl.color;
                    GUI.Label(new Rect(sp.x / scale - 100, (Screen.height - sp.y) / scale - 20 - rise, 200, 40), fl.text, st);
                }
            }

            if (game.Minimap != GreyboxGame.MinimapMode.Hidden && m.phase != MatchPhase.CornerBreak)
                Minimap(m, w, game.Minimap == GreyboxGame.MinimapMode.Large);

            // Footer: controls + status
            string status = "";
            if (game.Recorder.IsRecording) status += "<color=#ff5555>● REC " + game.Recorder.SampleCount + " samples</color>   ";
            if (game.Paused) status += "PAUSED   ";
            if (game.SlowMotion) status += "SLOW-MO   ";
            GUIStyle rich = new GUIStyle(small) { richText = true };
            GUI.Label(new Rect(0, H - 56, w, 22), status, rich);
            GUI.Label(new Rect(0, H - 32, w, 22),
                "WASD move (double-tap = dash)  ·  J jab  ·  K heavy  ·  L block  ·  Space dodge  ·  " +
                "M minimap  ·  F1 debug  ·  F2 slow-mo  ·  P pause  ·  R record  ·  Tab AI vs AI  ·  F5 new fight", small);
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
                StatLine(game.displayNames[0], a) + "\n" +
                StatLine(game.displayNames[1], b);
            GUI.Label(new Rect(panel.x, panel.y + 50, panel.width, 82), stats, new GUIStyle(center) { fontSize = 15 });

            if (m.judge.rounds.Count > 0)
            {
                JudgeScorer.RoundScore last = m.judge.rounds[m.judge.rounds.Count - 1];
                string verdict = last.score0 > last.score1 ? game.displayNames[0] + " probably won that round"
                               : last.score1 > last.score0 ? game.displayNames[1] + " probably won that round"
                               : "That round looked even";
                if (last.decidedByRingControl) verdict += " (on ring control)";
                GUI.Label(new Rect(panel.x, panel.y + 135, panel.width, 30), verdict, center);
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

        private static string StatLine(string name, Fighter f)
        {
            const float tps = FightConfig.TicksPerSecond;
            return name + ":  hits " + f.hitsLandedThisRound + " (" + f.angleHitsThisRound + " from angles) · blocks " + f.blocksThisRound +
                   " · dodges " + f.dodgesThisRound + "\n      centre " + Mathf.RoundToInt(f.ticksCenterControlThisRound / tps) + "s" +
                   " · on the ropes " + Mathf.RoundToInt(f.ticksOnRopesThisRound / tps) + "s" +
                   " · in a corner " + Mathf.RoundToInt(f.ticksInCornerThisRound / tps) + "s";
        }

        /// <summary>
        /// Top-down view of the ring: ropes, centre, both fighters with the direction their guard faces,
        /// and live ring-position readouts (on the ropes / in a corner / who controls the centre).
        /// </summary>
        private void Minimap(FightMatch m, float w, bool large)
        {
            FightConfig c = game.config;
            float size = large ? 300f : 170f;
            Rect panel = new Rect(w - size - 24f, H - size - 92f, size, size);
            float pad = 12f;
            Rect ring = new Rect(panel.x + pad, panel.y + pad, panel.width - pad * 2f, panel.height - pad * 2f);
            float half = ring.width * 0.5f;
            Vector2 centre = ring.center;

            Fill(panel, new Color(0f, 0f, 0f, 0.55f));
            Fill(ring, new Color(0.22f, 0.2f, 0.24f, 0.95f));

            // Rope zone shading (where a fighter counts as "on the ropes").
            float zone = (c.ropeZone + c.fighterWidth * 0.5f) / c.ringHalfSize * half;
            Color zc = new Color(0.75f, 0.15f, 0.2f, 0.18f);
            Fill(new Rect(ring.x, ring.y, ring.width, zone), zc);
            Fill(new Rect(ring.x, ring.yMax - zone, ring.width, zone), zc);
            Fill(new Rect(ring.x, ring.y + zone, zone, ring.height - zone * 2f), zc);
            Fill(new Rect(ring.xMax - zone, ring.y + zone, zone, ring.height - zone * 2f), zc);

            // Ropes and posts.
            Color rope = new Color(0.85f, 0.2f, 0.25f);
            Fill(new Rect(ring.x, ring.y, ring.width, 2f), rope);
            Fill(new Rect(ring.x, ring.yMax - 2f, ring.width, 2f), rope);
            Fill(new Rect(ring.x, ring.y, 2f, ring.height), rope);
            Fill(new Rect(ring.xMax - 2f, ring.y, 2f, ring.height), rope);
            foreach (Vector2 corner in new[] { new Vector2(ring.x, ring.y), new Vector2(ring.xMax, ring.y), new Vector2(ring.x, ring.yMax), new Vector2(ring.xMax, ring.yMax) })
                Fill(new Rect(corner.x - 4f, corner.y - 4f, 8f, 8f), new Color(0.6f, 0.6f, 0.65f));
            Fill(new Rect(centre.x - 3f, centre.y - 3f, 6f, 6f), new Color(1f, 1f, 1f, 0.25f));

            // Fighters.
            Color[] colors = { new Color(0.25f, 0.55f, 1f), new Color(0.75f, 0.75f, 0.75f) };
            float scale = half / c.ringHalfSize;
            for (int i = 0; i < 2; i++)
            {
                Fighter f = m.sim.fighters[i];
                Vector2 p = new Vector2(centre.x + f.pos.x * scale, centre.y - f.pos.y * scale);
                float dot = Mathf.Max(8f, c.fighterWidth * scale);

                // Guard direction: a short line of dots; brighter while blocking.
                bool blocking = f.mode == FighterMode.Block || f.mode == FighterMode.Blockstun;
                Color guardCol = blocking ? Color.white : new Color(colors[i].r, colors[i].g, colors[i].b, 0.8f);
                for (int k = 1; k <= 5; k++)
                {
                    float d = dot * 0.5f + k * 3f;
                    Fill(new Rect(p.x + f.facing.x * d - 1.5f, p.y - f.facing.y * d - 1.5f, 3f, 3f), guardCol);
                }

                Color body = f.IsKO ? colors[i] * 0.4f : colors[i];
                if (f.mode == FighterMode.Attack && f.Phase(c) == AttackPhase.Active) body = new Color(1f, 0.3f, 0.2f);
                Fill(new Rect(p.x - dot * 0.5f, p.y - dot * 0.5f, dot, dot), body);
            }

            // Live readouts under the map.
            Fighter a = m.sim.fighters[0], b = m.sim.fighters[1];
            string status = RingStatus(game.displayNames[0], a) + "   " + RingStatus(game.displayNames[1], b);
            int ca = a.ticksCenterControlThisRound, cb = b.ticksCenterControlThisRound;
            string control = ca + cb == 0 ? "Centre: even"
                           : "Centre: " + game.displayNames[0] + " " + Mathf.RoundToInt(100f * ca / (ca + cb)) + "% · " +
                             game.displayNames[1] + " " + Mathf.RoundToInt(100f * cb / (ca + cb)) + "%";
            GUIStyle st = new GUIStyle(small) { alignment = TextAnchor.UpperRight };
            GUI.Label(new Rect(panel.xMax - 400f, panel.yMax + 4f, 400f, 20f), status, st);
            GUI.Label(new Rect(panel.xMax - 400f, panel.yMax + 22f, 400f, 20f), control, st);
        }

        private static string RingStatus(string name, Fighter f)
        {
            if (f.inCorner) return name + ": CORNER";
            if (f.onRopes) return name + ": ropes";
            return name + ": open";
        }

        private static void Fill(Rect r, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(r, GreyboxArt.WhiteTexture);
            GUI.color = Color.white;
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
