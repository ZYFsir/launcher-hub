using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace LauncherHub
{
    // Coverage audit for interactive icons. It answers "which clickable icon
    // still has no sprite animation" instead of relying on someone eyeballing
    // the title bar after every UI change.
    static class AnimationAudit
    {
        static readonly string[,] Expected = new string[,] {
            { "window-min",     "minus" },
            { "window-max",     "max" },
            { "window-restore", "restore" },
            { "window-close",   "close" },
            { "home",           "home" },
            { "refresh",        "refresh-v2" },
            { "log",            "file-text" },
            { "settings",       "settings" },
            { "expand",         "maximize-2" },
            { "theme",          "moon" },
            { "play/pause",     "play-pause" },
        };

        public static int Run(string logPath)
        {
            List<string> lines = new List<string>();
            int fail = 0;
            lines.Add("LauncherHub animation audit · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            lines.Add("rule: every interactive UI icon must have a sprite with >1 frame");
            lines.Add("");

            for (int i = 0; i < Expected.GetLength(0); i++)
            {
                string role = Expected[i, 0];
                string key = Expected[i, 1];
                AnimMeta meta = Anim.Get(key);
                Image img = Assets.Get("anim/" + key);
                if (meta == null || img == null || meta.Frames <= 1)
                {
                    fail++;
                    lines.Add("FAIL  " + role + " -> " + key + " : " + (meta == null ? "missing manifest entry" : (img == null ? "missing sprite PNG" : "only " + meta.Frames + " frame")));
                }
                else
                {
                    lines.Add(string.Format("PASS  {0,-15} -> {1,-12} frames={2} fps={3} {4}x{5}", role, key, meta.Frames, meta.Fps, meta.W, meta.H));
                }
            }

            // Wiring check: assets can exist while a button still calls P.Icon.
            // Render a normal home, an app page, and a maximized home so every
            // window control / content icon is actually drawn through AnimIcon.
            Anim.Used.Clear();
            Anim.Aspect.Clear();
            try
            {
                using (HubController ctl = new HubController(false))
                using (Bitmap bmp = new Bitmap(1240, 800, System.Drawing.Imaging.PixelFormat.Format32bppRgb))
                using (Graphics g = Graphics.FromImage(bmp))
                using (Renderer r = new Renderer(0))
                {
                    HubState st = new HubState();
                    st.Page = -1; st.PrevPage = -1; st.SwitchT = 1f; st.Now = Environment.TickCount;
                    r.Render(g, st, new Size(1240, 800));

                    st = new HubState();
                    st.Page = 0; st.PrevPage = 0; st.SwitchT = 1f; st.Now = Environment.TickCount;
                    r.Render(g, st, new Size(1240, 800));

                    st = new HubState();
                    st.Page = -1; st.PrevPage = -1; st.SwitchT = 1f; st.Now = Environment.TickCount; st.Maximized = true;
                    r.Render(g, st, new Size(1240, 800));
                }
            }
            catch (Exception ex)
            {
                fail++;
                lines.Add("FAIL  wiring render crashed: " + ex.Message);
            }

            lines.Add("");
            for (int i = 0; i < Expected.GetLength(0); i++)
            {
                string role = Expected[i, 0];
                string key = Expected[i, 1];
                if (!Anim.Used.Contains(key))
                {
                    fail++;
                    lines.Add("FAIL  " + role + " -> " + key + " : asset exists but is NOT wired to AnimIcon (static icon)");
                }
            }

            for (int i = 0; i < Expected.GetLength(0); i++)
            {
                string role = Expected[i, 0];
                string key = Expected[i, 1];
                double aspect;
                if (Anim.Aspect.TryGetValue(key, out aspect) && Math.Abs(aspect - 1.0) > 0.05)
                {
                    fail++;
                    lines.Add("FAIL  " + role + " -> " + key + " : drawn into non-square dest aspect=" + aspect.ToString("0.00") + " (icon will look squashed)");
                }
            }

            lines.Add("");
            lines.Add(fail == 0 ? "PASS  all interactive icons have animation coverage" : ("FAIL  " + fail + " icon(s) without animation"));
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(logPath));
                File.WriteAllLines(logPath, lines.ToArray(), new System.Text.UTF8Encoding(false));
            }
            catch { }
            Environment.ExitCode = fail == 0 ? 0 : 1;
            return fail;
        }
    }
}
