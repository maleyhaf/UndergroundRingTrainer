using System;
using System.Globalization;
using System.IO;
using System.Text;
using FightTrainer.Core;
using FightTrainer.Rounds;

namespace FightTrainer.Training
{
    /// <summary>
    /// Records (situation -> button pressed) pairs for one fighter, one line of JSON per sample.
    /// This file is exactly what the ML step will train on (behavior cloning, PDD §5.5).
    ///
    /// A sample is taken every config.decisionInterval ticks (~0.1s), matching the model's
    /// decision rate. The situation is captured at the START of the window (what the player saw),
    /// and the action is the strongest button pressed during the window (a press like Jab
    /// outranks a hold like Block), so quick taps between samples are not lost.
    ///
    /// Every sample carries its source ("show", "takeover", ...) and session ID, which later powers
    /// action weighting and the "Learned in Drill #3" tags.
    /// </summary>
    public class TrainingRecorder : IDisposable
    {
        public bool IsRecording { get { return writer != null; } }
        public int SampleCount { get; private set; }
        public string FilePath { get; private set; }
        public string SessionId { get; private set; }

        private StreamWriter writer;
        private string source;
        private int fighterIndex;
        private int windowTicks;
        private float[] windowState;
        private FighterAction windowAction;
        private int windowRound;
        private int windowTick;

        public void Begin(string folder, string sourceTag, int fighter, string fighterName)
        {
            End();
            Directory.CreateDirectory(folder);
            SessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            source = sourceTag;
            fighterIndex = fighter;
            FilePath = Path.Combine(folder, fighterName + "_" + sourceTag + "_" + SessionId + ".jsonl");
            writer = new StreamWriter(FilePath, false, new UTF8Encoding(false));
            SampleCount = 0;
            windowState = null;

            // Header line: describes every number in "state".
            StringBuilder sb = new StringBuilder();
            sb.Append("{\"type\":\"header\",\"version\":" + StateEncoder.Version + ",\"session\":\"").Append(SessionId)
              .Append("\",\"source\":\"").Append(source)
              .Append("\",\"fighter\":\"").Append(fighterName)
              .Append("\",\"features\":[");
            string[] names = StateEncoder.FeatureNames;
            for (int i = 0; i < names.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('"').Append(names[i]).Append('"');
            }
            sb.Append("],\"actions\":[");
            for (int i = 0; i < FighterActions.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('"').Append((FighterAction)i).Append('"');
            }
            sb.Append("]}");
            writer.WriteLine(sb.ToString());
        }

        /// <summary>
        /// Call once per live fight tick, BEFORE sim.Tick, with the action the player is pressing.
        /// </summary>
        public void Observe(FightMatch match, FighterAction pressed)
        {
            if (writer == null) return;

            if (windowState == null)
            {
                windowState = StateEncoder.Encode(match, fighterIndex);
                windowAction = pressed;
                windowRound = match.round;
                windowTick = match.sim.tick;
                windowTicks = 0;
            }
            else if (FighterActions.Priority(pressed) > FighterActions.Priority(windowAction))
            {
                windowAction = pressed;
            }

            windowTicks++;
            if (windowTicks >= match.config.decisionInterval) WriteSample();
        }

        private void WriteSample()
        {
            StringBuilder sb = new StringBuilder(256);
            sb.Append("{\"tick\":").Append(windowTick)
              .Append(",\"round\":").Append(windowRound)
              .Append(",\"source\":\"").Append(source)
              .Append("\",\"session\":\"").Append(SessionId)
              .Append("\",\"action\":").Append((int)windowAction)
              .Append(",\"action_name\":\"").Append(windowAction)
              .Append("\",\"weight\":1,\"state\":[");
            for (int i = 0; i < windowState.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(windowState[i].ToString("0.####", CultureInfo.InvariantCulture));
            }
            sb.Append("]}");
            writer.WriteLine(sb.ToString());
            SampleCount++;
            windowState = null;
        }

        public void End()
        {
            if (writer == null) return;
            writer.Flush();
            writer.Dispose();
            writer = null;
        }

        public void Dispose() { End(); }
    }
}
