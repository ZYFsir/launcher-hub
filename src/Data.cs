using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace LauncherHub
{
    enum LogKind { Normal, Dim, Info, Warn, Error, Ok }

    sealed class LogLine
    {
        public string Time = "";
        public string Level = "";
        public string Text = "";
        public LogKind Kind = LogKind.Normal;

        public LogLine(string time, string level, string text, LogKind kind)
        {
            Time = time; Level = level; Text = text; Kind = kind;
        }
        public LogLine(string text, LogKind kind)
        {
            Time = ""; Level = ""; Text = text; Kind = kind;
        }
    }

    sealed class AppEntry
    {
        public string Id = "";
        public string Kind = "";
        public string Name = "";
        public string Sub = "";
        public string Tag = "";

        public string WordAsset = "";
        public string MarkAsset = "";
        public bool WhaleMark;

        public Color Brand = Color.Black;
        public Color Accent = Color.Orange;
        public Color AccentText = Color.White;

        public int Port;
        public string Ip = "127.0.0.1";
        public string Url = "";
        public string State = "stopped";   // stopped / starting / running / stopping / error
        public int Pid;
        public bool External;
        public string Error = "";
        public string StatusLine = "";
        public string Uptime = "00:00";
        public string LastRun = "未知";
        public string ModelLine = "";

        public string ModelDisplay = "";
        public int Ctx;
        public bool AutoOpenBrowser = true;
        public string MmprojDisplay = "未启用";
        public string ExtraArgs = "";
        public string EnvSummary = "0 项";
        public string InstallPath = "";
        public string PythonPath = "";
        public string ConfigPath = "";
        public string LogPath = "";
        public string LaunchType = "process";
        public string LaunchCommand = "";
        public string LaunchArgs = "";
        public string LaunchCwd = "";
        public string HealthType = "none";
        public string HealthUrl = "";
        public string StopMode = "process-tree";
        public Dictionary<string, string> LaunchEnv = new Dictionary<string, string>();
        public string RemoteHost = "";
        public string Version = "";
        public string Device = "";

        public string[] Folders = new string[0];
        public string[] HomeRows = new string[0];
        public List<LogLine> Log = new List<LogLine>();

        public bool Running { get { return State == "running"; } }
        public bool Starting { get { return State == "starting"; } }
        public bool Stopping { get { return State == "stopping"; } }
        public bool Failed { get { return State == "error"; } }
        public bool SupportsStop { get { return StopMode != "none"; } }

        public string StateLabel
        {
            get
            {
                if (State == "running") return "运行中";
                if (State == "starting") return "启动中";
                if (State == "stopping") return "停止中";
                if (State == "error") return "异常";
                return "已停止";
            }
        }

        public string PlayIcon
        {
            get
            {
                if (State == "running") return "pause";
                if (State == "starting" || State == "stopping") return "stop";
                return "play";
            }
        }
    }

    static class Data
    {
        public static List<AppEntry> Apps = new List<AppEntry>();
        public static int CountRunning()
        {
            int n = 0;
            for (int i = 0; i < Apps.Count; i++) if (Apps[i].State == "running") n++;
            return n;
        }
        public static AppEntry ById(string id)
        {
            for (int i = 0; i < Apps.Count; i++) if (Apps[i].Id == id) return Apps[i];
            return Apps.Count > 0 ? Apps[0] : null;
        }
    }

    // Built-in app definitions. New apps: add a factory here + an AppRuntime kind.
    static class Catalog
    {
        public static AppEntry Llama(AppConfig cfg)
        {
            AppEntry a = new AppEntry();
            a.Id = "llama"; a.Kind = "llama";
            a.Name = "llama.cpp";
            a.Sub = "本地推理 · Vulkan";
            a.Tag = "本地推理 · Vulkan";
            a.WordAsset = "llama_word"; a.MarkAsset = "llama_mark";
            a.Brand = Color.FromArgb(20, 19, 23);
            a.Accent = Color.FromArgb(246, 94, 0);
            a.AccentText = Color.White;
            a.Port = cfg.Port; a.Ip = "127.0.0.1"; a.Url = "http://127.0.0.1:" + cfg.Port + "/";
            a.Folders = new string[] { "模型目录", "llama.cpp", "日志" };
            a.HomeRows = new string[] { "模型|", "端口|" + cfg.Port, "状态|已停止" };
            a.AutoOpenBrowser = cfg.AutoOpenBrowser;
            a.Ctx = cfg.Ctx;
            a.ModelDisplay = "";
            a.StatusLine = "端口 " + cfg.Port;
            a.ConfigPath = cfg.FilePath;
            return a;
        }

        public static AppEntry Comfy(AppConfig cfg)
        {
            AppEntry a = new AppEntry();
            a.Id = "comfy"; a.Kind = "comfy";
            a.Name = "ComfyUI";
            a.Sub = "图像生成 · ROCm";
            a.Tag = "图像生成 · ROCm";
            a.WordAsset = "comfy_word"; a.MarkAsset = "comfy_mark";
            a.Brand = Color.FromArgb(33, 25, 39);
            a.Accent = Color.FromArgb(242, 255, 89);
            a.AccentText = Color.FromArgb(24, 23, 28);
            a.Port = cfg.Port; a.Ip = "127.0.0.1"; a.Url = "http://127.0.0.1:" + cfg.Port + "/";
            a.Folders = new string[] { "输出", "模型", "checkpoints", "LoRA", "日志" };
            a.HomeRows = new string[] { "版本|未检测", "端口|" + cfg.Port, "状态|已停止" };
            a.AutoOpenBrowser = cfg.AutoOpenBrowser;
            a.InstallPath = cfg.InstallPath;
            a.PythonPath = cfg.PythonPath;
            a.ExtraArgs = cfg.ExtraArgs;
            a.EnvSummary = cfg.EnvLines.Count + " 项";
            a.StatusLine = "端口 " + cfg.Port;
            a.ConfigPath = cfg.FilePath;
            return a;
        }

        public static AppEntry FromManifest(ManifestApp m, AppConfig cfg)
        {
            AppEntry a = new AppEntry();
            a.Id = m.id;
            a.Kind = m.kind.Length > 0 ? m.kind : "generic";
            a.Name = m.name.Length > 0 ? m.name : m.id;
            a.Sub = m.subtitle;
            a.Tag = m.subtitle;
            a.Brand = ColorUtil.Parse(m.brand, Color.FromArgb(27, 30, 39));
            a.Accent = ColorUtil.Parse(m.accent, Color.FromArgb(77, 107, 254));
            a.AccentText = Color.White;
            a.WordAsset = m.icon != null ? m.icon.word : "";
            a.MarkAsset = m.icon != null ? m.icon.mark : "";
            a.WhaleMark = false;
            a.Port = m.port > 0 ? m.port : cfg.Port;
            a.Ip = "127.0.0.1";
            a.Url = "http://127.0.0.1:" + a.Port + "/";
            a.LaunchType = m.launch != null ? m.launch.type : "process";
            a.LaunchCommand = m.launch != null ? m.launch.command : "";
            a.LaunchArgs = m.launch != null ? JoinArgs(m.launch.args) : "";
            a.LaunchCwd = m.launch != null ? m.launch.cwd : "";
            a.StopMode = m.launch != null && m.launch.stop.Length > 0 ? m.launch.stop : "process-tree";
            if (m.launch != null && m.launch.env != null) a.LaunchEnv = m.launch.env;
            a.HealthType = m.health != null ? m.health.type : "none";
            a.HealthUrl = m.health != null ? m.health.url : "";
            a.LogPath = m.logFile;
            a.Folders = m.folders != null ? m.folders : new string[0];
            a.HomeRows = m.homeRows != null ? m.homeRows : new string[0];
            a.RemoteHost = m.remoteHost;
            a.StatusLine = "端口 " + a.Port;
            a.ConfigPath = cfg.FilePath;
            return a;
        }

        static string JoinArgs(string[] args)
        {
            if (args == null || args.Length == 0) return "";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                string s = args[i] ?? "";
                if (s.IndexOf(' ') >= 0 || s.IndexOf('"') >= 0)
                    sb.Append('"').Append(s.Replace("\"", "\\\"")).Append('"');
                else
                    sb.Append(s);
            }
            return sb.ToString();
        }

        public static AppEntry Dsh(AppConfig cfg)
        {
            AppEntry a = new AppEntry();
            a.Id = "dsh"; a.Kind = "dsh";
            a.Name = "DSH";
            a.Sub = "DeepSeek Harness · Web 工作台";
            a.Tag = "Web 工作台 · Tailscale";
            a.WordAsset = "dsh_word"; a.MarkAsset = "dsh_mark"; a.WhaleMark = false; a.StopMode = "none"; a.RemoteHost = Paths.RemoteHost;
            a.Brand = Color.FromArgb(28, 58, 140);
            a.Accent = Color.FromArgb(77, 107, 254);
            a.AccentText = Color.White;
            a.Port = cfg.Port; a.Ip = "127.0.0.1"; a.Url = "http://127.0.0.1:" + cfg.Port + "/";
            a.Folders = new string[] { "服务目录", "日志", "配置" };
            a.HomeRows = new string[] { "端口|" + cfg.Port, "远程|tailnet 内可用", "状态|已停止" };
            a.AutoOpenBrowser = cfg.AutoOpenBrowser;
            a.StatusLine = "端口 " + cfg.Port;
            a.ConfigPath = cfg.FilePath;
            return a;
        }
    }
}
