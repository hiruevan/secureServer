using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using QRCoder;

namespace SecureServerCommand
{
    public class OtpForm : Form
    {
        #region Properties
        public string OtpCode => otpBox.Text.Trim();

        private TextBox otpBox;
        private Button verifyButton;
        private Button cancelButton;
        private Label titleLabel;
        private Label infoLabel;
        private PictureBox qrBox;
        private string qrData;
        #endregion

        #region Constructors
        public OtpForm(string qrData = null)
        {
            this.qrData = qrData;
            InitializeComponents();
            EnableDarkMode();
            SetControlDarkMode(this);
        }
        private void InitializeComponents()
        {
            // Form
            this.Text = "Two-Factor";
            this.Size = new Size(370, 220);
            this.Icon = Properties.Resources.favicon;
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(25, 25, 25);

            this.FormBorderStyle = FormBorderStyle.None;
            this.ControlBox = false;

            // Title
            titleLabel = new Label
            {
                Text = "Two-Factor Authentication",
                Dock = DockStyle.Top,
                Height = 45,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 14, FontStyle.Bold)
            };

            // Info
            infoLabel = new Label
            {
                Text = "Enter the 6-digit code from your authenticator app.",
                Location = new Point(20, 55),
                Size = new Size(320, 35),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(200, 200, 200),
                Font = new Font("Segoe UI", 9)
            };

            // OTP box
            otpBox = new TextBox
            {
                Location = new Point(80, 95),
                Width = 200,
                Font = new Font("Segoe UI", 14),
                TextAlign = HorizontalAlignment.Center,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                MaxLength = 6
            };
            otpBox.KeyPress += OtpBox_KeyPress;

            // Verify
            verifyButton = new Button
            {
                Text = "Verify",
                Location = new Point(80, 140),
                Size = new Size(90, 32),
                DialogResult = DialogResult.OK,
                BackColor = Color.FromArgb(0, 100, 180),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            verifyButton.FlatAppearance.BorderSize = 0;

            // Cancel
            cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(190, 140),
                Size = new Size(90, 32),
                DialogResult = DialogResult.Cancel,
                BackColor = Color.FromArgb(70, 70, 70),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            cancelButton.FlatAppearance.BorderSize = 0;

            this.AcceptButton = verifyButton;
            this.CancelButton = cancelButton;

            this.Controls.AddRange(new Control[]
            {
                titleLabel,
                infoLabel,
                otpBox,
                verifyButton,
                cancelButton
            });

            if (qrData != null)
            {
                this.Text = "Two-Factor Setup";
                this.Size = new Size(370, 550);
                titleLabel.Text = "Two-Factor Setup";
                infoLabel.Text = "Scan the QR code with your authenticator app.";
                otpBox.Location = new Point(80, 420);
                verifyButton.Location = new Point(80, 460);
                cancelButton.Location = new Point(190, 460);

                qrBox = new PictureBox
                {
                    Location = new Point(20, 100),
                    Size = new Size(300, 300),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = Color.White,
                };

                var qrGen = new QRCodeGenerator();
                var qr = new QRCode(qrGen.CreateQrCode(qrData, QRCodeGenerator.ECCLevel.Q));
                qrBox.Image = qr.GetGraphic(12);

                Controls.Add(qrBox);
            }
        }
        #endregion

        #region Event Handlers
        private void OtpBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            // Only allow digits
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
            {
                e.Handled = true;
            }
        }
        #endregion
        #region Dark Mode
        [DllImport("uxtheme.dll", SetLastError = true)]
        private static extern bool SetPreferredAppMode(PreferredAppMode appMode);
        [DllImport("uxtheme.dll", SetLastError = true)]
        private static extern bool AllowDarkModeForWindow(IntPtr hWnd, bool allow);
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_NEW = 38;

        private enum PreferredAppMode
        {
            Default,
            AllowDark,
            ForceDark,
            ForceLight,
            Max
        }

        private void EnableDarkMode()
        {
            try
            {
                SetPreferredAppMode(PreferredAppMode.ForceDark);
                AllowDarkModeForWindow(this.Handle, true);
            }
            catch { }
        }

        private void SetControlDarkMode(Control control)
        {
            if (control.Handle != IntPtr.Zero)
            {
                int useDarkMode = 1;
                int result = DwmSetWindowAttribute(control.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE_NEW, ref useDarkMode, sizeof(int));
                if (result != 0)
                {
                    DwmSetWindowAttribute(control.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));
                }
            }

            foreach (Control child in control.Controls)
            {
                SetControlDarkMode(child);
            }
        }
        #endregion
    }
}
