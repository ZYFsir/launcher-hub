using System;
using System.Drawing;
using System.IO;
using System.Text;

namespace LauncherHub
{
    // Static container/layout audit. It runs the real Geo layout at the same
    // physical-size matrix used by the app and checks the bounding boxes that
    // have historically overlapped (note vs. action row, fields vs. status).
    static class LayoutAudit
    {
        public static int Run(string path)
        {
            var lines = new StringBuilder();
            lines.AppendLine("LauncherHub layout audit · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            lines.AppendLine("rule: container/button/text boxes must not overlap; content must fit");
            lines.AppendLine();
            int fail = 0;
            int[,] sizes = { {1000,700}, {1240,800}, {1600,1000}, {2560,1392} };
            for (int i = 0; i < sizes.GetLength(0); i++)
            {
                int w = sizes[i, 0], h = sizes[i, 1];
                float ws = WindowScale(w, h);
                int lw = (int)Math.Round(w / ws);
                int lh = (int)Math.Round(h / ws);
                Geo.Resize(lw, lh);
                string tag = w + "x" + h + " -> logical " + lw + "x" + lh + " scale=" + ws.ToString("0.00");
                int before = fail;
                Rectangle c = Geo.Right;
                Rectangle content = Geo.Content;
                Rectangle term = Geo.Term;

                if (content.Width > 1380) Fail(lines, ref fail, tag, "content wider than 1380");
                if (content.Right > lw) Fail(lines, ref fail, tag, "content beyond window");
                if (c.Width < 330) Fail(lines, ref fail, tag, "right card too narrow: " + c.Width);

                // IP field + port field + state pill must not collide.
                int ipRight = c.X + 44 + 190;
                int portRight = c.X + 292 + 96;
                int pillLeft = c.Right - 112;
                if (Math.Max(ipRight, portRight) > pillLeft - 8)
                    Fail(lines, ref fail, tag, "IP/port fields collide with state pill");

                // Optional note must fit above the bottom action row.
                int noteY = Math.Min(348, c.Bottom - 58 - 44 - 12);
                bool noteDrawn = noteY >= 336 && c.Height >= 336;
                int buttonTop = c.Bottom - 58;
                if (noteDrawn && noteY + 44 + 6 > buttonTop)
                    Fail(lines, ref fail, tag, "note overlaps bottom action row");

                // Bottom actions must stay inside the card and not overlap the gear.
                int primaryRight = c.X + 18 + 128;
                int secondaryRight = primaryRight + 10 + 96;
                int gearLeft = c.Right - 62;
                if (secondaryRight > c.Right - 18)
                    Fail(lines, ref fail, tag, "action row exceeds card");
                if (secondaryRight > gearLeft - 8)
                    Fail(lines, ref fail, tag, "action row collides with gear");

                // Home cards: three equal buttons/cards must fit in content.
                for (int k = 0; k < 3; k++)
                {
                    Rectangle hc = Geo.HomeCard(k);
                    if (hc.Width < 280) Fail(lines, ref fail, tag, "home card too narrow #" + k);
                    if (hc.Right > content.Right + 1) Fail(lines, ref fail, tag, "home card beyond content #" + k);
                }

                if (term.Height < 40) Fail(lines, ref fail, tag, "terminal too short: " + term.Height);
                if (Geo.Left.Bottom + 16 > term.Y + 1) Fail(lines, ref fail, tag, "left card overlaps terminal");
                if (Geo.Right.Bottom + 16 > term.Y + 1) Fail(lines, ref fail, tag, "right card overlaps terminal");

                if (fail == before) lines.AppendLine("PASS  " + tag);
            }
            lines.AppendLine();
            lines.AppendLine(fail == 0 ? "PASS  all layout containers fit" : "FAIL  " + fail + " layout collision(s)");
            try { File.WriteAllText(path, lines.ToString(), Encoding.UTF8); } catch { }
            Console.Write(lines.ToString());
            return fail == 0 ? 0 : 1;
        }

        static float WindowScale(int w, int h)
        {
            float sx = Math.Min(1.25f, w / 1240f);
            float sy = Math.Min(1.25f, h / 800f);
            return Math.Max(0.78f, Math.Min(sx, sy));
        }

        static void Fail(StringBuilder lines, ref int fail, string tag, string msg)
        {
            fail++;
            lines.AppendLine("FAIL  " + tag + " : " + msg);
        }
    }
}
