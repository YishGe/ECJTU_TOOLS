// 校园网配置.exe —— 图形化配置工具
// 可设置：账号 / 密码 / 运营商 / 开机自启 / 延时 / 重试次数 / 重试间隔 / 认证服务器
// 参数（仅供调试）：--shot=<png>  --selftest=<txt>
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace CampusNet
{
    public class ConfigForm : Form
    {
        static readonly string[] IspNames = {
            "不选择（仅学号）", "中国移动（@cmcc）", "中国联通（@unicom）", "中国电信（@telecom）"
        };
        static readonly string[] IspValues = { "", "@cmcc", "@unicom", "@telecom" };
        static readonly string[] MethodNames = {
            "自动（本地认证 a70.htm）", "本地认证 a70.htm", "PORTAL 协议 :801"
        };
        static readonly string[] MethodValues = { "auto", "local", "portal" };

        NetConfig cfg;
        TextBox txtAccount, txtPassword, txtServer, txtStatus, txtSelfUrl;
        CheckBox chkShow, chkAutoStart, chkSkipOnline;
        ComboBox cmbIsp, cmbMethod;
        NumericUpDown numDelay, numRetry, numInterval, numNotifyRetry, numNotifyInterval;
        Button btnTest, btnSave, btnLog, btnClose;
        Label lblState;
        bool busy;

        public ConfigForm()
        {
            cfg = ConfigStore.Load();
            BuildUi();
            FillUi(cfg);
        }

        void BuildUi()
        {
            SuspendLayout();
            Text = "校园网自动登录 · 配置";
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(552, 652);
            BackColor = Color.FromArgb(246, 247, 249);

            GroupBox g1 = NewGroup("账户", 16, 12, 520, 150);
            g1.Controls.Add(NewLabel("学号 / 账号", 16, 30, 70));
            txtAccount = NewText(88, 26, 400, false);
            g1.Controls.Add(txtAccount);

            g1.Controls.Add(NewLabel("密　　码", 16, 68, 70));
            txtPassword = NewText(88, 64, 300, true);
            g1.Controls.Add(txtPassword);
            chkShow = new CheckBox();
            chkShow.Text = "显示";
            chkShow.Location = new Point(394, 66);
            chkShow.Size = new Size(70, 22);
            chkShow.CheckedChanged += delegate { txtPassword.UseSystemPasswordChar = !chkShow.Checked; };
            g1.Controls.Add(chkShow);

            g1.Controls.Add(NewLabel("运 营 商", 16, 106, 70));
            cmbIsp = new ComboBox();
            cmbIsp.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbIsp.Location = new Point(88, 102);
            cmbIsp.Size = new Size(400, 24);
            cmbIsp.Items.AddRange(IspNames);
            g1.Controls.Add(cmbIsp);

            GroupBox g2 = NewGroup("启动与重试", 16, 170, 520, 168);
            chkAutoStart = new CheckBox();
            chkAutoStart.Text = "开机自动登录（登录 Windows 后自动连接校园网）";
            chkAutoStart.Location = new Point(16, 24);
            chkAutoStart.Size = new Size(460, 22);
            chkAutoStart.CheckedChanged += OnAutoStartChanged;
            g2.Controls.Add(chkAutoStart);

            g2.Controls.Add(NewLabel("开机延时", 16, 60, 60));
            numDelay = NewNum(80, 56, 64, 0, 600, 6);
            g2.Controls.Add(numDelay);
            g2.Controls.Add(NewLabel("秒", 148, 60, 20));

            g2.Controls.Add(NewLabel("重试上限", 186, 60, 60));
            numRetry = NewNum(250, 56, 56, 1, 100, 10);
            g2.Controls.Add(numRetry);
            g2.Controls.Add(NewLabel("次", 310, 60, 20));

            g2.Controls.Add(NewLabel("重试间隔", 348, 60, 60));
            numInterval = NewNum(412, 56, 56, 1, 600, 5);
            g2.Controls.Add(numInterval);
            g2.Controls.Add(NewLabel("秒", 472, 60, 20));

            chkSkipOnline = new CheckBox();
            chkSkipOnline.Text = "检测到已在线时跳过登录（避免把在线会话踢掉）";
            chkSkipOnline.Location = new Point(16, 92);
            chkSkipOnline.Size = new Size(460, 22);
            g2.Controls.Add(chkSkipOnline);

            g2.Controls.Add(NewLabel("认证服务器", 16, 126, 74));
            txtServer = NewText(92, 122, 150, false);
            g2.Controls.Add(txtServer);
            g2.Controls.Add(NewLabel("认证方式", 260, 126, 64));
            cmbMethod = new ComboBox();
            cmbMethod.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbMethod.Location = new Point(324, 122);
            cmbMethod.Size = new Size(180, 24);
            cmbMethod.Items.AddRange(MethodNames);
            g2.Controls.Add(cmbMethod);

            GroupBox g3 = NewGroup("062 超限处理", 16, 346, 520, 128);
            g3.Controls.Add(NewLabel("自助服务地址", 16, 26, 88));
            txtSelfUrl = NewText(112, 22, 396, false);
            g3.Controls.Add(txtSelfUrl);

            Label hint = new Label();
            hint.Text = "遇到 code[062] 时打开该地址，并弹窗提醒你自行登录注销";
            hint.ForeColor = Color.FromArgb(130, 130, 130);
            hint.Location = new Point(112, 50);
            hint.Size = new Size(400, 20);
            g3.Controls.Add(hint);

            g3.Controls.Add(NewLabel("提醒后重试", 16, 92, 62));
            numNotifyRetry = NewNum(80, 88, 50, 0, 20, 2);
            g3.Controls.Add(numNotifyRetry);
            g3.Controls.Add(NewLabel("次", 134, 92, 20));
            g3.Controls.Add(NewLabel("间隔", 172, 92, 32));
            numNotifyInterval = NewNum(208, 88, 50, 1, 600, 5);
            g3.Controls.Add(numNotifyInterval);
            g3.Controls.Add(NewLabel("秒", 262, 92, 20));

            GroupBox g4 = NewGroup("状态", 16, 482, 520, 110);
            lblState = new Label();
            lblState.Text = "就绪";
            lblState.ForeColor = Color.FromArgb(110, 110, 110);
            lblState.Location = new Point(14, 22);
            lblState.Size = new Size(490, 20);
            g4.Controls.Add(lblState);

            txtStatus = new TextBox();
            txtStatus.Multiline = true;
            txtStatus.ReadOnly = true;
            txtStatus.ScrollBars = ScrollBars.Vertical;
            txtStatus.BackColor = Color.White;
            txtStatus.Location = new Point(14, 44);
            txtStatus.Size = new Size(492, 54);
            txtStatus.Font = new Font("Consolas", 8.5F);
            g4.Controls.Add(txtStatus);

            btnTest = NewButton("立即测试登录", 16, 604, 132);
            btnTest.Click += OnTest;
            btnSave = NewButton("保存设置", 156, 604, 96);
            btnSave.Click += OnSave;
            btnLog = NewButton("查看日志", 260, 604, 96);
            btnLog.Click += OnOpenLog;
            btnClose = NewButton("关闭", 448, 604, 88);
            btnClose.Click += delegate { Close(); };

            Controls.Add(g1);
            Controls.Add(g2);
            Controls.Add(g3);
            Controls.Add(g4);
            Controls.Add(btnTest);
            Controls.Add(btnSave);
            Controls.Add(btnLog);
            Controls.Add(btnClose);

            AcceptButton = btnSave;
            ResumeLayout(false);
        }

        // ---------------- 小工具 ----------------
        static GroupBox NewGroup(string text, int x, int y, int w, int h)
        {
            GroupBox g = new GroupBox();
            g.Text = text;
            g.Location = new Point(x, y);
            g.Size = new Size(w, h);
            g.BackColor = Color.White;
            return g;
        }

        static Label NewLabel(string text, int x, int y, int w)
        {
            Label l = new Label();
            l.Text = text;
            l.Location = new Point(x, y + 3);
            l.Size = new Size(w, 20);
            l.AutoSize = false;
            return l;
        }

        static TextBox NewText(int x, int y, int w, bool pwd)
        {
            TextBox t = new TextBox();
            t.Location = new Point(x, y);
            t.Size = new Size(w, 24);
            t.BorderStyle = BorderStyle.FixedSingle;
            if (pwd) t.UseSystemPasswordChar = true;
            return t;
        }

        static NumericUpDown NewNum(int x, int y, int w, int min, int max, int val)
        {
            NumericUpDown n = new NumericUpDown();
            n.Location = new Point(x, y);
            n.Size = new Size(w, 24);
            n.Minimum = min;
            n.Maximum = max;
            n.Value = val;
            n.BorderStyle = BorderStyle.FixedSingle;
            n.TextAlign = HorizontalAlignment.Center;
            return n;
        }

        static Button NewButton(string text, int x, int y, int w)
        {
            Button b = new Button();
            b.Text = text;
            b.Location = new Point(x, y);
            b.Size = new Size(w, 32);
            b.FlatStyle = FlatStyle.System;
            b.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            return b;
        }

        // ---------------- 数据 <-> 界面 ----------------
        void FillUi(NetConfig c)
        {
            busy = true;            // 关键：填充过程中屏蔽 chkAutoStart.CheckedChanged，避免用半成品覆盖 config.ini
            try
            {
            txtAccount.Text = c.Account;
            txtPassword.Text = c.Password;
            int ii = Array.IndexOf(IspValues, c.Isp);
            cmbIsp.SelectedIndex = ii < 0 ? 0 : ii;
            bool stale = AutoStart.IsStale();                // 文件夹被移动过 -> 自动修正自启路径
            if (stale) AutoStart.Sync(true);
            AutoStart.Sync(c.AutoStart);                    // 注册表与配置保持一致
            chkAutoStart.Checked = AutoStart.IsEnabled();   // 以注册表实际状态为准
            if (c.AutoStart && !AutoStart.IsEnabled())
                SetState("提示：开机自启写入注册表失败，可尝试以管理员身份运行本程序", Color.FromArgb(190, 30, 30));
            numDelay.Value = Clamp(c.DelaySeconds, 0, 600);
            numRetry.Value = Clamp(c.MaxRetry, 1, 100);
            numInterval.Value = Clamp(c.RetryIntervalSeconds, 1, 600);
            chkSkipOnline.Checked = c.SkipIfOnline;
            txtServer.Text = c.Server;
            int mi = Array.IndexOf(MethodValues, c.Method);
            cmbMethod.SelectedIndex = mi < 0 ? 0 : mi;
            txtSelfUrl.Text = c.SelfUrl;
            numNotifyRetry.Value = Clamp(c.NotifyRetryCount, 0, 20);
            numNotifyInterval.Value = Clamp(c.NotifyRetryInterval, 1, 600);
            txtStatus.Text = "";
            }
            finally { busy = false; }
        }

        static decimal Clamp(int v, int lo, int hi)
        {
            if (v < lo) return lo;
            if (v > hi) return hi;
            return v;
        }

        NetConfig ReadUi()
        {
            NetConfig c = new NetConfig();
            c.Account = txtAccount.Text.Trim();
            c.Password = txtPassword.Text;
            c.Isp = IspValues[cmbIsp.SelectedIndex < 0 ? 0 : cmbIsp.SelectedIndex];
            c.AutoStart = chkAutoStart.Checked;
            c.DelaySeconds = (int)numDelay.Value;
            c.MaxRetry = (int)numRetry.Value;
            c.RetryIntervalSeconds = (int)numInterval.Value;
            c.SkipIfOnline = chkSkipOnline.Checked;
            c.Server = txtServer.Text.Trim().Length == 0 ? "192.168.1.1" : txtServer.Text.Trim();
            c.Method = MethodValues[cmbMethod.SelectedIndex < 0 ? 0 : cmbMethod.SelectedIndex];
            c.SelfUrl = txtSelfUrl.Text.Trim().Length == 0 ? "http://192.168.1.1:8080/Self/dashboard" : txtSelfUrl.Text.Trim();
            c.NotifyRetryCount = (int)numNotifyRetry.Value;
            c.NotifyRetryInterval = (int)numNotifyInterval.Value;
            return c;
        }

        void SetState(string text, Color color)
        {
            lblState.Text = text;
            lblState.ForeColor = color;
        }

        void ShowTail()
        {
            string[] lines = Engine.TailLog(6);
            StringBuilder sb = new StringBuilder();
            foreach (string l in lines) sb.AppendLine(l);
            if (sb.Length == 0) sb.Append("（暂无日志）");
            txtStatus.Text = sb.ToString().Replace("\r\n", "\r\n");
            txtStatus.SelectionStart = txtStatus.Text.Length;
            txtStatus.ScrollToCaret();
        }

        // ---------------- 事件 ----------------
        void OnAutoStartChanged(object sender, EventArgs e)
        {
            if (busy) return;
            AutoStart.Sync(chkAutoStart.Checked);
            if (chkAutoStart.Checked)
            {
                cfg.AutoStart = true;
                try { ConfigStore.Save(ReadUi()); } catch { }
                SetState("已开启开机自启：" + AutoStart.CommandLine, Color.FromArgb(0, 120, 60));
            }
            else
            {
                cfg.AutoStart = false;
                try { ConfigStore.Save(ReadUi()); } catch { }
                SetState("已关闭开机自启", Color.FromArgb(110, 110, 110));
            }
        }

        void OnSave(object sender, EventArgs e)
        {
            NetConfig c = ReadUi();
            if (c.Account.Length == 0)
            {
                SetState("请先填写学号 / 账号", Color.FromArgb(190, 30, 30));
                txtAccount.Focus();
                return;
            }
            try
            {
                ConfigStore.Save(c);
                AutoStart.Sync(c.AutoStart);
                cfg = c;
                SetState("已保存到 config.ini（" + DateTime.Now.ToString("HH:mm:ss") + "）", Color.FromArgb(0, 120, 60));
            }
            catch (Exception ex)
            {
                SetState("保存失败：" + ex.Message, Color.FromArgb(190, 30, 30));
            }
        }

        void OnOpenLog(object sender, EventArgs e)
        {
            try
            {
                if (!File.Exists(AppEnv.LogPath))
                {
                    SetState("还没有日志，先点「立即测试登录」试试", Color.FromArgb(150, 100, 0));
                    return;
                }
                Process.Start("notepad.exe", "\"" + AppEnv.LogPath + "\"");
            }
            catch (Exception ex) { SetState("打开日志失败：" + ex.Message, Color.FromArgb(190, 30, 30)); }
        }

        void OnTest(object sender, EventArgs e)
        {
            if (busy) return;
            NetConfig c = ReadUi();
            if (c.Account.Length == 0)
            {
                SetState("请先填写学号 / 账号", Color.FromArgb(190, 30, 30));
                txtAccount.Focus();
                return;
            }
            busy = true;
            btnTest.Enabled = false;
            btnSave.Enabled = false;
            SetState("正在连接，请稍候…（最多约 20 秒）", Color.FromArgb(30, 90, 170));
            txtStatus.Text = "";

            Thread th = new Thread(delegate ()
            {
                LoginOutcome r;
                try { r = Engine.TryOnce(c); }
                catch (Exception ex) { r = LoginOutcome.Fail("异常：" + ex.Message); }
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        busy = false;
                        btnTest.Enabled = true;
                        btnSave.Enabled = true;
                        if (r.Success && r.AlreadyOnline) SetState("✔ " + r.Message, Color.FromArgb(0, 120, 60));
                        else if (r.Success) SetState("✔ " + r.Message, Color.FromArgb(0, 120, 60));
                        else SetState("✘ " + r.Message, Color.FromArgb(190, 30, 30));
                        ShowTail();
                    });
                }
                catch { }
            });
            th.IsBackground = true;
            th.Start();
        }

        // 供 --selftest 使用
        public string SelfCheck()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("ClientSize=" + ClientSize.Width.ToString() + "x" + ClientSize.Height.ToString());
            sb.AppendLine("Font=" + Font.Name + " " + Font.Size.ToString());
            sb.AppendLine("AutoScaleFactor=" + (AutoScaleFactor.Width.ToString()));
            int bad = 0;
            foreach (Control c in Controls)
            {
                if (c.Right > ClientSize.Width + 1 || c.Bottom > ClientSize.Height + 1)
                {
                    sb.AppendLine("越界: " + c.Name + " " + c.GetType().Name + " " + c.Bounds.ToString());
                    bad++;
                }
                if (c is GroupBox)
                {
                    foreach (Control k in c.Controls)
                    {
                        if (k.Right > c.Width + 1 || k.Bottom > c.Height + 1)
                        {
                            sb.AppendLine("组内越界: " + (c as GroupBox).Text + " / " + k.Text + " " + k.Bounds.ToString());
                            bad++;
                        }
                    }
                }
            }
            sb.AppendLine("Account=" + txtAccount.Text);
            sb.AppendLine("Isp=" + IspValues[cmbIsp.SelectedIndex]);
            sb.AppendLine("Method=" + MethodValues[cmbMethod.SelectedIndex]);
            sb.AppendLine("SelfUrl=" + txtSelfUrl.Text);
            sb.AppendLine("NotifyRetry=" + numNotifyRetry.Value.ToString() + "/" + numNotifyInterval.Value.ToString() + "s");
            sb.AppendLine("AutoStartReg=" + AutoStart.IsEnabled().ToString());
            sb.AppendLine("ConfigPath=" + AppEnv.ConfigPath);
            sb.AppendLine("LoginExe=" + AppEnv.LoginExePath);
            sb.AppendLine("BadControls=" + bad.ToString());
            return sb.ToString();
        }
    }

    internal static class ConfigProgram
    {
        [STAThread]
        static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string shot = null;
            string self = null;
            if (args != null)
            {
                foreach (string a in args)
                {
                    if (a.StartsWith("--shot=", StringComparison.OrdinalIgnoreCase)) shot = a.Substring(7);
                    else if (a.StartsWith("--selftest=", StringComparison.OrdinalIgnoreCase)) self = a.Substring(11);
                }
            }

            try
            {
                if (shot != null)
                {
                    using (ConfigForm f = new ConfigForm())
                    {
                        f.StartPosition = FormStartPosition.Manual;
                        f.Location = new Point(-3000, -200);
                        f.ShowInTaskbar = false;
                        f.Show();
                        Application.DoEvents();
                        Thread.Sleep(400);
                        Application.DoEvents();
                        using (Bitmap bmp = new Bitmap(f.Width, f.Height))
                        {
                            f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height));
                            bmp.Save(shot, System.Drawing.Imaging.ImageFormat.Png);
                        }
                        f.Close();
                    }
                    return 0;
                }
                if (self != null)
                {
                    using (ConfigForm f = new ConfigForm())
                    {
                        f.CreateControl();
                        File.WriteAllText(self, f.SelfCheck(), new UTF8Encoding(true));
                        f.Close();
                    }
                    return 0;
                }
                Application.Run(new ConfigForm());
                return 0;
            }
            catch (Exception ex)
            {
                try { Engine.Log("配置工具异常：" + ex.ToString()); } catch { }
                return 1;
            }
        }
    }
}
