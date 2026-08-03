using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ScreenBlackout
{
    internal static class Program
    {
        internal const string MutexName = @"Local\ScreenBlackoutToggle_v2";
        internal const string ToggleEventName = @"Local\ScreenBlackoutToggleEvent_v2";
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "ScreenBlackout";

        [STAThread]
        private static void Main()
        {
            bool createdNew;
            using (var mutex = new Mutex(true, MutexName, out createdNew))
            {
                if (!createdNew)
                {
                    // Already running in the tray -> toggle blackout.
                    try
                    {
                        using (var evt = EventWaitHandle.OpenExisting(ToggleEventName))
                            evt.Set();
                    }
                    catch { }
                    return;
                }

                bool blackOnStart = true;
                foreach (string a in Environment.GetCommandLineArgs())
                {
                    if (a.Equals("--autostart", StringComparison.OrdinalIgnoreCase))
                    {
                        blackOnStart = false;
                        break;
                    }
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                new TrayApp().Run(blackOnStart);
            }
        }

        public static bool IsAutoStartEnabled()
        {
            using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                return k != null && k.GetValue(RunValueName) != null;
        }

        public static void SetAutoStart(bool enabled)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (k == null) return;
                if (enabled)
                    k.SetValue(RunValueName, "\"" + Application.ExecutablePath + "\" --autostart");
                else
                    k.DeleteValue(RunValueName, false);
            }
        }
    }

    internal sealed class TrayApp
    {
        private readonly Form _host;              // invisible owner window (message pump)
        private readonly NotifyIcon _tray;
        private readonly ToolStripMenuItem _autostartItem;
        private readonly ToolStripMenuItem _toggleItem;
        private BlackoutForm _black;
        private EventWaitHandle _toggleEvent;

        public TrayApp()
        {
            _host = new Form();
            _host.ShowInTaskbar = false;
            _host.Opacity = 0;
            _host.FormBorderStyle = FormBorderStyle.None;
            _host.StartPosition = FormStartPosition.Manual;
            _host.Location = new Point(-32000, -32000);
            _host.ShowInTaskbar = false;

            Icon icon = null;
            try { icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            if (icon == null) icon = SystemIcons.Application;

            _tray = new NotifyIcon
            {
                Icon = icon,
                Text = "ScreenBlackout — 单击黑屏/恢复",
                Visible = true
            };

            _toggleItem = new ToolStripMenuItem("黑屏/恢复");
            _toggleItem.Click += (s, e) => Toggle();

            _autostartItem = new ToolStripMenuItem("开机自启动");
            _autostartItem.Click += (s, e) =>
            {
                bool en = !_autostartItem.Checked;
                _autostartItem.Checked = en;
                Program.SetAutoStart(en);
            };

            var exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += (s, e) => Exit();

            var menu = new ContextMenuStrip();
            menu.Items.Add(_toggleItem);
            menu.Items.Add(_autostartItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exitItem);
            _tray.ContextMenuStrip = menu;
            _tray.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left) Toggle();
            };
        }

        public void Run(bool blackOnStart)
        {
            _autostartItem.Checked = Program.IsAutoStartEnabled();
            if (!Program.IsAutoStartEnabled()) Program.SetAutoStart(true);

            _toggleEvent = new EventWaitHandle(false, EventResetMode.AutoReset, Program.ToggleEventName);
            var watcher = new Thread(() =>
            {
                while (true)
                {
                    try { _toggleEvent.WaitOne(); }
                    catch { return; }
                    try { _host.BeginInvoke(new Action(Toggle)); }
                    catch { }
                }
            });
            watcher.IsBackground = true;
            watcher.Start();

            if (blackOnStart) _host.Shown += (s, e) => Toggle();
            Application.Run(_host);
        }

        private void Toggle()
        {
            if (_black != null && !_black.IsDisposed && _black.Visible)
            {
                _black.Restore();
            }
            else
            {
                if (_black == null || _black.IsDisposed) _black = new BlackoutForm();
                _black.ShowBlackout();
            }
        }

        private void Exit()
        {
            if (_black != null && !_black.IsDisposed && _black.Visible) _black.Restore();
            _tray.Visible = false;
            Application.Exit();
        }
    }

    internal sealed class BlackoutForm : Form
    {
        private const int WM_SETCURSOR = 0x0020;

        public BlackoutForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Color.Black;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;

            // Cover every monitor (including left/negative-coordinate ones)
            Rectangle total = Screen.PrimaryScreen.Bounds;
            foreach (var s in Screen.AllScreens)
                total = Rectangle.Union(total, s.Bounds);
            // Overscan: extend slightly past the screen edge so no 1px desktop
            // line shows along the border (e.g. right edge white line).
            total.Inflate(2, 2);
            Bounds = total;

            KeyDown += OnKeyDown;
            MouseClick += (s, e) => Restore();
        }

        // Called on every blackout (Show() does not re-fire Shown on repeat shows).
        public void ShowBlackout()
        {
            Show();
            Cursor.Hide();
            Try(() => MsiKb.MsiKeyboard.TryTurnOff());   // keyboard backlight off
        }

        public void Restore()
        {
            Cursor.Show();
            Try(() => MsiKb.MsiKeyboard.TryTurnOn());    // keyboard backlight restore
            Hide();
        }

        private static void Try(Func<bool> action)
        {
            try { action(); }
            catch { }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) Restore();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { Restore(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // Keep the cursor hidden even when the mouse moves (WM_SETCURSOR would
        // normally re-show it over this window).
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_SETCURSOR)
            {
                Cursor.Hide();
                return;
            }
            base.WndProc(ref m);
        }
    }
}
