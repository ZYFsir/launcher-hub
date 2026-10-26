using System;
using System.Collections.Generic;
using System.IO;

namespace LauncherHub
{
    // Switch-jank telemetry. It records the actual UI path: input -> snapshot ->
    // first paint -> animation frames; a 60 fps switch should stay under ~34 ms.
    sealed class PerfSample
    {
        public string From = "";
        public string To = "";
        public long InputMs;
        public long SwitchMs;
        public long CaptureMs;
        public long FirstPaintMs;
        public long MaxFrameGapMs;
        public long MaxTickGapMs;
        public long MaxRenderMs;
        public int JankFrames;
        public bool Fail;
        public string Size = "";
        public string Stages = "";

        public string Line()
        {
            return string.Format("switch {0,-5}->{1,-5} {10} input={2,3}ms capture={3,3}ms firstPaint={4,3}ms maxFrame={5,3}ms maxTick={6,3}ms maxRender={7,3}ms jankFrames={8} {9}",
                From, To, InputMs, CaptureMs, FirstPaintMs, MaxFrameGapMs, MaxTickGapMs, MaxRenderMs, JankFrames, Fail ? "FAIL" : "PASS", Size + " " + Stages);
        }
    }

    static class Perf
    {
        public static bool Audit;
        public static bool Collect;
        public static string LogPath;
        public static string StageInfo = "";
        public static string RailDetail = "";
        public static string PageDetail = "";
        public static long TextTicks;
        public static int TextCount;
        public static long ImageTicks;
        public static int ImageCount;
        public static void ResetOps() { TextTicks = 0; TextCount = 0; ImageTicks = 0; ImageCount = 0; }
        public static string OpsInfo { get { return "txt=" + (TextTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency).ToString("0.0") + "ms/" + TextCount + " img=" + (ImageTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency).ToString("0.0") + "ms/" + ImageCount; } }
        public static readonly List<PerfSample> Samples = new List<PerfSample>();
        static readonly object _lock = new object();

        public static void Add(PerfSample s)
        {
            lock (_lock)
            {
                Samples.Add(s);
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
                    File.AppendAllText(LogPath, s.Line() + Environment.NewLine);
                }
                catch { }
            }
        }

        public static bool AnyFail()
        {
            lock (_lock)
            {
                for (int i = 0; i < Samples.Count; i++) if (Samples[i].Fail) return true;
            }
            return false;
        }

        public static string Report()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("LauncherHub switch audit · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("thresholds: firstPaint<=34ms, maxFrame<=34ms, capture<=22ms, maxRender<=22ms");
            int fails = 0;
            lock (_lock)
            {
                for (int i = 0; i < Samples.Count; i++)
                {
                    sb.AppendLine(Samples[i].Line());
                    if (Samples[i].Fail) fails++;
                }
            }
            sb.AppendLine(fails == 0 ? "PASS  no switch jank detected" : ("FAIL  " + fails + " switch(es) exceeded budget"));
            return sb.ToString();
        }
    }
}
