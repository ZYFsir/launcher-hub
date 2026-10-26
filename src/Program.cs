using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace LauncherHub
{
    static class Program
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")]
        static extern bool SetProcessDpiAwarenessContext(IntPtr context);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr FindWindow(string cls, string title);
        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll")]
        static extern bool AttachThreadInput(uint a, uint b, bool attach);
        [DllImport("kernel32.dll")]
        static extern uint GetCurrentThreadId();

        static Mutex _mutex;

        [STAThread]
        static void Main(string[] args)
        {
            // Per-Monitor V2 first; fall back to legacy System DPI awareness.
            try { SetProcessDpiAwarenessContext(new IntPtr(-4)); }
            catch { try { SetProcessDPIAware(); } catch { } }
            Settings.Load();
            try { System.Runtime.GCSettings.LatencyMode = System.Runtime.GCLatencyMode.SustainedLowLatency; } catch { }
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { LogException(e.ExceptionObject as Exception); };
            Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { LogException(e.Exception); };

            int style = 0;
            int page = 0;
            bool preview = false;
            bool popover = false;
            bool expanded = false;
            bool tabcard = false;
            bool selftest = false;
            bool auditIcons = false;
            bool auditSwitch = false;
            bool benchRender = false;
            bool auditAnim = false;
            bool auditLayout = false;
            float scale = 2f;
            string outPath = null;

            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (a == "--preview") preview = true;
                else if (a == "--popover") popover = true;
                else if (a == "--expanded") expanded = true;
                else if (a == "--tabcard") tabcard = true;
                else if (a == "--selftest") selftest = true;
                else if (a == "--audit-icons") auditIcons = true;
                else if (a == "--audit-switch") auditSwitch = true;
                else if (a == "--bench-render") benchRender = true;
                else if (a == "--audit-anim") auditAnim = true;
                else if (a == "--audit-layout") auditLayout = true;
                else if (a.StartsWith("--page="))
                {
                    string id = a.Substring(7);
                    if (id == "home") page = -1;
                    else
                    {
                        for (int k = 0; k < 3; k++)
                        {
                            string[] ids = new string[] { "llama", "comfy", "dsh" };
                            if (ids[k] == id) page = k;
                        }
                    }
                }
                else if (a.StartsWith("--scale="))
                {
                    float.TryParse(a.Substring(8), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out scale);
                }
                else if (a.StartsWith("--out=")) outPath = a.Substring(6);
            }

            Perf.LogPath = Path.Combine(Paths.LocalRoot, "switch-perf.log");
            if (auditLayout)
            {
                int rc = LayoutAudit.Run(Path.Combine(Paths.LocalRoot, "layout-audit.log"));
                Environment.ExitCode = rc;
                return;
            }
            if (auditAnim)
            {
                AnimationAudit.Run(Path.Combine(Paths.LocalRoot, "anim-audit.log"));
                return;
            }
            if (benchRender)
            {
                Perf.Collect = true;
                using (HubController ctl = new HubController(false))
                using (Bitmap bmp = new Bitmap(Geo.W, Geo.H, System.Drawing.Imaging.PixelFormat.Format32bppRgb))
                using (Graphics g = Graphics.FromImage(bmp))
                using (Renderer r = new Renderer(0))
                {
                    HubState st = new HubState();
                    st.Page = 0; st.PrevPage = 0; st.SwitchT = 1f; st.Now = Environment.TickCount;
                    r.Render(g, st, new Size(Geo.W, Geo.H));
                    long total = 0, max = 0;
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    for (int i = 0; i < 120; i++)
                    {
                        long t0 = sw.ElapsedMilliseconds;
                        r.Render(g, st, new Size(Geo.W, Geo.H));
                        long d = sw.ElapsedMilliseconds - t0;
                        total += d; if (d > max) max = d;
                    }
                    string msg = "bench render avg=" + (total / 120.0).ToString("0.0") + "ms max=" + max + "ms stage=" + Perf.StageInfo;
                    File.WriteAllText(Path.Combine(Paths.LocalRoot, "bench-render.log"), msg);
                    return;
                }
            }
            if (auditSwitch)
            {
                Perf.Audit = true;
                Perf.Collect = true;
                Perf.LogPath = Path.Combine(Paths.LocalRoot, "switch-audit.log");
                using (HubController ctl = new HubController(false))
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new HubForm(ctl, 0, -1));
                }
                return;
            }
            if (auditIcons)
            {
                string logPath = Path.Combine(Paths.LocalRoot, "icon-audit.log");
                IconAudit.Run(logPath);
                return;
            }
            if (selftest)
            {
                using (HubController ctl = new HubController(true))
                {
                    System.Threading.Thread.Sleep(3500);
                    ctl.SyncAll();
                    System.Text.StringBuilder sb = new System.Text.StringBuilder();
                    for (int i = 0; i < ctl.Runtimes.Count; i++)
                    {
                        AppRuntime rt = ctl.Runtimes[i];
                        sb.AppendLine(rt.Entry.Id + " phase=" + rt.Phase + " running=" + rt.IsRunning + " port=" + rt.Entry.Port + " state=" + rt.Entry.State + " status=" + rt.Entry.StatusLine);
                    }
                    try { Directory.CreateDirectory(Paths.LocalRoot); File.WriteAllText(Path.Combine(Paths.LocalRoot, "selftest.log"), sb.ToString()); } catch { }
                }
                return;
            }
            if (preview || outPath != null)
            {
                using (HubController ctl = new HubController(false))
                {
                    if (preview)
                    {
                        string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "preview");
                        Directory.CreateDirectory(dir);
                        WriteShot(Path.Combine(dir, "flat-llama.png"), false, 0, false, false, scale, false);
                        WriteShot(Path.Combine(dir, "flat-home.png"), false, -1, false, false, scale, false);
                        WriteShot(Path.Combine(dir, "flat-comfy.png"), false, 1, false, false, scale, false);
                        WriteShot(Path.Combine(dir, "flat-llama-tabcard.png"), false, 0, false, false, scale, true);
                        Console.WriteLine("preview written to " + dir);
                        return;
                    }
                    WriteShot(outPath, style == 1, page, popover, expanded, scale, tabcard);
                    return;
                }
            }

            bool created;
            _mutex = new Mutex(true, @"Local\LauncherHub-7C31A9", out created);
            if (!created)
            {
                IntPtr h = FindWindow(null, "应用工作台");
                if (h != IntPtr.Zero)
                {
                    uint pid2;
                    IntPtr fg = GetForegroundWindow();
                    uint ft = GetWindowThreadProcessId(fg, out pid2);
                    uint mt = GetCurrentThreadId();
                    bool at = ft != 0 && ft != mt && AttachThreadInput(mt, ft, true);
                    ShowWindow(h, 9);
                    SetWindowPos(h, (IntPtr)(-1), 0, 0, 0, 0, 0x0043);
                    SetWindowPos(h, (IntPtr)(-2), 0, 0, 0, 0, 0x0043);
                    SetForegroundWindow(h);
                    if (at) AttachThreadInput(mt, ft, false);
                }
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            HubController controller = new HubController(true);
            try
            {
                Application.Run(new HubForm(controller, style, page));
            }
            finally
            {
                controller.Dispose();
                GC.KeepAlive(_mutex);
            }
        }

        static void LogException(Exception ex)
        {
            try
            {
                Directory.CreateDirectory(Paths.LocalRoot);
                File.AppendAllText(Path.Combine(Paths.LocalRoot, "error.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + (ex == null ? "null" : ex.ToString()) + "\r\n\r\n");
            }
            catch { }
        }

        static void WriteShot(string path, bool sketch, int page, bool popover, bool expanded, float scale, bool tabcard)
        {
            using (Bitmap bmp = Shoot(sketch, page, popover, expanded, scale, tabcard))
                bmp.Save(path, ImageFormat.Png);
            Console.WriteLine("wrote " + path);
        }

        static Bitmap Shoot(bool sketch, int page, bool popover, bool expanded, float scale, bool tabcard)
        {
            int pad = 48;
            int cw = Geo.W + pad * 2;
            int ch = Geo.H + pad * 2;
            int W = (int)(cw * scale);
            int H = (int)(ch * scale);
            Bitmap bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

                using (LinearGradientBrush lb = new LinearGradientBrush(new Rectangle(0, 0, W, H),
                    Color.FromArgb(239, 243, 249), Color.FromArgb(219, 226, 237), 90f))
                    g.FillRectangle(lb, 0, 0, W, H);

                g.ScaleTransform(scale, scale);
                g.TranslateTransform(pad, pad);

                for (int i = 16; i >= 1; i--)
                {
                    Rectangle rr = new Rectangle(-i / 2, 10 - i / 2, Geo.W + i, Geo.H + i);
                    using (GraphicsPath sp = P.Round(rr, 18 + i / 2))
                    using (SolidBrush sb = new SolidBrush(Color.FromArgb(4, 22, 28, 40)))
                        g.FillPath(sb, sp);
                }
                if (sketch)
                {
                    using (GraphicsPath sp = P.Round(new Rectangle(6, 10, Geo.W, Geo.H), 18))
                    using (SolidBrush sb = new SolidBrush(Color.FromArgb(40, 43, 39, 35)))
                        g.FillPath(sb, sp);
                }

                using (Renderer r = new Renderer(sketch ? 1 : 0))
                {
                    HubState st = new HubState();
                    st.Style = sketch ? 1 : 0;
                    st.Page = page;
                    st.PrevPage = page;
                    st.SwitchT = 1f;
                    st.Popover = popover;
                    st.Expanded = expanded;
                    st.Now = Environment.TickCount;
                    st.TabToCard = tabcard;
                    r.Render(g, st, new Size(Geo.W, Geo.H));
                }

                g.ResetTransform();
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                using (Font f = Look.MakeFont(Look.UiFamily, 10f * scale, FontStyle.Bold))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 98, 112)))
                {
                    string cap = sketch ? "B · 手绘贴纸风" : "A · 现代扁平 + 品牌色";
                    g.DrawString(cap, f, b, pad * scale, 16 * scale);
                }
            }
            return bmp;
        }
    }
}
