using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LauncherHub
{
    sealed class HubForm : Form
    {
        Renderer _r;
        HubState _st = new HubState();
        HubController _ctl;
        Timer _timer;
        Bitmap _frame;
        Graphics _frameG;
        Bitmap _snap;
        IntPtr _snapDc = IntPtr.Zero;
        IntPtr _snapBmp = IntPtr.Zero;
        IntPtr _snapOld = IntPtr.Zero;
        float _fade;
        int _tick;
        const int SwitchMs = 220;
        const int FadeMs = 180;
        long _switchStart = -100000;
        long _fadeStart = -100000;
        System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
        long _lastTickMs;
        TextBox _edit;
        string _editKind = "";
        AppRuntime _editRt;
        bool _editCommitting;
        NotifyIcon _tray;
        string[] _lastStates = new string[0];
        bool _balloonShown;
        bool _exiting;
        bool _maximized;
        float _uiScale = 1f;
        Rectangle _normalBounds;
        bool _resizing;
        long _perfClickAt;
        bool _perfActive;
        long _perfSwitchAt;
        long _perfCaptureMs;
        long _perfFirstPaint = -1;
        long _perfLastPaint;
        long _perfLastTick;
        long _perfMaxFrame;
        long _perfMaxTick;
        long _perfMaxRender;
        int _perfJank;
        string _perfFrom = "";
        string _perfTo = "";
        Timer _auditTimer;
        int _auditStep;
        int _resizeEdge;
        Point _resizeStart;
        Rectangle _resizeBounds;
        int _frameW, _frameH;

        [DllImport("user32.dll")]
        static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")]
        static extern bool BringWindowToTop(IntPtr h);
        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll")]
        static extern bool AttachThreadInput(uint a, uint b, bool attach);
        [DllImport("kernel32.dll")]
        static extern uint GetCurrentThreadId();
        [DllImport("gdi32.dll")]
        static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")]
        static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
        [DllImport("gdi32.dll")]
        static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")]
        static extern bool DeleteObject(IntPtr obj);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        void ApplyDwmFrame()
        {
            try
            {
                int corner = _maximized ? 1 : 2; // DONOTROUND / ROUND
                DwmSetWindowAttribute(Handle, 33, ref corner, 4);
                // Let DWM use its native border for the rounded frame; a custom
                // DWMWA_BORDER_COLOR is still clipped at the rounded corner.
            }
            catch { }
        }
        [DllImport("msimg32.dll")]
        static extern bool AlphaBlend(IntPtr hdcDest, int x, int y, int w, int h, IntPtr hdcSrc, int sx, int sy, int sw, int sh, BLENDFUNCTION bf);
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct BLENDFUNCTION { public byte BlendOp; public byte BlendFlags; public byte SourceConstantAlpha; public byte AlphaFormat; }
        static readonly IntPtr HWND_TOPMOST = (IntPtr)(-1);
        static readonly IntPtr HWND_NOTOPMOST = (IntPtr)(-2);

        public HubForm(HubController ctl, int style, int page)
        {
            _ctl = ctl;
            Text = "应用工作台";
            // Hybrid Edge-style window: keep the native frame so DWM owns the
            // rounded outer shape, shadow and 1px border, and strip only the
            // non-client caption height in WM_NCCALCSIZE.
            FormBorderStyle = FormBorderStyle.Sizable;
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.Manual;
            Geo.Maximized = false;
            Geo.Resize(Geo.W, Geo.H);
            Size = new Size(Geo.W, Geo.H);
            DoubleBuffered = true;
            KeyPreview = true;
            MaximizeBox = true;
            MinimizeBox = true;
            BackColor = Color.FromArgb(243, 245, 250);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            _st.Page = page;
            _st.PrevPage = page;
            _st.SwitchT = 1f;
            _st.Dark = LoadTheme();
            _st.Maximized = false;
            _r = new Renderer(0);
            _r.SetTheme(_st.Dark, 1f);
            ApplyDwmFrame();
            EnsureFrame();
            ApplyRegion();
            float rs = DpiScale;
            Size = new Size((int)Math.Round(Geo.W * rs), (int)Math.Round(Geo.H * rs));
            MinimumSize = new Size((int)Math.Round(1000 * rs), (int)Math.Round(700 * rs));
            LoadWindowPos();
            _normalBounds = Bounds;
            CreateEditBox();
            CreateTray();

            _timer = new Timer();
            _timer.Interval = 15;
            _timer.Tick += delegate { Tick(); };
            _ctl.SyncAll();
            _lastStates = new string[_ctl.Entries.Count];
            for (int i = 0; i < _ctl.Entries.Count; i++) _lastStates[i] = _ctl.Entries[i].State;
            Warmup();
            try { GC.Collect(2, GCCollectionMode.Forced, true, true); } catch { }
            _timer.Start();
        }

        void Warmup()
        {
            try
            {
                using (Bitmap bmp = new Bitmap(Geo.W, Geo.H))
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    for (int page = -1; page < _ctl.Runtimes.Count; page++)
                    {
                        HubState s = new HubState();
                        s.Page = page; s.PrevPage = page; s.SwitchT = 1f; s.Now = Environment.TickCount;
                        _r.Render(g, s, new Size(Geo.W, Geo.H));
                    }
                    for (int page = 0; page < _ctl.Runtimes.Count; page++)
                    {
                        HubState s = new HubState();
                        s.Page = page; s.PrevPage = page; s.SwitchT = 1f; s.Now = Environment.TickCount; s.Popover = true;
                        _r.Render(g, s, new Size(Geo.W, Geo.H));
                        s.Popover = false; s.Expanded = true;
                        _r.Render(g, s, new Size(Geo.W, Geo.H));
                        s.Expanded = false;
                    }
                    for (int rail = 1; rail <= _ctl.Runtimes.Count; rail++)
                    {
                        HubState s = new HubState();
                        s.Page = 0; s.PrevPage = 0; s.SwitchT = 1f; s.Now = Environment.TickCount; s.RailHot = rail;
                        for (int i = 0; i < 4; i++) s.RailT[i] = i == rail ? 1f : 0f;
                        _r.Render(g, s, new Size(Geo.W, Geo.H));
                    }
                }
            }
            catch { }
        }

        AppRuntime Current { get { return _ctl.RuntimeByPage(_st.Page); } }

        float UiScale()
        {
            return 1f; // typographic scale is applied by the render transform
        }

        float DpiScale
        {
            get { try { return Math.Max(0.75f, DeviceDpi / 96f); } catch { return 1f; } }
        }

        float WindowScale
        {
            get
            {
                if (ClientSize.Width < 64 || ClientSize.Height < 64) return 1f;
                float dpi = DpiScale;
                float sx = (ClientSize.Width / dpi) / 1240f;
                float sy = (ClientSize.Height / dpi) / 800f;
                return Math.Max(0.78f, Math.Min(1.25f, Math.Min(sx, sy)));
            }
        }

        float RenderScale
        {
            get { return DpiScale * WindowScale; }
        }

        Point ToLogical(Point p)
        {
            float s = RenderScale;
            return new Point((int)Math.Round(p.X / s), (int)Math.Round(p.Y / s));
        }

        Rectangle ToPhysical(Rectangle r)
        {
            float s = RenderScale;
            return new Rectangle((int)Math.Round(r.X * s), (int)Math.Round(r.Y * s),
                Math.Max(1, (int)Math.Round(r.Width * s)), Math.Max(1, (int)Math.Round(r.Height * s)));
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_NCCALCSIZE = 0x0083;
            const int WM_NCHITTEST = 0x0084;
            if (m.Msg == WM_NCCALCSIZE && m.WParam != IntPtr.Zero)
            {
                // Client fills the whole native window: the top tab/banner now
                // lives where the caption used to be, while DWM still owns the
                // outer rounded clip, border and shadow.
                m.Result = IntPtr.Zero;
                return;
            }
            if (m.Msg == WM_NCHITTEST)
            {
                // Keep all input in the client area; resize/drag are handled by
                // the existing custom hit-testing (EdgeAt / WM_NCLBUTTONDOWN).
                m.Result = (IntPtr)1; // HTCLIENT
                return;
            }
            base.WndProc(ref m);
        }

        void EnsureFrame()
        {
            int w = Math.Max(1, ClientSize.Width);
            int h = Math.Max(1, ClientSize.Height);
            if (_frame != null && _frameW == w && _frameH == h) return;
            if (_frameG != null) _frameG.Dispose();
            if (_frame != null) _frame.Dispose();
            _frame = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            _frameG = Graphics.FromImage(_frame);
            _frameW = w; _frameH = h;
        }

        void ApplyLayout()
        {
            if (_r == null || ClientSize.Width < 64 || ClientSize.Height < 64) return;
            float rs = RenderScale;
            int lw = Math.Max(1, (int)Math.Round(ClientSize.Width / rs));
            int lh = Math.Max(1, (int)Math.Round(ClientSize.Height / rs));
            Geo.Maximized = _maximized;
            Geo.Resize(lw, lh);
            ApplyDwmFrame();
            float ui = UiScale();
            if (Math.Abs(_uiScale - ui) > 0.001f || _r.Dark != _st.Dark)
            {
                _uiScale = ui;
                _r.SetTheme(_st.Dark, ui);
            }
            EnsureFrame();
            if (_maximized) Region = null;
            else ApplyRegion();
        }

        bool LoadTheme()
        {
            try
            {
                string p = Path.Combine(Paths.LocalRoot, "theme.ini");
                return File.Exists(p) && File.ReadAllText(p).Trim().Equals("dark", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        void SaveTheme()
        {
            try
            {
                Directory.CreateDirectory(Paths.LocalRoot);
                File.WriteAllText(Path.Combine(Paths.LocalRoot, "theme.ini"), _st.Dark ? "dark" : "light");
            }
            catch { }
        }

        void ToggleTheme()
        {
            _st.Dark = !_st.Dark;
            _r.SetTheme(_st.Dark, _uiScale);
            ApplyDwmFrame();
            _st.Maximized = _maximized;
            SaveTheme();
            _fade = 0f;
            _fadeStart = -100000;
            if (_snap != null) { _snap.Dispose(); _snap = null; }
            _st.SwitchT = 1f;
            _switchStart = -100000;
            Toast(_st.Dark ? "已切换暗色模式" : "已切换亮色模式");
            Invalidate();
            Update();
        }

        void ToggleMaximize()
        {
            if (_maximized)
            {
                _maximized = false;
                Geo.Maximized = false;
                if (_normalBounds.Width > 0) Bounds = _normalBounds;
            }
            else
            {
                _normalBounds = Bounds;
                _maximized = true;
                Geo.Maximized = true;
                Rectangle wa = Screen.FromHandle(Handle).WorkingArea;
                Bounds = wa;
            }
            _st.Maximized = _maximized;
            _st.Hover = "";
            if (_maximized) _st.PlayAnim("restore", 1); else _st.PlayAnim("max", 1);
            ApplyLayout();
            Toast(_maximized ? "最大化" : "还原窗口");
            Invalidate();
            Update();
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            float rs = DpiScale;
            MinimumSize = new Size((int)Math.Round(1000 * rs), (int)Math.Round(700 * rs));
            ApplyLayout();
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (WindowState == FormWindowState.Minimized) return;
            if (_r == null) return;
            ApplyLayout();
            if (!_maximized && _normalBounds.Width > 0) _normalBounds = Bounds;
            Invalidate();
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button == MouseButtons.Left && _r.Hit(_st, ToLogical(e.Location)) == "drag")
            {
                ToggleMaximize();
                return;
            }
        }

        void ApplyRegion()
        {
            try
            {
                // No 1-bit Region: the native frame/DWM owns the outer shape.
                Region = null;
                ApplyDwmFrame();
            }
            catch { }
        }

        void LoadWindowPos()
        {
            try
            {
                string path = Path.Combine(Paths.LocalRoot, "window.ini");
                int ww = (int)Math.Round(Geo.W * DpiScale);
                int wh = (int)Math.Round(Geo.H * DpiScale);
                if (!File.Exists(path))
                {
                    Location = new Point((Screen.PrimaryScreen.WorkingArea.Width - ww) / 2, (Screen.PrimaryScreen.WorkingArea.Height - wh) / 2);
                    return;
                }
                string[] lines = File.ReadAllLines(path);
                int x = 0, y = 0;
                foreach (string l in lines)
                {
                    if (l.StartsWith("X=")) int.TryParse(l.Substring(2), out x);
                    else if (l.StartsWith("Y=")) int.TryParse(l.Substring(2), out y);
                }
                Rectangle r = new Rectangle(x, y, ww, wh);
                bool ok = false;
                foreach (Screen s in Screen.AllScreens) if (s.WorkingArea.IntersectsWith(r)) ok = true;
                Location = ok ? new Point(x, y) : new Point((Screen.PrimaryScreen.WorkingArea.Width - ww) / 2, (Screen.PrimaryScreen.WorkingArea.Height - wh) / 2);
            }
            catch { }
        }

        void SaveWindowPos()
        {
            try
            {
                Directory.CreateDirectory(Paths.LocalRoot);
                Point saveAt = _maximized && _normalBounds.Width > 0 ? _normalBounds.Location : Location;
                File.WriteAllText(Path.Combine(Paths.LocalRoot, "window.ini"), "X=" + saveAt.X + "\r\nY=" + saveAt.Y + "\r\n");
            }
            catch { }
        }

        void CreateEditBox()
        {
            _edit = new TextBox();
            _edit.BorderStyle = BorderStyle.None;
            _edit.Visible = false;
            _edit.KeyDown += EditKeyDown;
            _edit.LostFocus += delegate { if (_edit.Visible) CommitEdit(); };
            Controls.Add(_edit);
        }

        void CreateTray()
        {
            _tray = new NotifyIcon();
            try { _tray.Icon = Icon; } catch { }
            if (_tray.Icon == null) _tray.Icon = SystemIcons.Application;
            _tray.Text = "应用工作台";
            _tray.Visible = true;
            _tray.DoubleClick += delegate { ShowHub(); };
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Opening += delegate { BuildTrayMenu(menu); };
            _tray.ContextMenuStrip = menu;
        }

        void BuildTrayMenu(ContextMenuStrip menu)
        {
            menu.Items.Clear();
            menu.Items.Add("显示工作台", null, delegate { ShowHub(); });
            menu.Items.Add(new ToolStripSeparator());
            for (int i = 0; i < _ctl.Runtimes.Count; i++)
            {
                AppRuntime rt = _ctl.Runtimes[i];
                AppEntry a = rt.Entry;
                int idx = i;
                menu.Items.Add(new ToolStripMenuItem(a.Name + " · " + a.StateLabel, null, delegate { SwitchPage(idx); ShowHub(); }));
                if (a.Kind == "dsh")
                    menu.Items.Add(new ToolStripMenuItem(" 打开 Web UI", null, delegate { rt.OpenWeb(); }));
                else if (a.Running || a.Starting)
                    menu.Items.Add(new ToolStripMenuItem(" 停止", null, delegate { rt.StopAsync(); }));
                else
                    menu.Items.Add(new ToolStripMenuItem(" 启动", null, delegate { rt.StartAsync(); }));
            }
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("退出工作台（服务继续运行）", null, delegate { ExitApp(); }));
        }

        void ForceForeground()
        {
            try
            {
                IntPtr fg = GetForegroundWindow();
                uint pid;
                uint ft = GetWindowThreadProcessId(fg, out pid);
                uint mt = GetCurrentThreadId();
                bool attached = false;
                if (ft != 0 && ft != mt) attached = AttachThreadInput(mt, ft, true);
                SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, 0x0043);
                SetWindowPos(Handle, HWND_NOTOPMOST, 0, 0, 0, 0, 0x0043);
                SetForegroundWindow(Handle);
                BringWindowToTop(Handle);
                if (attached) AttachThreadInput(mt, ft, false);
            }
            catch { }
        }

        void ShowHub()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
            BringToFront();
            ForceForeground();
            if (!_timer.Enabled) _timer.Start();
        }

        void HideToTray()
        {
            SaveWindowPos();
            Hide();
            _timer.Stop();
            if (!_balloonShown)
            {
                _balloonShown = true;
                try { _tray.ShowBalloonTip(2500, "应用工作台", "已最小化到托盘；双击托盘图标恢复。", ToolTipIcon.Info); } catch { }
            }
        }

        void ExitApp()
        {
            _exiting = true;
            try { _ctl.Dispose(); } catch { }
            try { _tray.Visible = false; _tray.Dispose(); } catch { }
            Close();
            Application.Exit();
        }

        static float EaseOutCubic(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            float u = 1f - t;
            return 1f - u * u * u;
        }

        void Tick()
        {
            long tt = _clock.ElapsedMilliseconds;
            long dt = tt - _lastTickMs;
            if (dt < 1) dt = 1;
            _lastTickMs = tt;
            _st.Now = Environment.TickCount;
            bool need = false;

            float railStep = Math.Min(1f, dt / 90f);
            for (int i = 0; i < 4; i++)
            {
                float target = _st.RailHot == i ? 1f : 0f;
                _st.RailT[i] += (target - _st.RailT[i]) * railStep;
                if (Math.Abs(target - _st.RailT[i]) < 0.002f) _st.RailT[i] = target;
                if (_st.RailT[i] > 0.001f && _st.RailT[i] < 0.999f) need = true;
            }

            if (_st.Pulse > 0f)
            {
                _st.Pulse = Math.Max(0f, _st.Pulse - dt / 900f);
                need = true;
            }

            float sp = (tt - _switchStart) / (float)SwitchMs;
            if (sp < 1f)
            {
                _st.SwitchT = EaseOutCubic(sp);
                need = true;
            }
            else if (_st.SwitchT < 1f)
            {
                _st.SwitchT = 1f;
                need = true;
            }

            if (_fadeStart > 0)
            {
                float fp = 1f - (tt - _fadeStart) / (float)FadeMs;
                if (fp <= 0f)
                {
                    _fade = 0f;
                    _fadeStart = -100000;
                    if (_snap != null) { _snap.Dispose(); _snap = null; }
                    ReleaseSnapDc();
                    need = true;
                }
                else
                {
                    _fade = fp * fp;
                    need = true;
                }
            }

            _tick++;
            if ((_tick % 30) == 0)
            {
                for (int i = 0; i < _ctl.Entries.Count && i < _lastStates.Length; i++)
                {
                    string nowState = _ctl.Entries[i].State;
                    if (nowState != _lastStates[i])
                    {
                        if (nowState == "running" || _lastStates[i] == "running")
                            _st.PlayAnim("play:" + _ctl.Entries[i].Id, nowState == "running" ? -1 : 1);
                        _lastStates[i] = nowState;
                    }
                }
                need = true;
            }
            if (_perfActive)
            {
                if (_perfLastTick > 0)
                {
                    long tg = tt - _perfLastTick;
                    if (tg > _perfMaxTick) _perfMaxTick = tg;
                }
                _perfLastTick = tt;
                if (tt - _perfSwitchAt > 520) FinishPerf();
            }
            if (Anim.Active(_st)) need = true;
            if (!string.IsNullOrEmpty(_st.Toast) && Environment.TickCount > _st.ToastUntil)
            {
                _st.Toast = "";
                need = true;
            }
            if (need) Invalidate();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate();
            BringToFront();
            ForceForeground();
            if (Perf.Audit) StartPerfAudit();
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            long paintAt = _clock.ElapsedMilliseconds;
            e.Graphics.Clear(BackColor);
            long renderStart = paintAt;
            if (_frame != null && _frameG != null)
            {
                _frameG.Clear(BackColor);
                _frameG.ResetTransform();
                _frameG.ScaleTransform(RenderScale, RenderScale);
                _r.Render(_frameG, _st, new Size(Geo.W, Geo.H));
                e.Graphics.DrawImageUnscaled(_frame, 0, 0);
            }
            else
            {
                e.Graphics.ResetTransform();
                e.Graphics.ScaleTransform(RenderScale, RenderScale);
                _r.Render(e.Graphics, _st, new Size(Geo.W, Geo.H));
                e.Graphics.ResetTransform();
            }
            long renderMs = _clock.ElapsedMilliseconds - renderStart;
            if (_perfActive)
            {
                if (_perfFirstPaint < 0) _perfFirstPaint = paintAt - _perfSwitchAt;
                if (_perfLastPaint > 0)
                {
                    long gap = paintAt - _perfLastPaint;
                    if (gap > _perfMaxFrame) _perfMaxFrame = gap;
                    if (gap > 34) _perfJank++;
                }
                _perfLastPaint = paintAt;
                if (renderMs > _perfMaxRender) _perfMaxRender = renderMs;
            }
            if (_fade > 0.01f && _snap != null)
            {
                bool blended = false;
                if (_snapDc != IntPtr.Zero)
                {
                    try
                    {
                        IntPtr hdc = e.Graphics.GetHdc();
                        BLENDFUNCTION bf = new BLENDFUNCTION();
                        bf.BlendOp = 0;
                        bf.BlendFlags = 0;
                        bf.SourceConstantAlpha = (byte)Math.Max(0, Math.Min(255, (int)(_fade * 255f)));
                        bf.AlphaFormat = 0;
                        blended = AlphaBlend(hdc, Geo.RailW, 0, Geo.W - Geo.RailW, Geo.H, _snapDc,
                            Geo.RailW, 0, Geo.W - Geo.RailW, Geo.H, bf);
                        e.Graphics.ReleaseHdc(hdc);
                    }
                    catch { blended = false; }
                }
                if (!blended)
                {
                    Rectangle content = new Rectangle(Geo.RailW, 0, Geo.W - Geo.RailW, Geo.H);
                    GraphicsState s = e.Graphics.Save();
                    e.Graphics.SetClip(content);
                    ColorMatrix cm = new ColorMatrix(new float[][] {
                        new float[] {1,0,0,0,0},
                        new float[] {0,1,0,0,0},
                        new float[] {0,0,1,0,0},
                        new float[] {0,0,0,_fade,0},
                        new float[] {0,0,0,0,1}
                    });
                    using (ImageAttributes ia = new ImageAttributes())
                    {
                        ia.SetColorMatrix(cm);
                        e.Graphics.DrawImage(_snap, new Rectangle(0, 0, Geo.W, Geo.H),
                            0, 0, Geo.W, Geo.H, GraphicsUnit.Pixel, ia);
                    }
                    e.Graphics.Restore(s);
                }
            }
        }

        string AnimKeyForHit(string hit)
        {
            if (hit == "rail0") return "home";
            if (hit == "refresh") return "refresh-v2";
            if (hit == "log") return "file-text";
            if (hit == "gear") return "settings";
            if (hit == "expand" || hit == "expclose") return "maximize-2";
            if (hit == "min") return "minus";
            if (hit == "max") return _st.Maximized ? "restore" : "max";
            if (hit == "close") return "close";
            if (hit == "style") return "moon";
            return null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_resizing)
            {
                ResizeDrag(e.Location);
                return;
            }
            int edgeNow = EdgeAt(e.Location);
            if (edgeNow != 0)
            {
                Cursor = (edgeNow == 1 || edgeNow == 4) ? Cursors.SizeNWSE : (edgeNow == 2 || edgeNow == 3) ? Cursors.SizeNESW : (edgeNow == 5 || edgeNow == 6) ? Cursors.SizeWE : Cursors.SizeNS;
                _st.Hover = "";
                Invalidate();
                return;
            }
            string hit = _r.Hit(_st, ToLogical(e.Location));
            if (hit != _st.Hover)
            {
                string ak = AnimKeyForHit(hit);
                if (ak != null) _st.PlayAnim(ak, 1);
            }
            _st.Hover = hit;
            int rail = -1;
            if (hit.StartsWith("rail"))
            {
                int idx;
                if (int.TryParse(hit.Substring(4), out idx)) rail = idx;
            }
            _st.RailHot = rail;
            Cursor = IsClickable(hit) ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _st.RailHot = -1;
            _st.Hover = "";
            Invalidate();
        }

        static bool IsClickable(string hit)
        {
            if (string.IsNullOrEmpty(hit)) return false;
            if (hit == "drag" || hit == "pop" || hit == "modal" || hit == "drop" || hit == "dropclose") return false;
            return true;
        }

        int EdgeAt(Point p)
        {
            if (_maximized) return 0;
            int e = Math.Max(6, (int)Math.Round(6 * RenderScale));
            bool l = p.X <= e, r = p.X >= ClientSize.Width - e;
            bool t = p.Y <= e, b = p.Y >= ClientSize.Height - e;
            if (t && l) return 1;
            if (t && r) return 2;
            if (b && l) return 3;
            if (b && r) return 4;
            if (l) return 5;
            if (r) return 6;
            if (t) return 7;
            if (b) return 8;
            return 0;
        }

        void ResizeDrag(Point p)
        {
            int dx = p.X - _resizeStart.X;
            int dy = p.Y - _resizeStart.Y;
            Rectangle b = _resizeBounds;
            int minW = 1000, minH = 700;
            if (_resizeEdge == 1 || _resizeEdge == 3 || _resizeEdge == 5)
            {
                int nx = b.X + dx;
                int nw = b.Width - dx;
                if (nw >= minW) { b.X = nx; b.Width = nw; }
            }
            if (_resizeEdge == 2 || _resizeEdge == 4 || _resizeEdge == 6)
                b.Width = Math.Max(minW, b.Width + dx);
            if (_resizeEdge == 1 || _resizeEdge == 2 || _resizeEdge == 7)
            {
                int ny = b.Y + dy;
                int nh = b.Height - dy;
                if (nh >= minH) { b.Y = ny; b.Height = nh; }
            }
            if (_resizeEdge == 3 || _resizeEdge == 4 || _resizeEdge == 8)
                b.Height = Math.Max(minH, b.Height + dy);
            Bounds = b;
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            if (_edit.Visible && !_edit.Bounds.Contains(e.Location)) CommitEdit();
            int edge = EdgeAt(e.Location);
            if (edge != 0)
            {
                _resizing = true;
                _resizeEdge = edge;
                _resizeStart = e.Location;
                _resizeBounds = Bounds;
                Capture = true;
                return;
            }
            string hit = _r.Hit(_st, ToLogical(e.Location));
            _perfClickAt = _clock.ElapsedMilliseconds;
            if (hit == "drag")
            {
                if (_maximized) return;
                ReleaseCapture();
                SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
                return;
            }
            Do(hit);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_resizing)
            {
                _resizing = false;
                Capture = false;
                _normalBounds = Bounds;
                SaveWindowPos();
                Invalidate();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape)
            {
                if (_edit.Visible) { CancelEdit(); return; }
                if (_st.Dropdown) { CloseDropdown(); return; }
                if (_st.Expanded) { _st.Expanded = false; Invalidate(); return; }
                if (_st.Popover) { _st.Popover = false; Invalidate(); return; }
                if (_st.Page >= 0) { SwitchPage(-1); return; }
            }
            if (_edit.Visible) return;
            if (e.KeyCode == Keys.D1) { SwitchPage(0); return; }
            if (e.KeyCode == Keys.D2) { SwitchPage(1); return; }
            if (e.KeyCode == Keys.D3) { SwitchPage(2); return; }
            if (e.KeyCode == Keys.H) { SwitchPage(-1); return; }
        }

        void Do(string hit)
        {
            if (string.IsNullOrEmpty(hit)) return;
            if (hit == "pop" || hit == "modal" || hit == "drop") return;
            if (hit == "popclose" || hit == "dropclose") { _st.Popover = false; CloseDropdown(); Invalidate(); return; }

            if (hit == "close") { HideToTray(); return; }
            if (hit == "min") { WindowState = FormWindowState.Minimized; return; }
            if (hit == "max") { ToggleMaximize(); return; }
            if (hit == "style") { ToggleTheme(); return; }
            if (hit == "rail0") { SwitchPage(-1); return; }
            if (hit.StartsWith("rail"))
            {
                int idx;
                if (int.TryParse(hit.Substring(4), out idx) && idx >= 1 && idx <= _ctl.Runtimes.Count) SwitchPage(idx - 1);
                return;
            }
            if (hit.StartsWith("cardstart"))
            {
                int idx;
                if (int.TryParse(hit.Substring(9), out idx)) HomeStart(idx);
                return;
            }
            if (hit.StartsWith("cardopen"))
            {
                int idx;
                if (int.TryParse(hit.Substring(8), out idx)) HomeOpen(idx);
                return;
            }
            if (hit.StartsWith("card"))
            {
                int idx;
                if (int.TryParse(hit.Substring(4), out idx)) SwitchPage(idx);
                return;
            }
            if (hit == "refresh") { _ctl.SyncAll(); Toast("状态已刷新"); Invalidate(); return; }

            if (_st.Page < 0) return;
            AppRuntime rt = Current;
            if (rt == null) return;
            AppEntry a = rt.Entry;

            if (hit == "play") { ToggleApp(rt); return; }
            if (hit == "restart")
            {
                if (a.Kind == "dsh") { rt.OpenLog(); return; }
                if (a.Running || a.Starting) { rt.RestartAsync(); Toast("正在重启 " + a.Name + "…"); }
                else { rt.StartAsync(); Toast("正在启动 " + a.Name + "…"); }
                _ctl.SyncAll(); return;
            }
            if (hit == "log") { _st.Expanded = true; Invalidate(); return; }
            if (hit == "expand") { _st.Expanded = true; Invalidate(); return; }
            if (hit == "expclose") { _st.Expanded = false; Invalidate(); return; }
            if (hit == "openlog" || hit == "openlog2") { rt.OpenLog(); return; }
            if (hit == "web")
            {
                if (a.Running) { if (!rt.OpenWeb()) Toast("打开浏览器失败"); }
                else if (a.Starting) Toast("正在启动，稍等…");
                else { rt.StartAsync(); Toast("正在启动 " + a.Name + "…"); }
                _ctl.SyncAll(); return;
            }
            if (hit == "restart2")
            {
                if (a.Kind == "dsh") { rt.OpenLog(); return; }
                if (a.Running || a.Starting) { rt.RestartAsync(); Toast("正在重启 " + a.Name + "…"); }
                return;
            }
            if (hit == "gear") { CloseDropdown(); _st.Popover = !_st.Popover; if (_st.Popover) _st.Expanded = false; Invalidate(); return; }
            if (hit == "popdone") { _st.Popover = false; Invalidate(); return; }
            if (hit == "poptoggle") { rt.SetAutoOpen(!a.AutoOpenBrowser); _ctl.SyncAll(); Toast(a.AutoOpenBrowser ? "启动后不自动打开浏览器" : "启动后自动打开浏览器"); Invalidate(); return; }
            if (hit == "openconfig") { _st.Popover = false; rt.OpenConfig(); Invalidate(); return; }
            if (hit == "model") { OpenModelDropdown(); return; }
            if (hit.StartsWith("drop:"))
            {
                int idx;
                if (int.TryParse(hit.Substring(5), out idx) && idx >= 0 && idx < _st.DropItems.Count)
                {
                    AppRuntime owner = _ctl.RuntimeByPage(_st.DropOwner);
                    if (owner != null)
                    {
                        bool wasRunning = owner.IsRunning;
                        owner.SetModel(_st.DropItems[idx].Path);
                        Toast(wasRunning ? "模型已切换，正在用新模型重启…" : "模型已保存");
                        _ctl.SyncAll();
                    }
                }
                CloseDropdown();
                Invalidate();
                return;
            }
            if (hit == "ctx")
            {
                BeginEdit("ctx", new Rectangle(Geo.Right.X + 72, 272, 100, 36), a.Ctx.ToString(), true);
                return;
            }
            if (hit == "port")
            {
                BeginEdit("port", new Rectangle(Geo.Right.X + 292, 140, 96, 38), a.Port.ToString(), true);
                return;
            }
            if (hit == "copyremote")
            {
                try { AppRuntime remoteRt = Current; string remote = remoteRt != null && remoteRt.Entry != null && remoteRt.Entry.RemoteHost != null ? remoteRt.Entry.RemoteHost : ""; if (remote.Length == 0) { Toast("未配置远程入口"); return; } Clipboard.SetText(remote); Toast("已复制远程入口"); }
                catch { Toast("复制失败"); }
                return;
            }
            if (hit == "auto")
            {
                rt.SetAutoOpen(!a.AutoOpenBrowser);
                _ctl.SyncAll();
                Toast(a.AutoOpenBrowser ? "启动后不自动打开浏览器" : "启动后自动打开浏览器");
                Invalidate();
                return;
            }
            if (hit.StartsWith("chip:"))
            {
                int idx;
                if (int.TryParse(hit.Substring(5), out idx) && idx >= 0 && idx < _st.ChipLabels.Count)
                    rt.OpenFolder(_st.ChipLabels[idx]);
                return;
            }
        }

        void HomeStart(int idx)
        {
            AppRuntime rt = _ctl.RuntimeByPage(idx);
            if (rt == null) return;
            if (rt.Entry.Kind == "dsh") { if (rt.IsRunning) rt.OpenWeb(); else rt.StartAsync(); }
            else if (rt.IsRunning || rt.Phase == AppPhase.Starting) rt.StopAsync();
            else rt.StartAsync();
            _ctl.SyncAll();
        }

        void HomeOpen(int idx)
        {
            AppRuntime rt = _ctl.RuntimeByPage(idx);
            if (rt == null) return;
            if (rt.Entry.Kind == "dsh" && rt.IsRunning) rt.OpenLog();
            else if (rt.Entry.Kind == "dsh" || rt.IsRunning) rt.OpenWeb();
            else SwitchPage(idx);
        }

        void ToggleApp(AppRuntime rt)
        {
            AppEntry a = rt.Entry;
            if (a.Kind == "dsh")
            {
                if (rt.IsRunning) { if (!rt.OpenWeb()) Toast("打开浏览器失败"); }
                else { rt.StartAsync(); Toast("正在启动 DSH…"); }
            }
            else if (rt.Phase == AppPhase.Starting || rt.Phase == AppPhase.Stopping) return;
            else if (rt.IsRunning)
            {
                rt.StopAsync();
                Toast("正在停止 " + a.Name + "…");
            }
            else
            {
                rt.StartAsync();
                Toast("正在启动 " + a.Name + "…");
            }
            _ctl.SyncAll();
        }

        string PageName(int page)
        {
            return page < 0 ? "home" : (page < Data.Apps.Count ? Data.Apps[page].Id : "?");
        }

        void BeginPerf(int page)
        {
            if (_perfActive) FinishPerf();
            _perfActive = true;
            _perfFrom = PageName(_st.Page);
            _perfTo = PageName(page);
            _perfSwitchAt = _clock.ElapsedMilliseconds;
            if (_perfClickAt <= 0) _perfClickAt = _perfSwitchAt;
            _perfCaptureMs = 0;
            _perfFirstPaint = -1;
            _perfLastPaint = 0;
            _perfLastTick = _perfSwitchAt;
            _perfMaxFrame = 0;
            _perfMaxTick = 0;
            _perfMaxRender = 0;
            _perfJank = 0;
        }

        void FinishPerf()
        {
            if (!_perfActive) return;
            _perfActive = false;
            PerfSample s = new PerfSample();
            s.From = _perfFrom; s.To = _perfTo;
            s.InputMs = Math.Max(0, _perfSwitchAt - _perfClickAt);
            s.SwitchMs = _perfSwitchAt;
            s.CaptureMs = _perfCaptureMs;
            s.FirstPaintMs = _perfFirstPaint < 0 ? 999 : _perfFirstPaint;
            s.MaxFrameGapMs = _perfMaxFrame;
            s.MaxTickGapMs = _perfMaxTick;
            s.MaxRenderMs = _perfMaxRender;
            s.JankFrames = _perfJank;
            s.Size = ClientSize.Width + "x" + ClientSize.Height + (_st.Dark ? "/dark" : "/light");
            s.Stages = Perf.StageInfo;
            s.Fail = s.FirstPaintMs > 34 || s.MaxFrameGapMs > 34 || s.CaptureMs > 22 || s.MaxRenderMs > 22;
            if (Perf.Audit || s.Fail) Perf.Add(s);
            _perfClickAt = 0;
        }

        void StartPerfAudit()
        {
            _auditStep = 0;
            _auditTimer = new Timer();
            _auditTimer.Interval = 650;
            _auditTimer.Tick += delegate
            {
                int[] pages = new int[] { 2, 0, 1, 2, 0, 1 };
                if (_auditStep < pages.Length)
                {
                    SwitchPage(pages[_auditStep]);
                    _auditStep++;
                    return;
                }
                _auditTimer.Stop();
                FinishPerf();
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(Perf.LogPath));
                    File.WriteAllText(Perf.LogPath, Perf.Report(), new System.Text.UTF8Encoding(false));
                }
                catch { }
                Environment.ExitCode = Perf.AnyFail() ? 1 : 0;
                _exiting = true;
                Application.Exit();
            };
            _auditTimer.Start();
        }
        void SwitchPage(int page)
        {
            if (page == _st.Page) return;
            if (_edit.Visible) CancelEdit();
            CloseDropdown();
            BeginPerf(page);
            long cap0 = _clock.ElapsedMilliseconds;
            CaptureSnapshot();
            _perfCaptureMs = _clock.ElapsedMilliseconds - cap0;
            _perfLastTick = _clock.ElapsedMilliseconds;
            _st.PrevPage = _st.Page;
            _st.Page = page;
            _st.SwitchT = 0f;
            _switchStart = _clock.ElapsedMilliseconds;
            _st.Pulse = 1f;
            _st.Popover = false;
            _st.Expanded = false;
            Invalidate();
            Update();
        }

        void ReleaseSnapDc()
        {
            try
            {
                if (_snapDc != IntPtr.Zero)
                {
                    if (_snapOld != IntPtr.Zero) SelectObject(_snapDc, _snapOld);
                    DeleteDC(_snapDc);
                }
                if (_snapBmp != IntPtr.Zero) DeleteObject(_snapBmp);
            }
            catch { }
            _snapDc = IntPtr.Zero;
            _snapBmp = IntPtr.Zero;
            _snapOld = IntPtr.Zero;
        }

        void CaptureSnapshot()
        {
            ReleaseSnapDc();
            if (_snap != null) { _snap.Dispose(); _snap = null; }
            if (_frame != null)
            {
                _snap = (Bitmap)_frame.Clone();
            }
            else
            {
                _snap = new Bitmap(Geo.W, Geo.H);
                using (Graphics g = Graphics.FromImage(_snap))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    _r.Render(g, _st, new Size(Geo.W, Geo.H));
                }
            }
            _fade = 1f;
            _fadeStart = _clock.ElapsedMilliseconds;
            try
            {
                _snapBmp = _snap.GetHbitmap(Color.FromArgb(0));
                _snapDc = CreateCompatibleDC(IntPtr.Zero);
                if (_snapDc != IntPtr.Zero) _snapOld = SelectObject(_snapDc, _snapBmp);
            }
            catch { ReleaseSnapDc(); }
        }

        void OpenModelDropdown()
        {
            AppRuntime rt = Current;
            if (rt == null || rt.Kind != "llama") return;
            _st.DropOwner = _st.Page;
            _st.DropItems = rt.Models;
            int n = Math.Max(1, rt.Models.Count);
            _st.DropRect = new Rectangle(Geo.Right.X + 62, 266, 620, 8 + n * 36);
            _st.Dropdown = true;
            Invalidate();
        }

        void CloseDropdown()
        {
            _st.Dropdown = false;
            _st.DropItems = new System.Collections.Generic.List<ModelItem>();
            _st.DropOwner = -1;
        }

        void BeginEdit(string kind, Rectangle rect, string value, bool numeric)
        {
            _editKind = kind;
            _editRt = Current;
            _edit.Text = value;
            _edit.Bounds = ToPhysical(new Rectangle(rect.X + 11, rect.Y + 6, rect.Width - 22, rect.Height - 12));
            _edit.BackColor = _r.Style.FieldBg;
            _edit.ForeColor = _r.Style.Text;
            float fs = _r.Style.FBody.SizeInPoints * RenderScale;
            _edit.Font = new Font(_r.Style.FBody.FontFamily, fs, _r.Style.FBody.Style);
            _edit.Visible = true;
            _edit.BringToFront();
            _edit.Focus();
            _edit.SelectAll();
        }

        void CommitEdit()
        {
            if (!_edit.Visible || _editCommitting) return;
            _editCommitting = true;
            try
            {
                string text = _edit.Text.Trim();
                int n;
                if (!int.TryParse(text, out n)) { Toast("请输入数字"); return; }
                AppRuntime rt = _editRt;
                if (rt == null) return;
                if (_editKind == "port")
                {
                    if (n < 1 || n > 65535) { Toast("端口必须在 1–65535"); return; }
                    if (rt.Phase == AppPhase.Running || rt.Phase == AppPhase.Starting || rt.Phase == AppPhase.Stopping)
                    {
                        Toast("运行中不能改端口，请先停止");
                        return;
                    }
                    rt.SetPort(n);
                    Toast("端口已保存");
                }
                else if (_editKind == "ctx")
                {
                    if (n < 256 || n > 262144) { Toast("上下文必须在 256–262144"); return; }
                    rt.SetCtx(n);
                    Toast(rt.IsRunning ? "上下文已保存；重启后生效" : "上下文已保存");
                }
                _ctl.SyncAll();
            }
            finally
            {
                _edit.Visible = false;
                _editKind = "";
                _editRt = null;
                _editCommitting = false;
                Invalidate();
            }
        }

        void CancelEdit()
        {
            _edit.Visible = false;
            _editKind = "";
            _editRt = null;
            Invalidate();
        }

        void EditKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) { CommitEdit(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Escape) { CancelEdit(); e.SuppressKeyPress = true; }
        }

        void Toast(string text)
        {
            _st.Toast = text;
            _st.ToastUntil = Environment.TickCount + 2400;
            _st.Now = Environment.TickCount;
            Invalidate();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_exiting)
            {
                e.Cancel = true;
                HideToTray();
                return;
            }
            SaveWindowPos();
            try { _ctl.Dispose(); } catch { }
            try { if (_tray != null) { _tray.Visible = false; _tray.Dispose(); } } catch { }
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_timer != null) { _timer.Stop(); _timer.Dispose(); }
                if (_snap != null) { _snap.Dispose(); _snap = null; }
                if (_r != null) _r.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
