// 校园网登录.exe —— 静默运行，无窗口（仅在 code[062] 需要人工处理时弹窗）
// 用法：
//   校园网登录.exe           立即连接一次（带重试）
//   校园网登录.exe --boot    开机自启模式（先等 N 秒，再按重试策略连接）
//   校园网登录.exe --no-ui   调试用：抑制弹窗/浏览器
using System;
using System.Diagnostics;
using System.IO;

namespace CampusNet
{
    internal static class LoginProgram
    {
        [STAThread]
        static int Main(string[] args)
        {
            bool boot = false;
            if (args != null)
            {
                foreach (string a in args)
                {
                    string t = (a ?? "").Trim().ToLowerInvariant();
                    if (t == "--boot" || t == "/boot" || t == "-boot") boot = true;
                    if (t == "--no-ui" || t == "-no-ui") Engine.SuppressUi = true;
                    if (t == "--assume-offline") Engine.AssumeOffline = true;
                }
            }
            try
            {
                // 首次运行（还没有 config.ini）且是手动点击：直接弹出配置工具，而不是默默退出
                if (!boot && !File.Exists(AppEnv.ConfigPath))
                {
                    Engine.Log("首次运行：未找到 config.ini，打开配置工具");
                    string cfgExe = Path.Combine(AppEnv.Dir, "校园网配置.exe");
                    if (File.Exists(cfgExe))
                    {
                        try { Process.Start(cfgExe); } catch (Exception ex) { Engine.Log("打开配置工具失败：" + ex.Message); }
                        return 0;
                    }
                    Engine.Log("同目录下没有找到 校园网配置.exe，请先运行配置工具填写账号密码");
                }
                return Engine.Run(boot);
            }
            catch (Exception ex)
            {
                try { Engine.Log("致命异常：" + ex.ToString()); } catch { }
                return 1;
            }
        }
    }
}
