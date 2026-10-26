using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;

namespace LauncherHub
{
    // Rule-based icon audit. This exists because a white anti-alias halo around the
    // DSH home tile was invisible in code review but obvious in a screenshot.
    // Extend the region table when adding new app tiles.
    static class IconAudit
    {
        public static int Run(string logPath)
        {
            List<string> log = new List<string>();
            int failures = 0;
            log.Add("LauncherHub icon audit · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            log.Add("");

            try
            {
                using (HubController ctl = new HubController(false))
                using (Bitmap home = RenderPage(-1))
                {
                    for (int i = 0; i < ctl.Entries.Count; i++)
                    {
                        AppEntry a = ctl.Entries[i];
                        Rectangle card = Geo.HomeCard(i);
                        Rectangle tile = new Rectangle(card.X + 18, card.Y + 20, 44, 44);
                        failures += CheckTile(home, tile, a, "home card " + a.Id, log, true, 12);

                        Rectangle rail = Geo.RailItem(i + 1);
                        Rectangle railTile = new Rectangle(rail.X + 5, rail.Y + 5, 42, 42);
                        failures += CheckTile(home, railTile, a, "rail " + a.Id, log, false, 11);
                    }
                }

                CheckAsset("llama_word", true, log, ref failures);
                CheckAsset("comfy_word", true, log, ref failures);
                CheckAsset("dsh_word", true, log, ref failures);
                CheckAsset("llama_mark", false, log, ref failures);
                CheckAsset("dsh_mark", false, log, ref failures);
                CheckAsset("comfy_mark", false, log, ref failures);
            }
            catch (Exception ex)
            {
                failures++;
                log.Add("FAIL  audit crashed: " + ex.Message);
            }

            log.Add("");
            log.Add(failures == 0 ? "PASS  no icon anomalies detected" : ("FAIL  " + failures + " icon anomaly/anomalies"));
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(logPath));
                File.WriteAllLines(logPath, log.ToArray(), new System.Text.UTF8Encoding(false));
            }
            catch { }
            Environment.ExitCode = failures == 0 ? 0 : 1;
            return failures;
        }

        static Bitmap RenderPage(int page)
        {
            Bitmap bmp = new Bitmap(Geo.W, Geo.H, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            using (Graphics g = Graphics.FromImage(bmp))
            using (Renderer r = new Renderer(0))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                HubState st = new HubState();
                st.Page = page;
                st.PrevPage = page;
                st.SwitchT = 1f;
                st.Now = Environment.TickCount;
                r.Render(g, st, new Size(Geo.W, Geo.H));
            }
            return bmp;
        }

        static int CheckTile(Bitmap bmp, Rectangle tile, AppEntry a, string where, List<string> log, bool checkOutside, int radius)
        {
            // Only the whale tiles use a solid brand background under a white mark.
            // Light/icon artwork tiles are intentionally white/dark artwork, so skip them.
            if (!a.WhaleMark)
            {
                log.Add("PASS  " + where + " non-whale tile, halo scan not applicable");
                return 0;
            }

            int bright = 0, total = 0;
            int firstX = -1, firstY = -1;
            using (System.Drawing.Drawing2D.GraphicsPath shape = P.Round(tile, radius))
            {
                for (int y = tile.Top; y < tile.Bottom; y++)
                {
                    for (int x = tile.Left; x < tile.Right; x++)
                    {
                        if (!shape.IsVisible(x, y)) continue;
                        int d = Math.Min(Math.Min(x - tile.Left, tile.Right - 1 - x), Math.Min(y - tile.Top, tile.Bottom - 1 - y));
                        if (d > 4) continue;
                        total++;
                        Color c = bmp.GetPixel(x, y);
                        // Whale tile background must be the brand colour; any bright edge pixel
                        // is a white-fill/anti-alias halo, exactly the DSH regression.
                        if (Luma(c) > Luma(a.Brand) + 55 && Luma(c) > 95)
                        {
                            bright++;
                            if (firstX < 0) { firstX = x; firstY = y; }
                        }
                    }
                }
            }

            bool bad = bright >= 8;
            if (bad)
            {
                log.Add(string.Format("FAIL  {0}: bright halo inside dark tile ({1}/{2} edge px) at {3},{4}; brand={5}",
                    where, bright, total, firstX, firstY, a.Brand));
                return 1;
            }
            log.Add(string.Format("PASS  {0}: dark whale tile edge clean ({1}/{2} edge px)", where, bright, total));
            return 0;
        }
        static bool IsNearWhite(Color c)
        {
            return c.R > 225 && c.G > 225 && c.B > 225;
        }

        static double Luma(Color c)
        {
            return 0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B;
        }

        static void CheckAsset(string name, bool expectTransparent, List<string> log, ref int failures)
        {
            Image img = Assets.Get(name);
            if (img == null)
            {
                failures++;
                log.Add("FAIL  asset " + name + " missing");
                return;
            }
            using (Bitmap b = new Bitmap(img))
            {
                int edge = 0, edgeOpaque = 0;
                for (int x = 0; x < b.Width; x++)
                {
                    if (b.GetPixel(x, 0).A > 8) edgeOpaque++;
                    if (b.GetPixel(x, b.Height - 1).A > 8) edgeOpaque++;
                    edge += 2;
                }
                for (int y = 0; y < b.Height; y++)
                {
                    if (b.GetPixel(0, y).A > 8) edgeOpaque++;
                    if (b.GetPixel(b.Width - 1, y).A > 8) edgeOpaque++;
                    edge += 2;
                }
                double borderRatio = edge == 0 ? 0 : (double)edgeOpaque / edge;
                if (expectTransparent && borderRatio > 0.5)
                {
                    failures++;
                    log.Add(string.Format("FAIL  asset {0}: {1:0%} of border is opaque; expected transparent artwork", name, borderRatio));
                }
                else
                {
                    log.Add(string.Format("PASS  asset {0}: {1}x{2}, border alpha {3:0%}", name, b.Width, b.Height, borderRatio));
                }
            }
        }
    }
}
