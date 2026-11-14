using System.Collections.Generic;
using System.IO;
using FightTrainer.Core;
using FightTrainer.InputSources;
using FightTrainer.Rounds;
using FightTrainer.Training;
using UnityEngine;

namespace FightTrainer.Game
{
    /// <summary>
    /// Milestone 0 greybox: two rectangles boxing under the real rules, on a square ring.
    ///
    /// Runs the fight at a fixed 60 ticks per second, separate from rendering, feeds each fighter
    /// from an IInputSource (you or the Rookie AI), and draws the result with plain coloured boxes:
    /// a side view with depth (further back in the ring = higher on screen) plus a top-down minimap.
    ///
    /// Hotkeys: F1 debug overlay · F2 slow motion · P pause · F5 new fight · M minimap size ·
    ///          Tab swap you <-> Rookie AI (watch AI vs AI) · R start/stop recording
    /// </summary>
    public class GreyboxGame : MonoBehaviour
    {
        [Tooltip("All tunable numbers. Changes apply immediately, even mid-fight.")]
        public FightConfig config = new FightConfig();

        public string fighterName = "fighter_1";
        public string[] displayNames = { "You", "Rookie" };
        public bool showDebug = true;

        public FightMatch Match { get; private set; }
        public readonly IInputSource[] inputs = new IInputSource[2];
        public KeyboardInput KeyboardSource { get; private set; }
        public RookieAI RookieForPlayerSlot { get; private set; }
        public RookieAI RookieOpponent { get; private set; }
        public TrainingRecorder Recorder { get; private set; }
        public bool Paused { get; private set; }
        public bool SlowMotion { get; private set; }

        public enum MinimapMode { Normal, Large, Hidden }
        public MinimapMode Minimap { get; private set; }

        /// <summary>Screen height of the ring's centre line.</summary>
        public const float GroundY = -1.6f;
        /// <summary>How much the ring's depth is squashed in the side view.</summary>
        public const float DepthScale = 0.32f;
        public const float BodyHeight = 1.8f;

        /// <summary>Ring position (seen from above) -> world position in the side view.</summary>
        public static Vector3 RingToWorld(Vec2 ring, float height)
        {
            return new Vector3(ring.x, GroundY + ring.y * DepthScale + height, 0f);
        }

        public class Flash
        {
            public string text;
            public Vec2 pos;
            public int ticksLeft;
            public Color color;
        }

        public readonly List<Flash> flashes = new List<Flash>();

        private const float TickSeconds = 1f / FightConfig.TicksPerSecond;
        private float accumulator;
        private FighterView[] views;

        // ------------------------------------------------------------------ setup

        private void Awake()
        {
            Recorder = new TrainingRecorder();
            KeyboardSource = new KeyboardInput();
            RookieForPlayerSlot = new RookieAI(1000);
            RookieOpponent = new RookieAI(2000);
            inputs[0] = KeyboardSource;
            inputs[1] = RookieOpponent;

            Match = new FightMatch(config);
            Match.sim.OnEvent += OnFightEvent;

            BuildScene();
            if (GetComponent<FightHud>() == null) gameObject.AddComponent<FightHud>();
            if (GetComponent<FightTrainer.DebugTools.DebugOverlay>() == null)
                gameObject.AddComponent<FightTrainer.DebugTools.DebugOverlay>();
        }

        private void Start()
        {
            NewFight();
        }

        private void OnDisable()
        {
            Recorder.End();
        }

        private void BuildScene()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
            }
            cam.orthographic = true;
            cam.orthographicSize = 3.5f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.backgroundColor = new Color(0.06f, 0.05f, 0.08f);
            cam.clearFlags = CameraClearFlags.SolidColor;

            Transform root = new GameObject("Arena").transform;
            root.SetParent(transform, false);

            // Ring: canvas floor, apron, corner posts, ropes. Back ropes behind the fighters,
            // front ropes in front of them (see-through so the fight stays readable).
            float r = config.ringHalfSize;
            float depth = r * 2f * DepthScale;
            Color rope = new Color(0.75f, 0.15f, 0.2f);
            Color post = new Color(0.35f, 0.35f, 0.4f);
            Box(root, "Apron", new Vector2(0f, GroundY - r * DepthScale - 0.25f), new Vector2(r * 2f + 1f, 0.5f), new Color(0.12f, 0.1f, 0.13f), -20);
            Box(root, "Canvas", new Vector2(0f, GroundY), new Vector2(r * 2f, depth), new Color(0.22f, 0.2f, 0.24f), -20);
            Box(root, "Centre mark", new Vector2(0f, GroundY), new Vector2(0.3f, 0.3f * DepthScale), new Color(0.3f, 0.28f, 0.33f), -19);
            float backY = GroundY + r * DepthScale, frontY = GroundY - r * DepthScale;
            foreach (float sx in new[] { -r, r })
            {
                Box(root, "Post back", new Vector2(sx, backY + 0.6f), new Vector2(0.12f, 1.2f), post, -18);
                Box(root, "Post front", new Vector2(sx, frontY + 0.6f), new Vector2(0.14f, 1.2f), post, 300);
            }
            foreach (float hy in new[] { 0.45f, 0.85f, 1.15f })
            {
                Box(root, "Rope back", new Vector2(0f, backY + hy), new Vector2(r * 2f, 0.05f), rope, -18);
                Box(root, "Rope front", new Vector2(0f, frontY + hy), new Vector2(r * 2f, 0.06f), new Color(rope.r, rope.g, rope.b, 0.35f), 300);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                // Side ropes run "into" the screen: drawn as slanted lines from front post to back post.
                for (int k = 0; k < 3; k++)
                {
                    float hy = k == 0 ? 0.45f : (k == 1 ? 0.85f : 1.15f);
                    SpriteRenderer sr = Box(root, "Rope side", new Vector2(side * r, GroundY + hy), new Vector2(0.04f, depth), new Color(rope.r, rope.g, rope.b, 0.5f), -17);
                    sr.transform.localScale = new Vector3(0.04f, depth, 1f);
                }
            }

            views = new[]
            {
                new FighterView(root, "Fighter 0 (" + displayNames[0] + ")", new Color(0.25f, 0.55f, 1f)),
                new FighterView(root, "Fighter 1 (" + displayNames[1] + ")", new Color(0.65f, 0.65f, 0.65f))
            };
        }

        public static SpriteRenderer Box(Transform parent, string name, Vector2 pos, Vector2 size, Color color, int order)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GreyboxArt.WhiteSprite;
            if (GreyboxArt.SpriteMaterial != null) sr.sharedMaterial = GreyboxArt.SpriteMaterial;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        // ------------------------------------------------------------------ loop

        public void NewFight()
        {
            Recorder.End();
            flashes.Clear();
            KeyboardSource.ClearBuffer();
            Match.StartFight();
        }

        private void Update()
        {
            KeyboardSource.Poll();
            HandleHotkeys();

            if (!Paused)
            {
                accumulator += Time.deltaTime * (SlowMotion ? 0.25f : 1f);
                int steps = 0;
                while (accumulator >= TickSeconds && steps < 5)
                {
                    StepTick();
                    accumulator -= TickSeconds;
                    steps++;
                }
                if (steps == 5) accumulator = 0f; // don't spiral after a hitch
            }

            for (int i = 0; i < 2; i++) views[i].Sync(Match.sim.fighters[i], config);
        }

        private void StepTick()
        {
            FightSim sim = Match.sim;

            if (Match.phase == MatchPhase.CornerBreak)
            {
                for (int i = 0; i < 2; i++)
                    if (!inputs[i].IsHuman) Match.SetCornerChoice(i, inputs[i].ChooseCorner(Match, i));
            }

            bool live = Match.phase == MatchPhase.Fighting;
            FighterAction a0 = FighterAction.Idle, a1 = FighterAction.Idle;
            if (live)
            {
                a0 = inputs[0].GetAction(Match, 0);
                a1 = inputs[1].GetAction(Match, 1);
                if (Recorder.IsRecording && inputs[0].IsHuman) Recorder.Observe(Match, a0);
            }
            else
            {
                KeyboardSource.ClearBuffer();
            }

            Match.Tick(a0, a1);

            if (live)
            {
                inputs[0].OnActionResult(a0, sim.fighters[0].actionAccepted);
                inputs[1].OnActionResult(a1, sim.fighters[1].actionAccepted);
            }

            for (int i = flashes.Count - 1; i >= 0; i--)
                if (--flashes[i].ticksLeft <= 0) flashes.RemoveAt(i);
        }

        private void HandleHotkeys()
        {
            UnityEngine.InputSystem.Keyboard kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;

            if (kb.f1Key.wasPressedThisFrame) showDebug = !showDebug;
            if (kb.f2Key.wasPressedThisFrame) SlowMotion = !SlowMotion;
            if (kb.pKey.wasPressedThisFrame) Paused = !Paused;
            if (kb.f5Key.wasPressedThisFrame) NewFight();
            if (kb.mKey.wasPressedThisFrame) Minimap = (MinimapMode)(((int)Minimap + 1) % 3);

            if (kb.tabKey.wasPressedThisFrame)
            {
                bool toAI = inputs[0] == KeyboardSource;
                if (toAI) Recorder.End();
                inputs[0] = toAI ? (IInputSource)RookieForPlayerSlot : KeyboardSource;
                displayNames[0] = toAI ? "Rookie (P1)" : "You";
            }

            if (kb.rKey.wasPressedThisFrame)
            {
                if (Recorder.IsRecording)
                {
                    Recorder.End();
                    Debug.Log("[Greybox] Recording saved: " + Recorder.FilePath + " (" + Recorder.SampleCount + " samples)");
                }
                else if (inputs[0].IsHuman)
                {
                    string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Recordings"));
                    Recorder.Begin(folder, inputs[0].SourceTag, 0, fighterName);
                    Debug.Log("[Greybox] Recording to " + Recorder.FilePath);
                }
            }
        }

        private void OnFightEvent(FightEvent e)
        {
            Fighter target = e.target >= 0 ? Match.sim.fighters[e.target] : Match.sim.fighters[e.actor];
            switch (e.type)
            {
                case FightEventType.CounterHit: AddFlash("COUNTER!", target.pos, new Color(1f, 0.85f, 0.2f)); break;
                case FightEventType.AngleHit: AddFlash("ANGLE!", target.pos, new Color(1f, 0.6f, 0.2f)); break;
                case FightEventType.GuardBreak: AddFlash("GUARD BREAK", target.pos, new Color(1f, 0.3f, 0.9f)); break;
                case FightEventType.Dodged: AddFlash("DODGE", Match.sim.fighters[e.actor].pos, new Color(0.4f, 1f, 0.9f)); break;
                case FightEventType.KO: AddFlash("KO", target.pos, new Color(1f, 0.25f, 0.2f)); break;
            }
        }

        private void AddFlash(string text, Vec2 pos, Color color)
        {
            flashes.Add(new Flash { text = text, pos = pos, ticksLeft = 45, color = color });
        }
    }
}
