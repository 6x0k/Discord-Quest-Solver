using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;

namespace MadDCStub
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            try
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (_, e) => ShowFatal(e.Exception);
                AppDomain.CurrentDomain.UnhandledException += (_, e) => ShowFatal(e.ExceptionObject as Exception);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new StubForm());
            }
            catch (Exception ex)
            {
                ShowFatal(ex);
            }
        }

        private static void ShowFatal(Exception? ex)
        {
            try
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "mad-dc");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "stub-error.log"),
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\r\n");
                MessageBox.Show(
                    "The mad-dc stub could not start.\n\n" + (ex?.Message ?? "Unknown error") +
                    "\n\nLog: " + Path.Combine(dir, "stub-error.log"),
                    "mad-dc", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        }
    }

    internal sealed class StubForm : Form
    {
        private readonly Timer animationTimer;
        private float pulse;
        private Point dragStart;
        private bool dragging;
        private readonly string executableName;
        private readonly string gameName;

        public StubForm()
        {
            executableName = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "mad-dc-stub");
            gameName = PrettyGameName(executableName);

            Text = "mad-dc";
            ClientSize = new Size(720, 430);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            MinimizeBox = true;
            DoubleBuffered = true;
            BackColor = Color.FromArgb(8, 10, 15);
            ShowInTaskbar = true;

            MouseDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseUp += (_, _) => dragging = false;

            animationTimer = new Timer { Interval = 32 };
            animationTimer.Tick += (_, _) =>
            {
                pulse += 0.055f;
                if (pulse > MathF.PI * 2) pulse -= MathF.PI * 2;
                Invalidate();
            };
            animationTimer.Start();

            Controls.Add(new CloseButton(this));
            Controls.Add(new MinButton(this));
        }

        private static string PrettyGameName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Game";
            value = value.Replace("-Win64-Shipping", "", StringComparison.OrdinalIgnoreCase)
                         .Replace("_Win64_Shipping", "", StringComparison.OrdinalIgnoreCase)
                         .Replace("-Shipping", "", StringComparison.OrdinalIgnoreCase);
            return value.Replace("_", " ").Replace("-", " ").Trim();
        }

        private void OnMouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || e.Y < 60) return;
            dragging = true;
            dragStart = e.Location;
        }

        private void OnMouseMove(object? sender, MouseEventArgs e)
        {
            if (!dragging) return;
            Left += e.X - dragStart.X;
            Top += e.Y - dragStart.Y;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            using var bg = new LinearGradientBrush(ClientRectangle,
                Color.FromArgb(8, 10, 15), Color.FromArgb(15, 19, 29), 135f);
            g.FillRectangle(bg, ClientRectangle);

            using var glow = new SolidBrush(Color.FromArgb(22, 70, 105, 220));
            g.FillEllipse(glow, new Rectangle(ClientSize.Width - 220, -80, 300, 300));

            using var borderPen = new Pen(Color.FromArgb(75, 83, 105), 1);
            g.DrawRoundedRectangle(borderPen, new Rectangle(0, 0, Width - 1, Height - 1), 20);

            DrawBrand(g);
            DrawMainCard(g);
            DrawFooter(g);
        }

        private void DrawBrand(Graphics g)
        {
            using var logoBrush = new LinearGradientBrush(new Rectangle(28, 24, 38, 38),
                Color.FromArgb(89, 142, 255), Color.FromArgb(133, 91, 255), 45f);
            g.FillRoundedRectangle(logoBrush, new Rectangle(28, 24, 38, 38), 11);
            using var logoFont = new Font("Segoe UI", 15, FontStyle.Bold);
            TextRenderer.DrawText(g, "M", logoFont, new Rectangle(28, 25, 38, 36), Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            using var titleFont = new Font("Segoe UI", 11, FontStyle.Bold);
            using var smallFont = new Font("Segoe UI", 7.5f, FontStyle.Bold);
            TextRenderer.DrawText(g, "mad-dc", titleFont, new Point(76, 23), Color.White);
            TextRenderer.DrawText(g, "LOCAL GAME PRESENCE", smallFont, new Point(77, 42), Color.FromArgb(125, 139, 165));
        }

        private void DrawMainCard(Graphics g)
        {
            var card = new Rectangle(30, 92, Width - 60, 265);
            using var cardBrush = new SolidBrush(Color.FromArgb(18, 23, 33));
            using var cardPen = new Pen(Color.FromArgb(42, 51, 67), 1);
            g.FillRoundedRectangle(cardBrush, card, 22);
            g.DrawRoundedRectangle(cardPen, card, 22);

            using var eyebrow = new Font("Segoe UI", 8, FontStyle.Bold);
            TextRenderer.DrawText(g, "ACTIVE PRESENCE", eyebrow, new Point(58, 120), Color.FromArgb(89, 142, 255));

            using var gameFont = new Font("Segoe UI", 25, FontStyle.Bold);
            TextRenderer.DrawText(g, gameName, gameFont, new Rectangle(56, 143, 520, 48), Color.White,
                TextFormatFlags.EndEllipsis);

            using var subFont = new Font("Segoe UI", 10);
            TextRenderer.DrawText(g, "mad-dc is running a local detection stub for Discord.", subFont,
                new Point(58, 198), Color.FromArgb(151, 162, 181));

            var dotAlpha = 150 + (int)(70 * (0.5 + 0.5 * Math.Sin(pulse)));
            using var dot = new SolidBrush(Color.FromArgb(dotAlpha, 75, 218, 150));
            g.FillEllipse(dot, new Rectangle(59, 239, 10, 10));
            TextRenderer.DrawText(g, "PROCESS ACTIVE", eyebrow, new Point(78, 236), Color.FromArgb(100, 218, 165));

            DrawInfo(g, "EXECUTABLE", executableName, 58, 274);
            DrawInfo(g, "STATUS", "Running · Discord presence ready", 340, 274);
        }

        private static void DrawInfo(Graphics g, string label, string value, int x, int y)
        {
            using var labelFont = new Font("Segoe UI", 7.5f, FontStyle.Bold);
            using var valueFont = new Font("Segoe UI", 9, FontStyle.Bold);
            TextRenderer.DrawText(g, label, labelFont, new Point(x, y), Color.FromArgb(105, 118, 139));
            TextRenderer.DrawText(g, value, valueFont, new Rectangle(x, y + 17, 300, 24), Color.FromArgb(226, 232, 242), TextFormatFlags.EndEllipsis);
        }

        private void DrawFooter(Graphics g)
        {
            using var font = new Font("Segoe UI", 8.5f);
            TextRenderer.DrawText(g, "This window can be closed at any time.", font,
                new Point(32, 387), Color.FromArgb(102, 113, 133));
            TextRenderer.DrawText(g, "mad-dc", font,
                new Point(Width - 80, 387), Color.FromArgb(89, 142, 255));
        }

        private sealed class CloseButton : Control
        {
            private readonly Form form;
            public CloseButton(Form form)
            {
                this.form = form;
                Bounds = new Rectangle(form.ClientSize.Width - 52, 12, 36, 32);
                Anchor = AnchorStyles.Top | AnchorStyles.Right;
                Cursor = Cursors.Hand;
                BackColor = Color.FromArgb(8, 10, 15);
                TabStop = false;
                Click += (_, _) => form.Close();
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var pen = new Pen(Color.FromArgb(180, 210, 218, 230), 1.8f);
                e.Graphics.DrawLine(pen, 12, 10, 24, 22);
                e.Graphics.DrawLine(pen, 24, 10, 12, 22);
            }
        }

        private sealed class MinButton : Control
        {
            private readonly Form form;
            public MinButton(Form form)
            {
                this.form = form;
                Bounds = new Rectangle(form.ClientSize.Width - 94, 12, 36, 32);
                Anchor = AnchorStyles.Top | AnchorStyles.Right;
                Cursor = Cursors.Hand;
                BackColor = Color.FromArgb(8, 10, 15);
                TabStop = false;
                Click += (_, _) => form.WindowState = FormWindowState.Minimized;
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                using var pen = new Pen(Color.FromArgb(180, 210, 218, 230), 1.8f);
                e.Graphics.DrawLine(pen, 11, 16, 25, 16);
            }
        }
    }

    internal static class GraphicsExtensions
    {
        public static void FillRoundedRectangle(this Graphics g, Brush brush, Rectangle r, int radius)
        {
            using var path = RoundedPath(r, radius);
            g.FillPath(brush, path);
        }
        public static void DrawRoundedRectangle(this Graphics g, Pen pen, Rectangle r, int radius)
        {
            using var path = RoundedPath(r, radius);
            g.DrawPath(pen, path);
        }
        private static GraphicsPath RoundedPath(Rectangle r, int radius)
        {
            var d = radius * 2;
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
