using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Management;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace LauncherHub
{
    enum AppPhase { Stopped, Starting, Running, Stopping, Error }

    sealed class ModelItem
    {
        public string Path = "";
        public string Display = "";
        public double GiB;
        public override string ToString() { return Display; }
    }

    static class Paths
    {
        public static string LlamaRoot { get { return Settings.Current.paths.llamaRoot; } }
        public static string LlamaScript { get { return Settings.Current.paths.llamaScript; } }
        public static string LlamaModels { get { return Settings.Current.paths.llamaModels; } }
        public static string LlamaLog { get { return Settings.Current.paths.llamaLog; } }
        public static string ComfyDefaultDir { get { return Settings.Current.paths.comfyDir; } }
        public static string ComfyDefaultPython { get { return Settings.Current.paths.comfyPython; } }
        public static string DshDir { get { return Settings.Current.paths.dshDir; } }
        public static string DshScript { get { return Settings.Current.paths.dshScript; } }
        public static string DshVbs { get { return Settings.Current.paths.dshVbs; } }
        public static string DshLaunchLog { get { return Settings.Current.paths.dshLaunchLog; } }
        public static string DshOutLog { get { return Settings.Current.paths.dshOutLog; } }
        public static string RemoteHost { get { return Settings.Current.remoteHost; } }

        public static string LocalRoot
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LauncherHub"); }
        }
        public static string ConfigFile(string id)
        {
            return Path.Combine(LocalRoot, id + ".ini");
        }
        public static string Word(string name)
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", name + ".png");
        }
    }

    // ---------------- config ----------------
    sealed class AppConfig
    {
        public string Id = "";
        public string FilePath = "";
        public int Port = 0;
        public bool AutoOpenBrowser = true;

        public string ModelPath = "";
        public string MmprojPath = "";
        public int Ctx = 8192;
        public string ExtraArgs = "";

        public string InstallPath = "";
        public string PythonPath = "";
        public List<string> EnvLines = new List<string>();

        public static AppConfig Load(string id)
        {
            AppConfig c = new AppConfig();
            c.Id = id;
            c.FilePath = Paths.ConfigFile(id);
            if (File.Exists(c.FilePath))
            {
                c.ReadFile(c.FilePath);
            }
            else
            {
                c.ApplyDefaults();
                c.ImportLegacy();
                c.Save();
            }
            c.Normalize();
            return c;
        }

        void ApplyDefaults()
        {
            if (Id == "llama")
            {
                Port = 8082;
                Ctx = 8192;
                AutoOpenBrowser = true;
                ModelPath = PickDefaultModel(Paths.LlamaModels);
            }
            else if (Id == "comfy")
            {
                Port = 8188;
                AutoOpenBrowser = true;
                InstallPath = Paths.ComfyDefaultDir;
                PythonPath = Paths.ComfyDefaultPython;
                ExtraArgs = "--reserve-vram 1 --enable-manager";
            }
            else
            {
                Port = 3080;
                AutoOpenBrowser = true;
            }
        }

        void ImportLegacy()
        {
            try
            {
                string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (Id == "llama")
                {
                    string p = Path.Combine(baseDir, "Llama-Launcher", "config.ini");
                    if (!File.Exists(p)) return;
                    Dictionary<string, string> d = ReadIni(p);
                    string v;
                    if (d.TryGetValue("Model", out v) && v.Length > 0) ModelPath = v;
                    if (d.TryGetValue("Mmproj", out v)) MmprojPath = v;
                    int n;
                    if (d.TryGetValue("Port", out v) && int.TryParse(v, out n) && n > 0) Port = n;
                    if (d.TryGetValue("Ctx", out v) && int.TryParse(v, out n) && n > 0) Ctx = n;
                    if (d.TryGetValue("ExtraArgs", out v)) ExtraArgs = v;
                    if (d.TryGetValue("AutoOpenBrowser", out v)) AutoOpenBrowser = Truthy(v);
                }
                else if (Id == "comfy")
                {
                    string p = Path.Combine(baseDir, "ComfyUI-Launcher", "config.ini");
                    if (!File.Exists(p)) return;
                    Dictionary<string, string> d = ReadIni(p);
                    string v;
                    int n;
                    if (d.TryGetValue("Port", out v) && int.TryParse(v, out n) && n > 0) Port = n;
                    if (d.TryGetValue("AutoOpenBrowser", out v)) AutoOpenBrowser = Truthy(v);
                    if (d.TryGetValue("InstallPath", out v) && v.Length > 0) InstallPath = v;
                    if (d.TryGetValue("PythonPath", out v) && v.Length > 0) PythonPath = v;
                    if (d.TryGetValue("ExtraArgs", out v)) ExtraArgs = v;
                    foreach (KeyValuePair<string, string> kv in d)
                    {
                        if (kv.Key.StartsWith("Env.", StringComparison.OrdinalIgnoreCase) && kv.Key.Length > 4)
                            EnvLines.Add(kv.Key.Substring(4) + "=" + kv.Value);
                    }
                }
            }
            catch { }
        }

        static bool Truthy(string v)
        {
            v = (v == null ? "" : v.Trim().ToLowerInvariant());
            return v == "1" || v == "true" || v == "yes" || v == "on";
        }

        public static Dictionary<string, string> ReadIni(string path)
        {
            Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim();
                    string v = line.Substring(eq + 1).Trim();
                    d[k] = v;
                }
            }
            catch { }
            return d;
        }

        void ReadFile(string path)
        {
            Dictionary<string, string> d = ReadIni(path);
            string v;
            int n;
            if (d.TryGetValue("Port", out v) && int.TryParse(v, out n) && n > 0) Port = n;
            if (d.TryGetValue("AutoOpenBrowser", out v)) AutoOpenBrowser = Truthy(v);
            if (d.TryGetValue("Model", out v)) ModelPath = v;
            if (d.TryGetValue("Mmproj", out v)) MmprojPath = v;
            if (d.TryGetValue("Ctx", out v) && int.TryParse(v, out n) && n > 0) Ctx = n;
            if (d.TryGetValue("ExtraArgs", out v)) ExtraArgs = v;
            if (d.TryGetValue("InstallPath", out v)) InstallPath = v;
            if (d.TryGetValue("PythonPath", out v)) PythonPath = v;
            foreach (KeyValuePair<string, string> kv in d)
            {
                if (kv.Key.StartsWith("Env.", StringComparison.OrdinalIgnoreCase) && kv.Key.Length > 4)
                    EnvLines.Add(kv.Key.Substring(4) + "=" + kv.Value);
            }
        }

        void Normalize()
        {
            if (Port < 1 || Port > 65535) Port = Id == "llama" ? 8082 : (Id == "comfy" ? 8188 : 3080);
            if (Id == "llama")
            {
                if (Ctx < 256) Ctx = 8192;
                if (ModelPath.Length == 0 || !File.Exists(ModelPath)) ModelPath = PickDefaultModel(Paths.LlamaModels);
                if (MmprojPath.Length > 0 && !File.Exists(MmprojPath)) MmprojPath = "";
            }
            else if (Id == "comfy")
            {
                if (InstallPath.Length == 0) InstallPath = Paths.ComfyDefaultDir;
                if (PythonPath.Length == 0) PythonPath = Paths.ComfyDefaultPython;
            }
        }

        public static string PickDefaultModel(string dir)
        {
            try
            {
                if (!Directory.Exists(dir)) return "";
                List<string> files = new List<string>(Directory.GetFiles(dir, "*.gguf"));
                files.Sort(StringComparer.OrdinalIgnoreCase);
                foreach (string f in files)
                {
                    if (Path.GetFileName(f).StartsWith("mmproj-", StringComparison.OrdinalIgnoreCase)) continue;
                    return f;
                }
                foreach (string f in files)
                    if (!Path.GetFileName(f).StartsWith("mmproj-", StringComparison.OrdinalIgnoreCase)) return f;
            }
            catch { }
            return "";
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Paths.LocalRoot);
                List<string> lines = new List<string>();
                lines.Add("# LauncherHub / " + Id + " config (UTF-8)");
                lines.Add("Port=" + Port.ToString());
                lines.Add("AutoOpenBrowser=" + (AutoOpenBrowser ? "1" : "0"));
                if (Id == "llama")
                {
                    lines.Add("Model=" + ModelPath);
                    lines.Add("Mmproj=" + MmprojPath);
                    lines.Add("Ctx=" + Ctx.ToString());
                    lines.Add("ExtraArgs=" + ExtraArgs);
                }
                else if (Id == "comfy")
                {
                    lines.Add("InstallPath=" + InstallPath);
                    lines.Add("PythonPath=" + PythonPath);
                    lines.Add("ExtraArgs=" + ExtraArgs);
                    for (int i = 0; i < EnvLines.Count; i++)
                        lines.Add("Env." + EnvLines[i]);
                }
                File.WriteAllLines(FilePath, lines.ToArray(), new UTF8Encoding(false));
            }
            catch { }
        }
    }

    // ---------------- one application ----------------
    sealed class AppRuntime
    {
        public readonly string Kind;
        public readonly AppConfig Cfg;
        public readonly AppEntry Entry;
        public readonly List<ModelItem> Models = new List<ModelItem>();
        public readonly List<ModelItem> Mmprojs = new List<ModelItem>();

        readonly object _sync = new object();
        readonly string _logPath;
        readonly ManifestApp _manifest;
        Process _proc;
        bool _weStarted;
        bool _operating;
        bool _autoOpenPending;
        DateTime _phaseSince = DateTime.Now;
        DateTime _runningSince = DateTime.MinValue;
        DateTime _lastRun = DateTime.MinValue;
        AppPhase _phase = AppPhase.Stopped;
        int _pid;
        bool _external;
        string _error = "";
        string _liveModel = "";
        int _liveCtx;
        string _version = "";
        string _device = "";
        long _vramUsed;
        long _vramTotal;
        int _healthFail;
        DateTime _lastExternalScan = DateTime.MinValue;
        DateTime _lastPidLookup = DateTime.MinValue;
        List<LogLine> _logLines = new List<LogLine>();
        DateTime _lastLogRead = DateTime.MinValue;
        StreamWriter _outWriter;
        StreamWriter _errWriter;
        volatile bool _polling;

        public AppRuntime(string kind, AppConfig cfg, AppEntry entry)
            : this(kind, cfg, entry, null)
        {
        }

        public AppRuntime(string kind, AppConfig cfg, AppEntry entry, ManifestApp manifest)
        {
            Kind = kind;
            Cfg = cfg;
            Entry = entry;
            _manifest = manifest;
            if (entry.LogPath != null && entry.LogPath.Length > 0) _logPath = entry.LogPath;
            else if (Kind == "llama") _logPath = Paths.LlamaLog;
            else if (Kind == "comfy") _logPath = Path.Combine(cfg.InstallPath, "comfyui.err.log");
            else if (Kind == "dsh") _logPath = Paths.DshLaunchLog;
            else _logPath = Path.Combine(Paths.LocalRoot, Kind + ".out.log");
            if (!Path.IsPathRooted(_logPath))
                _logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _logPath);
            try { if (File.Exists(_logPath)) _lastRun = File.GetLastWriteTime(_logPath); } catch { }
            RefreshModels();
        }

        public AppPhase Phase { get { lock (_sync) return _phase; } }
        public string Error { get { lock (_sync) return _error; } }
        public bool IsBusy { get { lock (_sync) return _operating || _phase == AppPhase.Starting || _phase == AppPhase.Stopping; } }
        public bool IsRunning { get { lock (_sync) return _phase == AppPhase.Running; } }

        public void RefreshModels()
        {
            Models.Clear();
            Mmprojs.Clear();
            if (Kind == "llama")
            {
                try
                {
                    if (Directory.Exists(Paths.LlamaModels))
                    {
                        List<string> files = new List<string>(Directory.GetFiles(Paths.LlamaModels, "*.gguf"));
                        files.Sort(StringComparer.OrdinalIgnoreCase);
                        foreach (string f in files)
                        {
                            string name = Path.GetFileName(f);
                            if (name.StartsWith("mmproj-", StringComparison.OrdinalIgnoreCase))
                            {
                                Mmprojs.Add(MakeItem(f));
                            }
                            else
                            {
                                Models.Add(MakeItem(f));
                            }
                        }
                    }
                }
                catch { }
                if (Cfg.ModelPath.Length == 0 && Models.Count > 0) Cfg.ModelPath = Models[0].Path;
            }
        }

        static ModelItem MakeItem(string path)
        {
            ModelItem m = new ModelItem();
            m.Path = path;
            m.Display = Path.GetFileNameWithoutExtension(path);
            try { m.GiB = Math.Round(new FileInfo(path).Length / 1073741824.0, 2); }
            catch { }
            if (m.GiB > 0) m.Display += " · " + m.GiB.ToString("0.00") + " GiB";
            return m;
        }

        // ---------------- operations ----------------
        public void StartAsync()
        {
            lock (_sync)
            {
                if (_operating || _phase == AppPhase.Starting || _phase == AppPhase.Running || _phase == AppPhase.Stopping) return;
                _operating = true;
                _phase = AppPhase.Starting;
                _phaseSince = DateTime.Now;
                _error = "";
                _weStarted = false;
                _autoOpenPending = Cfg.AutoOpenBrowser;
                _healthFail = 0;
            }
            StartThread(delegate
            {
                try { DoStart(); }
                catch (Exception ex) { FailStart(ex.Message); }
                finally { lock (_sync) _operating = false; }
            });
        }

        public void StopAsync()
        {
            if (Entry.StopMode == "none") return;
            lock (_sync)
            {
                if (_operating || _phase == AppPhase.Stopped || _phase == AppPhase.Stopping) return;
                _operating = true;
                _phase = AppPhase.Stopping;
                _phaseSince = DateTime.Now;
                _error = "";
            }
            StartThread(delegate
            {
                try
                {
                    DoStop();
                    lock (_sync)
                    {
                        _phase = AppPhase.Stopped;
                        _phaseSince = DateTime.Now;
                        _lastRun = DateTime.Now;
                        _pid = 0;
                        _external = false;
                        _weStarted = false;
                    }
                }
                catch (Exception ex)
                {
                    lock (_sync) { _phase = AppPhase.Error; _error = "停止失败：" + ex.Message; }
                }
                finally { lock (_sync) _operating = false; }
            });
        }

        public void RestartAsync()
        {
            if (Entry.StopMode == "none") { OpenWeb(); return; }
            lock (_sync)
            {
                if (_operating) return;
                _operating = true;
                _phase = AppPhase.Stopping;
                _phaseSince = DateTime.Now;
                _error = "";
            }
            StartThread(delegate
            {
                try
                {
                    DoStop();
                    Thread.Sleep(500);
                    lock (_sync) { _phase = AppPhase.Starting; _phaseSince = DateTime.Now; _autoOpenPending = Cfg.AutoOpenBrowser; _healthFail = 0; }
                    DoStart();
                }
                catch (Exception ex) { FailStart(ex.Message); }
                finally { lock (_sync) _operating = false; }
            });
        }

        public void ToggleAsync()
        {
            lock (_sync)
            {
                if (Kind == "dsh")
                {
                    if (_phase == AppPhase.Running) { }
                    else { }
                }
            }
            if (Kind == "dsh")
            {
                if (IsRunning) OpenWeb();
                else StartAsync();
                return;
            }
            if (IsRunning || Phase == AppPhase.Starting || Phase == AppPhase.Error) StopAsync();
            else StartAsync();
        }

        static void StartThread(ThreadStart body)
        {
            Thread t = new Thread(body);
            t.IsBackground = true;
            t.Start();
        }

        void DoStart()
        {
            int port = Cfg.Port;
            if (Kind == "generic")
            {
                DoStartGeneric();
                return;
            }
            if (Kind == "llama")
            {
                if (!File.Exists(Paths.LlamaScript)) throw new FileNotFoundException("找不到启动脚本：" + Paths.LlamaScript);
                if (IsPortOpen(port)) throw new InvalidOperationException("端口 " + port + " 已被占用，请先停止或换端口。");
                string ps = PowerShellPath();
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = ps;
                psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + Paths.LlamaScript + "\"";
                psi.WorkingDirectory = Paths.LlamaRoot;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                psi.EnvironmentVariables["LLAMA_PORT"] = port.ToString();
                psi.EnvironmentVariables["LLAMA_MODEL"] = Cfg.ModelPath;
                psi.EnvironmentVariables["LLAMA_CTX"] = Cfg.Ctx.ToString();
                if (Cfg.MmprojPath.Length > 0) psi.EnvironmentVariables["LLAMA_MMPROJ"] = Cfg.MmprojPath;
                if (Cfg.ExtraArgs.Length > 0) psi.EnvironmentVariables["LLAMA_EXTRA_ARGS"] = Cfg.ExtraArgs;
                lock (_sync)
                {
                    _proc = Process.Start(psi);
                    _weStarted = true;
                    _error = "";
                }
            }
            else if (Kind == "comfy")
            {
                string mainPy = Path.Combine(Cfg.InstallPath, "main.py");
                if (!File.Exists(Cfg.PythonPath)) throw new FileNotFoundException("找不到 Python：" + Cfg.PythonPath);
                if (!File.Exists(mainPy)) throw new FileNotFoundException("找不到 ComfyUI main.py：" + mainPy);
                if (IsPortOpen(port)) throw new InvalidOperationException("端口 " + port + " 已被占用，请先停止或换端口。");
                OpenComfyWriters();
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = Cfg.PythonPath;
                psi.Arguments = "main.py --listen 127.0.0.1 --port " + port.ToString()
                    + (Cfg.ExtraArgs.Length > 0 ? " " + Cfg.ExtraArgs : "");
                psi.WorkingDirectory = Cfg.InstallPath;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.StandardErrorEncoding = Encoding.UTF8;
                psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
                psi.EnvironmentVariables["PYTHONUNBUFFERED"] = "1";
                for (int i = 0; i < Cfg.EnvLines.Count; i++)
                {
                    string line = Cfg.EnvLines[i];
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    psi.EnvironmentVariables[line.Substring(0, eq).Trim()] = line.Substring(eq + 1);
                }
                Process p = new Process();
                p.StartInfo = psi;
                p.EnableRaisingEvents = true;
                p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e) { if (e.Data != null) AppendComfy(e.Data, false); };
                p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e) { if (e.Data != null) AppendComfy(e.Data, true); };
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                lock (_sync)
                {
                    _proc = p;
                    _weStarted = true;
                    _error = "";
                    _pid = p.Id;
                }
            }
            else
            {
                if (IsPortOpen(port)) return;
                string ps = PowerShellPath();
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = ps;
                psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + Paths.DshScript + "\" -NoOpen -Port " + port.ToString();
                psi.WorkingDirectory = Paths.DshDir;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                lock (_sync)
                {
                    _proc = Process.Start(psi);
                    _weStarted = true;
                    _error = "";
                }
            }
        }

        void DoStartGeneric()
        {
            if (string.Equals(Entry.LaunchType, "http", StringComparison.OrdinalIgnoreCase))
            {
                lock (_sync) { _weStarted = false; _external = true; _error = ""; }
                return;
            }
            if (Entry.LaunchCommand.Length == 0)
                throw new InvalidOperationException("manifest 缺少 launch.command");
            string cwd = Entry.LaunchCwd;
            if (cwd.Length == 0) cwd = AppDomain.CurrentDomain.BaseDirectory;
            else if (!Path.IsPathRooted(cwd)) cwd = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, cwd);
            if (Entry.Port > 0 && IsPortOpen(Entry.Port))
                throw new InvalidOperationException("端口 " + Entry.Port + " 已被占用，请先停止或换端口。");

            ProcessStartInfo psi = new ProcessStartInfo();
            if (string.Equals(Entry.LaunchType, "powershell", StringComparison.OrdinalIgnoreCase))
            {
                psi.FileName = PowerShellPath();
                psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" +
                    ResolveCommand(Entry.LaunchCommand, cwd) + "\"" +
                    (Entry.LaunchArgs.Length > 0 ? " " + Entry.LaunchArgs : "");
            }
            else
            {
                psi.FileName = ResolveCommand(Entry.LaunchCommand, cwd);
                psi.Arguments = Entry.LaunchArgs;
            }
            psi.WorkingDirectory = cwd;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;
            foreach (KeyValuePair<string, string> kv in Entry.LaunchEnv)
                psi.EnvironmentVariables[kv.Key] = kv.Value;

            OpenGenericWriters();
            Process p = new Process();
            p.StartInfo = psi;
            p.EnableRaisingEvents = true;
            p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e) { if (e.Data != null) AppendGeneric(e.Data, false); };
            p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e) { if (e.Data != null) AppendGeneric(e.Data, true); };
            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            lock (_sync)
            {
                _proc = p;
                _weStarted = true;
                _pid = p.Id;
                _error = "";
            }
        }

        string ResolveCommand(string command, string cwd)
        {
            if (command.Length == 0) return command;
            if (Path.IsPathRooted(command)) return command;
            string joined = Path.Combine(cwd, command);
            if (File.Exists(joined)) return joined;
            return command;
        }

        void OpenGenericWriters()
        {
            try
            {
                string dir = Path.GetDirectoryName(_logPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                FileStream fs = new FileStream(_logPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                StreamWriter sw = new StreamWriter(fs, new UTF8Encoding(false));
                sw.AutoFlush = true;
                lock (_sync) { CloseGenericWritersLocked(); _outWriter = sw; }
            }
            catch { }
        }

        void AppendGeneric(string line, bool err)
        {
            try
            {
                lock (_sync)
                {
                    StreamWriter w = err ? _errWriter : _outWriter;
                    if (w == null) w = _outWriter;
                    if (w != null) w.WriteLine(line);
                }
            }
            catch { }
        }

        void CloseGenericWritersLocked()
        {
            try { if (_outWriter != null) _outWriter.Dispose(); } catch { }
            try { if (_errWriter != null) _errWriter.Dispose(); } catch { }
            _outWriter = null; _errWriter = null;
        }

        void DoStopGeneric()
        {
            int pid;
            lock (_sync) pid = _pid;
            if (pid == 0) pid = FindComfyPid(Entry.Port);
            if (pid > 0) KillTree(pid);
            else
            {
                lock (_sync)
                {
                    if (_proc != null) { try { _proc.Kill(); } catch { } }
                }
            }
            for (int i = 0; i < 40 && Entry.Port > 0 && IsPortOpen(Entry.Port); i++) Thread.Sleep(250);
            lock (_sync) { CloseGenericWritersLocked(); _pid = 0; }
        }

        void DoStop()
        {
            if (Kind == "generic")
            {
                DoStopGeneric();
                return;
            }
            if (Kind == "llama")
            {
                int[] pids = FindLlamaPids();
                for (int i = 0; i < pids.Length; i++) KillTree(pids[i]);
                Process wrapper;
                lock (_sync) { wrapper = _proc; }
                try { if (wrapper != null && !wrapper.HasExited) wrapper.Kill(); } catch { }
                for (int i = 0; i < 40 && IsPortOpen(Cfg.Port); i++) Thread.Sleep(250);
                if (IsPortOpen(Cfg.Port)) throw new Exception("端口 " + Cfg.Port + " 仍在监听");
            }
            else if (Kind == "comfy")
            {
                int pid;
                lock (_sync) pid = _pid;
                if (pid == 0) pid = FindComfyPid(Cfg.Port);
                if (pid > 0) KillTree(pid);
                else { lock (_sync) { if (_proc != null) { try { _proc.Kill(); } catch { } } } }
                for (int i = 0; i < 48 && IsPortOpen(Cfg.Port); i++) Thread.Sleep(250);
                lock (_sync) { CloseComfyWritersLocked(); _pid = 0; }
            }
        }

        void FailStart(string msg)
        {
            lock (_sync)
            {
                _phase = AppPhase.Error;
                _phaseSince = DateTime.Now;
                _error = msg;
                _weStarted = false;
            }
        }

        // ---------------- monitoring ----------------
        public void Poll()
        {
            if (_polling) return;
            _polling = true;
            try { Probe(); RefreshLog(); }
            catch (Exception ex) { try { File.AppendAllText(Path.Combine(Paths.LocalRoot, "error.log"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " poll " + Kind + ": " + ex + "\r\n"); } catch { } }
            finally { _polling = false; }
        }

        public void Probe()
        {
            lock (_sync) if (_operating) return;
            if (Kind == "llama") ProbeLlama();
            else if (Kind == "comfy") ProbeComfy();
            else if (Kind == "generic") ProbeGeneric();
            else ProbeDsh();
        }

        void ProbeGeneric()
        {
            bool healthy = GenericHealthy();
            int pid = 0;
            lock (_sync) if (_proc != null && !_proc.HasExited) pid = _proc.Id;
            if (pid == 0 && Entry.Port > 0) pid = FindComfyPid(Entry.Port);
            DateTime now = DateTime.Now;
            lock (_sync)
            {
                if (healthy)
                {
                    if (_phase != AppPhase.Running) _runningSince = now;
                    _phase = AppPhase.Running;
                    _error = "";
                    _healthFail = 0;
                    _pid = pid;
                    _external = !_weStarted;
                    TryAutoOpen();
                    return;
                }
                if (pid > 0)
                {
                    if (_phase == AppPhase.Starting && (now - _phaseSince).TotalSeconds > 120)
                    {
                        _phase = AppPhase.Error;
                        _error = "启动超时（120s），看日志";
                    }
                    else if (_phase == AppPhase.Running)
                    {
                        _phase = AppPhase.Error;
                        _error = "进程在，但健康检查不通";
                    }
                    else if (_phase == AppPhase.Stopped)
                    {
                        _phase = AppPhase.Starting;
                        _phaseSince = now;
                        _pid = pid;
                        _external = true;
                    }
                    return;
                }
                if (_phase == AppPhase.Starting)
                {
                    if ((now - _phaseSince).TotalSeconds < 10) return;
                    _phase = AppPhase.Error;
                    _error = "启动失败：进程没有起来，看日志";
                    return;
                }
                if (_phase == AppPhase.Running)
                {
                    _phase = AppPhase.Stopped;
                    _pid = 0;
                    _external = false;
                }
            }
        }

        bool GenericHealthy()
        {
            if (Entry.HealthType == "http")
            {
                string url = Entry.HealthUrl;
                if (url.Length == 0 && Entry.Port > 0) url = "http://127.0.0.1:" + Entry.Port + "/";
                return url.Length > 0 && HttpOk(url, 1200);
            }
            if (Entry.HealthType == "tcp")
                return Entry.Port > 0 && IsPortOpen(Entry.Port);
            if (Entry.HealthType == "none")
            {
                lock (_sync) return _proc != null && !_proc.HasExited;
            }
            return false;
        }

        void ProbeLlama()
        {
            int port;
            lock (_sync) port = Cfg.Port;
            bool healthy = HttpOk("http://127.0.0.1:" + port + "/health", 700);
            int[] pids = FindLlamaPids();
            int pid = pids.Length > 0 ? pids[0] : 0;
            string model = "";
            int ctx = 0;
            if (healthy)
            {
                string body = HttpGet("http://127.0.0.1:" + port + "/props", 900);
                if (body.Length > 0)
                {
                    model = JsonString(body, "model_path");
                    ctx = JsonInt(body, "n_ctx");
                }
            }
            DateTime now = DateTime.Now;
            lock (_sync)
            {
                if (healthy)
                {
                    if (_phase != AppPhase.Running)
                    {
                        _runningSince = now;
                    }
                    _phase = AppPhase.Running;
                    _error = "";
                    _healthFail = 0;
                    _pid = pid;
                    _external = !_weStarted;
                    if (model.Length > 0) _liveModel = model;
                    if (ctx > 0) _liveCtx = ctx;
                    TryAutoOpen();
                    return;
                }
                if (pid > 0)
                {
                    if (_phase == AppPhase.Starting)
                    {
                        if ((now - _phaseSince).TotalSeconds > 240) { _phase = AppPhase.Error; _error = "启动超时（240s），看日志"; }
                    }
                    else if (_phase == AppPhase.Running)
                    {
                        _phase = AppPhase.Error;
                        _error = "进程在，但 /health 不通（可能卡住或正在加载）";
                    }
                    else if (_phase == AppPhase.Stopped)
                    {
                        _phase = AppPhase.Starting;
                        _phaseSince = now;
                        _pid = pid;
                        _external = true;
                    }
                    return;
                }
                if (_phase == AppPhase.Starting)
                {
                    if ((now - _phaseSince).TotalSeconds < 15) return;
                    _phase = AppPhase.Error;
                    _error = "启动失败：llama-server 进程没有起来，看日志";
                    return;
                }
                if (_phase == AppPhase.Running)
                {
                    _phase = AppPhase.Error;
                    _error = "llama-server 已退出";
                    _lastRun = now;
                    _runningSince = DateTime.MinValue;
                    return;
                }
                if (IsPortOpen(port))
                {
                    if (_phase != AppPhase.Error) { _phase = AppPhase.Error; _error = "端口 " + port + " 被其他程序占用"; }
                    return;
                }
                if (_phase != AppPhase.Stopped)
                {
                    _lastRun = now;
                    _runningSince = DateTime.MinValue;
                }
                _phase = AppPhase.Stopped;
                _pid = 0;
                _external = false;
            }
        }

        void ProbeComfy()
        {
            int port;
            lock (_sync) port = Cfg.Port;
            string body = "";
            bool healthy = HttpOkBody("http://127.0.0.1:" + port + "/system_stats", 800, out body);
            string dev, ver;
            long vt, vf;
            ParseComfyStats(body, out dev, out ver, out vt, out vf);
            DateTime now = DateTime.Now;
            bool ownAlive = false;
            Process own;
            lock (_sync) own = _proc;
            if (own != null) { try { ownAlive = !own.HasExited; } catch { } }

            int pid = 0;
            lock (_sync) pid = _pid;
            if (healthy)
            {
                bool lookup;
                lock (_sync) { lookup = _pid == 0 && (now - _lastPidLookup).TotalSeconds >= 3; if (lookup) _lastPidLookup = now; }
                if (lookup) pid = FindComfyPid(port);
                lock (_sync)
                {
                    if (_phase != AppPhase.Running) _runningSince = now;
                    _phase = AppPhase.Running;
                    _error = "";
                    _healthFail = 0;
                    _pid = pid;
                    _external = !_weStarted;
                    if (ver.Length > 0) _version = ver;
                    if (dev.Length > 0) _device = dev;
                    if (vt > 0) { _vramTotal = vt; _vramUsed = Math.Max(0, vt - vf); }
                    TryAutoOpen();
                }
                return;
            }

            if (ownAlive)
            {
                lock (_sync)
                {
                    if (_phase == AppPhase.Running)
                    {
                        _healthFail++;
                        if (_healthFail >= 10) _error = "服务无响应（进程仍在），建议重启";
                    }
                    else if (_phase == AppPhase.Starting)
                    {
                        if ((now - _phaseSince).TotalSeconds > 180) { _phase = AppPhase.Error; _error = "启动超时（180s）；看日志"; }
                    }
                    return;
                }
            }

            // no healthy endpoint and own process is gone / never started
            bool scan;
            lock (_sync)
            {
                scan = (now - _lastExternalScan).TotalSeconds >= 10;
                if (scan) _lastExternalScan = now;
            }
            if (scan)
            {
                List<int[]> found = FindComfyProcesses();
                for (int i = 0; i < found.Count; i++)
                {
                    string b = "";
                    if (HttpOkBody("http://127.0.0.1:" + found[i][1] + "/system_stats", 600, out b))
                    {
                        lock (_sync)
                        {
                            if (_phase != AppPhase.Stopped) return;
                            _phase = AppPhase.Running;
                            _phaseSince = now;
                            _runningSince = now;
                            _pid = found[i][0];
                            _external = true;
                            _error = "";
                            TryAutoOpen();
                        }
                        return;
                    }
                }
                if (found.Count > 0)
                {
                    lock (_sync)
                    {
                        if (_phase == AppPhase.Stopped)
                        {
                            _phase = AppPhase.Starting;
                            _phaseSince = now;
                            _pid = found[0][0];
                            _external = true;
                        }
                    }
                    return;
                }
            }

            lock (_sync)
            {
                if (_phase == AppPhase.Starting)
                {
                    string line = LastErrorLine(_logPath);
                    _phase = AppPhase.Error;
                    _error = "启动失败" + (line.Length > 0 ? "：" + line : "（进程已退出）");
                    _lastRun = now;
                    return;
                }
                if (_phase == AppPhase.Running)
                {
                    string line = LastErrorLine(_logPath);
                    _phase = AppPhase.Error;
                    _error = "ComfyUI 已停止" + (line.Length > 0 ? "：" + line : "");
                    _lastRun = now;
                    _runningSince = DateTime.MinValue;
                    _pid = 0;
                    _external = false;
                    return;
                }
                if (IsPortOpen(port)) { if (_phase != AppPhase.Error) { _phase = AppPhase.Error; _error = "端口 " + port + " 被其他程序占用"; } return; }
                if (_phase != AppPhase.Stopped) _lastRun = now;
                _phase = AppPhase.Stopped;
                _pid = 0;
                _external = false;
                _device = "";
                _version = "";
                _vramUsed = 0;
                _vramTotal = 0;
            }
        }

        void ProbeDsh()
        {
            int port;
            lock (_sync) port = Cfg.Port;
            bool up = IsPortOpen(port);
            DateTime now = DateTime.Now;
            lock (_sync)
            {
                if (up)
                {
                    if (_phase != AppPhase.Running) _runningSince = now;
                    _phase = AppPhase.Running;
                    _error = "";
                    _external = true;
                    TryAutoOpen();
                    return;
                }
                if (_phase == AppPhase.Starting)
                {
                    if ((now - _phaseSince).TotalSeconds < 60) return;
                    _phase = AppPhase.Error;
                    _error = "启动超时：60s 内 3080 未监听，看 dsh-launch.log";
                    return;
                }
                if (_phase == AppPhase.Running)
                {
                    _phase = AppPhase.Error;
                    _error = "DSH 未监听 3080（Guardian 可能正在拉起）";
                    _lastRun = now;
                    _runningSince = DateTime.MinValue;
                    return;
                }
                _phase = AppPhase.Stopped;
                _pid = 0;
                _external = false;
            }
        }

        void TryAutoOpen()
        {
            bool pending;
            bool auto;
            lock (_sync) { pending = _autoOpenPending; auto = Cfg.AutoOpenBrowser; _autoOpenPending = false; }
            if (!pending || !auto) return;
            // never launch a browser while holding the state lock: Process.Start can block for seconds
            ThreadPool.QueueUserWorkItem(delegate { try { OpenWeb(); } catch { } });
        }

        // ---------------- log ----------------
        public void RefreshLog()
        {
            DateTime now = DateTime.Now;
            if ((now - _lastLogRead).TotalMilliseconds < 900) return;
            _lastLogRead = now;
            string path = _logPath;
            if (Kind == "dsh" && !File.Exists(path)) path = Paths.DshOutLog;
            string[] lines = TailLines(path, 140);
            List<LogLine> parsed = ParseLog(Kind, lines);
            lock (_sync) _logLines = parsed;
        }

        // ---------------- view sync ----------------
        public void SyncEntry()
        {
            lock (_sync)
            {
                AppEntry e = Entry;
                e.Port = Cfg.Port;
                e.Ip = "127.0.0.1";
                e.Url = Kind == "dsh" ? "http://127.0.0.1:" + Cfg.Port + "/" : "http://127.0.0.1:" + Cfg.Port + "/";
                e.State = StateKey(_phase);
                e.Pid = _pid;
                e.External = _external;
                e.Error = _error;
                e.Uptime = FormatUptime(_runningSince);
                e.LastRun = Relative(_lastRun);
                e.AutoOpenBrowser = Cfg.AutoOpenBrowser;
                e.Ctx = Cfg.Ctx;
                e.ModelDisplay = DisplayModel(Cfg.ModelPath);
                e.MmprojDisplay = Cfg.MmprojPath.Length > 0 ? Path.GetFileName(Cfg.MmprojPath) : "未启用";
                e.ExtraArgs = Cfg.ExtraArgs;
                e.EnvSummary = Cfg.EnvLines.Count + " 项";
                e.InstallPath = Cfg.InstallPath;
                e.PythonPath = Cfg.PythonPath;
                e.ConfigPath = Cfg.FilePath;
                e.LogPath = _logPath;
                e.Version = _version;
                e.Device = _device;
                e.StatusLine = BuildStatusLine();
                e.ModelLine = BuildModelLine();
                if (Kind == "llama") e.ModelDisplay = DisplayModel(_liveModel.Length > 0 && _phase == AppPhase.Running ? _liveModel : Cfg.ModelPath);
                e.HomeRows = BuildHomeRows();
                if (_logLines.Count > 0) e.Log = new List<LogLine>(_logLines);
                else e.Log = PlaceholderLog();
            }
        }

        string BuildStatusLine()
        {
            if (_phase == AppPhase.Starting)
                return "正在启动 · 已等待 " + (int)(DateTime.Now - _phaseSince).TotalSeconds + "s" + (_external ? " · 外部进程" : "");
            if (_phase == AppPhase.Stopping) return "正在停止…";
            if (_phase == AppPhase.Error) return _error.Length > 0 ? _error : "异常";
            if (_phase == AppPhase.Running)
            {
                string s = (_pid > 0 ? "PID " + _pid + " · " : "") + "端口 " + Cfg.Port + " · 已运行 " + FormatUptime(_runningSince);
                if (_external) s += " · 外部启动";
                if (Kind == "dsh") s = "端口 " + Cfg.Port + " · 由 DSH-Guardian 守护";
                return s;
            }
            string r = "端口 " + Cfg.Port;
            if (_lastRun != DateTime.MinValue) r += " · 上次运行 " + Relative(_lastRun);
            return r;
        }

        string BuildModelLine()
        {
            if (Kind == "llama")
            {
                string path = _phase == AppPhase.Running && _liveModel.Length > 0 ? _liveModel : Cfg.ModelPath;
                string s = FriendlyModel(path);
                int ctx = _phase == AppPhase.Running && _liveCtx > 0 ? _liveCtx : Cfg.Ctx;
                if (ctx > 0) s += " · ctx " + ctx;
                return s;
            }
            if (Kind == "comfy")
            {
                string s = "ComfyUI " + (_version.Length > 0 ? _version : "未检测");
                if (_device.Length > 0) s += " · " + _device;
                if (_vramTotal > 0) s += " · 显存 " + (_vramUsed / 1073741824.0).ToString("0.0") + "/" + (_vramTotal / 1073741824.0).ToString("0.0") + " GB";
                if (_phase != AppPhase.Running) s = "ComfyUI · 未运行";
                return s;
            }
            string remote = Entry.RemoteHost != null ? Entry.RemoteHost : "";
            return remote.Length > 0 ? "远程入口 · " + remote + "（tailnet 内可用）" : "远程入口 · 未配置";
        }

        string[] BuildHomeRows()
        {
            if (Kind == "llama")
                return new string[] { "模型|" + ShortModel(Cfg.ModelPath), "端口|" + Cfg.Port, "状态|" + StateText(_phase) };
            if (Kind == "comfy")
                return new string[] { "版本|" + (_version.Length > 0 ? _version : "未检测"), "端口|" + Cfg.Port, "状态|" + StateText(_phase) };
            string remote = Entry.RemoteHost != null && Entry.RemoteHost.Length > 0 ? "已配置" : "未配置";
            return new string[] { "端口|" + Cfg.Port, "远程|" + remote, "状态|" + StateText(_phase) };
        }

        List<LogLine> PlaceholderLog()
        {
            List<LogLine> l = new List<LogLine>();
            if (_phase == AppPhase.Error) l.Add(new LogLine("[启动器] " + (_error.Length > 0 ? _error : "发生异常"), LogKind.Error));
            else if (_phase == AppPhase.Starting) l.Add(new LogLine("[启动器] 正在启动，等待健康检查…", LogKind.Info));
            else if (_phase == AppPhase.Stopping) l.Add(new LogLine("[启动器] 正在停止…", LogKind.Info));
            else l.Add(new LogLine("[启动器] 服务已停止。点左下角按钮启动。", LogKind.Info));
            return l;
        }

        static string StateKey(AppPhase p)
        {
            switch (p)
            {
                case AppPhase.Starting: return "starting";
                case AppPhase.Running: return "running";
                case AppPhase.Stopping: return "stopping";
                case AppPhase.Error: return "error";
                default: return "stopped";
            }
        }

        static string StateText(AppPhase p)
        {
            switch (p)
            {
                case AppPhase.Starting: return "启动中";
                case AppPhase.Running: return "运行中";
                case AppPhase.Stopping: return "停止中";
                case AppPhase.Error: return "异常";
                default: return "已停止";
            }
        }

        static string FriendlyModel(string path)
        {
            string n = DisplayModel(path);
            // Keep provider prefixes intact; labels come from the model file.
            if (n.StartsWith("bartowski_", StringComparison.OrdinalIgnoreCase)) n = n.Substring(10);
            n = n.Replace('_', ' ');
            return n;
        }

        static string DisplayModel(string path)
        {
            if (string.IsNullOrEmpty(path)) return "未选择";
            try { return Path.GetFileNameWithoutExtension(path); }
            catch { return path; }
        }

        static string ShortModel(string path)
        {
            string s = DisplayModel(path);
            if (s.Length > 22) s = s.Substring(0, 21) + "…";
            return s;
        }

        static string FormatUptime(DateTime since)
        {
            if (since == DateTime.MinValue) return "00:00";
            TimeSpan d = DateTime.Now - since;
            if (d.TotalHours >= 1) return (int)d.TotalHours + " 小时 " + d.Minutes + " 分";
            return ((int)d.TotalMinutes).ToString("00") + ":" + d.Seconds.ToString("00");
        }

        static string Relative(DateTime t)
        {
            if (t == DateTime.MinValue) return "未知";
            TimeSpan d = DateTime.Now - t;
            if (d.TotalSeconds < 90) return "刚刚";
            if (d.TotalMinutes < 60) return (int)d.TotalMinutes + " 分钟前";
            if (d.TotalHours < 24) return (int)d.TotalHours + " 小时前";
            return (int)d.TotalDays + " 天前";
        }

        // ---------------- config updates ----------------
        public void SetAutoOpen(bool v) { Cfg.AutoOpenBrowser = v; Cfg.Save(); }
        public void SetPort(int v) { Cfg.Port = v; Cfg.Save(); }
        public void SetCtx(int v) { Cfg.Ctx = v; Cfg.Save(); }
        public void SetModel(string path)
        {
            Cfg.ModelPath = path;
            Cfg.Save();
            bool running = IsRunning;
            if (running) RestartAsync();
        }
        public void SetMmproj(string path) { Cfg.MmprojPath = path; Cfg.Save(); }
        public void SaveConfig() { Cfg.Save(); }

        public void OpenConfig()
        {
            try { Process.Start("notepad.exe", "\"" + Cfg.FilePath + "\""); } catch { }
        }

        public void OpenLog()
        {
            try
            {
                if (File.Exists(_logPath)) Process.Start("notepad.exe", "\"" + _logPath + "\"");
                else Process.Start("explorer.exe", "\"" + Path.GetDirectoryName(_logPath) + "\"");
            }
            catch { }
        }

        public bool OpenWeb()
        {
            try
            {
                if (Kind == "dsh")
                {
                    if (!IsPortOpen(Cfg.Port))
                    {
                        Process.Start("wscript.exe", "\"" + Paths.DshVbs + "\"");
                        return true;
                    }
                    Process.Start("http://127.0.0.1:" + Cfg.Port + "/");
                    return true;
                }
                if (!IsRunning) return false;
                Process.Start("http://127.0.0.1:" + Cfg.Port + "/");
                return true;
            }
            catch { return false; }
        }

        public void OpenFolder(string label)
        {
            string path = "";
            if (Kind == "llama")
            {
                if (label == "模型目录") path = Paths.LlamaModels;
                else if (label == "llama.cpp") path = Paths.LlamaRoot;
                else if (label == "日志") { OpenLog(); return; }
            }
            else if (Kind == "comfy")
            {
                if (label == "输出") path = Path.Combine(Cfg.InstallPath, "output");
                else if (label == "模型") path = Path.Combine(Cfg.InstallPath, "models");
                else if (label == "checkpoints") path = Path.Combine(Cfg.InstallPath, "models", "checkpoints");
                else if (label == "LoRA") path = Path.Combine(Cfg.InstallPath, "models", "loras");
                else if (label == "日志") { OpenLog(); return; }
            }
            else
            {
                if (label == "服务目录") path = Paths.DshDir;
                else if (label == "日志") { OpenLog(); return; }
                else if (label == "配置") { OpenConfig(); return; }
            }
            try
            {
                if (Directory.Exists(path)) Process.Start("explorer.exe", "\"" + path + "\"");
                else if (File.Exists(path)) Process.Start("notepad.exe", "\"" + path + "\"");
            }
            catch { }
        }

        // ---------------- helpers ----------------
        static string PowerShellPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe");
        }

        void OpenComfyWriters()
        {
            lock (_sync)
            {
                CloseComfyWritersLocked();
                try
                {
                    Directory.CreateDirectory(Cfg.InstallPath);
                    _outWriter = new StreamWriter(Path.Combine(Cfg.InstallPath, "comfyui.out.log"), false, Encoding.UTF8);
                    _errWriter = new StreamWriter(Path.Combine(Cfg.InstallPath, "comfyui.err.log"), false, Encoding.UTF8);
                    _outWriter.AutoFlush = true;
                    _errWriter.AutoFlush = true;
                }
                catch { }
            }
        }

        void CloseComfyWritersLocked()
        {
            try { if (_outWriter != null) { _outWriter.Dispose(); _outWriter = null; } } catch { }
            try { if (_errWriter != null) { _errWriter.Dispose(); _errWriter = null; } } catch { }
        }

        void AppendComfy(string line, bool err)
        {
            lock (_sync)
            {
                try
                {
                    StreamWriter w = err ? _errWriter : _outWriter;
                    if (w != null) w.WriteLine(line);
                }
                catch { }
            }
        }

        static bool HttpOk(string url, int ms)
        {
            string b;
            return HttpOkBody(url, ms, out b);
        }

        static bool HttpOkBody(string url, int ms, out string body)
        {
            body = "";
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Proxy = null;
                req.Timeout = ms;
                req.ReadWriteTimeout = ms;
                req.KeepAlive = false;
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    if (resp.StatusCode != HttpStatusCode.OK) return false;
                    using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                        body = sr.ReadToEnd();
                    return true;
                }
            }
            catch { return false; }
        }

        static string HttpGet(string url, int ms)
        {
            string b;
            HttpOkBody(url, ms, out b);
            return b;
        }

        static bool IsPortOpen(int port)
        {
            try
            {
                using (TcpClient c = new TcpClient())
                {
                    IAsyncResult ar = c.BeginConnect("127.0.0.1", port, null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(250)) return false;
                    c.EndConnect(ar);
                    return true;
                }
            }
            catch { return false; }
        }

        static void KillTree(int pid)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("taskkill", "/PID " + pid.ToString() + " /T /F");
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                using (Process p = Process.Start(psi)) p.WaitForExit(6000);
            }
            catch { }
        }

        static int[] FindLlamaPids()
        {
            try
            {
                Process[] ps = Process.GetProcessesByName("llama-server");
                List<int> ids = new List<int>();
                for (int i = 0; i < ps.Length; i++) { try { ids.Add(ps[i].Id); } catch { } try { ps[i].Dispose(); } catch { } }
                return ids.ToArray();
            }
            catch { return new int[0]; }
        }

        public static List<int[]> FindComfyProcesses()
        {
            List<int[]> list = new List<int[]>();
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "SELECT ProcessId,CommandLine FROM Win32_Process WHERE Name='python.exe'"))
                {
                    using (ManagementObjectCollection col = searcher.Get())
                    {
                        foreach (ManagementObject mo in col)
                        {
                            try
                            {
                                object cmdObj = mo["CommandLine"];
                                if (cmdObj == null) continue;
                                string cmd = cmdObj.ToString();
                                if (cmd.IndexOf("main.py", StringComparison.OrdinalIgnoreCase) < 0) continue;
                                if (cmd.IndexOf("ComfyUI", StringComparison.OrdinalIgnoreCase) < 0) continue;
                                int pp = ParsePort(cmd);
                                int id = Convert.ToInt32(mo["ProcessId"]);
                                list.Add(new int[] { id, pp });
                            }
                            catch { }
                            finally { try { mo.Dispose(); } catch { } }
                        }
                    }
                }
            }
            catch { }
            return list;
        }

        static Regex _rexPort = new Regex("--port(?:\\s+|=)(\\d+)", RegexOptions.Compiled);
        static int ParsePort(string cmd)
        {
            Match m = _rexPort.Match(cmd);
            if (m.Success) { int p; if (int.TryParse(m.Groups[1].Value, out p)) return p; }
            return 8188;
        }

        static int FindComfyPid(int port)
        {
            List<int[]> found = FindComfyProcesses();
            for (int i = 0; i < found.Count; i++) if (found[i][1] == port) return found[i][0];
            if (found.Count > 0) return found[0][0];
            return 0;
        }

        static void ParseComfyStats(string body, out string device, out string version, out long vramTotal, out long vramFree)
        {
            device = ""; version = ""; vramTotal = 0; vramFree = 0;
            if (string.IsNullOrEmpty(body)) return;
            Match v = Regex.Match(body, "\"comfyui_version\"\\s*:\\s*\"([^\"]+)\"");
            if (v.Success) version = v.Groups[1].Value;
            int di = body.IndexOf("\"devices\"", StringComparison.Ordinal);
            if (di >= 0)
            {
                string seg = body.Substring(di);
                Match n = Regex.Match(seg, "\"name\"\\s*:\\s*\"([^\"]+)\"");
                if (n.Success)
                {
                    device = n.Groups[1].Value;
                    device = Regex.Replace(device, "^cuda:\\d+\\s*", "");
                    device = Regex.Replace(device, "\\s*:\\s*native\\s*$", "");
                }
                Match t = Regex.Match(seg, "\"vram_total\"\\s*:\\s*(\\d+)");
                if (t.Success) long.TryParse(t.Groups[1].Value, out vramTotal);
                Match f = Regex.Match(seg, "\"vram_free\"\\s*:\\s*(\\d+)");
                if (f.Success) long.TryParse(f.Groups[1].Value, out vramFree);
            }
        }

        static string JsonString(string json, string key)
        {
            Match m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            if (!m.Success) return "";
            return m.Groups[1].Value.Replace("\\\\", "\\").Replace("\\\"", "\"").Replace("\\/", "/");
        }

        static int JsonInt(string json, string key)
        {
            Match m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*(\\d+)");
            int n;
            if (m.Success && int.TryParse(m.Groups[1].Value, out n)) return n;
            return 0;
        }

        static string LastErrorLine(string path)
        {
            string[] lines = TailLines(path, 80);
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                string s = lines[i];
                if (s.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    s.IndexOf("错误", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    s.IndexOf("Traceback", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    s.IndexOf("Exception", StringComparison.OrdinalIgnoreCase) >= 0)
                    return s.Trim();
            }
            return "";
        }

        static string[] TailLines(string path, int max)
        {
            try
            {
                if (!File.Exists(path)) return new string[0];
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    long len = fs.Length;
                    int take = (int)Math.Min(len, 65536);
                    fs.Seek(-take, SeekOrigin.End);
                    byte[] buf = new byte[take];
                    int read = fs.Read(buf, 0, take);
                    string all = Encoding.UTF8.GetString(buf, 0, read);
                    string[] lines = all.Replace("\r\n", "\n").Split('\n');
                    int start = Math.Max(0, lines.Length - max);
                    List<string> outl = new List<string>();
                    for (int i = start; i < lines.Length; i++) if (lines[i].Length > 0) outl.Add(lines[i]);
                    return outl.ToArray();
                }
            }
            catch { return new string[0]; }
        }

        static List<LogLine> ParseLog(string kind, string[] lines)
        {
            List<LogLine> list = new List<LogLine>();
            for (int i = 0; i < lines.Length; i++)
            {
                string s = Regex.Replace(lines[i], "\\x1B\\[[0-9;?]*[A-Za-z]", "");
                s = s.Replace("\0", "");
                // Never expose launch tokens from child-process logs.
                s = Regex.Replace(s, @"([?&]token=)[^&\s]+", "$1***", RegexOptions.IgnoreCase);
                if (s.Length > 600) s = s.Substring(0, 600);
                if (s.Length == 0) continue;
                if (kind == "llama")
                {
                    Match m = Regex.Match(s, "^(\\S+)\\s+([IWED])\\s+(.*)$");
                    if (m.Success)
                    {
                        LogKind k = LogKind.Normal;
                        string lv = m.Groups[2].Value;
                        if (lv == "W") k = LogKind.Warn;
                        else if (lv == "E") k = LogKind.Error;
                        else if (lv == "I") k = LogKind.Info;
                        else k = LogKind.Dim;
                        list.Add(new LogLine(m.Groups[1].Value, lv, m.Groups[3].Value, k));
                        continue;
                    }
                }
                else if (kind == "comfy")
                {
                    Match m = Regex.Match(s, "^\\[(INFO|WARNING|ERROR|DEBUG|WARN)\\]\\s*(.*)$");
                    if (m.Success)
                    {
                        string lv = m.Groups[1].Value;
                        LogKind k = lv == "ERROR" ? LogKind.Error : (lv.StartsWith("WARN") ? LogKind.Warn : (lv == "DEBUG" ? LogKind.Dim : LogKind.Info));
                        string tag = lv == "WARNING" ? "W" : (lv == "ERROR" ? "E" : (lv == "DEBUG" ? "D" : "I"));
                        list.Add(new LogLine("", tag, m.Groups[2].Value, k));
                        continue;
                    }
                }
                else
                {
                    Match m = Regex.Match(s, "^\\[(\\d{4}-\\d{2}-\\d{2} \\d{2}:\\d{2}:\\d{2})\\]\\s*(.*)$");
                    if (m.Success)
                    {
                        list.Add(new LogLine(m.Groups[1].Value, "", m.Groups[2].Value, LogKind.Normal));
                        continue;
                    }
                }
                LogKind kk = LogKind.Normal;
                if (s.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("失败", StringComparison.OrdinalIgnoreCase) >= 0) kk = LogKind.Error;
                else if (s.IndexOf("warn", StringComparison.OrdinalIgnoreCase) >= 0) kk = LogKind.Warn;
                list.Add(new LogLine(s, kk));
            }
            return list;
        }
    }

    // ---------------- controller ----------------
    sealed class HubController : IDisposable
    {
        public readonly List<AppEntry> Entries = new List<AppEntry>();
        public readonly List<AppRuntime> Runtimes = new List<AppRuntime>();
        Thread _monitor;
        volatile bool _exit;

        public HubController(bool startMonitor)
        {
            List<ManifestApp> manifests = ManifestApp.LoadAll();
            if (manifests.Count > 0)
            {
                for (int i = 0; i < manifests.Count; i++)
                {
                    ManifestApp m = manifests[i];
                    AppConfig cfg = AppConfig.Load(m.id);
                    if (m.port > 0) cfg.Port = m.port;
                    AppEntry entry = Catalog.FromManifest(m, cfg);
                    AppRuntime rt = new AppRuntime(entry.Kind, cfg, entry, m);
                    Entries.Add(entry);
                    Runtimes.Add(rt);
                }
            }
            else
            {
                Add("llama", Catalog.Llama);
                Add("comfy", Catalog.Comfy);
                Add("dsh", Catalog.Dsh);
            }
            Data.Apps = Entries;
            if (startMonitor)
            {
                _monitor = new Thread(MonitorLoop);
                _monitor.IsBackground = true;
                _monitor.Name = "HubMonitor";
                _monitor.Start();
            }
            SyncAll();
        }

        void Add(string id, Func<AppConfig, AppEntry> factory)
        {
            AppConfig cfg = AppConfig.Load(id);
            AppEntry entry = factory(cfg);
            AppRuntime rt = new AppRuntime(id, cfg, entry);
            Entries.Add(entry);
            Runtimes.Add(rt);
        }

        public AppRuntime RuntimeById(string id)
        {
            for (int i = 0; i < Runtimes.Count; i++)
                if (Runtimes[i].Entry.Id == id) return Runtimes[i];
            return null;
        }

        public AppRuntime RuntimeByPage(int page)
        {
            if (page < 0 || page >= Runtimes.Count) return null;
            return Runtimes[page];
        }

        public void SyncAll()
        {
            for (int i = 0; i < Runtimes.Count; i++) Runtimes[i].SyncEntry();
        }

        void MonitorLoop()
        {
            while (!_exit)
            {
                for (int i = 0; i < Runtimes.Count; i++)
                {
                    if (_exit) return;
                    AppRuntime rt = Runtimes[i];
                    ThreadPool.QueueUserWorkItem(delegate { rt.Poll(); });
                }
                for (int i = 0; i < 10; i++)
                {
                    if (_exit) return;
                    Thread.Sleep(100);
                }
                try { SyncAll(); } catch { }
            }
        }

        public void Dispose()
        {
            _exit = true;
        }
    }
}
