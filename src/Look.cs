using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace LauncherHub
{
    // Visual tokens for one of the two candidate skins.
    sealed class Look : IDisposable
    {
        public bool Sketch;
        public bool Dark;
        public TextureBrush Noise;
        public Color WindowBorder;

        public Color Bg;
        public Color RailBg;
        public Color RailBorder;
        public Color CardBg;
        public Color CardBorder;
        public Color Text;
        public Color Sub;
        public Color Faint;
        public Color Divider;
        public Color FieldBg;
        public Color FieldBorder;
        public Color ChipBg;
        public Color ChipBorder;
        public Color Ink;
        public Color Green;
        public Color Red;
        public Color Amber;

        public Color TermBg;
        public Color TermHeader;
        public Color TermBorder;
        public Color TermText;
        public Color TermDim;
        public Color TermInfo;
        public Color TermWarn;
        public Color TermError;
        public Color TermOk;

        public Font FSmall;
        public Font FSmallBold;
        public Font FBody;
        public Font FBodyBold;
        public Font FTitle;
        public Font FStatus;
        public Font FTiny;
        public Font FMono;
        public Font FMonoSmall;
        public Font FIcon;

        public static string UiFamily = "Microsoft YaHei UI";

        public static Look Flat() { return Flat(1f); }
        public static Look Flat(float s)
        {
            Look l = new Look();
            l.Sketch = false;
            l.Dark = false;
            l.WindowBorder = Color.FromArgb(94, 106, 124);
            l.Bg = Color.FromArgb(243, 245, 250);
            l.RailBg = Color.FromArgb(251, 252, 254);
            l.RailBorder = Color.FromArgb(228, 233, 242);
            l.CardBg = Color.White;
            l.CardBorder = Color.FromArgb(229, 234, 243);
            l.Text = Color.FromArgb(22, 25, 34);
            l.Sub = Color.FromArgb(88, 99, 116);
            l.Faint = Color.FromArgb(124, 135, 153);
            l.Divider = Color.FromArgb(237, 240, 246);
            l.FieldBg = Color.FromArgb(247, 249, 252);
            l.FieldBorder = Color.FromArgb(222, 228, 239);
            l.ChipBg = Color.FromArgb(245, 247, 251);
            l.ChipBorder = Color.FromArgb(226, 232, 242);
            l.Ink = Color.FromArgb(22, 25, 34);
            l.Green = Color.FromArgb(34, 197, 94);
            l.Red = Color.FromArgb(239, 68, 68);
            l.Amber = Color.FromArgb(245, 158, 11);
            l.TermBg = Color.FromArgb(27, 31, 39);
            l.TermHeader = Color.FromArgb(21, 25, 34);
            l.TermBorder = Color.FromArgb(42, 48, 64);
            l.TermText = Color.FromArgb(215, 222, 233);
            l.TermDim = Color.FromArgb(142, 155, 178);
            l.TermInfo = Color.FromArgb(136, 192, 208);
            l.TermWarn = Color.FromArgb(235, 203, 139);
            l.TermError = Color.FromArgb(191, 97, 106);
            l.TermOk = Color.FromArgb(163, 190, 140);
            l.FSmall = MakeFont(UiFamily, 10f * s, FontStyle.Regular);
            l.FSmallBold = MakeFont(UiFamily, 10f * s, FontStyle.Bold);
            l.FBody = MakeFont(UiFamily, 11f * s, FontStyle.Regular);
            l.FBodyBold = MakeFont(UiFamily, 11f * s, FontStyle.Bold);
            l.FTitle = MakeFont(UiFamily, 17.5f * s, FontStyle.Bold);
            l.FStatus = MakeFont(UiFamily, 15.5f * s, FontStyle.Bold);
            l.FTiny = MakeFont(UiFamily, 8.75f * s, FontStyle.Regular);
            l.FMono = MakeFont("Consolas", 9.5f * s, FontStyle.Regular);
            l.FMonoSmall = MakeFont("Consolas", 9.25f * s, FontStyle.Regular);
            l.FIcon = MakeFont("Segoe UI Symbol", 12f * s, FontStyle.Regular);
            return l;
        }

        public static Look DarkTheme() { return DarkTheme(1f); }
        public static Look DarkTheme(float s)
        {
            Look l = new Look();
            l.Sketch = false;
            l.Dark = true;
            l.WindowBorder = Color.FromArgb(58, 69, 89);
            l.Bg = Color.FromArgb(17, 21, 29);
            l.RailBg = Color.FromArgb(13, 17, 24);
            l.RailBorder = Color.FromArgb(35, 43, 58);
            l.CardBg = Color.FromArgb(26, 32, 44);
            l.CardBorder = Color.FromArgb(40, 49, 66);
            l.Text = Color.FromArgb(229, 234, 243);
            l.Sub = Color.FromArgb(154, 166, 186);
            l.Faint = Color.FromArgb(113, 126, 148);
            l.Divider = Color.FromArgb(39, 47, 61);
            l.FieldBg = Color.FromArgb(20, 26, 37);
            l.FieldBorder = Color.FromArgb(43, 53, 70);
            l.ChipBg = Color.FromArgb(24, 31, 44);
            l.ChipBorder = Color.FromArgb(41, 51, 68);
            l.Ink = Color.FromArgb(229, 234, 243);
            l.Green = Color.FromArgb(74, 222, 128);
            l.Red = Color.FromArgb(248, 113, 113);
            l.Amber = Color.FromArgb(251, 191, 36);
            l.TermBg = Color.FromArgb(15, 19, 27);
            l.TermHeader = Color.FromArgb(12, 16, 23);
            l.TermBorder = Color.FromArgb(38, 47, 61);
            l.TermText = Color.FromArgb(215, 222, 233);
            l.TermDim = Color.FromArgb(142, 155, 178);
            l.TermInfo = Color.FromArgb(136, 192, 208);
            l.TermWarn = Color.FromArgb(235, 203, 139);
            l.TermError = Color.FromArgb(240, 113, 120);
            l.TermOk = Color.FromArgb(163, 190, 140);
            l.FSmall = MakeFont(UiFamily, 10f * s, FontStyle.Regular);
            l.FSmallBold = MakeFont(UiFamily, 10f * s, FontStyle.Bold);
            l.FBody = MakeFont(UiFamily, 11f * s, FontStyle.Regular);
            l.FBodyBold = MakeFont(UiFamily, 11f * s, FontStyle.Bold);
            l.FTitle = MakeFont(UiFamily, 17.5f * s, FontStyle.Bold);
            l.FStatus = MakeFont(UiFamily, 15.5f * s, FontStyle.Bold);
            l.FTiny = MakeFont(UiFamily, 8.75f * s, FontStyle.Regular);
            l.FMono = MakeFont("Consolas", 9.5f * s, FontStyle.Regular);
            l.FMonoSmall = MakeFont("Consolas", 9.25f * s, FontStyle.Regular);
            l.FIcon = MakeFont("Segoe UI Symbol", 12f * s, FontStyle.Regular);
            return l;
        }

        public static Font MakeFont(string family, float size, FontStyle style)
        {
            try
            {
                Font f = new Font(family, size, style);
                if (f.Name == family) return f;
                f.Dispose();
            }
            catch { }
            try { return new Font("Microsoft YaHei UI", size, style); }
            catch { }
            try { return new Font("Segoe UI", size, style); }
            catch { }
            return new Font(FontFamily.GenericSansSerif, size, style);
        }

        public void Dispose()
        {
            if (FSmall != null) FSmall.Dispose();
            if (FSmallBold != null) FSmallBold.Dispose();
            if (FBody != null) FBody.Dispose();
            if (FBodyBold != null) FBodyBold.Dispose();
            if (FTitle != null) FTitle.Dispose();
            if (FStatus != null) FStatus.Dispose();
            if (FTiny != null) FTiny.Dispose();
            if (FMono != null) FMono.Dispose();
            if (FMonoSmall != null) FMonoSmall.Dispose();
            if (FIcon != null) FIcon.Dispose();
        }
    }
}
