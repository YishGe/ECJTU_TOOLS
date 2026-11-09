// 校园网登录.exe —— 静默运行，无窗口（仅在 code[062] 需要人工处理时弹窗）
// 用法：
//   校园网登录.exe           立即连接一次（带重试）
//   校园网登录.exe --boot    开机自启模式（先等 N 秒，再按重试策略连接）
//   校园网登录.exe --no-ui   调试用：抑制弹窗/浏览器
using System;

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
