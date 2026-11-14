// CampusNet 核心引擎（校园网 Dr.COM 门户自动登录）
// 编译：csc /target:library ... 或直接编入两个 exe，见 build.ps1
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32;

namespace CampusNet
{
    /// <summary>配置项</summary>
    public class NetConfig
    {
        public string Account = "";
        public string Password = "";
        public string Isp = "";                 // "" / "@cmcc" / "@unicom" / "@telecom"
        public bool AutoStart = false;
        public int DelaySeconds = 6;
        public int MaxRetry = 10;
        public int RetryIntervalSeconds = 5;
        public bool SkipIfOnline = true;
        public string Server = "192.168.1.1";
        public string Method = "auto";          // auto / local / portal
        public string SelfUrl = "http://192.168.1.1:8080/Self/dashboard";   // 062 时打开的自助服务平台
        public int NotifyRetryCount = 2;        // 弹窗提醒后重新登录的尝试次数
        public int NotifyRetryInterval = 5;     // 每次提醒后重试的间隔秒
        public string Mac = "";                 // 手动指定本机 MAC，留空=自动识别
        public string AcIp = "";                // wlanacip（AC 地址），留空=null
        public string AcName = "";              // wlanacname（AC 名称），留空=null
        public bool WakeReconnect = false;      // 睡眠唤醒后自动重连（计划任务）

        public string AccountWithIsp
        {
            get
            {
                string a = (Account ?? "").Trim();
                if (a.Length == 0) return a;
                if (a.IndexOf('@') >= 0) return a;      // 账号里已带后缀，不再追加
                return a + (Isp ?? "");
            }
        }
    }

    /// <summary>程序所在目录等环境信息（全部使用相对定位，文件夹可随意移动）</summary>
    public static class AppEnv
    {
        public static string Dir
        {
            get
            {
                string d = AppDomain.CurrentDomain.BaseDirectory;
                if (string.IsNullOrEmpty(d)) d = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                return d.TrimEnd('\\');
            }
        }
        public static string ConfigPath { get { return Path.Combine(Dir, "config.ini"); } }
        public static string ExampleConfigPath { get { return Path.Combine(Dir, "config.example.ini"); } }
        public static string LogPath { get { return Path.Combine(Dir, "login.log"); } }
        public static string LastResponsePath { get { return Path.Combine(Dir, "last_response.html"); } }
        public const string LoginExeName = "校园网登录.exe";
        public static string LoginExePath { get { return Path.Combine(Dir, LoginExeName); } }
    }

    /// <summary>config.ini 读写（UTF-8，键名中文，可手工编辑）</summary>
    public static class ConfigStore
    {
        public static NetConfig Load()
        {
            NetConfig c = new NetConfig();
            string cfgPath = AppEnv.ConfigPath;
            if (!File.Exists(cfgPath)) cfgPath = AppEnv.ExampleConfigPath;
            Dictionary<string, string> d = ReadIni(cfgPath);
            c.Account = Get(d, "账号", c.Account).Trim();
            c.Password = DecodePassword(Get(d, "密码", ""));
            c.Isp = NormalizeIsp(Get(d, "运营商", ""));
            c.AutoStart = ParseBool(Get(d, "开机自启", "0"), false);
            c.DelaySeconds = Clamp(ParseInt(Get(d, "开机延时秒", "6"), 6), 0, 3600);
            c.MaxRetry = Clamp(ParseInt(Get(d, "重试次数", "10"), 10), 1, 1000);
            c.RetryIntervalSeconds = Clamp(ParseInt(Get(d, "重试间隔秒", "5"), 5), 1, 3600);
            c.SkipIfOnline = ParseBool(Get(d, "已在线则跳过", "1"), true);
            string s = Get(d, "认证服务器", c.Server).Trim();
            if (s.Length > 0) c.Server = s;
            string m = Get(d, "认证方式", "auto").Trim().ToLowerInvariant();
            if (m != "auto" && m != "local" && m != "portal") m = "auto";
            c.Method = m;
            string su = Get(d, "自助服务", "").Trim();
            if (su.Length > 0) c.SelfUrl = su;
            c.NotifyRetryCount = Clamp(ParseInt(Get(d, "提醒后重试次数", "2"), 2), 0, 20);
            c.NotifyRetryInterval = Clamp(ParseInt(Get(d, "提醒后重试间隔秒", "5"), 5), 1, 600);
            c.Mac = Get(d, "本机MAC", "").Trim();
            c.AcIp = Get(d, "AC地址", "").Trim();
            c.AcName = Get(d, "AC名称", "").Trim();
            c.WakeReconnect = ParseBool(Get(d, "唤醒重连", "0"), false);
            return c;
        }

        public static void Save(NetConfig c)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# 校园网自动登录 配置文件（可用记事本修改，也可用「校园网配置.exe」图形化修改）");
            sb.AppendLine("# 密码使用 b64: 前缀做了简单编码，防止旁人一眼看到；直接写明文也可以被识别。");
            sb.AppendLine("账号=" + c.Account.Trim());
            sb.AppendLine("密码=" + EncodePassword(c.Password));
            sb.AppendLine("运营商=" + (c.Isp ?? ""));
            sb.AppendLine("开机自启=" + (c.AutoStart ? "1" : "0"));
            sb.AppendLine("开机延时秒=" + c.DelaySeconds.ToString());
            sb.AppendLine("重试次数=" + c.MaxRetry.ToString());
            sb.AppendLine("重试间隔秒=" + c.RetryIntervalSeconds.ToString());
            sb.AppendLine("已在线则跳过=" + (c.SkipIfOnline ? "1" : "0"));
            sb.AppendLine("认证服务器=" + c.Server);
            sb.AppendLine("认证方式=" + c.Method);
            sb.AppendLine("自助服务=" + c.SelfUrl);
            sb.AppendLine("提醒后重试次数=" + c.NotifyRetryCount.ToString());
            sb.AppendLine("提醒后重试间隔秒=" + c.NotifyRetryInterval.ToString());
            sb.AppendLine("本机MAC=" + (c.Mac ?? ""));
            sb.AppendLine("AC地址=" + (c.AcIp ?? ""));
            sb.AppendLine("AC名称=" + (c.AcName ?? ""));
            sb.AppendLine("唤醒重连=" + (c.WakeReconnect ? "1" : "0"));
            File.WriteAllText(AppEnv.ConfigPath, sb.ToString(), new UTF8Encoding(true));
        }

        public static string NormalizeIsp(string s)
        {
            s = (s ?? "").Trim();
            if (s.Length == 0 || s == "-1") return "";
            if (s.IndexOf('@') == 0) return s.ToLowerInvariant();
            string t = s.ToLowerInvariant();
            if (t == "cmcc" || t.Contains("移动")) return "@cmcc";
            if (t == "unicom" || t.Contains("联通")) return "@unicom";
            if (t == "telecom" || t.Contains("电信")) return "@telecom";
            return s;
        }

        static Dictionary<string, string> ReadIni(string path)
        {
            Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!File.Exists(path)) return d;
                foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";") || line.StartsWith("[")) continue;
                    int i = line.IndexOf('=');
                    if (i <= 0) continue;
                    string k = line.Substring(0, i).Trim();
                    string v = line.Substring(i + 1).Trim();
                    if (v.Length >= 2 && v.StartsWith("\"") && v.EndsWith("\"")) v = v.Substring(1, v.Length - 2);
                    d[k] = v;
                }
            }
            catch { }
            return d;
        }

        static string Get(Dictionary<string, string> d, string key, string def)
        {
            string v;
            if (d.TryGetValue(key, out v)) return v;
            return def;
        }

        public static string EncodePassword(string pwd)
        {
            if (pwd == null) pwd = "";
            if (pwd.Length == 0) return "";
            return "b64:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(pwd));
        }

        public static string DecodePassword(string v)
        {
            if (v == null) return "";
            v = v.Trim();
            if (v.StartsWith("b64:", StringComparison.OrdinalIgnoreCase))
            {
                try { return Encoding.UTF8.GetString(Convert.FromBase64String(v.Substring(4))); }
                catch { return ""; }
            }
            return v;
        }

        static bool ParseBool(string v, bool def)
        {
            if (v == null) return def;
            v = v.Trim().ToLowerInvariant();
            if (v == "1" || v == "true" || v == "yes" || v == "on" || v == "是" || v == "开") return true;
            if (v == "0" || v == "false" || v == "no" || v == "off" || v == "否" || v == "关") return false;
            return def;
        }

        static int ParseInt(string v, int def)
        {
            int n;
            if (int.TryParse((v ?? "").Trim(), out n)) return n;
            return def;
        }

        static int Clamp(int v, int lo, int hi)
        {
            if (v < lo) return lo;
            if (v > hi) return hi;
            return v;
        }
    }

    /// <summary>登录结果</summary>
    public class LoginOutcome
    {
        public bool Success = false;
        public bool AlreadyOnline = false;
        public string Message = "";
        public bool AuthError = false;      // 被认证系统明确拒绝（密码错/欠费等），不能拿"能否上网"兜底
        public string ErrorCode = "";       // 认证系统返回的错误码，例如 "062"（同时在线设备数超限）
        public bool Fatal = false;          // 不必再重试（例如 062 已弹窗提示用户）
        public bool TransportError = false; // 连不上/端点不存在（区别于"AC 明确拒绝"）

        public static LoginOutcome Ok(string msg) { LoginOutcome o = new LoginOutcome(); o.Success = true; o.Message = msg; return o; }
        public static LoginOutcome Online(string msg) { LoginOutcome o = new LoginOutcome(); o.Success = true; o.AlreadyOnline = true; o.Message = msg; return o; }
        public static LoginOutcome Fail(string msg) { LoginOutcome o = new LoginOutcome(); o.Success = false; o.Message = msg; return o; }
    }

    /// <summary>开机自启（HKCU Run，无需管理员）</summary>
    public static class AutoStart
    {
        const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public const string ValueName = "CampusNetAutoLogin";

        public static string CommandLine { get { return "\"" + AppEnv.LoginExePath + "\" --boot"; } }

        /// <summary>读取当前注册表里的自启命令行，没有则返回 null</summary>
        public static string ReadValue()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (k == null) return null;
                    return k.GetValue(ValueName) as string;
                }
            }
            catch { return null; }
        }

        public static bool IsEnabled() { return ReadValue() != null; }

        /// <summary>自启项指向的路径已经不是当前文件夹（文件夹被移动过）</summary>
        public static bool IsStale()
        {
            string cur = ReadValue();
            if (cur == null) return false;
            return !string.Equals(cur, CommandLine, StringComparison.OrdinalIgnoreCase);
        }

        public static void Set(bool on)
        {
            try
            {
                if (!on && ReadValue() == null) return;      // 本来就没开，不必碰注册表
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (k == null) return;
                    if (on) k.SetValue(ValueName, CommandLine, RegistryValueKind.String);
                    else if (k.GetValue(ValueName) != null) k.DeleteValue(ValueName, false);
                }
            }
            catch (Exception ex) { Engine.Log("设置开机自启失败：" + ex.Message); }
        }

        /// <summary>让注册表里的路径与当前文件夹保持一致（移动文件夹后自动修复）</summary>
        public static void Sync(bool enabled)
        {
            try
            {
                string cur = ReadValue();
                if (!enabled)
                {
                    if (cur != null) Set(false);
                    return;
                }
                if (!string.Equals(cur, CommandLine, StringComparison.OrdinalIgnoreCase))
                {
                    Set(true);
                    if (cur != null) Engine.Log("已更新开机自启路径为：" + CommandLine);
                }
            }
            catch (Exception ex) { Engine.Log("同步开机自启失败：" + ex.Message); }
        }
    }

    /// <summary>
    /// 用"计划任务"保证自动重连：两个事件触发任务，都不需要管理员权限。
    ///   1) CampusNetAutoLogin_Wake  —— 睡眠/休眠唤醒（传统 S3 107、新型待机 507、通用恢复 1）
    ///   2) CampusNetAutoLogin_Logon —— 用户登录（Winlogon 7001），作为 Run 键失效时的兜底
    /// 用 schtasks.exe 注册；同一时刻只会有一个实例真正干活（程序内有单实例互斥 + 已在线则跳过）。
    /// </summary>
    public static class AutoTasks
    {
        public const string WakeName = "CampusNetAutoLogin_Wake";
        public const string LogonName = "CampusNetAutoLogin_Logon";

        /// <summary>唤醒：传统睡眠 107 / 新型待机(S0ix) 507 / 通用低功耗恢复 1</summary>
        public const string WakeFilter =
            "*[System[(Provider[@Name='Microsoft-Windows-Kernel-Power'] and (EventID=107 or EventID=507))" +
            " or (Provider[@Name='Microsoft-Windows-Power-Troubleshooter'] and EventID=1)]]";

        /// <summary>用户登录：Winlogon 7001</summary>
        public const string LogonFilter =
            "*[System[Provider[@Name='Microsoft-Windows-Winlogon'] and EventID=7001]]";

        public static bool WakeExists() { return Exists(WakeName); }
        public static bool LogonExists() { return Exists(LogonName); }

        /// <summary>两个任务是否都齐全</summary>
        public static bool AllExist() { return WakeExists() && LogonExists(); }

        public static bool Exists(string name)
        {
            return Run("/Query /TN \"" + name + "\"", true) == 0;
        }

        public static bool CreateWake(string exePath) { return Create(WakeName, WakeFilter, exePath); }
        public static bool CreateLogon(string exePath) { return Create(LogonName, LogonFilter, exePath); }

        static bool Create(string name, string filter, string exePath)
        {
            // 动作只写 exe、不带参数：无参数运行 = 立即连接（自带重试），唤醒/登录后正合适；
            // 同时避开 schtasks 对带空格的 /TR 命令行的解析差异。
            string args = "/Create /TN \"" + name + "\" /TR \"" + exePath + "\""
                        + " /SC ONEVENT /EC System /MO \"" + filter + "\" /F /RL LIMITED";
            if (Run(args, false) == 0) { Engine.Log("已创建自动任务：" + name); return true; }
            Engine.Log("创建自动任务失败（可能被安全软件拦截）：" + name);
            return false;
        }

        public static bool Delete(string name)
        {
            bool ok = Run("/Delete /TN \"" + name + "\" /F", false) == 0;
            if (ok) Engine.Log("已删除自动任务：" + name);
            return ok;
        }

        /// <summary>把两个任务的状态同步成 on / off</summary>
        public static void Sync(bool on)
        {
            if (on)
            {
                if (!WakeExists()) CreateWake(AppEnv.LoginExePath);
                if (!LogonExists()) CreateLogon(AppEnv.LoginExePath);
            }
            else
            {
                if (WakeExists()) Delete(WakeName);
                if (LogonExists()) Delete(LogonName);
            }
        }

        /// <summary>
        /// 调 schtasks。故意不重定向输出：某些受限环境下管道会让子进程直接失败，
        /// 而我们只需要退出码（0=成功）。
        /// </summary>
        static int Run(string args, bool quiet)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("schtasks.exe", args);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = false;
                psi.RedirectStandardError = false;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                using (Process p = Process.Start(psi))
                {
                    if (!p.WaitForExit(10000)) { try { p.Kill(); } catch { } return -1; }
                    if (p.ExitCode != 0 && !quiet) Engine.Log("schtasks " + args + " 失败，退出码 " + p.ExitCode);
                    return p.ExitCode;
                }
            }
            catch (Exception ex) { if (!quiet) Engine.Log("schtasks 异常：" + ex.Message); return -1; }
        }
    }
    internal class HttpResult
    {
        public int Status = 0;
        public string Location = "";
        public string Body = "";
        public string Error = "";
        public bool Ok { get { return Error.Length == 0; } }
    }

    internal class PortalInfo
    {
        public string BaseUrl = "";
        public string Ip = "";
        public string Vlan = "0";
        public string Mac = "";
        public string ActionUrl = "";
    }

    /// <summary>登录引擎</summary>
    public static class Engine
    {
        const string UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36";
        static readonly object LogLock = new object();
        static readonly Encoding Gbk = Encoding.GetEncoding(936);
        const int LogLimit = 512 * 1024;

        // ---------------- 日志 ----------------
        public static void Log(string msg)
        {
            try
            {
                lock (LogLock)
                {
                    string p = AppEnv.LogPath;
                    try
                    {
                        FileInfo fi = new FileInfo(p);
                        if (fi.Exists && fi.Length > LogLimit)
                        {
                            string bak = p + ".1";
                            if (File.Exists(bak)) File.Delete(bak);
                            File.Move(p, bak);
                        }
                    }
                    catch { }
                    File.AppendAllText(p, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + "\r\n", Encoding.UTF8);
                }
            }
            catch { }
        }

        public static string[] TailLog(int maxLines)
        {
            try
            {
                if (!File.Exists(AppEnv.LogPath)) return new string[0];
                string[] all = File.ReadAllLines(AppEnv.LogPath, Encoding.UTF8);
                if (all.Length <= maxLines) return all;
                string[] tail = new string[maxLines];
                Array.Copy(all, all.Length - maxLines, tail, 0, maxLines);
                return tail;
            }
            catch { return new string[0]; }
        }

        // ---------------- HTTP ----------------
        static HttpResult Http(string url, string method, string body, string referer, int timeoutMs)
        {
            HttpResult r = new HttpResult();
            url = NormalizeUrl(url);
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Proxy = null;                       // 不走系统代理，校园网内直连
                req.Method = method;
                req.Timeout = timeoutMs;
                req.ReadWriteTimeout = timeoutMs;
                req.AllowAutoRedirect = false;           // 手动处理跳转，便于发现认证服务器
                req.UserAgent = UA;
                req.KeepAlive = false;
                req.Accept = "*/*";
                try { req.ServicePoint.Expect100Continue = false; } catch { }
                if (referer != null && referer.Length > 0) { try { req.Referer = referer; } catch { } }

                if (body != null)
                {
                    byte[] bs = Encoding.ASCII.GetBytes(body);
                    req.ContentType = "application/x-www-form-urlencoded";
                    req.ContentLength = bs.Length;
                    using (Stream s = req.GetRequestStream()) s.Write(bs, 0, bs.Length);
                }

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    r.Status = (int)resp.StatusCode;
                    if (resp.Headers["Location"] != null) r.Location = resp.Headers["Location"];
                    r.Body = ReadBody(resp);
                }
            }
            catch (WebException wex)
            {
                HttpWebResponse resp = wex.Response as HttpWebResponse;
                if (resp != null)
                {
                    r.Status = (int)resp.StatusCode;
                    try { if (resp.Headers["Location"] != null) r.Location = resp.Headers["Location"]; } catch { }
                    try { r.Body = ReadBody(resp); } catch { }
                    try { resp.Close(); } catch { }
                }
                else r.Error = wex.Message;
            }
            catch (Exception ex) { r.Error = ex.Message; }
            return r;
        }

        static string ReadBody(HttpWebResponse resp)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                try
                {
                    using (Stream st = resp.GetResponseStream())
                    {
                        if (st != null)
                        {
                            byte[] buf = new byte[8192];
                            int n;
                            while ((n = st.Read(buf, 0, buf.Length)) > 0) ms.Write(buf, 0, n);
                        }
                    }
                }
                catch { }
                byte[] bytes = ms.ToArray();
                Encoding enc = Gbk;
                string ct = resp.ContentType ?? "";
                if (ct.ToLowerInvariant().IndexOf("utf-8") >= 0) enc = Encoding.UTF8;
                try { return enc.GetString(bytes); } catch { return ""; }
            }
        }

        public static string NormalizeServer(string s)
        {
            s = (s ?? "").Trim();
            if (s.Length == 0) s = "192.168.1.1";
            if (!s.StartsWith("http", StringComparison.OrdinalIgnoreCase)) s = "http://" + s;
            return s.TrimEnd('/');
        }

        /// <summary>手动指定 MAC（配置项 本机MAC，留空=自动）</summary>
        public static string MacOverride = "";

        /// <summary>手动指定 AC 地址 / 名称（配置项 AC地址 / AC名称）</summary>
        public static string AcOverride = "";
        public static string AcNameOverride = "";

        /// <summary>
        /// 离线时先访问一个外网地址，让 AC 把请求劫持到它的认证页，
        /// 再把这个跳转地址原样当成登录目标 —— 浏览器就是这样拿到 wlanacip / wlanacname 的。
        /// </summary>
        static string TryGetCaptivePortalUrl(int timeoutMs)
        {
            string[] probes = new string[]
            {
                "http://www.msftconnecttest.com/connecttest.txt",
                "http://connect.rom.miui.com/generate_204",
                "http://captive.apple.com/hotspot-detect.html"
            };
            foreach (string u in probes)
            {
                HttpResult r = Http(u, "GET", null, null, timeoutMs);
                if (!r.Ok) continue;
                if (r.Status < 300 || r.Status >= 400) continue;
                string loc = r.Location;
                if (string.IsNullOrEmpty(loc) || loc.StartsWith("/")) continue;
                if (loc.IndexOf("a70.htm", StringComparison.OrdinalIgnoreCase) < 0
                    && loc.IndexOf("wlanuserip", StringComparison.OrdinalIgnoreCase) < 0) continue;
                Log("捕获到 AC 劫持跳转地址：" + loc);
                return loc;
            }
            return "";
        }

        /// <summary>
        /// 修正门户返回的畸形跳转地址。这套 AC 会返回 http://192.168.1.1:/2.htm?...
        /// （冒号后端口为空），.NET 的 Uri 遇到会直接抛异常，导致"明明成功却判成失败"。
        /// </summary>
        static string NormalizeUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return "";
            url = url.Trim();
            url = Regex.Replace(url, "^(https?)://([^/:?#]+):(?:/|$)", "$1://$2/");
            return url;
        }
        /// <summary>解出跳转地址里 ErrorMsg=（base64）携带的错误文本</summary>
        static string DecodeErrorMsg(string url)
        {
            try
            {
                Match m = Regex.Match(url ?? "", "ErrorMsg=([A-Za-z0-9%+/=]+)");
                if (!m.Success) return "";
                string b64 = Uri.UnescapeDataString(Uri.UnescapeDataString(m.Groups[1].Value));
                return Encoding.UTF8.GetString(Convert.FromBase64String(b64));
            }
            catch { return ""; }
        }
        /// <summary>从 URL 查询串里取某个参数</summary>
        static string GetQueryValue(string url, string key)
        {
            try
            {
                int q = url.IndexOf("?");
                if (q < 0) return "";
                string query = url.Substring(q + 1);
                foreach (string kv in query.Split(new char[] { '&' }))
                {
                    int e = kv.IndexOf("=");
                    if (e <= 0) continue;
                    if (string.Equals(kv.Substring(0, e), key, StringComparison.OrdinalIgnoreCase))
                        return Uri.UnescapeDataString(kv.Substring(e + 1));
                }
            }
            catch { }
            return "";
        }

        /// <summary>找本机对应 IP 那张网卡的 MAC（格式 AA-BB-CC-DD-EE-FF）</summary>
        static string LocalMacFor(string ip)
        {
            if (!string.IsNullOrEmpty(MacOverride)) return NormalizeMac(MacOverride);
            try
            {
                NetworkInterface only = null;
                int count = 0;
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    string mac = FormatMac(ni.GetPhysicalAddress());
                    if (mac.Length != 17 || mac == "00-00-00-00-00-00") continue;
                    bool hasIpv4 = false;
                    foreach (UnicastIPAddressInformation u in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (u.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        string a = u.Address.ToString();
                        if (a.StartsWith("169.254.")) continue;
                        hasIpv4 = true;
                        if (a == ip) return mac;
                    }
                    if (hasIpv4) { count++; only = ni; }
                }
                if (count == 1 && only != null) return FormatMac(only.GetPhysicalAddress());
            }
            catch (Exception ex) { Log("读取网卡 MAC 失败：" + ex.Message); }
            return "";
        }

        static string FormatMac(PhysicalAddress pa)
        {
            byte[] b = pa.GetAddressBytes();
            if (b == null || b.Length != 6) return "";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < b.Length; i++)
            {
                if (i > 0) sb.Append('-');
                sb.Append(b[i].ToString("X2"));
            }
            return sb.ToString();
        }

        /// <summary>把用户填的 MAC 规整成 AA-BB-CC-DD-EE-FF</summary>
        static string NormalizeMac(string s)
        {
            string hex = Regex.Replace((s ?? "").ToUpperInvariant(), "[^0-9A-F]", "");
            if (hex.Length != 12) return "";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < 12; i += 2)
            {
                if (i > 0) sb.Append('-');
                sb.Append(hex.Substring(i, 2));
            }
            return sb.ToString();
        }
        // ---------------- 在线检测 ----------------
        public static bool IsOnline()
        {
            if (AssumeOffline) return false;        // 调试用：伪装成"不能上网"
            HttpResult a = Http("http://www.msftconnecttest.com/connecttest.txt", "GET", null, null, 3000);
            if (a.Ok && a.Status == 200 && a.Body.IndexOf("Microsoft Connect Test", StringComparison.Ordinal) >= 0) return true;
            HttpResult b = Http("http://connect.rom.miui.com/generate_204", "GET", null, null, 3000);
            if (b.Ok && b.Status == 204) return true;
            return false;
        }

        /// <summary>调试用：强制视作离线（自动化测试 062 分支）</summary>
        public static bool AssumeOffline = false;

        // ---------------- 读取门户页，拿本机 IP / VLAN / 提交地址 ----------------
        static PortalInfo Probe(string baseUrl, int timeoutMs, out string err)
        {
            err = "";
            HttpResult r = Http(baseUrl + "/a70.htm", "GET", null, null, timeoutMs);
            if (!r.Ok) { err = r.Error; return null; }
            if (r.Status != 200) { err = "HTTP " + r.Status.ToString(); return null; }
            string html = r.Body ?? "";
            Match mIp = Regex.Match(html, "ss5\\s*=\\s*\"([0-9]{1,3}(?:\\.[0-9]{1,3}){3})\"");
            if (!mIp.Success) { err = "页面中未找到本机 IP"; return null; }
            PortalInfo info = new PortalInfo();
            info.BaseUrl = baseUrl;
            info.Ip = mIp.Groups[1].Value;
            Match mVlan = Regex.Match(html, "vlanid\\s*=\\s*\"([^\"]*)\"");
            if (mVlan.Success) info.Vlan = mVlan.Groups[1].Value.Trim();
            info.Mac = LocalMacFor(info.Ip);
            string macParam = info.Mac.Length > 0 ? info.Mac : "00-00-00-00-00-00";
            if (info.Mac.Length > 0) Log("本机 MAC：" + info.Mac + (MacOverride.Length > 0 ? "（配置项指定）" : "（自动识别）"));
            else Log("警告：未能匹配到本机网卡 MAC，将使用全 0（AC 会记成一台假设备）");
            string acip = string.IsNullOrEmpty(AcOverride) ? "null" : Uri.EscapeDataString(AcOverride);
            string acnm = string.IsNullOrEmpty(AcNameOverride) ? "null" : Uri.EscapeDataString(AcNameOverride);
            info.ActionUrl = baseUrl + "/a70.htm?wlanuserip=" + info.Ip
                + "&wlanacip=" + acip + "&wlanacname=" + acnm + "&vlanid=" + info.Vlan
                + "&ip=" + info.Ip + "&ssid=null&areaID=null&mac=" + macParam;
            return info;
        }

        /// <summary>被门户劫持时，从跳转地址里推断认证服务器</summary>
        static string DiscoverBase(int timeoutMs)
        {
            string[] urls = new string[]
            {
                "http://www.msftconnecttest.com/connecttest.txt",
                "http://connect.rom.miui.com/generate_204",
                "http://www.apple.com/library/test/success.html"
            };
            foreach (string u in urls)
            {
                HttpResult r = Http(u, "GET", null, null, timeoutMs);
                if (!r.Ok) continue;
                if (r.Status >= 300 && r.Status < 400 && r.Location.Length > 0)
                {
                    string loc = r.Location;
                    if (loc.StartsWith("/")) continue;
                    try
                    {
                        Uri uri = new Uri(loc);
                        string b = uri.Scheme + "://" + uri.Host + (uri.Port == 80 ? "" : ":" + uri.Port.ToString());
                        Log("通过跳转发现认证服务器：" + b);
                        return b;
                    }
                    catch { }
                }
                if (r.Status == 200 && r.Body.IndexOf("wlanuserip", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Match m = Regex.Match(r.Body, "(https?://[0-9\\.:]+/?a70\\.htm)");
                    if (m.Success)
                    {
                        try
                        {
                            Uri uri = new Uri(m.Groups[1].Value);
                            string b = uri.Scheme + "://" + uri.DnsSafeHost + (uri.Port == 80 ? "" : ":" + uri.Port.ToString());
                            Log("从页面发现认证服务器：" + b);
                            return b;
                        }
                        catch { }
                    }
                }
            }
            return null;
        }

        // ---------------- 提交登录 ----------------
        static LoginOutcome PostLocal(NetConfig c, PortalInfo info)
        {
            string body = "DDDDD=" + Uri.EscapeDataString(c.AccountWithIsp)
                        + "&upass=" + Uri.EscapeDataString(c.Password ?? "")
                        + "&R1=0&R2=&R3=0&R6=0&para=00&0MKKey=123456"
                        + "&buttonClicked=&redirect_url=&err_flag=&username=&password=&user=&cmd=&Login=";
            Log("提交登录（本地认证）：" + info.ActionUrl);
            HttpResult p = Http(info.ActionUrl, "POST", body, info.BaseUrl + "/a70.htm", 12000);
            p = FollowRedirects(p, info.ActionUrl, 2);
            return Judge(c, p, info);
        }

        /// <summary>
        /// PORTAL 协议认证（Dr.COM 的 :801/eportal/?c=ACSetting&amp;a=Login）。
        /// 参数取值口径与门户页 a41.js 的 getCurrMAC / getCurrIP / login_portal 完全一致：
        ///   mac  = 真实网卡 MAC（缺省才是 000000000000）
        ///   wlanacip / wlanacname 来自 AC 劫持跳转地址或配置
        /// </summary>
        static LoginOutcome PostPortal(NetConfig c, PortalInfo info)
        {
            string host = new Uri(info.BaseUrl).Host;
            string pb = PortalBase(info.BaseUrl);
            string acip = string.IsNullOrEmpty(AcOverride) ? "null" : Uri.EscapeDataString(AcOverride);
            string acnm = string.IsNullOrEmpty(AcNameOverride) ? "null" : Uri.EscapeDataString(AcNameOverride);
            // 与门户页 getCurrMAC() 完全一致：小写、带横杠（如 00-1a-2b-3c-4d-5e）
            string mac = info.Mac.Length > 0 ? info.Mac.Replace("-", "").ToLowerInvariant() : "000000000000";
            if (mac.Length == 12)
                mac = mac.Substring(0, 2) + "-" + mac.Substring(2, 2) + "-" + mac.Substring(4, 2) + "-"
                    + mac.Substring(6, 2) + "-" + mac.Substring(8, 2) + "-" + mac.Substring(10, 2);

            string q = pb + "/eportal/?c=ACSetting&a=Login"
                     + "&protocol=http:&hostname=" + Uri.EscapeDataString(host) + "&port="
                     + "&iTermType=1&wlanuserip=" + info.Ip
                     + "&wlanacip=" + acip + "&wlanacname=" + acnm
                     + "&mac=" + Uri.EscapeDataString(mac)
                     + "&ip=" + info.Ip
                     + "&redirect=&session=&enAdvert=0&queryACIP=0&loginMethod=1";
            string body = "DDDDD=" + Uri.EscapeDataString(c.AccountWithIsp)
                        + "&upass=" + Uri.EscapeDataString(c.Password ?? "")
                        + "&R1=0&R2=&R3=0&R6=0&para=00&0MKKey=123456"
                        + "&buttonClicked=&redirect_url=&err_flag=&username=&password=&user=&cmd=&Login=";
            Log("提交登录（PORTAL 协议）：" + q);
            Log("表单：DDDDD=" + c.AccountWithIsp + "  mac=" + mac);
            HttpResult p = Http(q, "POST", body, info.BaseUrl + "/a70.htm", 12000);
            p = FollowRedirects(p, q, 2);
            return Judge(c, p, info);
        }

        /// <summary>门户的 :801 端口（配置里已带端口就沿用，否则补 801）</summary>
        static string PortalBase(string baseUrl)
        {
            try
            {
                Uri u = new Uri(baseUrl);
                if (u.Port > 0 && u.Port != 80) return u.Scheme + "://" + u.Host + ":" + u.Port.ToString();
                return "http://" + u.Host + ":801";
            }
            catch { return baseUrl; }
        }

        /// <summary>
        /// 跟随 3xx 跳转再判定。Dr.COM 的 PORTAL 协议认证成功后返回的是 302 跳到成功页
        /// （Dr.COMWebLoginID_3.htm），正文往往只有一个 BOM，不能直接当失败。
        /// </summary>
        static HttpResult FollowRedirects(HttpResult p, string requestUrl, int maxHops)
        {
            int hop = 0;
            while (p.Ok && p.Status >= 300 && p.Status < 400 && !string.IsNullOrEmpty(p.Location) && hop < maxHops)
            {
                string loc = NormalizeUrl(p.Location);
                try { loc = new Uri(new Uri(requestUrl), loc).ToString(); }
                catch { }
                hop++;
                Log("跟随跳转（" + p.Status.ToString() + "）：" + loc);
                string errMsg = DecodeErrorMsg(loc);
                if (errMsg.Length > 0) Log("跳转里携带的错误信息：" + errMsg);
                p = Http(loc, "GET", null, null, 12000);
                requestUrl = loc;
            }
            return p;
        }
        static LoginOutcome Judge(NetConfig c, HttpResult p, PortalInfo info)
        {
            if (!p.Ok)
            {
                Log("请求失败：" + p.Error);
                LoginOutcome bad0 = LoginOutcome.Fail("请求认证服务器失败：" + p.Error);
                bad0.TransportError = true;
                return bad0;
            }
            string html = p.Body ?? "";
            try
            {
                if (html.Length > 0) File.WriteAllText(AppEnv.LastResponsePath, html, Encoding.UTF8);
                else if (!string.IsNullOrEmpty(p.Location))
                    File.WriteAllText(AppEnv.LastResponsePath,
                        "<pre>HTTP " + p.Status.ToString() + " Location: " + p.Location + "</pre>", Encoding.UTF8);
            }
            catch { }

            bool ok = html.IndexOf("认证成功", StringComparison.Ordinal) >= 0
                   || html.IndexOf("Dr.COMWebLoginID_3", StringComparison.Ordinal) >= 0;
            bool al = html.IndexOf("已经在线", StringComparison.Ordinal) >= 0
                   || html.IndexOf("已在线", StringComparison.Ordinal) >= 0
                   || html.IndexOf("无需重复登录", StringComparison.Ordinal) >= 0;

            if (ok)
            {
                string m = "认证成功 " + info.Ip + "（" + c.AccountWithIsp + "）";
                Log(m);
                return LoginOutcome.Ok(m);
            }
            if (al)
            {
                string m = "已经是登录状态 " + info.Ip;
                Log(m);
                return LoginOutcome.Online(m);
            }
            string msg = ExtractMessage(html);
            bool recognized = MatchedAuthKeyword(msg);
            Log("认证失败：HTTP " + p.Status.ToString() + "  " + msg);
            string code0 = DetectErrorCode(html, p.Location);
            string fallback = msg.Length > 0 ? msg
                            : (code0.Length > 0 ? ("认证系统返回错误码 code[" + code0 + "]") : ("认证失败（HTTP " + p.Status.ToString() + "）"));
            LoginOutcome bad = LoginOutcome.Fail(fallback);
            bad.AuthError = recognized || code0.Length > 0;
            bad.ErrorCode = code0;
            if (p.Status == 404) bad.TransportError = true;    // 端点不存在 -> 允许退回本地认证
            if (bad.ErrorCode.Length > 0) Log("认证系统返回错误码 code[" + bad.ErrorCode + "]");
            return bad;
        }

        /// <summary>
        /// 识别 Dr.COM 错误码。三种来源都要覆盖：
        /// 1) 响应正文里的提示，如 &lt;div id="message"&gt;Rad:Oppp error: code[062]:num: m[2/0] s[2/2]&lt;/div&gt;
        /// 2) 302 跳转地址里的 RetCode=/ErrorMsg=（ErrorMsg 是 base64）
        /// 3) 2.htm?ACLogOut=..&amp;RetCode=.. 这类错误页
        /// </summary>
        public static string DetectErrorCode(string body, string location)
        {
            string code = DetectErrorCode(body);
            if (code.Length > 0) return code;

            string loc = location ?? "";
            if (loc.Length > 0)
            {
                code = DetectErrorCode(loc);
                if (code.Length > 0) return code;
                // ErrorMsg=<base64>
                foreach (Match em in Regex.Matches(loc, "ErrorMsg=([A-Za-z0-9%+/=]+)"))
                {
                    string b64 = Uri.UnescapeDataString(Uri.UnescapeDataString(em.Groups[1].Value));
                    try
                    {
                        string text = Encoding.UTF8.GetString(Convert.FromBase64String(b64));
                        code = DetectErrorCode(text);
                        if (code.Length > 0) return code;
                    }
                    catch { }
                }
                // RetCode=Rad:Oppp%20error:%20code[062]...
                int i = loc.IndexOf("RetCode=", StringComparison.OrdinalIgnoreCase);
                if (i >= 0)
                {
                    string rest = loc.Substring(i + 8);
                    try { rest = Uri.UnescapeDataString(rest); } catch { }
                    code = DetectErrorCode(rest);
                    if (code.Length > 0) return code;
                }
            }
            return "";
        }

        /// <summary>从一段文本里识别 Dr.COM 错误码，例如 "Rad:Oppp error:code[062]:num: m[2/0] s[2/2]"</summary>
        public static string DetectErrorCode(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            Match m = Regex.Match(text, "code\\s*\\[\\s*(\\d{1,4})\\s*\\]");
            if (m.Success) return m.Groups[1].Value;
            m = Regex.Match(text, "(?i)code\\s*\\[\\s*(\\d{1,4})\\s*\\]");
            if (m.Success) return m.Groups[1].Value;
            m = Regex.Match(text, "(?i)error\\s*:?\\s*code\\s*=\\s*(\\d{1,4})");
            if (m.Success) return m.Groups[1].Value;
            return "";
        }

        static readonly string[] AuthKeys = new string[] {
            "密码错误","密码不正确","用户名或密码错误","账号或密码错误","用户不存在","账号不存在",
            "余额不足","已欠费","欠费","账号已停机","已停机","账号被锁定","认证被拒绝","达到最大在线数",
            "该IP已经在线","IP地址不在","绑定","次数过多","不在允许范围","用户已在线","费用",
            "账号已过期","欠费停机","流量已用完","该账号已被禁用"
        };

        static bool MatchedAuthKeyword(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (string k in AuthKeys)
                if (text.IndexOf(k, StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        /// <summary>从返回页里提取一句人能看懂的提示</summary>
        public static string ExtractMessage(string html)
        {
            if (html == null) return "";
            string text = Regex.Replace(html, "(?is)<script.*?</script>", " ");
            text = Regex.Replace(text, "(?is)<style.*?</style>", " ");
            Match t = Regex.Match(text, "(?is)<title>(.*?)</title>");
            string title = t.Success ? Clean(t.Groups[1].Value) : "";
            text = Regex.Replace(text, "(?is)<title>.*?</title>", " ");
            text = Regex.Replace(text, "(?s)<[^>]+>", " ");
            text = Clean(text);
            foreach (string k in AuthKeys)
            {
                if (text.IndexOf(k, StringComparison.Ordinal) >= 0)
                {
                    int i = text.IndexOf(k, StringComparison.Ordinal);
                    int s = Math.Max(0, i - 20);
                    int len = Math.Min(60, text.Length - s);
                    return Clean(text.Substring(s, len));
                }
            }
            if (text.Length > 0) return text.Length > 80 ? text.Substring(0, 80) : text;
            return title;
        }

        static string Clean(string s)
        {
            if (s == null) return "";
            s = s.Replace("&nbsp;", " ").Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">");
            s = Regex.Replace(s, "\\s+", " ");
            return s.Trim();
        }

        // ---------------- 一次完整尝试 ----------------
        public static LoginOutcome TryOnce(NetConfig c)
        {
            if (string.IsNullOrEmpty((c.Account ?? "").Trim()))
                return LoginOutcome.Fail("未配置账号，请先打开「校园网配置.exe」填写学号和密码");

            MacOverride = c.Mac;
            AcOverride = c.AcIp;
            AcNameOverride = c.AcName;
            // 先连认证服务器（校内地址，开机后最快可用），比探测外网更快
            string err = "";
            string baseUrl = NormalizeServer(c.Server);
            PortalInfo info = Probe(baseUrl, 6000, out err);
            if (info == null)
            {
                Log("访问认证服务器失败：" + baseUrl + " " + err);
                if (IsOnline())
                    return LoginOutcome.Online("已在线（认证页暂时打不开，但网络是通的）");
                string disc = DiscoverBase(5000);
                if (disc != null)
                {
                    // 把发现到的门户地址记回配置：下次开机直接连，省掉一次超时，也不用用户手填
                    try
                    {
                        string host = new Uri(disc).Authority;
                        if (host.Length > 0 && !string.Equals(host, c.Server, StringComparison.OrdinalIgnoreCase))
                        {
                            c.Server = host;
                            ConfigStore.Save(c);
                            Log("已把认证服务器地址记入配置：" + host);
                        }
                    }
                    catch (Exception ex) { Log("记录认证服务器地址失败：" + ex.Message); }
                    info = Probe(disc, 6000, out err);
                }
            }
            if (info == null) return LoginOutcome.Fail("无法访问认证服务器（网络可能还没连上）");

            if (c.SkipIfOnline && IsOnline())
                return LoginOutcome.Online("当前已在线，跳过登录");

            // 从 AC 的劫持跳转地址里取 wlanacip / wlanacname / wlanuserip，
            // 供 PORTAL 协议登录使用（浏览器就是带着这些参数去 :801/eportal 的）
            string capUrl = TryGetCaptivePortalUrl(2500);
            if (capUrl.Length > 0)
            {
                string v;
                if (AcOverride.Length == 0)
                {
                    v = GetQueryValue(capUrl, "wlanacip");
                    if (v.Length > 0 && v != "null") AcOverride = v;
                }
                if (AcNameOverride.Length == 0)
                {
                    v = GetQueryValue(capUrl, "wlanacname");
                    if (v.Length > 0 && v != "null") AcNameOverride = v;
                }
                if (info.Ip.Length == 0)
                {
                    v = GetQueryValue(capUrl, "wlanuserip");
                    if (v.Length > 0 && v != "null") info.Ip = v;
                }
                Log("从劫持地址取到 AC 参数：wlanacip=" + (AcOverride.Length > 0 ? AcOverride : "null")
                    + "  wlanacname=" + (AcNameOverride.Length > 0 ? AcNameOverride : "null"));
            }
            LoginOutcome r;
            if (c.Method == "local") r = PostLocal(c, info);
            else r = PostPortal(c, info);          // auto / portal 都先走 PORTAL 协议（这套门户实际用的就是它）

            // auto 模式下 PORTAL 没成功，再退回 a70.htm 本地认证试一次
            if (!r.Success && c.Method == "auto" && r.ErrorCode.Length == 0 && r.TransportError)
            {
                Log("PORTAL 端点不可用（" + r.Message + "），改试本地认证 a70.htm");
                LoginOutcome r2 = PostLocal(c, info);
                if (r2.Success) return r2;
                if (r2.ErrorCode.Length > 0) r = r2;
            }
            else if (!r.Success && c.Method == "auto" && !r.TransportError)
            {
                Log("PORTAL 协议被 AC 拒绝，不再退回本地认证（本地认证可能不校验密码、会生成 MAC 全 0 的假设备）");
            }

            // 062：账号同时在线设备数超限 —— 只有在"本机确实不能上网"时才处理
            if (!r.Success && r.ErrorCode == "062")
                return Handle062(c, info, r);

            // 认证页没给明确结论（例如返回信息页）时，用"能不能上网"兜底；被明确拒绝则不当成功
            if (!r.Success && !r.AuthError && IsOnline())
                return LoginOutcome.Ok("已连通 " + info.Ip + "（认证页未给出明确结果，但网络已可访问）");
            return r;
        }

        // ---------------- 062：同时在线设备超限 ----------------
        static bool _notified062 = false;

        /// <summary>调试/自动化测试用：为 true 时不弹窗、不开浏览器</summary>
        public static bool SuppressUi = false;

        static LoginOutcome Handle062(NetConfig c, PortalInfo info, LoginOutcome r)
        {
            Log("检测到 code[062]：该账号同时在线设备数已达上限（本机 IP " + info.Ip + "）");
            if (IsOnline())
            {
                Log("本机其实已经能上网，忽略 062");
                return LoginOutcome.Ok("已连通 " + info.Ip + "（收到 062，但本机网络正常）");
            }

            // 按需求：不替用户注销。打开自助服务平台 + 弹窗提醒，由用户自行登录注销
            if (!_notified062)
            {
                _notified062 = true;
                Notify062(c);
            }

            // 用户处理完（点掉提示）后，自动再试几次登录
            int tries = c.NotifyRetryCount < 0 ? 0 : c.NotifyRetryCount;
            for (int k = 1; k <= tries; k++)
            {
                Log("提醒后重新登录（第 " + k.ToString() + "/" + tries.ToString() + " 次），等待 "
                    + c.NotifyRetryInterval.ToString() + " 秒…");
                Thread.Sleep(c.NotifyRetryInterval * 1000);
                LoginOutcome again;
                if (c.Method == "portal") again = PostPortal(c, info);
                else again = PostLocal(c, info);
                if (again.Success)
                {
                    Log("重新登录成功");
                    return again;
                }
                Log("第 " + k.ToString() + " 次重新登录仍失败：" + again.Message);
                r = again;
                if (again.ErrorCode.Length > 0 && again.ErrorCode != "062") break;   // 换成别的错就不用再试了
            }

            LoginOutcome bad = LoginOutcome.Fail("超出同时在线设备数上限（062）：" + r.Message);
            bad.ErrorCode = "062";
            bad.Fatal = true;          // 已提醒用户处理，不再无意义重试
            return bad;
        }

        /// <summary>打开自助服务平台并弹窗提醒用户自行登录注销</summary>
        static void Notify062(NetConfig c)
        {
            string url = string.IsNullOrEmpty(c.SelfUrl) ? "http://192.168.1.1:8080/Self/dashboard" : c.SelfUrl;
            string text = "检测到校园网认证返回 code[062]：该账号的同时在线设备数已达上限。\r\n\r\n"
                        + "已为你打开自助服务平台：\r\n" + url + "\r\n\r\n"
                        + "请在该页面登录后，注销其他设备（或本机旧会话），然后点「确定」关闭本提示，"
                        + "程序会自动重试登录。";
            Log("062 处理方式：打开自助服务平台并提醒用户自行登录注销");
            Log("自助服务地址：" + url);
            if (SuppressUi)
            {
                Log("[测试模式] 已抑制弹窗与浏览器");
                return;
            }
            // 用后台线程打开浏览器：ShellExecute 在少数情况下会阻塞几十秒，不能拖住登录进程
            try
            {
                string u = url;
                Thread th = new Thread(delegate ()
                {
                    try { Process.Start(u); } catch (Exception ex) { Log("打开自助服务平台失败：" + ex.Message); }
                });
                th.IsBackground = true;
                th.Start();
            }
            catch (Exception ex) { Log("打开自助服务平台失败：" + ex.Message); }
            try
            {
                System.Windows.Forms.MessageBox.Show(text, "校园网登录 - 需要你手动处理",
                    System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
            }
            catch { }
        }

        // ---------------- 主流程（带延时与重试） ----------------
        public static int Run(bool bootMode)
        {
            // 第一行就落盘：以后"进程到底有没有被启动"永远有据可查
            Log("---- 进程启动（" + (bootMode ? "--boot 开机模式" : "手动") + "）pid="
                + Process.GetCurrentProcess().Id.ToString() + " ----");
            bool created;
            using (Mutex mtx = new Mutex(true, "CampusNetAutoLogin_Mutex", out created))
            {
                if (!created)
                {
                    Log("已有实例在运行，本次退出（不重复登录）");
                    return 0;
                }
                try { return RunCore(bootMode); }
                catch (Exception ex) { Log("运行异常：" + ex.ToString()); return 1; }
            }
        }

        static int RunCore(bool bootMode)
        {
            NetConfig c = ConfigStore.Load();
            Log("==== " + (bootMode ? "开机自动登录" : "手动连接") + " 开始 ====");
            AutoStart.Sync(c.AutoStart);

            if (string.IsNullOrEmpty(c.Account.Trim()))
            {
                Log("未配置账号，已退出（请打开「校园网配置.exe」填写）");
                return 2;
            }

            if (bootMode && c.DelaySeconds > 0)
            {
                Log("等待 " + c.DelaySeconds.ToString() + " 秒后开始连接…");
                int left = c.DelaySeconds;
                while (left > 0)
                {
                    int step = left > 1 ? 1 : left;
                    Thread.Sleep(step * 1000);
                    left -= step;
                }
            }

            int attempts = c.MaxRetry < 1 ? 1 : c.MaxRetry;
            for (int i = 1; i <= attempts; i++)
            {
                LoginOutcome r = TryOnce(c);
                if (r.Success)
                {
                    Log("第 " + i.ToString() + "/" + attempts.ToString() + " 次：" + r.Message);
                    Log("==== 结束（成功）====");
                    return 0;
                }
                Log("第 " + i.ToString() + "/" + attempts.ToString() + " 次失败：" + r.Message);
                if (r.Fatal)
                {
                    Log("已停止重试：需要人工处理（详见上方提示）");
                    Log("==== 结束（需人工处理）====");
                    return 4;
                }
                if (i < attempts)
                {
                    Log("等待 " + c.RetryIntervalSeconds.ToString() + " 秒后重试…");
                    Thread.Sleep(c.RetryIntervalSeconds * 1000);
                }
            }
            Log("全部 " + attempts.ToString() + " 次尝试均失败，放弃。详见 login.log");
            return 3;
        }
    }
}