using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace LauncherHub
{
    sealed class AnimMeta
    {
        public string Id = "";
        public int Frames;
        public int Fps;
        public int W;
        public int H;
        public int DurationMs { get { return Fps <= 0 ? 0 : (int)Math.Round(Frames * 1000.0 / Fps); } }
    }

    // Sprite-sheet micro animations generated from useAnimations Lottie icons (MIT).
    static class Anim
    {
        static Dictionary<string, AnimMeta> _meta;
        public static readonly HashSet<string> Used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public static readonly Dictionary<string, double> Aspect = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        public static void MarkUse(string key) { if (!string.IsNullOrEmpty(key)) Used.Add(key); }
        public static void MarkAspect(string key, Rectangle dest) { if (!string.IsNullOrEmpty(key) && dest.Height > 0) Aspect[key] = (double)dest.Width / dest.Height; }
        static bool _loaded;

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _meta = new Dictionary<string, AnimMeta>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "anim", "manifest.txt");
                if (!File.Exists(p)) return;
                foreach (string raw in File.ReadAllLines(p))
                {
                    string[] a = raw.Trim().Split('|');
                    if (a.Length < 5) continue;
                    AnimMeta m = new AnimMeta();
                    m.Id = a[0];
                    int.TryParse(a[1], out m.Frames);
                    int.TryParse(a[2], out m.Fps);
                    int.TryParse(a[3], out m.W);
                    int.TryParse(a[4], out m.H);
                    if (m.Frames > 0 && m.W > 0 && m.H > 0) _meta[m.Id] = m;
                }
            }
            catch { }
        }

        public static AnimMeta Get(string id)
        {
            Load();
            AnimMeta m;
            return _meta.TryGetValue(id, out m) ? m : null;
        }

        public static bool Active(HubState st)
        {
            Load();
            int now = st.Now;
            foreach (KeyValuePair<string, long> kv in st.AnimStart)
            {
                string id = kv.Key;
                if (id.StartsWith("play:", StringComparison.Ordinal)) id = "play-pause";
                AnimMeta m = Get(id);
                if (m == null) continue;
                long el = now - kv.Value;
                if (el >= 0 && el <= m.DurationMs + 40) return true;
            }
            return false;
        }

        public static int Frame(HubState st, string key, AnimMeta m, int staticFrame)
        {
            long start;
            if (!st.AnimStart.TryGetValue(key, out start) || m.Frames <= 1) return staticFrame;
            long el = st.Now - start;
            if (el < 0 || el > m.DurationMs) return staticFrame;
            float p = el / (float)m.DurationMs;
            return Math.Max(0, Math.Min(m.Frames - 1, (int)Math.Round(p * (m.Frames - 1))));
        }

        public static void Prune(HubState st)
        {
            // Starts are tiny; keep them. This method exists so callers can force
            // a repaint just after an animation finishes.
        }
    }
}
