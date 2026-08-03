using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace ScreenBlackout
{
    internal static class Program
    {
        private const string MutexName = @"Local\ScreenBlackoutToggle_v1";
        private const string CloseEventName = @"Local\ScreenBlackoutClose_v1";

        [STAThread]
        private static void Main()
        {
            bool createdNew;
            using (var mutex = new Mutex(true, MutexName, out createdNew))
            {
                if (!createdNew)
                {
                    // Second click: tell the running instance to close gracefully,
                    // so the screen AND the keyboard backlight both get restored.
                    try
                    {
                        using (var evt = EventWaitHandle.OpenExisting(CloseEventName))
                            evt.Set();
                    }
                    catch { }
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                var form = new BlackoutForm();

                // Create the close-signal handle up front, watch it in a background thread.
                var closeEvent = new EventWaitHandle(false, EventResetMode.AutoReset, CloseEventName);
                var watcher = new Thread(() =>
                {
                    try
                    {
                        closeEvent.WaitOne();
                        try { form.BeginInvoke(new Action(form.Close)); } catch { }
                    }
                    catch { }
                });
                watcher.IsBackground = true;
                watcher.Start();

                Application.Run(form);
            }
        }
    }

    internal sealed class BlackoutForm : Form
    {
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
            MouseClick += (s, e) => Close();
            Shown += (s, e) =>
            {
                Cursor.Hide();
                Try(() => MsiKb.MsiKeyboard.TryTurnOff());   // keyboard backlight off
            };
            FormClosed += (s, e) =>
            {
                Cursor.Show();
                Try(() => MsiKb.MsiKeyboard.TryTurnOn());    // keyboard backlight restore
            };
        }

        private static void Try(Func<bool> action)
        {
            try { action(); }
            catch { }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) Close();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { Close(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
