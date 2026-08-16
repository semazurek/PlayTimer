using PT2.Properties;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PT2
{
    public class LoginForm : Form
    {
        private TextBox txtPassword;
        private Button btnLogin;
        private Label lblInfo;

        private Panel titleBar;
        private Label lblWindowTitle;
        private Button btnClose;

        public bool IsAuthenticated { get; private set; } = false;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(
            IntPtr hWnd,
            int Msg,
            IntPtr wParam,
            IntPtr lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        public LoginForm()
        {
            InitializeModernUI();

            AppManager.EnsureDirectories();

            if (AppManager.IsPasswordSet())
            {
                lblInfo.Text =
                    "Enter the PlayTimer access password:";

                btnLogin.Text = "Log in";
            }
            else
            {
                lblInfo.Text =
                    "First launch. Create a master password:";

                btnLogin.Text = "Save and Log In";
            }
        }

        private void InitializeModernUI()
        {

            this.Text = "Authorization - PlayTimer";

            this.ClientSize = new Size(420, 240);

            this.BackColor =
                Color.FromArgb(24, 24, 27);

            this.ForeColor = Color.White;

            this.FormBorderStyle =
                FormBorderStyle.None;

            this.StartPosition =
                FormStartPosition.CenterScreen;

            this.Icon = Resources.PTIcon;

            this.MaximizeBox = false;
            this.MinimizeBox = false;


            Panel cardPanel = new Panel()
            {
                Dock = DockStyle.Fill,

                BackColor =
                    Color.FromArgb(24, 24, 27),

                Padding =
                    new Padding(25),

                Margin =
                    new Padding(0)
            };

            lblInfo = new Label()
            {
                Location = new Point(25, 45),

                Size = new Size(350, 45),

                ForeColor =
                    Color.FromArgb(210, 210, 215),

                Font =
                    new Font(
                        "Segoe UI",
                        10F,
                        FontStyle.Regular),

                TextAlign =
                    ContentAlignment.MiddleLeft
            };

            txtPassword = new TextBox()
            {
                Location = new Point(25, 100),

                Size = new Size(350, 32),

                UseSystemPasswordChar = true,

                BackColor =
                    Color.FromArgb(38, 38, 42),

                ForeColor = Color.White,

                BorderStyle =
                    BorderStyle.FixedSingle,

                Font =
                    new Font(
                        "Segoe UI",
                        11F)
            };

            btnLogin = new Button()
            {
                Location = new Point(25, 160),

                Size = new Size(350, 42),

                FlatStyle =
                    FlatStyle.Flat,

                BackColor =
                    Color.FromArgb(0, 122, 204),

                ForeColor = Color.White,

                Font =
                    new Font(
                        "Segoe UI",
                        10F,
                        FontStyle.Bold),

                Cursor =
                    Cursors.Hand,

                TabStop = true
            };

            btnLogin.FlatAppearance.BorderSize = 0;

            btnLogin.FlatAppearance.MouseOverBackColor =
                Color.FromArgb(20, 140, 220);

            btnLogin.FlatAppearance.MouseDownBackColor =
                Color.FromArgb(0, 105, 180);

            btnLogin.Click += BtnLogin_Click;

            cardPanel.Controls.Add(lblInfo);
            cardPanel.Controls.Add(txtPassword);
            cardPanel.Controls.Add(btnLogin);

            titleBar = new Panel()
            {
                Dock = DockStyle.Top,

                Height = 42,

                BackColor =
                    Color.FromArgb(20, 20, 23)
            };

            titleBar.MouseDown += TitleBar_MouseDown;

            lblWindowTitle = new Label()
            {
                Text = "PlayTimer",

                Location =
                    new Point(15, 0),

                Size =
                    new Size(320, 42),

                ForeColor =
                    Color.FromArgb(235, 235, 240),

                Font =
                    new Font(
                        "Segoe UI",
                        10F,
                        FontStyle.Bold),

                TextAlign =
                    ContentAlignment.MiddleLeft,

                BackColor =
                    Color.Transparent
            };

            lblWindowTitle.MouseDown +=
                TitleBar_MouseDown;

            btnClose = new Button()
            {
                Text = "×",

                Dock = DockStyle.Right,

                Width = 46,

                FlatStyle =
                    FlatStyle.Flat,

                BackColor =
                    Color.FromArgb(20, 20, 23),

                ForeColor =
                    Color.FromArgb(180, 195, 205),

                Font =
                    new Font(
                        "Segoe UI",
                        15F,
                        FontStyle.Regular),

                Cursor =
                    Cursors.Hand,

                TabStop = false
            };

            btnClose.FlatAppearance.BorderSize = 0;

            btnClose.FlatAppearance.MouseOverBackColor =
                Color.FromArgb(190, 45, 45);

            btnClose.FlatAppearance.MouseDownBackColor =
                Color.FromArgb(160, 35, 35);

            btnClose.Click +=
                BtnClose_Click;

            titleBar.Controls.Add(btnClose);
            titleBar.Controls.Add(lblWindowTitle);

            this.Controls.Add(cardPanel);
            this.Controls.Add(titleBar);

            titleBar.BringToFront();

            this.Shown += (s, e) =>
            {
                txtPassword.Focus();
            };
        }

        private void TitleBar_MouseDown(
            object sender,
            MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            ReleaseCapture();

            SendMessage(
                this.Handle,
                WM_NCLBUTTONDOWN,
                (IntPtr)HTCAPTION,
                IntPtr.Zero);
        }

        private void BtnClose_Click(
            object sender,
            EventArgs e)
        {
            IsAuthenticated = false;

            this.Close();
        }

        private void BtnLogin_Click(
            object sender,
            EventArgs e)
        {
            string password =
                txtPassword.Text;

            if (string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show(
                    "Enter the password.",
                    "PlayTimer",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                txtPassword.Focus();

                return;
            }

            if (AppManager.IsPasswordSet())
            {
                if (AppManager.VerifyPassword(password))
                {
                    IsAuthenticated = true;

                    this.DialogResult =
                        DialogResult.OK;

                    this.Close();
                }
                else
                {
                    MessageBox.Show(
                        "Incorrect password!",
                        "Błąd",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);

                    txtPassword.SelectAll();
                    txtPassword.Focus();
                }
            }

            else
            {
                AppManager.SetPassword(password);

                IsAuthenticated = true;

                this.DialogResult =
                    DialogResult.OK;

                this.Close();
            }
        }

        protected override bool ProcessCmdKey(
            ref Message msg,
            Keys keyData)
        {
            if (keyData == Keys.Enter)
            {
                BtnLogin_Click(
                    this,
                    EventArgs.Empty);

                return true;
            }

            if (keyData == Keys.Escape)
            {
                IsAuthenticated = false;

                this.Close();

                return true;
            }

            return base.ProcessCmdKey(
                ref msg,
                keyData);
        }
    }
}