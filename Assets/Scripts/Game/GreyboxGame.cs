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
    /// Milestone 0 greybox: two rectangles boxing under the real rules.
    ///
    /// Runs the fight at a fixed 60 ticks per second, separate from rendering, feeds each fighter
    /// from an IInputSource (you or the Rookie AI), and draws the result with plain coloured boxes.
    ///
    /// Hotkeys: F1 debug overlay · F2 slow motion · P pause · F5 new fight ·
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

        public const float GroundY = -2f;
        public const float BodyHeight = 1.8f;

        public class Flash
        {
            public string text;
            public float x;
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
            cam.orthographicSize = 4f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.backgroundColor = new Color(0.06f, 0.05f, 0.08f);
            cam.clearFlags = CameraClearFlags.SolidColor;

            Transform root = new GameObject("Arena").transform;
            root.SetParent(transform, false);

            float width = config.arenaHalfWidth * 2f;
            Box(root, "Floor", new Vector2(0f, GroundY - 0.15f), new Vector2(width + 2f, 0.3f), new Color(0.22f, 0.2f, 0.24f), -10);
            Box(root, "Wall L", new Vector2(-config.arenaHalfWidth - 0.1f, GroundY + 1.5f), new Vector2(0.2f, 3f), new Color(0.3f, 0.12f, 0.14f), -10);
            Box(root, "Wall R", new Vector2(config.arenaHalfWidth + 0.1f, GroundY + 1.5f), new Vector2(0.2f, 3f), new Color(0.3f, 0.12f, 0.14f), -10);

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
                case FightEventType.CounterHit: AddFlash("COUNTER!", target.x, new Color(1f, 0.85f, 0.2f)); break;
                case FightEventType.GuardBreak: AddFlash("GUARD BREAK", target.x, new Color(1f, 0.3f, 0.9f)); break;
                case FightEventType.Dodged: AddFlash("DODGE", Match.sim.fighters[e.actor].x, new Color(0.4f, 1f, 0.9f)); break;
                case FightEventType.KO: AddFlash("KO", target.x, new Color(1f, 0.25f, 0.2f)); break;
            }
        }

        private void AddFlash(string text, float x, Color color)
        {
            flashes.Add(new Flash { text = text, x = x, ticksLeft = 45, color = color });
        }
    }
}
