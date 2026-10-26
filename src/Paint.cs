using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace LauncherHub
{
    enum BtnKind { Primary, Secondary, Ghost }

    static class P
    {
        // ---------- shapes ----------
        public static GraphicsPath Round(Rectangle r, int rad)
        {
            GraphicsPath p = new GraphicsPath();
            if (r.Width <= 0 || r.Height <= 0) { p.AddRectangle(new Rectangle(r.X, r.Y, 1, 1)); return p; }
            int d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
            if (d <= 1) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillRound(Graphics g, Rectangle r, int rad, Color c)
        {
            using (GraphicsPath p = Round(r, rad))
            using (SolidBrush b = new SolidBrush(c))
                g.FillPath(b, p);
        }

        public static void FillRound(Graphics g, Rectangle r, int rad, Brush b)
        {
            using (GraphicsPath p = Round(r, rad))
                g.FillPath(b, p);
        }

        public static void DrawRound(Graphics g, Rectangle r, int rad, Color c, float w)
        {
            using (GraphicsPath p = Round(r, rad))
            using (Pen pen = new Pen(c, w))
                g.DrawPath(pen, p);
        }

        static int Seed(Rectangle r)
        {
            return (r.X * 397) ^ (r.Y * 31) ^ (r.Width * 17) ^ (r.Height * 13) ^ 0x5A5A;
        }

        public static void Shadow(Graphics g, Rectangle r, int rad)
        {
            for (int i = 1; i <= 6; i++)
            {
                Rectangle rr = Rectangle.Inflate(r, i, i);
                using (GraphicsPath p = Round(rr, rad + i))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(6, 15, 23, 42)))
                    g.FillPath(b, p);
            }
        }

        public static void RoughBorder(Graphics g, Rectangle r, int rad, Color c, float w, int seed)
        {
            Random rnd = new Random(seed);
            for (int pass = 0; pass < 2; pass++)
            {
                int dx = pass == 0 ? rnd.Next(-1, 2) : rnd.Next(-2, 3);
                int dy = pass == 0 ? rnd.Next(-1, 2) : rnd.Next(-2, 3);
                Rectangle rr = new Rectangle(r.X + dx, r.Y + dy, r.Width, r.Height);
                using (GraphicsPath p = Round(rr, rad))
                using (Pen pen = new Pen(pass == 0 ? c : Color.FromArgb(85, c), pass == 0 ? w : w * 0.8f))
                {
                    pen.LineJoin = LineJoin.Round;
                    g.DrawPath(pen, p);
                }
            }
        }

        static readonly Dictionary<string, Bitmap> _cardCache = new Dictionary<string, Bitmap>();
        const int CardPad = 8;
        static Bitmap CachedCard(Look L, Rectangle r, int rad)
        {
            string key = r.Width + "x" + r.Height + "_" + rad + "_" + L.CardBg.ToArgb() + "_" + L.CardBorder.ToArgb();
            Bitmap cached;
            if (_cardCache.TryGetValue(key, out cached)) return cached;
            int w = r.Width + CardPad * 2;
            int h = r.Height + CardPad * 2;
            Bitmap bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (Graphics bg = Graphics.FromImage(bmp))
            {
                bg.SmoothingMode = SmoothingMode.AntiAlias;
                bg.PixelOffsetMode = PixelOffsetMode.HighQuality;
                bg.CompositingQuality = CompositingQuality.HighQuality;
                bg.Clear(Color.Transparent);
                Rectangle rr = new Rectangle(CardPad, CardPad, r.Width, r.Height);
                Shadow(bg, rr, rad);
                FillRound(bg, rr, rad, L.CardBg);
                DrawRound(bg, rr, rad, L.CardBorder, 1f);
            }
            _cardCache[key] = bmp;
            return bmp;
        }

        public static void Card(Graphics g, Look L, Rectangle r, int rad)
        {
            if (!L.Sketch)
            {
                Bitmap bmp = CachedCard(L, r, rad);
                g.DrawImageUnscaled(bmp, r.X - CardPad, r.Y - CardPad);
                return;
            }
            CardFlat(g, L, r, rad);
        }

        static void CardFlat(Graphics g, Look L, Rectangle r, int rad)
        {
            if (L.Sketch)
            {
                using (GraphicsPath sp = Round(new Rectangle(r.X + 4, r.Y + 5, r.Width, r.Height), rad))
                using (SolidBrush sb = new SolidBrush(Color.FromArgb(38, L.Ink)))
                    g.FillPath(sb, sp);
                using (GraphicsPath p = Round(r, rad))
                using (SolidBrush b = new SolidBrush(L.CardBg))
                    g.FillPath(b, p);
                if (L.Noise != null)
                {
                    using (GraphicsPath p = Round(r, rad))
                        g.FillPath(L.Noise, p);
                }
                RoughBorder(g, r, rad, L.CardBorder, 1.7f, Seed(r));
            }
            else
            {
                Shadow(g, r, rad);
                FillRound(g, r, rad, L.CardBg);
                DrawRound(g, r, rad, L.CardBorder, 1f);
            }
        }

        public static void Divider(Graphics g, Look L, int x1, int x2, int y)
        {
            if (L.Sketch)
            {
                Random rnd = new Random(y * 131 + x1);
                Point a = new Point(x1, y + rnd.Next(-1, 2));
                Point b = new Point(x2, y + rnd.Next(-1, 2));
                using (Pen pen = new Pen(Color.FromArgb(120, L.Divider), 1.4f))
                    g.DrawLine(pen, a, b);
            }
            else
            {
                using (Pen pen = new Pen(L.Divider, 1f))
                    g.DrawLine(pen, x1, y, x2, y);
            }
        }

        // ---------- text ----------
        public static void Text(Graphics g, string s, Font f, Color c, float x, float y)
        {
            using (SolidBrush b = new SolidBrush(c))
                g.DrawString(s, f, b, x, y);
        }

        [ThreadStatic] static SolidBrush _textBrush;
        [ThreadStatic] static StringFormat _textFormat;

        public static void Text(Graphics g, string s, Font f, Color c, Rectangle r, StringAlignment hAlign, StringAlignment vAlign)
        {
            if (_textFormat == null)
            {
                _textFormat = new StringFormat();
                _textFormat.Trimming = StringTrimming.EllipsisCharacter;
                _textFormat.FormatFlags = StringFormatFlags.NoWrap;
            }
            if (_textBrush == null) _textBrush = new SolidBrush(c);
            else _textBrush.Color = c;
            _textFormat.Alignment = hAlign;
            _textFormat.LineAlignment = vAlign;
            long __t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            g.DrawString(s, f, _textBrush, r, _textFormat);
            Perf.TextTicks += System.Diagnostics.Stopwatch.GetTimestamp() - __t0;
            Perf.TextCount++;
        }
        public static void TextC(Graphics g, string s, Font f, Color c, Rectangle r)
        {
            Text(g, s, f, c, r, StringAlignment.Center, StringAlignment.Center);
        }

        public static string Trim(Graphics g, string s, Font f, int maxWidth)
        {
            if (s == null) return "";
            if (maxWidth <= 10) return "";
            if (g.MeasureString(s, f).Width <= maxWidth) return s;
            int lo = 1, hi = s.Length;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (g.MeasureString(s.Substring(0, mid) + "…", f).Width <= maxWidth) lo = mid;
                else hi = mid - 1;
            }
            return s.Substring(0, lo) + "…";
        }

        public static int TextW(Graphics g, string s, Font f)
        {
            return (int)Math.Ceiling(g.MeasureString(s, f).Width);
        }

        // ---------- widgets ----------
        public static void Chip(Graphics g, Look L, Rectangle r, string label, bool hover)
        {
            Color bg = hover ? Mix(L.ChipBg, L.Ink, 0.08f) : L.ChipBg;
            if (L.Sketch)
            {
                FillRound(g, r, 7, bg);
                RoughBorder(g, r, 7, L.ChipBorder, 1.4f, Seed(r));
            }
            else
            {
                FillRound(g, r, 9, bg);
                DrawRound(g, r, 9, L.ChipBorder, 1f);
            }
            TextC(g, label, L.FSmall, L.Text, new Rectangle(r.X + 8, r.Y, r.Width - 16, r.Height));
        }

        public static void Field(Graphics g, Look L, Rectangle r, string value, Color? textColor, bool chevron)
        {
            if (L.Sketch)
            {
                FillRound(g, r, 7, L.FieldBg);
                RoughBorder(g, r, 7, L.FieldBorder, 1.5f, Seed(r));
            }
            else
            {
                FillRound(g, r, 9, L.FieldBg);
                DrawRound(g, r, 9, L.FieldBorder, 1f);
            }
            Color c = textColor.HasValue ? textColor.Value : L.Text;
            Rectangle tr = new Rectangle(r.X + 12, r.Y, r.Width - (chevron ? 34 : 22), r.Height);
            Text(g, Trim(g, value, L.FBody, tr.Width), L.FBody, c, tr, StringAlignment.Near, StringAlignment.Center);
            if (chevron)
            {
                int cx = r.Right - 17; int cy = r.Y + r.Height / 2;
                DrawChevron(g, cx, cy, L.Sub, 4);
            }
        }

        public static void Toggle(Graphics g, Look L, Rectangle r, bool on, Color accent)
        {
            int h = r.Height;
            if (L.Sketch)
            {
                FillRound(g, r, h / 2, on ? accent : L.ChipBg);
                RoughBorder(g, r, h / 2, L.Ink, 1.4f, Seed(r));
            }
            else
            {
                FillRound(g, r, h / 2, on ? accent : Color.FromArgb(216, 222, 233));
            }
            int knob = h - 6;
            int kx = on ? r.Right - knob - 3 : r.X + 3;
            FillRound(g, new Rectangle(kx, r.Y + 3, knob, knob), knob / 2, Color.White);
        }

        public static void Button(Graphics g, Look L, Rectangle r, string label, BtnKind kind, Color accent, Color accentText, bool hover, bool disabled)
        {
            if (kind == BtnKind.Ghost)
            {
                if (hover) FillRound(g, r, 9, Color.FromArgb(16, L.Ink));
                TextC(g, label, L.FSmall, disabled ? L.Faint : (hover ? L.Text : L.Sub), r);
                return;
            }
            if (kind == BtnKind.Primary)
            {
                Color fill = disabled ? Color.FromArgb(120, accent) : (hover ? Mix(accent, Color.White, 0.10f) : accent);
                if (L.Sketch)
                {
                    using (GraphicsPath sp = Round(new Rectangle(r.X + 3, r.Y + 4, r.Width, r.Height), 9))
                    using (SolidBrush sb = new SolidBrush(Color.FromArgb(58, L.Ink)))
                        g.FillPath(sb, sp);
                    FillRound(g, r, 9, fill);
                    RoughBorder(g, r, 9, L.Ink, 2f, Seed(r));
                }
                else
                {
                    FillRound(g, r, 10, fill);
                }
                TextC(g, label, L.FBody, disabled ? Color.FromArgb(230, accentText) : accentText, r);
                return;
            }
            // secondary
            if (L.Sketch)
            {
                FillRound(g, r, 8, hover ? Mix(L.CardBg, L.Ink, 0.06f) : L.CardBg);
                RoughBorder(g, r, 8, L.CardBorder, 1.5f, Seed(r));
            }
            else
            {
                FillRound(g, r, 10, hover ? Color.FromArgb(247, 249, 253) : L.CardBg);
                DrawRound(g, r, 10, L.CardBorder, 1f);
            }
            TextC(g, label, L.FSmall, disabled ? L.Faint : L.Text, r);
        }

        public static void CircleButton(Graphics g, Look L, Rectangle r, Color fill, Color fg, string icon, bool hover, bool sketch)
        {
            Color c = hover ? Mix(fill, Color.White, 0.12f) : fill;
            if (sketch)
            {
                using (GraphicsPath sp = Round(new Rectangle(r.X + 2, r.Y + 3, r.Width, r.Height), r.Width / 2))
                using (SolidBrush sb = new SolidBrush(Color.FromArgb(45, L.Ink)))
                    g.FillPath(sb, sp);
                FillRound(g, r, r.Width / 2, c);
                DrawRound(g, r, r.Width / 2, L.Ink, 1.6f);
            }
            else
            {
                FillRound(g, r, r.Width / 2, c);
            }
            Icon(g, icon, r, fg, 1f);
        }

        public static void IconCircle(Graphics g, Look L, Rectangle r, string icon, Color color, bool hover)
        {
            if (hover)
            {
                if (L.Sketch) FillRound(g, r, r.Width / 2, Color.FromArgb(18, L.Ink));
                else FillRound(g, r, r.Width / 2, Color.FromArgb(18, L.Ink));
            }
            Icon(g, icon, r, color, 1f);
        }

        public static void Dot(Graphics g, Look L, int cx, int cy, int rad, Color c)
        {
            using (SolidBrush halo = new SolidBrush(Color.FromArgb(46, c)))
                g.FillEllipse(halo, cx - rad - 3, cy - rad - 3, (rad + 3) * 2, (rad + 3) * 2);
            using (SolidBrush b = new SolidBrush(c))
                g.FillEllipse(b, cx - rad, cy - rad, rad * 2, rad * 2);
            if (L.Sketch)
            {
                using (Pen pen = new Pen(Color.FromArgb(110, L.Ink), 1.2f))
                    g.DrawEllipse(pen, cx - rad - 1, cy - rad - 1, rad * 2 + 2, rad * 2 + 2);
            }
        }

        public static void Tag(Graphics g, Look L, Rectangle r, string text, Color dotColor)
        {
            FillRound(g, r, r.Height / 2, Color.FromArgb(28, 255, 255, 255));
            Dot(g, L, r.X + 12, r.Y + r.Height / 2, 3, dotColor);
            Text(g, text, L.FTiny, Color.FromArgb(235, 255, 255, 255), new Rectangle(r.X + 21, r.Y, r.Width - 27, r.Height), StringAlignment.Near, StringAlignment.Center);
        }

        public static Color Mix(Color a, Color b, float t)
        {
            if (t < 0) t = 0; if (t > 1) t = 1;
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t),
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        // ---------- icons ----------
        public static void Icon(Graphics g, string name, Rectangle r, Color c, float scale)
        {
            int s = Math.Min(r.Width, r.Height);
            float cx = r.X + r.Width / 2f;
            float cy = r.Y + r.Height / 2f;
            float k = (s / 20f) * scale;
            using (Pen pen = new Pen(c, Math.Max(1.4f, 1.7f * k)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                switch (name)
                {
                    case "play":
                        {
                            using (GraphicsPath p = new GraphicsPath())
                            {
                                p.AddPolygon(new PointF[] {
                                    new PointF(cx - 4.5f * k, cy - 7f * k),
                                    new PointF(cx - 4.5f * k, cy + 7f * k),
                                    new PointF(cx + 7.5f * k, cy) });
                                using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, p);
                            }
                            break;
                        }
                    case "pause":
                        {
                            using (SolidBrush b = new SolidBrush(c))
                            {
                                g.FillRectangle(b, cx - 6f * k, cy - 7f * k, 4f * k, 14f * k);
                                g.FillRectangle(b, cx + 2f * k, cy - 7f * k, 4f * k, 14f * k);
                            }
                            break;
                        }
                    case "stop":
                        {
                            using (SolidBrush b = new SolidBrush(c))
                                g.FillRectangle(b, cx - 5f * k, cy - 5f * k, 10f * k, 10f * k);
                            break;
                        }
                    case "restart":
                        {
                            using (GraphicsPath p = new GraphicsPath())
                            {
                                p.AddArc(cx - 8f * k, cy - 8f * k, 16f * k, 16f * k, 40, 280);
                                g.DrawPath(pen, p);
                            }
                            PointF tip = new PointF(cx + 5.5f * k, cy - 8f * k);
                            using (SolidBrush b = new SolidBrush(c))
                            {
                                g.FillPolygon(b, new PointF[] {
                                    tip,
                                    new PointF(tip.X - 5f * k, tip.Y - 1f * k),
                                    new PointF(tip.X - 1f * k, tip.Y + 4.5f * k) });
                            }
                            break;
                        }
                    case "refresh":
                        {
                            using (GraphicsPath p = new GraphicsPath())
                            {
                                p.AddArc(cx - 7f * k, cy - 7f * k, 14f * k, 14f * k, 120, 250);
                                g.DrawPath(pen, p);
                            }
                            PointF tip = new PointF(cx + 2f * k, cy - 8.5f * k);
                            using (SolidBrush b = new SolidBrush(c))
                            {
                                g.FillPolygon(b, new PointF[] {
                                    tip,
                                    new PointF(tip.X - 5f * k, tip.Y + 0.5f * k),
                                    new PointF(tip.X - 0.5f * k, tip.Y + 5f * k) });
                            }
                            break;
                        }
                    case "gear":
                        {
                            float rOut = 8.6f * k, rIn = 6.1f * k;
                            for (int i = 0; i < 8; i++)
                            {
                                double a = i * Math.PI / 4;
                                double half = 0.20;
                                PointF p1 = new PointF(cx + (float)Math.Cos(a - half) * rIn, cy + (float)Math.Sin(a - half) * rIn);
                                PointF p2 = new PointF(cx + (float)Math.Cos(a - half * 0.55) * rOut, cy + (float)Math.Sin(a - half * 0.55) * rOut);
                                PointF p3 = new PointF(cx + (float)Math.Cos(a + half * 0.55) * rOut, cy + (float)Math.Sin(a + half * 0.55) * rOut);
                                PointF p4 = new PointF(cx + (float)Math.Cos(a + half) * rIn, cy + (float)Math.Sin(a + half) * rIn);
                                using (SolidBrush b = new SolidBrush(c))
                                    g.FillPolygon(b, new PointF[] { p1, p2, p3, p4 });
                            }
                            using (Pen ring = new Pen(c, Math.Max(1.6f, 1.4f * k)))
                                g.DrawEllipse(ring, cx - 5.4f * k, cy - 5.4f * k, 10.8f * k, 10.8f * k);
                            using (Pen hole = new Pen(c, Math.Max(1.2f, 1.1f * k)))
                                g.DrawEllipse(hole, cx - 2.1f * k, cy - 2.1f * k, 4.2f * k, 4.2f * k);
                            break;
                        }
                    case "log":
                        {
                            RectangleF dr = new RectangleF(cx - 6f * k, cy - 7.5f * k, 12f * k, 15f * k);
                            g.DrawRectangle(pen, dr.X, dr.Y, dr.Width, dr.Height);
                            g.DrawLine(pen, cx - 3f * k, cy - 3f * k, cx + 3f * k, cy - 3f * k);
                            g.DrawLine(pen, cx - 3f * k, cy, cx + 3f * k, cy);
                            g.DrawLine(pen, cx - 3f * k, cy + 3f * k, cx + 1f * k, cy + 3f * k);
                            break;
                        }
                    case "web":
                        {
                            g.DrawEllipse(pen, cx - 7f * k, cy - 7f * k, 14f * k, 14f * k);
                            g.DrawEllipse(pen, cx - 3f * k, cy - 7f * k, 6f * k, 14f * k);
                            g.DrawLine(pen, cx - 7f * k, cy, cx + 7f * k, cy);
                            break;
                        }
                    case "expand":
                        {
                            g.DrawLine(pen, cx + 2f * k, cy - 2f * k, cx + 7.5f * k, cy - 7.5f * k);
                            g.DrawLine(pen, cx + 3.5f * k, cy - 7.5f * k, cx + 7.5f * k, cy - 7.5f * k);
                            g.DrawLine(pen, cx + 7.5f * k, cy - 3.5f * k, cx + 7.5f * k, cy - 7.5f * k);
                            g.DrawLine(pen, cx - 2f * k, cy + 2f * k, cx - 7.5f * k, cy + 7.5f * k);
                            g.DrawLine(pen, cx - 3.5f * k, cy + 7.5f * k, cx - 7.5f * k, cy + 7.5f * k);
                            g.DrawLine(pen, cx - 7.5f * k, cy + 3.5f * k, cx - 7.5f * k, cy + 7.5f * k);
                            break;
                        }
                    case "close":
                        {
                            g.DrawLine(pen, cx - 5f * k, cy - 5f * k, cx + 5f * k, cy + 5f * k);
                            g.DrawLine(pen, cx - 5f * k, cy + 5f * k, cx + 5f * k, cy - 5f * k);
                            break;
                        }
                    case "min":
                        {
                            g.DrawLine(pen, cx - 6f * k, cy + 5f * k, cx + 6f * k, cy + 5f * k);
                            break;
                        }
                    case "max":
                        {
                            g.DrawRectangle(pen, cx - 5.5f * k, cy - 5.5f * k, 11f * k, 11f * k);
                            break;
                        }
                    case "restore":
                        {
                            g.DrawRectangle(pen, cx - 6.5f * k, cy - 4.5f * k, 8f * k, 8f * k);
                            g.DrawRectangle(pen, cx - 1.5f * k, cy - 1.5f * k, 8f * k, 8f * k);
                            break;
                        }
                    case "sun":
                        {
                            float rr = 3.4f * k;
                            g.DrawEllipse(pen, cx - rr, cy - rr, rr * 2, rr * 2);
                            for (int i = 0; i < 8; i++)
                            {
                                double ang = i * Math.PI / 4;
                                float x1 = cx + (float)Math.Cos(ang) * 6.2f * k;
                                float y1 = cy + (float)Math.Sin(ang) * 6.2f * k;
                                float x2 = cx + (float)Math.Cos(ang) * 8.6f * k;
                                float y2 = cy + (float)Math.Sin(ang) * 8.6f * k;
                                g.DrawLine(pen, x1, y1, x2, y2);
                            }
                            break;
                        }
                    case "home":
                        {
                            using (GraphicsPath p = new GraphicsPath())
                            {
                                p.AddLines(new PointF[] {
                                    new PointF(cx - 9f * k, cy - 1f * k),
                                    new PointF(cx, cy - 10f * k),
                                    new PointF(cx + 9f * k, cy - 1f * k) });
                                g.DrawPath(pen, p);
                                g.DrawLine(pen, cx - 7f * k, cy - 1.5f * k, cx - 7f * k, cy + 9f * k);
                                g.DrawLine(pen, cx + 7f * k, cy - 1.5f * k, cx + 7f * k, cy + 9f * k);
                                g.DrawLine(pen, cx - 7f * k, cy + 9f * k, cx + 7f * k, cy + 9f * k);
                                g.DrawLine(pen, cx - 2.5f * k, cy + 9f * k, cx - 2.5f * k, cy + 2f * k);
                                g.DrawLine(pen, cx + 2.5f * k, cy + 9f * k, cx + 2.5f * k, cy + 2f * k);
                                g.DrawLine(pen, cx - 2.5f * k, cy + 2f * k, cx + 2.5f * k, cy + 2f * k);
                            }
                            break;
                        }
                    case "plus":
                        {
                            g.DrawLine(pen, cx - 6f * k, cy, cx + 6f * k, cy);
                            g.DrawLine(pen, cx, cy - 6f * k, cx, cy + 6f * k);
                            break;
                        }
                    case "grid":
                        {
                            float q = 4.2f * k;
                            float gap = 1.8f * k;
                            using (SolidBrush b = new SolidBrush(c))
                            {
                                g.FillRectangle(b, cx - q - gap, cy - q - gap, q, q);
                                g.FillRectangle(b, cx + gap, cy - q - gap, q, q);
                                g.FillRectangle(b, cx - q - gap, cy + gap, q, q);
                                g.FillRectangle(b, cx + gap, cy + gap, q, q);
                            }
                            break;
                        }
                }
            }
        }

        public static void DrawChevron(Graphics g, int cx, int cy, Color c, int size)
        {
            using (Pen pen = new Pen(c, 1.5f))
            {
                pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                g.DrawLine(pen, cx - size, cy - size / 2, cx, cy + size / 2);
                g.DrawLine(pen, cx, cy + size / 2, cx + size, cy - size / 2);
            }
        }

        // ---------- sketch decorations ----------
        public static void Tape(Graphics g, int cx, int cy, int w, int h, float angle)
        {
            GraphicsState s = g.Save();
            g.TranslateTransform(cx, cy);
            g.RotateTransform(angle);
            Rectangle r = new Rectangle(-w / 2, -h / 2, w, h);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(198, 232, 221, 192)))
                g.FillRectangle(b, r);
            using (Pen p = new Pen(Color.FromArgb(55, 118, 102, 76), 1f))
            {
                g.DrawLine(p, r.Left + 2, r.Top + 3, r.Right - 3, r.Top + 4);
                g.DrawLine(p, r.Left + 3, r.Bottom - 4, r.Right - 2, r.Bottom - 3);
                g.DrawLine(p, r.Left + 1, r.Top + 3, r.Left + 1, r.Bottom - 3);
            }
            g.Restore(s);
        }

        public static void Squiggle(Graphics g, int x, int y, int width, Color c, float amp)
        {
            using (Pen p = new Pen(c, 1.7f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                int steps = Math.Max(4, width / 7);
                PointF prev = new PointF(x, y);
                for (int i = 1; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    PointF pt = new PointF(x + width * t, y + amp * (float)Math.Sin(t * Math.PI * 3.2));
                    g.DrawLine(p, prev, pt);
                    prev = pt;
                }
            }
            using (Pen p2 = new Pen(Color.FromArgb(90, c), 1.1f))
            {
                int steps = Math.Max(4, width / 9);
                PointF prev = new PointF(x + 3, y + 2);
                for (int i = 1; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    PointF pt = new PointF(x + 3 + (width - 6) * t, y + 2 + (amp * 0.7f) * (float)Math.Sin(t * Math.PI * 3.2 + 0.6));
                    g.DrawLine(p2, prev, pt);
                    prev = pt;
                }
            }
        }

        // ---------- images ----------
        public static void ImageRect(Graphics g, Image img, Rectangle r, int radius)
        {
            if (img == null) return;
            using (GraphicsPath p = Round(r, radius))
            {
                GraphicsState s = g.Save();
                g.SetClip(p);
                if (img.Width == r.Width && img.Height == r.Height) g.DrawImageUnscaled(img, r.X, r.Y);
                else g.DrawImage(img, r);
                g.Restore(s);
            }
        }

        static readonly Dictionary<string, Bitmap> _tintSheets = new Dictionary<string, Bitmap>();

        public static void TintedFrame(Graphics g, Image img, Rectangle dest, Rectangle src, Color color)
        {
            if (img == null) return;
            string key = img.GetHashCode() + "_" + color.ToArgb();
            Bitmap sheet;
            if (!_tintSheets.TryGetValue(key, out sheet) || sheet == null)
            {
                sheet = new Bitmap(img.Width, img.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                using (Graphics tg = Graphics.FromImage(sheet))
                {
                    tg.CompositingQuality = CompositingQuality.HighQuality;
                    tg.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    ColorMatrix cm = new ColorMatrix(new float[][] {
                        new float[] {0,0,0,0,0},
                        new float[] {0,0,0,0,0},
                        new float[] {0,0,0,0,0},
                        new float[] {0,0,0,1,0},
                        new float[] {color.R/255f,color.G/255f,color.B/255f,0,1}
                    });
                    using (ImageAttributes ia = new ImageAttributes())
                    {
                        ia.SetColorMatrix(cm, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                        tg.DrawImage(img, new Rectangle(0, 0, img.Width, img.Height), 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, ia);
                    }
                }
                _tintSheets[key] = sheet;
            }
            InterpolationMode __old = g.InterpolationMode;
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            long __i0 = System.Diagnostics.Stopwatch.GetTimestamp();
            g.DrawImage(sheet, dest, src.X, src.Y, src.Width, src.Height, GraphicsUnit.Pixel);
            Perf.ImageTicks += System.Diagnostics.Stopwatch.GetTimestamp() - __i0;
            Perf.ImageCount++;
            g.InterpolationMode = __old;
        }
        public static void Tinted(Graphics g, Image img, Rectangle r, Color color)
        {
            if (img == null) return;
            ColorMatrix cm = new ColorMatrix(new float[][] {
                new float[] {0,0,0,0,0},
                new float[] {0,0,0,0,0},
                new float[] {0,0,0,0,0},
                new float[] {0,0,0,1,0},
                new float[] {color.R/255f,color.G/255f,color.B/255f,0,1}
            });
            using (ImageAttributes ia = new ImageAttributes())
            {
                ia.SetColorMatrix(cm);
                g.DrawImage(img, r, 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, ia);
            }
        }
    }
}
