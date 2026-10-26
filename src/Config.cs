using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Web.Script.Serialization;

namespace LauncherHub
{
    // Optional manifest model. Files live under apps/<id>/app.json.
    sealed class LaunchSpec
    {
        public string type = "process";
        public string command = "";
        public string[] args = new string[0];
        public string cwd = "";
        public Dictionary<string, string> env = new Dictionary<string, string>();
        public string stop = "process-tree";
    }

    sealed class HealthSpec
    {
        public string type = "none";
        public string url = "";
        public string host = "127.0.0.1";
        public int port;
        public int timeoutMs = 1500;
    }

    sealed class IconSpec
    {
        public string tile = "";
        public string mark = "";
        public string word = "";
        public string mono = "";
        public string source = "";
        public string wordSource = "text";
        public string license = "";
        public string credit = "";
    }

    sealed class ManifestApp
    {
        public string id = "";
        public string name = "";
        public string subtitle = "";
        public string kind = "generic";
        public string accent = "";
        public string brand = "";
        public LaunchSpec launch = new LaunchSpec();
        public HealthSpec health = new HealthSpec();
        public int port;
        public string logFile = "";
        public string[] folders = new string[0];
        public string[] homeRows = new string[0];
        public string remoteHost = "";
        public IconSpec icon = new IconSpec();

        public static List<ManifestApp> LoadAll()
        {
            List<ManifestApp> list = new List<ManifestApp>();
            string dir = Settings.Current.appsDir;
            if (dir.Length == 0 || !Directory.Exists(dir)) return list;
            string[] files;
            try { files = Directory.GetFiles(dir, "app.json", SearchOption.AllDirectories); }
            catch { return list; }
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            JavaScriptSerializer js = new JavaScriptSerializer();
            for (int i = 0; i < files.Length; i++)
            {
                try
                {
                    ManifestApp m = js.Deserialize<ManifestApp>(File.ReadAllText(files[i]));
                    if (m != null && m.id.Length > 0)
                    {
                        if (m.launch == null) m.launch = new LaunchSpec();
                        if (m.health == null) m.health = new HealthSpec();
                        if (m.icon == null) m.icon = new IconSpec();
                        list.Add(m);
                    }
                }
                catch { }
            }
            return list;
        }
    }

    sealed class PathConfig
    {
        public string llamaRoot = "";
        public string llamaScript = "";
        public string llamaModels = "";
        public string llamaLog = "";
        public string comfyDir = "";
        public string comfyPython = "";
        public string dshDir = "";
        public string dshScript = "";
        public string dshVbs = "";
        public string dshLaunchLog = "";
        public string dshOutLog = "";
    }

    sealed class SettingsData
    {
        public string appsDir = "apps";
        public string remoteHost = "";
        public PathConfig paths = new PathConfig();
    }

    static class Settings
    {
        public static SettingsData Current = new SettingsData();

        public static void Load()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string local = Path.Combine(baseDir, "config.local.json");
                string example = Path.Combine(baseDir, "config.example.json");
                string path = File.Exists(local) ? local : (File.Exists(example) ? example : "");
                if (path.Length == 0) return;
                JavaScriptSerializer js = new JavaScriptSerializer();
                SettingsData d = js.Deserialize<SettingsData>(File.ReadAllText(path));
                if (d == null) return;
                if (d.paths == null) d.paths = new PathConfig();
                if (d.appsDir == null || d.appsDir.Length == 0) d.appsDir = "apps";
                if (!Path.IsPathRooted(d.appsDir)) d.appsDir = Path.Combine(baseDir, d.appsDir);
                Current = d;
            }
            catch { Current = new SettingsData(); }
        }
    }

    static class ColorUtil
    {
        public static Color Parse(string s, Color fallback)
        {
            if (string.IsNullOrEmpty(s)) return fallback;
            try
            {
                if (s[0] == '#') s = s.Substring(1);
                int v = int.Parse(s, System.Globalization.NumberStyles.HexNumber);
                if (s.Length == 6) return Color.FromArgb((v >> 16) & 255, (v >> 8) & 255, v & 255);
            }
            catch { }
            return fallback;
        }
    }
}
