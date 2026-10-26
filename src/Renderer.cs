using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;

namespace LauncherHub
{
    static class Assets
    {
        static Dictionary<string, Image> _cache = new Dictionary<string, Image>();
        static Dictionary<string, Image> _sized = new Dictionary<string, Image>();
        public static Image Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (_cache.ContainsKey(name)) return _cache[name];
            Image img = null;
            try
            {
                string p;
                if (Path.IsPathRooted(name)) p = name;
                else if (name.IndexOf('/') >= 0 || name.IndexOf('\\') >= 0)
                {
                    string direct = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name);
                    string byAsset = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets",
                        name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? name : name + ".png");
                    p = File.Exists(direct) ? direct : byAsset;
                }
                else
                    p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", name + ".png");
                if (File.Exists(p))
                {
                    using (Bitmap b = new Bitmap(p))
                        img = new Bitmap(b);
                }
            }
            catch { img = null; }
            _cache[name] = img;
            return img;
        }

        public static Image GetSized(string name, int w, int h)
        {
            if (w <= 0 || h <= 0) return null;
            string key = name + "_" + w + "x" + h;
            Image sized;
            if (_sized.TryGetValue(key, out sized)) return sized;
            Image src = Get(name);
            if (src == null) { _sized[key] = null; return null; }
            Bitmap b = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                g.DrawImage(src, new Rectangle(0, 0, w, h));
            }
            _sized[key] = b;
            return b;
        }
    }

    sealed class HubState
    {
        public int Page = 0;              // -1 = overview
        public int PrevPage = 0;
        public float SwitchT = 1f;        // 0..1 selection / page transition
        public bool TabToCard;          // selected tab bridges into the white card instead of the page background
        public bool Popover;
        public bool Expanded;
        public string Hover = "";
        public int RailHot = -1;
        public float[] RailT = new float[4];
        public float Pulse;
        public string Toast = "";
        public int ToastUntil;
        public int Now;
        public bool Dark;
        public bool Maximized;
        public int Style;                 // 0 flat, 1 sketch
        public Point Mouse = new Point(-999, -999);

        // runtime UI state
        public bool Dropdown;
        public int DropOwner = -1;
        public List<ModelItem> DropItems = new List<ModelItem>();
        public Rectangle DropRect;
        public Dictionary<string, long> AnimStart = new Dictionary<string, long>();
        public Dictionary<string, int> AnimDir = new Dictionary<string, int>();
        public List<Rectangle> ChipRects = new List<Rectangle>();
        public List<string> ChipLabels = new List<string>();

        public void PlayAnim(string key, int dir)
        {
            AnimStart[key] = Environment.TickCount;
            AnimDir[key] = dir;
        }
    }

    static class Geo
    {
        public static int W = 1240;
        public static int H = 800;
        public const int RailW = 76;
        public const int BannerH = 108;
        public static int Radius = 18;
        public static bool Maximized;

        public static Rectangle Banner;
        public static Rectangle Content;
        public static Rectangle Left;
        public static Rectangle Right;
        public static Rectangle Term;
        public static Rectangle Pop;
        public static Rectangle StyleBtn;

        static Geo() { Resize(1240, 800); }

        public static void Resize(int w, int h)
        {
            if (w < 1000) w = 1000;
            if (h < 700) h = 700;
            W = w; H = h;
            Radius = Maximized ? 0 : 18;
            int margin = 20;
            Banner = new Rectangle(RailW, 0, W - RailW, BannerH);
            // Responsive: cap the content plane and center it on wide windows
            // instead of stretching cards to 2000px+.
            int avail = W - RailW - margin * 2;
            int contentW = Math.Min(avail, 1380);
            int contentX = RailW + margin + (avail - contentW) / 2;
            Content = new Rectangle(contentX, 128, contentW, H - 148);
            int leftW = Math.Min(460, Math.Max(330, (int)(contentW * 0.36f)));
            int leftH = H <= 800 ? 330 : Math.Min(440, 330 + (H - 800) * 35 / 100);
            Left = new Rectangle(Content.X, Content.Y, leftW, leftH);
            Right = new Rectangle(Left.Right + 16, Content.Y, Content.Width - leftW - 16, Left.Height);
            int termY = Left.Bottom + 16;
            Term = new Rectangle(Content.X, termY, Content.Width, Content.Bottom - termY);
            Pop = new Rectangle(W - 400, 120, 360, 442);
            StyleBtn = new Rectangle(18, H - 62, 40, 40);
        }

        public static Rectangle WinBtn(int i) { return new Rectangle(W - 156 + i * 48, 39, 44, 30); }

        public static Rectangle RailItem(int i)
        {
            if (i == 0) return new Rectangle(12, 28, 52, 52);
            return new Rectangle(12, 128 + (i - 1) * 74, 52, 52);
        }

        public static Rectangle HomeCard(int i)
        {
            int gap = 16;
            int cardW = (Content.Width - gap * 2) / 3;
            int cardY = Content.Y + 62;
            int cardH = H - 340;
            return new Rectangle(Content.X + i * (cardW + gap), cardY, cardW, cardH);
        }
    }
    sealed class Renderer : IDisposable
    {
        Look L;
        bool _dark;

        public Renderer(int style)
        {
            SetTheme(false);
        }

        public Look Style { get { return L; } }
        public bool Dark { get { return _dark; } }

        public void SetTheme(bool dark, float scale = 1f)
        {
            _dark = dark;
            if (L != null) L.Dispose();
            L = dark ? Look.DarkTheme(scale) : Look.Flat(scale);
        }

        public void Dispose()
        {
            if (L != null) L.Dispose();
        }

        // ---------------- top level ----------------
        public void Render(Graphics g, HubState st, Size size)
        {
            Geo.Resize(size.Width, size.Height);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.TextContrast = 8;

            Rectangle win = new Rectangle(0, 0, Geo.W, Geo.H);
            // Rounded corners only in windowed mode. Maximized/edge-to-edge MUST
            // be square: a rounded clip exposes the black frame at screen corners.
            using (SolidBrush bg = new SolidBrush(L.Bg))
                g.FillRectangle(bg, win);

            st.TabToCard = st.Page >= 0;
            Perf.ResetOps();
            System.Diagnostics.Stopwatch __sw = System.Diagnostics.Stopwatch.StartNew();
            DrawRail(g, st);

            long __rail = __sw.ElapsedMilliseconds;
            DrawBanner(g, st);
            long __banner = __sw.ElapsedMilliseconds;
            if (st.Page < 0) DrawHome(g, st);
            else DrawApp(g, st, Data.Apps[st.Page]);
            long __page = __sw.ElapsedMilliseconds;
            long __stageEnd = __sw.ElapsedMilliseconds;
            if (Perf.Collect) Perf.StageInfo = "rail=" + __rail + "[" + Perf.RailDetail + "] banner=" + (__banner - __rail) + " page=" + (__page - __banner) + "[" + Perf.PageDetail + "] rest=" + (__stageEnd - __page) + " " + Perf.OpsInfo;

            // rail hover callout sits above page content
            for (int i = 0; i < Data.Apps.Count; i++)
            {
                if (st.RailHot == i + 1 && st.RailT[i + 1] > 0.2f)
                    DrawRailCallout(g, st, i, st.RailT[i + 1]);
            }

            if (st.Expanded) DrawExpanded(g, st);
            else if (st.Popover) DrawPopover(g, st);
            if (st.Dropdown) DrawDropdown(g, st);

            DrawToast(g, st);
            g.ResetClip();
        }

        // ---------------- rail ----------------
        void DrawRail(Graphics g, HubState st)
        {
            Rectangle rail = new Rectangle(0, 0, Geo.RailW, Geo.H);
            using (SolidBrush b = new SolidBrush(L.RailBg))
                g.FillRectangle(b, rail);

            // border first: the selected tab paints over it to connect with the page
            using (Pen pen = new Pen(L.RailBorder, L.Sketch ? 1.4f : 1f))
                g.DrawLine(pen, Geo.RailW - 0.5f, 0, Geo.RailW - 0.5f, Geo.H);

            // divider aligned with the bottom edge of the banner
            using (Pen pen = new Pen(L.Sketch ? Color.FromArgb(90, L.Ink) : Color.FromArgb(212, 220, 234), 1.2f))
                g.DrawLine(pen, 18, Geo.BannerH, 58, Geo.BannerH);

            System.Diagnostics.Stopwatch __rs = System.Diagnostics.Stopwatch.StartNew();
            DrawRailSelection(g, st);
            long __sel = __rs.ElapsedMilliseconds;
            DrawRailHome(g, st);
            for (int i = 0; i < Data.Apps.Count; i++)
                DrawRailApp(g, st, i);
            long __apps = __rs.ElapsedMilliseconds;
            DrawStyleButton(g, st);
            Perf.RailDetail = "sel=" + __sel + " apps=" + (__apps - __sel) + " style=" + (__rs.ElapsedMilliseconds - __apps);
        }

        void DrawRailSelection(Graphics g, HubState st)
        {
            int cur = st.Page < 0 ? 0 : st.Page + 1;
            int prev = st.PrevPage < 0 ? 0 : st.PrevPage + 1;
            Rectangle a = Geo.RailItem(prev);
            Rectangle b = Geo.RailItem(cur);
            float t = Math.Max(0f, Math.Min(1f, st.SwitchT));
            float e = 1f - (1f - t) * (1f - t) * (1f - t);
            int y = (int)Math.Round(a.Y + (b.Y - a.Y) * e);
            Rectangle plate = new Rectangle(b.X - 4, y - 4, b.Width + 8, b.Height + 8);

            if (L.Sketch)
            {
                P.FillRound(g, plate, 14, Color.FromArgb(232, 222, 201));
                P.RoughBorder(g, plate, 14, L.Ink, 1.6f, 1234);
                GraphicsState sk = g.Save();
                using (GraphicsPath clip = P.Round(plate, 14))
                    g.SetClip(clip, CombineMode.Intersect);
                using (Pen dark = new Pen(Color.FromArgb(60, L.Ink), 4f))
                    g.DrawArc(dark, plate.X - 1, plate.Y - 1, plate.Width + 2, plate.Height + 2, 150, 220);
                using (Pen light = new Pen(Color.FromArgb(180, 255, 255, 255), 3f))
                    g.DrawArc(light, plate.X - 1, plate.Y - 1, plate.Width + 2, plate.Height + 2, -30, 180);
                g.Restore(sk);
                return;
            }

            // A: selected app entry becomes a tab that merges into the page surface
            if (st.Page >= 0)
            {
                DrawContentTab(g, st, SelTabRect(st));
            }
            else
            {
                DrawHomePlate(g, plate);
            }
        }

        Rectangle SelTabRect(HubState st)
        {
            int cur = st.Page < 0 ? 0 : st.Page + 1;
            int prev = st.PrevPage < 0 ? 0 : st.PrevPage + 1;
            Rectangle a = Geo.RailItem(prev);
            Rectangle b = Geo.RailItem(cur);
            float t = Math.Max(0f, Math.Min(1f, st.SwitchT));
            float e = 1f - (1f - t) * (1f - t) * (1f - t);
            int y = (int)Math.Round(a.Y + (b.Y - a.Y) * e);
            int right = Geo.RailW; // tab ends at rail edge
            return new Rectangle(8, y - 4, right - 8, 60);
        }

        static GraphicsPath LeftTab(Rectangle r, int rad)
        {
            int d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
            GraphicsPath p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            // Left side rounds; the right seam into the card is square.
            p.AddLine(r.X + rad, r.Y, r.Right, r.Y);
            p.AddLine(r.Right, r.Y, r.Right, r.Bottom);
            p.AddLine(r.Right, r.Bottom, r.X + rad, r.Bottom);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        static GraphicsPath TopRound(Rectangle r, int rad)
        {
            int d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
            GraphicsPath p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddLine(r.X + rad, r.Y, r.Right - rad, r.Y);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddLine(r.Right, r.Bottom, r.X, r.Bottom);
            p.CloseFigure();
            return p;
        }
        static readonly Dictionary<string, Bitmap> _tabShadowCache = new Dictionary<string, Bitmap>();

        static void BlurAlpha(Bitmap bmp, int radius)
        {
            if (radius <= 0) return;
            int w = bmp.Width, h = bmp.Height;
            System.Drawing.Imaging.BitmapData bd = bmp.LockBits(new Rectangle(0, 0, w, h),
                System.Drawing.Imaging.ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            int[] p = new int[w * h];
            System.Runtime.InteropServices.Marshal.Copy(bd.Scan0, p, 0, p.Length);
            int[] a = new int[w * h];
            for (int i = 0; i < p.Length; i++) a[i] = (p[i] >> 24) & 255;
            int[] tmp = new int[w * h];
            for (int pass = 0; pass < 2; pass++)
            {
                for (int y = 0; y < h; y++)
                {
                    int sum = 0, count = 0, baseI = y * w;
                    for (int x = -radius; x < w + radius; x++)
                    {
                        int add = x + radius;
                        int rem = x - radius - 1;
                        if (add >= 0 && add < w) { sum += a[baseI + add]; count++; }
                        if (rem >= 0 && rem < w) { sum -= a[baseI + rem]; count--; }
                        if (x >= 0 && x < w) tmp[baseI + x] = count > 0 ? sum / count : 0;
                    }
                }
                for (int x = 0; x < w; x++)
                {
                    int sum = 0, count = 0;
                    for (int y = -radius; y < h + radius; y++)
                    {
                        int add = y + radius;
                        int rem = y - radius - 1;
                        if (add >= 0 && add < h) { sum += tmp[add * w + x]; count++; }
                        if (rem >= 0 && rem < h) { sum -= tmp[rem * w + x]; count--; }
                        if (y >= 0 && y < h) a[y * w + x] = count > 0 ? sum / count : 0;
                    }
                }
            }
            for (int i = 0; i < p.Length; i++) p[i] = (a[i] << 24);
            System.Runtime.InteropServices.Marshal.Copy(p, 0, bd.Scan0, p.Length);
            bmp.UnlockBits(bd);
        }

        static void FadeTopLeftAlpha(Bitmap bmp)
        {
            int w = bmp.Width, h = bmp.Height;
            System.Drawing.Imaging.BitmapData bd = bmp.LockBits(new Rectangle(0, 0, w, h),
                System.Drawing.Imaging.ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            int[] p = new int[w * h];
            System.Runtime.InteropServices.Marshal.Copy(bd.Scan0, p, 0, p.Length);
            for (int y = 0; y < h; y++)
            {
                float fy = 1f - 0.65f * y / Math.Max(1, h - 1);
                for (int x = 0; x < w; x++)
                {
                    float fx = 1f - 0.35f * x / Math.Max(1, w - 1);
                    int a = (p[y * w + x] >> 24) & 255;
                    a = (int)(a * Math.Max(0f, Math.Min(1f, fy * fx)));
                    p[y * w + x] = a << 24;
                }
            }
            System.Runtime.InteropServices.Marshal.Copy(p, 0, bd.Scan0, p.Length);
            bmp.UnlockBits(bd);
        }
        static Bitmap GetTabShadow(int w, int h, bool dark, bool inner)
        {
            string key = (inner ? "i" : "o") + "_" + w + "x" + h + (dark ? "_d" : "_l");
            Bitmap bmp;
            if (_tabShadowCache.TryGetValue(key, out bmp)) return bmp;
            int pad = inner ? 0 : 14;
            bmp = new Bitmap(w + pad * 2, h + pad * 2, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics sg = Graphics.FromImage(bmp))
            {
                sg.SmoothingMode = SmoothingMode.AntiAlias;
                if (inner)
                {
                    using (GraphicsPath outer = LeftTab(new Rectangle(0, 0, w, h), 16))
                    using (GraphicsPath innerPath = LeftTab(new Rectangle(2, 2, w - 4, h - 4), 14))
                    using (Region ring = new Region(outer))
                    using (SolidBrush sb = new SolidBrush(Color.FromArgb(dark ? 125 : 100, 0, 0, 0)))
                    {
                        ring.Exclude(innerPath);
                        sg.FillRegion(sb, ring);
                    }
                    BlurAlpha(bmp, 5);
                    FadeTopLeftAlpha(bmp);
                }
                else
                {
                    using (GraphicsPath path = LeftTab(new Rectangle(pad + 2, pad + 3, w, h), 16))
                    using (SolidBrush sb = new SolidBrush(Color.FromArgb(dark ? 85 : 42, 0, 0, 0)))
                        sg.FillPath(sb, path);
                    BlurAlpha(bmp, 8);
                }
            }
            _tabShadowCache[key] = bmp;
            return bmp;
        }
        Color TabBase
        {
            get { return P.Mix(L.Bg, L.Ink, L.Dark ? 0.10f : 0.055f); }
        }
        void DrawContentTab(Graphics g, HubState st, Rectangle tab)
        {
            // Simple neumorphic inset: one path, darker fill, cached inner
            // shadow + lower highlight. No contour/trapezoid math.
            using (GraphicsPath p = LeftTab(tab, 16))
            using (SolidBrush fb = new SolidBrush(TabBase))
                g.FillPath(fb, p);

            Bitmap inner = GetTabShadow(tab.Width, tab.Height, L.Dark, true);
            if (inner != null)
            {
                GraphicsState shadowClip = g.Save();
                g.SetClip(new Rectangle(0, 0, tab.Right - 1, Geo.H));
                g.DrawImageUnscaled(inner, tab.X, tab.Y);
                g.Restore(shadowClip);
            }
            // bottom highlight: concave surface gets a light lower rim
            using (Pen hi = new Pen(Color.FromArgb(L.Dark ? 55 : 150, 255, 255, 255), 1.1f))
            {
                GraphicsState s = g.Save();
                g.SetClip(new Rectangle(0, 0, tab.Right - 1, Geo.H));
                g.DrawLine(hi, tab.X + 16, tab.Bottom - 1.5f, tab.Right - 3, tab.Bottom - 1.5f);
                g.Restore(s);
            }

            Color accent = P.Mix(Data.Apps[st.Page].Accent, L.Text, 0.15f);
            P.FillRound(g, new Rectangle(tab.X + 5, tab.Y + tab.Height / 2 - 9, 3, 18), 2, Color.FromArgb(225, accent));
        }

        void DrawHomePlate(Graphics g, Rectangle plate)
        {
            using (LinearGradientBrush lb = new LinearGradientBrush(plate,
                L.Dark ? P.Mix(L.RailBg, L.Ink, 0.08f) : Color.FromArgb(227, 233, 242), L.Dark ? P.Mix(L.RailBg, L.Ink, 0.03f) : Color.FromArgb(252, 253, 255), 55f))
                P.FillRound(g, plate, 16, lb);

            GraphicsState gs = g.Save();
            using (GraphicsPath clip = P.Round(plate, 16))
                g.SetClip(clip, CombineMode.Intersect);

            GraphicsState s1 = g.Save();
            g.TranslateTransform(2.2f, 2.2f);
            using (Pen dark = new Pen(Color.FromArgb(55, 38, 52, 72), 6f))
            using (GraphicsPath p = P.Round(plate, 16))
                g.DrawPath(dark, p);
            g.Restore(s1);

            GraphicsState s2 = g.Save();
            g.TranslateTransform(-2.2f, -2.2f);
            using (Pen light = new Pen(L.Dark ? Color.FromArgb(34, L.Ink) : Color.FromArgb(255, 255, 255, 255), 5f))
            using (GraphicsPath p = P.Round(plate, 16))
                g.DrawPath(light, p);
            g.Restore(s2);
            g.Restore(gs);

            P.DrawRound(g, plate, 16, L.Dark ? Color.FromArgb(70, L.CardBorder) : Color.FromArgb(30, 90, 110, 140), 1f);
            P.FillRound(g, new Rectangle(plate.X + 5, plate.Y + plate.Height / 2 - 9, 3, 18), 2, Color.FromArgb(225, Color.FromArgb(105, 118, 145)));
        }

        void DrawRailHome(Graphics g, HubState st)
        {
            Rectangle r = Geo.RailItem(0);
            float t = st.RailT[0];
            bool sel = st.Page < 0;

            if (t > 0.02f && !sel)
                P.FillRound(g, new Rectangle(r.X + 2, r.Y + 2, r.Width - 4, r.Height - 4), 13, Color.FromArgb((int)(30 * t), 120, 130, 150));

            Image home = L.Sketch ? Assets.Get("home") : null;
            if (home != null)
            {
                P.ImageRect(g, home, new Rectangle(r.X + 12, r.Y + 13, 28, 25), 0);
            }
            else
            {
                AnimIcon(g, st, "home", new Rectangle(r.X + 15, r.Y + 15, 22, 22), sel ? L.Text : L.Sub, "home");
            }
        }

        void DrawRailApp(Graphics g, HubState st, int i)
        {
            AppEntry a = Data.Apps[i];
            Rectangle r = Geo.RailItem(i + 1);
            bool sel = st.Page == i;
            bool hover = st.RailHot == i + 1;
            float t = st.RailT[i + 1];

            if (t > 0.02f && !sel)
                P.FillRound(g, new Rectangle(r.X + 2, r.Y + 2, r.Width - 4, r.Height - 4), 13, Color.FromArgb((int)(34 * t), 120, 130, 150));

            // icon transform
            float scale = 1f;
            float rot = 0f;
            if (t > 0.01f && !sel)
            {
                float amp = (float)Math.Sin(Math.Min(1f, t) * Math.PI);
                scale = 1f + 0.09f * amp;
                rot = 5f * (float)Math.Sin(st.Now / 90.0) * amp;
            }
            if (sel && st.Pulse > 0.01f)
            {
                scale += 0.05f * (float)Math.Sin(Math.Min(1f, st.Pulse) * Math.PI);
            }

            GraphicsState gs = g.Save();
            g.TranslateTransform(r.X + r.Width / 2f, r.Y + r.Height / 2f);
            g.RotateTransform(rot);
            g.ScaleTransform(scale, scale);
            Rectangle ir = new Rectangle(-21, -21, 42, 42);

            if (a.WhaleMark)
            {
                P.FillRound(g, Rectangle.Inflate(ir, 2, 2), 13, a.Brand);
                P.FillRound(g, ir, 11, a.Brand);
                Image whale = Assets.GetSized(a.MarkAsset, 30, 27);
                P.Tinted(g, whale, new Rectangle(-15, -15, 30, 27), Color.White);
            }
            else
            {
                Rectangle mr = a.Id == "llama" ? new Rectangle(-17, -17, 34, 34) : ir;
                Image mark = Assets.GetSized(L.Dark && a.Id == "llama" ? "llama_mark_dark" : a.MarkAsset, mr.Width, mr.Height);
                if (mark != null) P.ImageRect(g, mark, mr, 9);
                else P.FillRound(g, ir, 10, a.Brand);
            }
            if (L.Sketch)
            {
                Rectangle fr = new Rectangle(ir.X - 1, ir.Y - 1, ir.Width + 2, ir.Height + 2);
                P.RoughBorder(g, fr, 11, L.Ink, 1.7f, 700 + i);
            }
            g.Restore(gs);

            // running dot
            if (a.Running)
            {
                using (SolidBrush ring = new SolidBrush(L.RailBg))
                    g.FillEllipse(ring, r.Right - 13, r.Bottom - 13, 10, 10);
                P.Dot(g, L, r.Right - 8, r.Bottom - 8, 3, L.Green);
            }
        }

        void DrawStyleButton(Graphics g, HubState st)
        {
            Rectangle r = Geo.StyleBtn;
            bool hover = st.Hover == "style";
            P.FillRound(g, r, 12, hover ? Color.FromArgb(22, L.Ink) : Color.FromArgb(8, L.Ink));
            if (L.Dark) P.Icon(g, "sun", new Rectangle(r.X + 11, r.Y + 11, 20, 20), L.Text, 1f);
            else AnimIcon(g, st, "moon", new Rectangle(r.X + 11, r.Y + 11, 20, 20), L.Text, "");
        }

        void DrawRailCallout(Graphics g, HubState st, int appIndex, float t)
        {
            AppEntry a = Data.Apps[appIndex];
            Rectangle item = Geo.RailItem(appIndex + 1);
            Image srcWord = Assets.Get(a.WordAsset);
            int wh = 32;
            int ww = srcWord != null ? (int)(wh * (float)srcWord.Width / srcWord.Height) : 120;
            Image word = Assets.GetSized(a.WordAsset, ww, wh);
            int panelW = Math.Max(170, ww + 30);
            int panelH = 74;
            int x = 84 + (int)((1f - t) * 10f);
            int y = item.Y + item.Height / 2 - panelH / 2;
            Rectangle panel = new Rectangle(x, y, panelW, panelH);

            using (SolidBrush sc = new SolidBrush(Color.FromArgb(30, 15, 23, 42)))
                g.FillRectangle(sc, panel.X + 3, panel.Y + 4, panel.Width, panel.Height);

            if (L.Sketch)
            {
                P.FillRound(g, panel, 12, a.Brand);
                P.RoughBorder(g, panel, 12, L.CardBorder, 1.6f, panel.GetHashCode());
            }
            else
            {
                P.Shadow(g, panel, 12);
                P.FillRound(g, panel, 12, a.Brand);
                P.DrawRound(g, panel, 12, P.Mix(a.Brand, Color.White, 0.22f), 1f);
            }
            if (a.Id == "dsh")
            {
                Image head = Assets.GetSized("dsh_mark", 30, 30);
                if (head != null) g.DrawImageUnscaled(head, panel.X + 15, panel.Y + 10);
                P.Text(g, "DSH", L.FBody, Color.White, new Rectangle(panel.X + 53, panel.Y + 10, 120, 30), StringAlignment.Near, StringAlignment.Center);
            }
            else if (word != null)
                g.DrawImageUnscaled(word, panel.X + 15, panel.Y + 13);
            else
                P.Text(g, a.Name, L.FBodyBold, Color.White, new Rectangle(panel.X + 15, panel.Y + 13, panel.Width - 26, 32), StringAlignment.Near, StringAlignment.Center);
            string status = a.StateLabel + " · 端口 " + a.Port;
            Color stateC = a.Running ? L.Green : (a.Failed ? Color.FromArgb(248, 113, 113) : (a.Starting || a.Stopping ? Color.FromArgb(251, 191, 36) : P.Mix(Color.White, a.Brand, 0.35f)));
            P.Text(g, status, L.FSmall, stateC, new Rectangle(panel.X + 15, panel.Y + 48, panel.Width - 26, 18), StringAlignment.Near, StringAlignment.Center);
        }

        Color StateColor(AppEntry a)
        {
            if (a.Running) return L.Green;
            if (a.Starting || a.Stopping) return L.Amber;
            if (a.Failed) return L.Red;
            return L.Sub;
        }

        // ---------------- banner ----------------
        Color BrandOf(int page)
        {
            return page < 0 ? Color.FromArgb(27, 30, 39) : Data.Apps[page].Brand;
        }

        void DrawBanner(Graphics g, HubState st)
        {
            bool home = st.Page < 0;
            AppEntry app = home ? null : Data.Apps[st.Page];
            Color brand = BrandOf(st.Page);


            Rectangle r = Geo.Banner;
            using (LinearGradientBrush lb = new LinearGradientBrush(r, P.Mix(brand, Color.White, 0.05f), brand, 90f))
                g.FillRectangle(lb, r);

            if (L.Sketch)
            {
                using (Pen pen = new Pen(Color.FromArgb(40, 255, 255, 255), 1.6f))
                    g.DrawLine(pen, r.X + 14, r.Bottom - 1, r.Right - 14, r.Bottom - 1);
                using (Pen pen = new Pen(Color.FromArgb(80, 0, 0, 0), 2f))
                    g.DrawLine(pen, r.X, r.Bottom - 0.5f, r.Right, r.Bottom - 0.5f);
            }

            int x = 100;
            if (home)
            {
                P.Icon(g, "grid", new Rectangle(x, 34, 30, 30), Color.White, 1.2f);
                P.Text(g, "应用工作台", L.FBodyBold, Color.White, new Rectangle(x + 40, 30, 320, 24), StringAlignment.Near, StringAlignment.Center);
                P.Text(g, "本机服务 · 一屏启停", L.FSmall, P.Mix(Color.White, brand, 0.28f), new Rectangle(x + 40, 54, 320, 18), StringAlignment.Near, StringAlignment.Center);
            }
            else
            {
                if (app.Id == "dsh")
                {
                    Image head = Assets.GetSized("dsh_mark", 34, 34);
                    if (head != null) g.DrawImageUnscaled(head, x, (Geo.BannerH - 34) / 2);
                    x += 44;
                    P.Text(g, "DSH", L.FBodyBold, Color.White, new Rectangle(x, 30, 120, 24), StringAlignment.Near, StringAlignment.Center);
                    x += 52;
                }
                else
                {
                    Image srcWord = Assets.Get(app.WordAsset);
                    if (srcWord != null)
                    {
                        int wh = 34;
                        int ww = (int)(wh * (float)srcWord.Width / srcWord.Height);
                        Image word = Assets.GetSized(app.WordAsset, ww, wh);
                        Rectangle wr = new Rectangle(x, (Geo.BannerH - wh) / 2, ww, wh);
                        if (L.Sketch)
                        {
                            GraphicsState gs = g.Save();
                            g.TranslateTransform(wr.X + ww / 2f, wr.Y + wh / 2f);
                            g.RotateTransform(-1.1f);
                            g.DrawImage(word, new Rectangle(-ww / 2, -wh / 2, ww, wh));
                            g.Restore(gs);
                        }
                        else
                        {
                            g.DrawImageUnscaled(word, wr.X, wr.Y);
                        }
                        x = wr.Right + 18;
                    }
                    else
                    {
                        P.Text(g, app.Name, L.FBodyBold, Color.White, new Rectangle(x, 30, 260, 24), StringAlignment.Near, StringAlignment.Center);
                        x += 270;
                    }
                }
                P.Text(g, app.Sub, L.FSmall, P.Mix(Color.White, brand, 0.30f), new Rectangle(x, 40, 360, 28), StringAlignment.Near, StringAlignment.Center);
            }

            // right side status
            string status = home
                ? (Data.CountRunning() + " / " + Data.Apps.Count + " 个应用运行中")
                : (app.StateLabel + " · 端口 " + app.Port);
            Rectangle sr = new Rectangle(Geo.W - 150 - 320, 0, 300, Geo.BannerH);
            P.Text(g, status, L.FSmall, P.Mix(Color.White, brand, 0.16f), sr, StringAlignment.Far, StringAlignment.Center);
            if (!home)
            {
                Color dot = app.Running ? Color.FromArgb(74, 222, 128)
                    : (app.Starting || app.Stopping ? Color.FromArgb(240, 180, 70)
                    : (app.Failed ? Color.FromArgb(240, 96, 96) : Color.FromArgb(160, 160, 160)));
                P.Dot(g, L, sr.Right - P.TextW(g, status, L.FSmall) - 10, Geo.BannerH / 2, 3, dot);
            }

            DrawWindowButtons(g, st);
        }

        void AnimIcon(Graphics g, HubState st, string key, Rectangle r, Color c, string fallback)
        {
            AnimMeta m = Anim.Get(key);
            Image img = Assets.Get("anim/" + key);
            if (m == null || img == null)
            {
                P.Icon(g, fallback, r, c, 1f);
                return;
            }
            int f = Anim.Frame(st, key, m, 0);
            Anim.MarkUse(key); Anim.MarkAspect(key, r);
            P.TintedFrame(g, img, r, new Rectangle(f * m.W, 0, m.W, m.H), c);
        }

        void PlayPauseIcon(Graphics g, HubState st, AppEntry a, Rectangle r, Color c)
        {
            if (a.Starting || a.Stopping)
            {
                P.Icon(g, "stop", r, c, 1f);
                return;
            }
            AnimMeta m = Anim.Get("play-pause");
            Image img = Assets.Get("anim/play-pause");
            if (m == null || img == null)
            {
                P.Icon(g, a.PlayIcon, r, c, 1f);
                return;
            }
            int target = a.Running ? 0 : m.Frames - 1;
            int f = target;
            string key = "play:" + a.Id;
            long start;
            if (st.AnimStart.TryGetValue(key, out start))
            {
                long el = st.Now - start;
                if (el >= 0 && el < m.DurationMs)
                {
                    float p = el / (float)m.DurationMs;
                    int dir;
                    if (!st.AnimDir.TryGetValue(key, out dir)) dir = 1;
                    if (dir < 0) p = 1f - p;
                    f = Math.Max(0, Math.Min(m.Frames - 1, (int)Math.Round(p * (m.Frames - 1))));
                }
            }
            Anim.MarkUse("play-pause"); Anim.MarkAspect("play-pause", r);
            P.TintedFrame(g, img, r, new Rectangle(f * m.W, 0, m.W, m.H), c);
        }
        void DrawWindowButtons(Graphics g, HubState st)
        {
            for (int i = 0; i < 3; i++)
            {
                Rectangle r = Geo.WinBtn(i);
                string id = i == 0 ? "min" : (i == 1 ? "max" : "close");
                bool hover = st.Hover == id;
                if (hover)
                {
                    Color bg = i == 2 ? Color.FromArgb(232, 72, 72) : Color.FromArgb(36, 255, 255, 255);
                    P.FillRound(g, new Rectangle(r.X + 2, r.Y + 1, r.Width - 4, r.Height - 2), 8, bg);
                }
                Color ic = L.Sketch ? Color.FromArgb(240, 170, 60) : Color.FromArgb(225, 255, 255, 255);
                if (hover && i == 2) ic = Color.White;
                // Sprites are authored square. Never stretch them into the 44x30
                // caption-button rect; that is what made min/max/close look squat.
                int side = 40;
                Rectangle ir = new Rectangle(r.X + (r.Width - side) / 2, r.Y + (r.Height - side) / 2, side, side);
                if (i == 0) AnimIcon(g, st, "minus", ir, ic, "min");
                else if (i == 1) AnimIcon(g, st, st.Maximized ? "restore" : "max", ir, ic, st.Maximized ? "restore" : "max");
                else AnimIcon(g, st, "close", ir, ic, "close");
            }
        }

        // ---------------- app page ----------------
        void DrawApp(Graphics g, HubState st, AppEntry a)
        {
            System.Diagnostics.Stopwatch __ps = System.Diagnostics.Stopwatch.StartNew();
            DrawLeftCard(g, st, a);
            long __left = __ps.ElapsedMilliseconds;
            DrawRightCard(g, st, a);
            long __right = __ps.ElapsedMilliseconds;
            DrawTerminal(g, st, a, Geo.Term, false);
            Perf.PageDetail = "left=" + __left + " right=" + (__right - __left) + " term=" + (__ps.ElapsedMilliseconds - __right);
        }

        void DrawArt(Graphics g, AppEntry a, Rectangle r)
        {
            using (LinearGradientBrush lb = new LinearGradientBrush(r, P.Mix(a.Brand, Color.White, 0.10f), P.Mix(a.Brand, Color.Black, 0.35f), 25f))
                P.FillRound(g, r, 12, lb);

            GraphicsState gs = g.Save();
            using (GraphicsPath clip = P.Round(r, 12))
                g.SetClip(clip, CombineMode.Intersect);

            // decorative rings
            using (Pen pen = new Pen(Color.FromArgb(26, 255, 255, 255), 1.4f))
            {
                g.DrawEllipse(pen, r.Right - 120, r.Y - 50, 180, 180);
                g.DrawEllipse(pen, r.Right - 90, r.Y - 20, 120, 120);
            }
            if (a.Id == "dsh")
            {
                Image head = Assets.GetSized("dsh_mark", 28, 28);
                if (head != null) g.DrawImageUnscaled(head, r.X + 18, r.Y + 42);
                P.Text(g, "DSH", L.FBodyBold, Color.White, new Rectangle(r.X + 52, r.Y + 42, 120, 28), StringAlignment.Near, StringAlignment.Center);
            }
            else
            {
                Image srcWord = Assets.Get(a.WordAsset);
                if (srcWord != null)
                {
                    int wh = 26;
                    int ww = (int)(wh * (float)srcWord.Width / srcWord.Height);
                    Image word = Assets.GetSized(a.WordAsset, ww, wh);
                    g.DrawImageUnscaled(word, r.X + 18, r.Y + 44);
                }
                else
                {
                    P.Text(g, a.Name, L.FBodyBold, Color.White, new Rectangle(r.X + 18, r.Y + 42, r.Width - 36, 28), StringAlignment.Near, StringAlignment.Center);
                }
            }
            P.Text(g, a.Tag, L.FTiny, P.Mix(Color.White, a.Brand, 0.38f), new Rectangle(r.X + 18, r.Bottom - 24, r.Width - 36, 16), StringAlignment.Near, StringAlignment.Center);
            g.Restore(gs);
        }

        void DrawLeftCard(Graphics g, HubState st, AppEntry a)
        {
            Rectangle c = Geo.Left;
            P.Card(g, L, c, 18);
            if (L.Sketch) P.Tape(g, c.X + 52, c.Y + 3, 68, 17, -6f);
            Rectangle art = new Rectangle(c.X + 16, c.Y + 16, c.Width - 32, 112);
            DrawArt(g, a, art);

            bool run = a.Running;
            Color sc = StateColor(a);
            P.Dot(g, L, c.X + 27, c.Y + 176, 5, sc);
            P.Text(g, a.StateLabel, L.FStatus, a.Failed ? L.Red : (run ? L.Text : (a.Starting || a.Stopping ? L.Text : L.Sub)),
                new Rectangle(c.X + 40, c.Y + 158, 260, 34), StringAlignment.Near, StringAlignment.Center);

            P.Text(g, P.Trim(g, a.StatusLine, L.FSmall, c.Width - 40), L.FSmall, a.Failed ? L.Red : L.Sub,
                new Rectangle(c.X + 20, c.Y + 194, c.Width - 40, 18), StringAlignment.Near, StringAlignment.Center);

            string model = P.Trim(g, a.ModelLine, L.FSmall, c.Width - 40);
            Color modelColor = a.Failed ? L.Red : (run ? P.Mix(a.Accent, L.Text, 0.35f) : L.Faint);
            P.Text(g, model, L.FSmall, modelColor, new Rectangle(c.X + 20, c.Y + 212, c.Width - 40, 18), StringAlignment.Near, StringAlignment.Center);

            // buttons
            Rectangle play = new Rectangle(c.X + 16, c.Bottom - 72, 56, 56);
            string icon = a.Kind == "dsh" && run ? "web" : a.PlayIcon;
            bool playHover = st.Hover == "play";
            P.CircleButton(g, L, play, a.Accent, a.AccentText, "", playHover, L.Sketch);
            Rectangle pir = new Rectangle(play.X + 9, play.Y + 9, play.Width - 18, play.Height - 18);
            if (a.Kind == "dsh" && run) P.Icon(g, "web", pir, a.AccentText, 1f);
            else PlayPauseIcon(g, st, a, pir, a.AccentText);

            Rectangle restart = new Rectangle(play.Right + 12, c.Bottom - 64, 88, 40);
            bool canRestart = a.Kind == "dsh" ? true : (a.SupportsStop && (run || a.Starting || a.Failed));
            P.Button(g, L, restart, a.Kind == "dsh" ? "打开日志" : "重启", BtnKind.Secondary, a.Accent, a.AccentText, st.Hover == "restart", !canRestart);

            Rectangle logBtn = new Rectangle(c.Right - 56, c.Bottom - 64, 40, 40);
            P.IconCircle(g, L, logBtn, "", st.Hover == "log" ? L.Text : L.Sub, st.Hover == "log");
            AnimIcon(g, st, "file-text", new Rectangle(logBtn.X + 8, logBtn.Y + 8, 24, 24), st.Hover == "log" ? L.Text : L.Sub, "log");
            if (st.Hover == "log") P.DrawRound(g, logBtn, 20, L.CardBorder, 1f);
        }

        void DrawRightCard(Graphics g, HubState st, AppEntry a)
        {
            Rectangle c = Geo.Right;
            P.Card(g, L, c, 18);
            if (L.Sketch) P.Tape(g, c.Right - 76, c.Y + 3, 78, 18, 7f);

            // IP + port row
            P.Text(g, "IP", L.FSmall, L.Sub, new Rectangle(c.X + 18, 138, 24, 40), StringAlignment.Near, StringAlignment.Center);
            P.Field(g, L, new Rectangle(c.X + 44, 140, 190, 38), a.Ip, null, false);
            P.Text(g, "端口", L.FSmall, L.Sub, new Rectangle(c.X + 252, 138, 36, 40), StringAlignment.Near, StringAlignment.Center);
            P.Field(g, L, new Rectangle(c.X + 292, 140, 96, 38), a.Port.ToString(), null, false);

            Rectangle pill = new Rectangle(c.Right - 112, 145, 96, 28);
            P.FillRound(g, pill, 14, a.Running ? Color.FromArgb(30, L.Green) : (a.Failed ? Color.FromArgb(30, L.Red) : Color.FromArgb(16, L.Ink)));
            P.Dot(g, L, pill.X + 14, pill.Y + 14, 3, StateColor(a));
            P.Text(g, a.StateLabel, L.FTiny, StateColor(a), new Rectangle(pill.X + 22, pill.Y, pill.Width - 26, pill.Height), StringAlignment.Near, StringAlignment.Center);

            P.Divider(g, L, c.X + 18, c.Right - 18, 192);
            P.Text(g, "常用设置", L.FSmallBold, L.Sub, new Rectangle(c.X + 18, 198, 200, 20), StringAlignment.Near, StringAlignment.Center);

            if (a.Kind == "llama") DrawLlamaSettings(g, st, a, c);
            else if (a.Kind == "comfy") DrawComfySettings(g, st, a, c);
            else if (a.Kind == "dsh") DrawDshSettings(g, st, a, c);
            else DrawGenericSettings(g, st, a, c);

            int fy = a.Kind == "llama" ? 344 : 310;
            if (a.Folders != null && a.Folders.Length > 0) DrawFolders(g, st, a, c, fy);
            // Container query: the note only exists when the card has enough
            // vertical room above the bottom action row.
            int noteY = Math.Min(348, c.Bottom - 58 - 44 - 12);
            if (noteY >= 336 && c.Height >= 336)
            {
                if (a.Kind == "comfy")
                    DrawNote(g, c, noteY, "停止 = 直接结束进程树；换模型前先「重启」，避免 ROCm 混载模型 Abort。");
                else if (a.Kind == "dsh")
                    DrawNote(g, c, noteY, "停止会中断当前会话，所以不提供停止；只保留「打开 Web」与「打开日志」。");
            }

            // bottom actions
            Rectangle primary = new Rectangle(c.X + 18, c.Bottom - 58, 128, 40);
            string plabel;
            bool pdisabled = a.Starting || a.Stopping;
            if (a.Running) plabel = "打开 Web UI";
            else if (a.Starting) plabel = "启动中…";
            else if (a.Stopping) plabel = "停止中…";
            else plabel = "启动 " + a.Name;
            P.Button(g, L, primary, plabel, BtnKind.Primary, a.Accent, a.AccentText, st.Hover == "web", pdisabled);

            Rectangle secondary = new Rectangle(primary.Right + 10, c.Bottom - 58, 96, 40);
            string slabel = a.Kind == "dsh" ? "打开日志" : "重启";
            bool sdisabled = a.Kind == "dsh" ? false : !(a.Running || a.Starting || a.Failed);
            P.Button(g, L, secondary, slabel, BtnKind.Secondary, a.Accent, a.AccentText, st.Hover == "restart2", sdisabled);

            Rectangle gear = new Rectangle(c.Right - 62, c.Bottom - 60, 44, 44);
            P.IconCircle(g, L, gear, "", st.Hover == "gear" ? L.Text : L.Sub, st.Hover == "gear");
            AnimIcon(g, st, "settings", new Rectangle(gear.X + 8, gear.Y + 8, 28, 28), st.Hover == "gear" ? L.Text : L.Sub, "gear");
            if (st.Hover == "gear") P.DrawRound(g, gear, 22, L.CardBorder, 1f);
        }

        void DrawLlamaSettings(Graphics g, HubState st, AppEntry a, Rectangle c)
        {
            P.Text(g, "模型", L.FSmall, L.Sub, new Rectangle(c.X + 18, 218, 40, 40), StringAlignment.Near, StringAlignment.Center);
            P.Field(g, L, new Rectangle(c.X + 62, 220, 620, 40), a.ModelDisplay.Length > 0 ? a.ModelDisplay : "未选择模型", null, true);

            P.Text(g, "上下文", L.FSmall, L.Sub, new Rectangle(c.X + 18, 272, 50, 36), StringAlignment.Near, StringAlignment.Center);
            P.Field(g, L, new Rectangle(c.X + 72, 272, 100, 36), a.Ctx.ToString(), null, false);
            P.Text(g, "启动后打开浏览器", L.FSmall, L.Sub, new Rectangle(c.X + 194, 272, 120, 36), StringAlignment.Near, StringAlignment.Center);
            P.Toggle(g, L, new Rectangle(c.X + 314, 279, 40, 22), a.AutoOpenBrowser, a.Accent);
            P.Text(g, "视觉 mmproj：" + a.MmprojDisplay, L.FSmall, L.Faint, new Rectangle(c.X + 380, 272, 300, 36), StringAlignment.Near, StringAlignment.Center);
        }

        void DrawComfySettings(Graphics g, HubState st, AppEntry a, Rectangle c)
        {
            P.Text(g, P.Trim(g, a.ModelLine, L.FBody, 460), L.FBody, L.Sub, new Rectangle(c.X + 18, 218, 460, 34), StringAlignment.Near, StringAlignment.Center);
            string hint = "额外参数：" + (a.ExtraArgs.Length > 0 ? a.ExtraArgs : "无") + " · 环境变量 " + a.EnvSummary;
            P.Text(g, P.Trim(g, hint, L.FSmall, c.Width - 36), L.FSmall, L.Faint, new Rectangle(c.X + 18, 252, c.Width - 36, 22), StringAlignment.Near, StringAlignment.Center);
        }

        void DrawDshSettings(Graphics g, HubState st, AppEntry a, Rectangle c)
        {
            P.Text(g, "远程入口", L.FSmall, L.Sub, new Rectangle(c.X + 18, 216, 70, 40), StringAlignment.Near, StringAlignment.Center);
            P.Field(g, L, new Rectangle(c.X + 88, 218, 400, 38), a.RemoteHost != null && a.RemoteHost.Length > 0 ? a.RemoteHost : "未配置远程入口", null, false);
            P.Button(g, L, new Rectangle(c.X + 498, 218, 64, 38), "复制", BtnKind.Secondary, a.Accent, a.AccentText, false, false);
            P.Text(g, "由 DSH-Guardian 守护；停止会中断会话，因此不提供停止", L.FSmall, L.Faint, new Rectangle(c.X + 18, 262, 660, 28), StringAlignment.Near, StringAlignment.Center);
        }

        void DrawGenericSettings(Graphics g, HubState st, AppEntry a, Rectangle c)
        {
            string title = a.Sub.Length > 0 ? a.Sub : "由 apps/<id>/app.json 定义";
            P.Text(g, P.Trim(g, title, L.FBody, c.Width - 36), L.FBody, L.Sub,
                new Rectangle(c.X + 18, 218, c.Width - 36, 34), StringAlignment.Near, StringAlignment.Center);
            string cmd = a.LaunchCommand.Length > 0 ? a.LaunchCommand : "未配置 launch.command";
            P.Text(g, P.Trim(g, "启动：" + cmd, L.FSmall, c.Width - 36), L.FSmall, L.Faint,
                new Rectangle(c.X + 18, 252, c.Width - 36, 22), StringAlignment.Near, StringAlignment.Center);
            string health = a.HealthType.Length > 0 ? a.HealthType : "none";
            P.Text(g, "健康检查：" + health, L.FSmall, L.Faint,
                new Rectangle(c.X + 18, 276, c.Width - 36, 22), StringAlignment.Near, StringAlignment.Center);
        }
        void DrawNote(Graphics g, Rectangle c, int y, string text)
        {
            Rectangle r = new Rectangle(c.X + 18, y, c.Width - 36, 44);
            P.FillRound(g, r, 10, Color.FromArgb(8, L.Ink));
            if (L.Sketch) P.RoughBorder(g, r, 10, Color.FromArgb(70, L.Ink), 1.2f, 909);
            P.Text(g, P.Trim(g, text, L.FSmall, r.Width - 26), L.FSmall, L.Sub, new Rectangle(r.X + 13, r.Y, r.Width - 26, r.Height), StringAlignment.Near, StringAlignment.Center);
        }

        void DrawFolders(Graphics g, HubState st, AppEntry a, Rectangle c, int y)
        {
            st.ChipRects.Clear();
            st.ChipLabels.Clear();
            P.Text(g, "常用目录", L.FSmallBold, L.Sub, new Rectangle(c.X + 18, y - 22, 200, 18), StringAlignment.Near, StringAlignment.Center);
            int x = c.X + 18;
            int maxX = c.Right - 18;
            for (int i = 0; i < a.Folders.Length; i++)
            {
                string f = a.Folders[i];
                int w = P.TextW(g, f, L.FSmall) + 24;
                if (x + w > maxX) break;
                Rectangle cr = new Rectangle(x, y, w, 32);
                P.Chip(g, L, cr, f, st.Hover == "chip" + i);
                st.ChipRects.Add(cr);
                st.ChipLabels.Add(f);
                x += w + 8;
            }
        }

        // ---------------- terminal ----------------
        void DrawTerminal(Graphics g, HubState st, AppEntry a, Rectangle r, bool big)
        {
            if (L.Sketch)
            {
                Rectangle frame = Rectangle.Inflate(r, 6, 6);
                P.Card(g, L, frame, 18);
            }
            P.FillRound(g, r, 16, L.TermBg);
            if (L.Sketch)
            {
                P.RoughBorder(g, r, 16, Color.FromArgb(170, L.TermBorder), 1.8f, r.GetHashCode());
                P.Tape(g, r.X + 96, r.Y + 3, 66, 14, -4f);
            }
            else
            {
                P.DrawRound(g, r, 16, L.TermBorder, 1f);
            }
            int hh = big ? 56 : 44;
            Rectangle hr = new Rectangle(r.X, r.Y, r.Width, hh);
            using (GraphicsPath top = TopRound(hr, 16))
            using (SolidBrush hb = new SolidBrush(L.TermHeader))
                g.FillPath(hb, top);

            P.Text(g, big ? "命令行 / 日志" : "命令行 / 日志", L.FSmallBold, L.TermText, new Rectangle(r.X + 18, r.Y + 6, 240, hh - 12), StringAlignment.Near, StringAlignment.Center);
            P.Text(g, big ? ("· " + a.Name) : ((a.Running || a.Starting) ? "· 实时输出" : "· 最近日志"), L.FTiny, L.TermDim, new Rectangle(r.X + 116, r.Y + 6, 200, hh - 12), StringAlignment.Near, StringAlignment.Center);

            // right controls
            Rectangle exp = new Rectangle(r.Right - 56, r.Y + (hh - 28) / 2, 36, 28);
            if (big)
            {
                Rectangle close = new Rectangle(r.Right - 56, r.Y + (hh - 32) / 2, 32, 32);
                P.IconCircle(g, L, close, "close", st.Hover == "expclose" ? Color.White : Color.FromArgb(220, L.TermText), st.Hover == "expclose");
            }
            else
            {
                P.IconCircle(g, L, exp, "", st.Hover == "expand" ? Color.White : Color.FromArgb(220, L.TermText), st.Hover == "expand");
                AnimIcon(g, st, "maximize-2", new Rectangle(exp.X + 6, exp.Y + 6, 24, 24), st.Hover == "expand" ? Color.White : Color.FromArgb(220, L.TermText), "expand");
                Rectangle live = new Rectangle(r.Right - 148, r.Y + (hh - 24) / 2, 62, 24);
                Color liveColor = (a.Running || a.Starting) ? L.TermOk : L.TermDim;
                P.FillRound(g, live, 12, Color.FromArgb(26, liveColor));
                P.Dot(g, L, live.X + 12, live.Y + 12, 3, liveColor);
                P.Text(g, (a.Running || a.Starting) ? "实时" : "历史", L.FTiny, liveColor, new Rectangle(live.X + 19, live.Y, 38, 24), StringAlignment.Near, StringAlignment.Center);
                P.Text(g, "打开日志文件", L.FTiny, st.Hover == "openlog" ? Color.White : L.TermDim, new Rectangle(r.Right - 272, r.Y, 110, hh), StringAlignment.Far, StringAlignment.Center);
            }

            Rectangle body = new Rectangle(r.X + 1, r.Y + hh, r.Width - 2, r.Height - hh - 1);
            List<LogLine> lines = a.Log;
            int lineH = big ? 22 : 20;
            int maxLines = (body.Height - 16) / lineH;
            int start = Math.Max(0, lines.Count - maxLines);
            int y = body.Y + 10;
            for (int i = start; i < lines.Count; i++)
            {
                LogLine ln = lines[i];
                Color lc = LevelColor(ln.Kind);
                int x = body.X + 16;
                if (ln.Time.Length > 0)
                {
                    int tw = ln.Time.Length > 14 ? (big ? 190 : 156) : (big ? 150 : 96);
                    P.Text(g, ln.Time, L.FMonoSmall, L.TermDim, new Rectangle(x, y, tw, lineH), StringAlignment.Near, StringAlignment.Center);
                    x += tw + 4;
                }
                if (ln.Level.Length > 0)
                {
                    P.Text(g, ln.Level, L.FMonoSmall, lc, new Rectangle(x, y, 22, lineH), StringAlignment.Near, StringAlignment.Center);
                    x += 22;
                }
                string text = P.Trim(g, ln.Text, L.FMonoSmall, body.Right - x - 12);
                P.Text(g, text, L.FMonoSmall, lc, new Rectangle(x, y, body.Right - x - 10, lineH), StringAlignment.Near, StringAlignment.Center);
                y += lineH;
            }
        }



        Color LevelColor(LogKind k)
        {
            switch (k)
            {
                case LogKind.Info: return L.TermInfo;
                case LogKind.Warn: return L.TermWarn;
                case LogKind.Error: return L.TermError;
                case LogKind.Ok: return L.TermOk;
                case LogKind.Dim: return L.TermDim;
                default: return L.TermText;
            }
        }

        // ---------------- expanded terminal ----------------
        void DrawExpanded(Graphics g, HubState st)
        {
            AppEntry a = st.Page >= 0 ? Data.Apps[st.Page] : Data.Apps[0];
            Rectangle r = Geo.Content;
            using (SolidBrush sc = new SolidBrush(Color.FromArgb(90, 15, 23, 42)))
                g.FillRectangle(sc, 0, 0, Geo.W, Geo.H);
            P.Shadow(g, r, 18);
            DrawTerminal(g, st, a, r, true);
        }

        // ---------------- popover ----------------
        void DrawPopover(Graphics g, HubState st)
        {
            AppEntry a = st.Page >= 0 ? Data.Apps[st.Page] : Data.Apps[0];
            Rectangle r = Geo.Pop;
            if (L.Sketch)
            {
                using (SolidBrush sc = new SolidBrush(Color.FromArgb(35, L.Ink)))
                    g.FillRectangle(sc, r.X + 5, r.Y + 6, r.Width, r.Height);
                P.FillRound(g, r, 15, L.CardBg);
                if (L.Noise != null) P.FillRound(g, r, 15, L.Noise);
                P.RoughBorder(g, r, 15, L.CardBorder, 1.7f, 77);
            }
            else
            {
                P.Shadow(g, r, 16);
                P.FillRound(g, r, 16, L.CardBg);
                P.DrawRound(g, r, 16, L.CardBorder, 1f);
            }
            P.Text(g, "设置 · " + a.Name, L.FBodyBold, L.Text, new Rectangle(r.X + 20, r.Y + 14, 230, 28), StringAlignment.Near, StringAlignment.Center);
            P.Text(g, "不常用参数", L.FTiny, L.Faint, new Rectangle(r.X + 20, r.Y + 38, 200, 16), StringAlignment.Near, StringAlignment.Center);
            Rectangle close = new Rectangle(r.Right - 44, r.Y + 14, 30, 30);
            P.IconCircle(g, L, close, "close", st.Hover == "popclose" ? L.Text : L.Sub, st.Hover == "popclose");
            P.Divider(g, L, r.X + 18, r.Right - 18, r.Y + 60);

            if (a.Id == "llama")
            {
                PopRow(g, r, a.Accent, 0, "视觉投影 mmproj", "text", a.MmprojDisplay);
                PopRow(g, r, a.Accent, 1, "额外参数 ExtraArgs", "text", a.ExtraArgs.Length > 0 ? a.ExtraArgs : "无");
                PopRow(g, r, a.Accent, 2, "启动后打开浏览器", "toggle", a.AutoOpenBrowser ? "1" : "0");
                PopRow(g, r, a.Accent, 3, "模型目录", "text", a.InstallPath.Length > 0 ? a.InstallPath : "未配置");
                PopRow(g, r, a.Accent, 4, "监听端口", "field", a.Port.ToString());
            }
            else if (a.Id == "comfy")
            {
                PopRow(g, r, a.Accent, 0, "额外参数", "text", a.ExtraArgs.Length > 0 ? a.ExtraArgs : "无");
                PopRow(g, r, a.Accent, 1, "环境变量", "text", a.EnvSummary + "（改配置文件）");
                PopRow(g, r, a.Accent, 2, "启动后打开浏览器", "toggle", a.AutoOpenBrowser ? "1" : "0");
                PopRow(g, r, a.Accent, 3, "安装路径", "text", a.InstallPath);
                PopRow(g, r, a.Accent, 4, "Python", "text", a.PythonPath);
            }
            else
            {
                PopRow(g, r, a.Accent, 0, "监听端口", "field", a.Port.ToString());
                PopRow(g, r, a.Accent, 1, "打开浏览器", "toggle", a.AutoOpenBrowser ? "1" : "0");
                PopRow(g, r, a.Accent, 2, "服务目录", "text", a.InstallPath.Length > 0 ? a.InstallPath : "未配置");
                PopRow(g, r, a.Accent, 3, "配置文件", "text", a.ConfigPath);
                PopRow(g, r, a.Accent, 4, "日志", "text", a.LogPath);
            }

            P.Divider(g, L, r.X + 18, r.Right - 18, r.Bottom - 62);
            P.Button(g, L, new Rectangle(r.X + 18, r.Bottom - 48, 86, 34), "打开配置", BtnKind.Secondary, a.Accent, a.AccentText, st.Hover == "openconfig", false);
            P.Button(g, L, new Rectangle(r.X + 110, r.Bottom - 48, 86, 34), "打开日志", BtnKind.Secondary, a.Accent, a.AccentText, st.Hover == "openlog2", false);
            P.Button(g, L, new Rectangle(r.Right - 18 - 84, r.Bottom - 48, 84, 34), "完成", BtnKind.Primary, a.Accent, a.AccentText, st.Hover == "popdone", false);
        }

        void PopRow(Graphics g, Rectangle r, Color accent, int i, string label, string type, string value)
        {
            int y = r.Y + 74 + i * 50;
            P.Text(g, label, L.FSmall, L.Sub, new Rectangle(r.X + 18, y, 140, 34), StringAlignment.Near, StringAlignment.Center);
            if (type == "field")
                P.Field(g, L, new Rectangle(r.X + 160, y, 178, 34), value, null, false);
            else if (type == "toggle")
                P.Toggle(g, L, new Rectangle(r.X + 298, y + 6, 40, 22), value == "1", accent);
            else
                P.Text(g, P.Trim(g, value, L.FSmall, 178), L.FSmall, L.Text, new Rectangle(r.X + 160, y, 178, 34), StringAlignment.Near, StringAlignment.Center);
        }

        // ---------------- home ----------------
        void DrawHome(Graphics g, HubState st)
        {
            P.Text(g, "总览", L.FTitle, L.Text, new Rectangle(Geo.Content.X, Geo.Content.Y - 2, 200, 34), StringAlignment.Near, StringAlignment.Center);
            if (L.Sketch) P.Squiggle(g, 99, 155, 42, Color.FromArgb(160, L.Amber), 1.8f);
            P.Text(g, "3 个应用 · " + Data.CountRunning() + " 个运行中 · 更新于 " + DateTime.Now.ToString("HH:mm"), L.FSmall, L.Sub, new Rectangle(Geo.Content.X, Geo.Content.Y + 30, 520, 20), StringAlignment.Near, StringAlignment.Center);

            Rectangle refresh = new Rectangle(Geo.Content.Right - 100, Geo.Content.Y + 2, 100, 34);
            if (st.Hover == "refresh") P.FillRound(g, refresh, 9, Color.FromArgb(14, L.Ink));
            AnimIcon(g, st, "refresh-v2", new Rectangle(refresh.X + 7, refresh.Y + 6, 22, 22), st.Hover == "refresh" ? L.Text : L.Sub, "refresh");
            P.Text(g, "刷新状态", L.FSmall, st.Hover == "refresh" ? L.Text : L.Sub, new Rectangle(refresh.X + 30, refresh.Y, 66, refresh.Height), StringAlignment.Near, StringAlignment.Center);

            for (int i = 0; i < Data.Apps.Count; i++)
                DrawHomeCard(g, st, i, Geo.HomeCard(i));

            P.Text(g, "提示：点卡片进入应用页；所有启动/停止/日志都在应用页操作。", L.FTiny, L.Faint, new Rectangle(Geo.Content.X, Geo.H - 128, 620, 20), StringAlignment.Near, StringAlignment.Center);
            P.Text(g, "模板说明：新增应用 = Catalog 定义 + AppRuntime 分支（详见 README）", L.FTiny, L.Faint, new Rectangle(Geo.Content.X, Geo.H - 128, Geo.Content.Width, 20), StringAlignment.Far, StringAlignment.Center);
        }

        void DrawHomeCard(Graphics g, HubState st, int index, Rectangle r)
        {
            AppEntry a = Data.Apps[index];
            bool hover = st.Hover == "card" + index;
            GraphicsState cardGs = g.Save();
            if (L.Sketch)
            {
                float ang = index == 0 ? -0.45f : (index == 1 ? 0.3f : -0.25f);
                g.TranslateTransform(r.X + r.Width / 2f, r.Y + r.Height / 2f);
                g.RotateTransform(ang);
                g.TranslateTransform(-(r.X + r.Width / 2f), -(r.Y + r.Height / 2f));
            }
            P.Card(g, L, r, 18);
            if (L.Sketch) P.Tape(g, r.Right - 58, r.Y + 5, 68, 17, 6f);

            Rectangle head = new Rectangle(r.X, r.Y, r.Width, 92);
            using (GraphicsPath hp = TopRound(head, 18))
            using (LinearGradientBrush lb = new LinearGradientBrush(head, P.Mix(a.Brand, Color.White, 0.08f), a.Brand, 20f))
                g.FillPath(lb, hp);
            if (L.Sketch)
            {
                using (Pen pen = new Pen(L.CardBg, 3.4f))
                {
                    for (int tx = head.X + 6; tx < head.Right - 6; tx += 15)
                    {
                        g.DrawLine(pen, tx, head.Bottom - 1, tx + 7, head.Bottom - 5);
                        g.DrawLine(pen, tx + 7, head.Bottom - 5, tx + 15, head.Bottom - 1);
                    }
                }
            }

            Rectangle tile = new Rectangle(r.X + 18, r.Y + 20, 44, 44);
            if (L.Sketch)
            {
                P.FillRound(g, tile, 11, L.CardBg);
                P.RoughBorder(g, tile, 11, L.CardBorder, 1.4f, 900 + index);
                Image mark = Assets.Get(a.MarkAsset);
                if (a.WhaleMark) P.Tinted(g, mark, new Rectangle(tile.X + 7, tile.Y + 8, 30, 27), L.Ink);
                else P.ImageRect(g, mark, new Rectangle(tile.X + 6, tile.Y + 6, 32, 32), 8);
            }
            else
            {
                Image mark = Assets.GetSized(a.MarkAsset, a.WhaleMark ? 30 : 34, a.WhaleMark ? 27 : 34);
                if (a.WhaleMark)
                {
                    P.FillRound(g, tile, 12, a.Brand);
                    P.Tinted(g, mark, new Rectangle(tile.X + 7, tile.Y + 8, 30, 27), Color.White);
                }
                else
                {
                    P.FillRound(g, tile, 12, Color.White);
                    P.ImageRect(g, mark, new Rectangle(tile.X + 5, tile.Y + 5, 34, 34), 9);
                }
            }

            P.Text(g, a.Name, L.FBodyBold, Color.White, new Rectangle(r.X + 72, r.Y + 24, 180, 24), StringAlignment.Near, StringAlignment.Center);
            P.Text(g, a.Tag + " · " + a.Port, L.FTiny, P.Mix(Color.White, a.Brand, 0.30f), new Rectangle(r.X + 72, r.Y + 48, 220, 18), StringAlignment.Near, StringAlignment.Center);

            Rectangle tag = new Rectangle(r.Right - 100, r.Y + 26, 84, 24);
            P.FillRound(g, tag, 12, Color.FromArgb(28, 255, 255, 255));
            Color tagColor = a.Running ? Color.FromArgb(74, 222, 128) : (a.Failed ? Color.FromArgb(240, 96, 96) : (a.Starting || a.Stopping ? Color.FromArgb(240, 180, 70) : Color.FromArgb(180, 180, 180)));
            P.Dot(g, L, tag.X + 12, tag.Y + 12, 3, tagColor);
            P.Text(g, a.StateLabel, L.FTiny, Color.White, new Rectangle(tag.X + 21, tag.Y, tag.Width - 26, tag.Height), StringAlignment.Near, StringAlignment.Center);

            for (int k = 0; k < a.HomeRows.Length && k < 3; k++)
            {
                string[] parts = a.HomeRows[k].Split('|');
                string label = parts[0];
                string val = parts.Length > 1 ? parts[1] : "";
                if (label == "状态") val = a.StateLabel;
                int y = r.Y + 108 + k * 32;
                P.Text(g, label, L.FSmall, L.Sub, new Rectangle(r.X + 20, y, 60, 30), StringAlignment.Near, StringAlignment.Center);
                Color valColor = L.Text;
                if (label == "状态") valColor = a.Running ? L.Green : (a.Failed ? L.Red : (a.Starting || a.Stopping ? L.Amber : L.Text));
                P.Text(g, val, L.FSmall, valColor, new Rectangle(r.X + 84, y, r.Width - 104, 30), StringAlignment.Near, StringAlignment.Center);
            }

            P.Text(g, "常用目录", L.FSmallBold, L.Sub, new Rectangle(r.X + 20, r.Y + 212, 160, 18), StringAlignment.Near, StringAlignment.Center);
            int cx = r.X + 20;
            int cy = r.Y + 232;
            for (int k = 0; k < a.Folders.Length; k++)
            {
                int w = P.TextW(g, a.Folders[k], L.FSmall) + 22;
                if (cx + w > r.Right - 20)
                {
                    cx = r.X + 20;
                    cy += 36;
                }
                if (cy > r.Y + 270) break;
                P.Chip(g, L, new Rectangle(cx, cy, w, 30), a.Folders[k], false);
                cx += w + 8;
            }

            string hint;
            if (a.Failed) hint = P.Trim(g, a.Error, L.FTiny, r.Width - 40);
            else if (a.Starting || a.Stopping) hint = P.Trim(g, a.StatusLine, L.FTiny, r.Width - 40);
            else if (a.Id == "dsh") hint = "远程：tailnet 内可用";
            else hint = a.Running ? "运行正常 · 端口占用已确认" : "端口占用检查：启动前自动";
            P.Text(g, hint, L.FTiny, a.Failed ? L.Red : L.Faint, new Rectangle(r.X + 20, r.Y + 306, r.Width - 40, 20), StringAlignment.Near, StringAlignment.Center);
            P.Text(g, "上次使用 " + a.LastRun, L.FTiny, L.Faint, new Rectangle(r.X + 20, r.Y + 336, r.Width - 40, 20), StringAlignment.Near, StringAlignment.Center);

            Rectangle primary = new Rectangle(r.X + 20, r.Bottom - 58, 100, 38);
            string plabel;
            bool pdisabled = a.Starting || a.Stopping;
            if (a.Starting) plabel = "启动中";
            else if (a.Stopping) plabel = "停止中";
            else if (a.Id == "dsh") plabel = a.Running ? "打开 Web" : "启动";
            else plabel = a.Running ? "停止" : "启动";
            P.Button(g, L, primary, plabel, BtnKind.Primary, a.Accent, a.AccentText, st.Hover == "cardstart" + index, pdisabled);
            Rectangle secondary = new Rectangle(primary.Right + 10, r.Bottom - 58, 100, 38);
            string slabel = (a.Id == "dsh" && a.Running) ? "打开日志" : "打开 Web";
            bool sdisabled = a.Id != "dsh" && !a.Running;
            P.Button(g, L, secondary, slabel, BtnKind.Secondary, a.Accent, a.AccentText, st.Hover == "cardopen" + index, sdisabled);

            if (hover)
            {
                // hover wash + ring
                using (GraphicsPath p = P.Round(r, 18))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(8, L.Ink)))
                    g.FillPath(b, p);
                P.DrawRound(g, r, 18, Color.FromArgb(120, L.Sub), 1.2f);
            }
            g.Restore(cardGs);
        }

        void DrawDropdown(Graphics g, HubState st)
        {
            Rectangle r = st.DropRect;
            if (r.Width <= 0) return;
            if (L.Sketch)
            {
                P.FillRound(g, r, 12, L.CardBg);
                P.RoughBorder(g, r, 12, L.CardBorder, 1.4f, 3131);
            }
            else
            {
                P.Shadow(g, r, 12);
                P.FillRound(g, r, 12, L.CardBg);
                P.DrawRound(g, r, 12, L.CardBorder, 1f);
            }
            for (int i = 0; i < st.DropItems.Count; i++)
            {
                Rectangle ir = new Rectangle(r.X + 6, r.Y + 4 + i * 36, r.Width - 12, 34);
                bool hover = st.Hover == "drop:" + i;
                if (hover) P.FillRound(g, ir, 8, Color.FromArgb(18, 90, 110, 140));
                string s = st.DropItems[i].Display;
                bool current = i < st.DropItems.Count && st.DropOwner >= 0
                    && st.DropOwner < Data.Apps.Count
                    && Data.Apps[st.DropOwner].ModelDisplay.Length > 0
                    && s.StartsWith(Data.Apps[st.DropOwner].ModelDisplay);
                P.Text(g, P.Trim(g, s, L.FBody, ir.Width - 40), L.FBody, current ? L.Text : L.Sub,
                    new Rectangle(ir.X + 12, ir.Y, ir.Width - 24, ir.Height), StringAlignment.Near, StringAlignment.Center);
                if (current) P.Dot(g, L, ir.Right - 14, ir.Y + ir.Height / 2, 3, L.Green);
            }
            if (st.DropItems.Count == 0)
                P.Text(g, "没有可用模型", L.FSmall, L.Faint, new Rectangle(r.X + 12, r.Y + 4, r.Width - 24, 34), StringAlignment.Near, StringAlignment.Center);
        }

        void DrawToast(Graphics g, HubState st)
        {
            if (string.IsNullOrEmpty(st.Toast) || st.Now > st.ToastUntil) return;
            int w = P.TextW(g, st.Toast, L.FSmall) + 34;
            Rectangle r = new Rectangle((Geo.W - w) / 2, Geo.H - 66, w, 34);
            P.FillRound(g, r, 12, Color.FromArgb(235, 20, 22, 28));
            P.TextC(g, st.Toast, L.FSmall, Color.White, r);
        }

        // ---------------- hit test ----------------
        public string Hit(HubState st, Point p)
        {
            if (st.Dropdown)
            {
                for (int i = 0; i < st.DropItems.Count; i++)
                {
                    Rectangle ir = new Rectangle(st.DropRect.X + 6, st.DropRect.Y + 4 + i * 36, st.DropRect.Width - 12, 34);
                    if (ir.Contains(p)) return "drop:" + i;
                }
                return st.DropRect.Contains(p) ? "drop" : "dropclose";
            }
            if (st.Expanded)
            {
                Rectangle close = new Rectangle(Geo.Content.Right - 56, Geo.Content.Y + 12, 32, 32);
                if (close.Contains(p)) return "expclose";
                return "modal";
            }
            if (st.Popover)
            {
                Rectangle close = new Rectangle(Geo.Pop.Right - 44, Geo.Pop.Y + 14, 30, 30);
                if (close.Contains(p)) return "popclose";
                if (new Rectangle(Geo.Pop.X + 18, Geo.Pop.Bottom - 48, 86, 34).Contains(p)) return "openconfig";
                if (new Rectangle(Geo.Pop.X + 110, Geo.Pop.Bottom - 48, 86, 34).Contains(p)) return "openlog2";
                if (new Rectangle(Geo.Pop.Right - 102, Geo.Pop.Bottom - 48, 84, 34).Contains(p)) return "popdone";
                if (new Rectangle(Geo.Pop.X + 298, Geo.Pop.Y + 74 + 2 * 50 + 6, 40, 22).Contains(p)) return "poptoggle";
                if (Geo.Pop.Contains(p)) return "pop";
                return "popclose";
            }

            for (int i = 0; i < 3; i++)
                if (Geo.WinBtn(i).Contains(p)) return i == 0 ? "min" : (i == 1 ? "max" : "close");

            for (int i = 0; i < 4; i++)
                if (Geo.RailItem(i).Contains(p)) return "rail" + i;

            if (Geo.StyleBtn.Contains(p)) return "style";

            if (Geo.Banner.Contains(p)) return "drag";

            if (st.Page < 0)
            {
                for (int i = 0; i < Data.Apps.Count; i++)
                {
                    Rectangle c = Geo.HomeCard(i);
                    Rectangle primary = new Rectangle(c.X + 20, c.Bottom - 58, 100, 38);
                    Rectangle secondary = new Rectangle(primary.Right + 10, c.Bottom - 58, 100, 38);
                    if (primary.Contains(p)) return "cardstart" + i;
                    if (secondary.Contains(p)) return "cardopen" + i;
                    if (c.Contains(p)) return "card" + i;
                }
                if (new Rectangle(1120, 130, 100, 34).Contains(p)) return "refresh";
                return "";
            }

            Rectangle lc = Geo.Left;
            if (new Rectangle(lc.X + 16, lc.Bottom - 72, 56, 56).Contains(p)) return "play";
            if (new Rectangle(lc.X + 84, lc.Bottom - 64, 88, 40).Contains(p)) return "restart";
            if (new Rectangle(lc.Right - 56, lc.Bottom - 64, 40, 40).Contains(p)) return "log";

            Rectangle rc = Geo.Right;
            if (new Rectangle(rc.X + 18, rc.Bottom - 58, 128, 40).Contains(p)) return "web";
            if (new Rectangle(rc.X + 156, rc.Bottom - 58, 96, 40).Contains(p)) return "restart2";
            if (new Rectangle(rc.Right - 62, rc.Bottom - 60, 44, 44).Contains(p)) return "gear";

            AppEntry app = Data.Apps[st.Page];
            if (app.Id == "llama")
            {
                if (new Rectangle(rc.X + 62, 220, 620, 40).Contains(p)) return "model";
                if (new Rectangle(rc.X + 72, 272, 100, 36).Contains(p)) return "ctx";
                if (new Rectangle(rc.X + 314, 279, 40, 22).Contains(p)) return "auto";
            }
            if (app.Id == "dsh" && new Rectangle(rc.X + 498, 218, 64, 38).Contains(p)) return "copyremote";
            if (app.Id != "dsh" && new Rectangle(rc.X + 292, 140, 96, 38).Contains(p)) return "port";

            for (int i = 0; i < st.ChipRects.Count; i++)
                if (st.ChipRects[i].Contains(p)) return "chip:" + i;

            if (new Rectangle(Geo.Term.Right - 56, Geo.Term.Y + 8, 36, 28).Contains(p)) return "expand";
            if (new Rectangle(Geo.Term.Right - 272, Geo.Term.Y, 110, 44).Contains(p)) return "openlog";
            return "";
        }
    }
}
