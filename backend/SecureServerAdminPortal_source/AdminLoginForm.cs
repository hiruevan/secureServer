using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.IO;
using System.Diagnostics;

namespace SecureServerCommand
{
    public partial class AdminLoginForm : Form
    {
        #region Properties
        private TextBox usernameBox;
        private TextBox passwordBox;
        private Button loginButton;
        private Button cancelButton;
        private Label titleLabel;
        private Label usernameLabel;
        private Label passwordLabel;
        private Label errorLabel;

        private const int MAX_LOGIN_ATTEMPTS = 5;
        private int loginAttempts = 0;

        public bool LoginSuccessful { get; private set; } = false;
        #endregion

        #region Constructors
        public AdminLoginForm()
        {
            InitializeComponents();
            EnableDarkMode();
            SetControlDarkMode(this);
        }
        private void InitializeComponents()
        {
            this.Text = "SecureServer Login";
            this.Size = new Size(450, 400);
            this.Icon = Properties.Resources.favicon;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(35, 35, 35);

            this.FormBorderStyle = FormBorderStyle.None;
            this.ControlBox = false;

            // Title
            titleLabel = new Label
            {
                Text = "SecureServer Administrator Login",
                Location = new Point(0, 30),
                Size = new Size(450, 40),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 16, FontStyle.Bold)
            };

            // Username
            usernameLabel = new Label
            {
                Text = "Username:",
                Location = new Point(50, 100),
                AutoSize = true,
                ForeColor = Color.FromArgb(200, 200, 200),
                Font = new Font("Segoe UI", 10)
            };

            usernameBox = new TextBox
            {
                Location = new Point(50, 125),
                Size = new Size(350, 30),
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 11)
            };

            // Password
            passwordLabel = new Label
            {
                Text = "Password:",
                Location = new Point(50, 165),
                AutoSize = true,
                ForeColor = Color.FromArgb(200, 200, 200),
                Font = new Font("Segoe UI", 10)
            };

            passwordBox = new TextBox
            {
                Location = new Point(50, 190),
                Size = new Size(350, 30),
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 11),
                UseSystemPasswordChar = true
            };
            passwordBox.KeyPress += PasswordBox_KeyPress;

            // Error label
            errorLabel = new Label
            {
                Text = "",
                Location = new Point(50, 255),
                Size = new Size(350, 20),
                ForeColor = Color.FromArgb(244, 67, 54),
                Font = new Font("Segoe UI", 9),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // Login button
            loginButton = new Button
            {
                Text = "Login",
                Location = new Point(50, 285),
                Size = new Size(100, 35),
                BackColor = Color.FromArgb(0, 100, 180),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            loginButton.FlatAppearance.BorderSize = 0;
            loginButton.Click += LoginButton_Click;

            // Cancel button
            cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(160, 285),
                Size = new Size(100, 35),
                BackColor = Color.FromArgb(70, 70, 70),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10)
            };
            cancelButton.FlatAppearance.BorderSize = 0;
            cancelButton.Click += CancelButton_Click;

            this.Controls.AddRange(new Control[] {
                titleLabel,
                usernameLabel, usernameBox,
                passwordLabel, passwordBox,
                errorLabel,
                loginButton, cancelButton
            });

            this.AcceptButton = loginButton;
            this.CancelButton = cancelButton;
        }
        #endregion

        #region Event Handlers
        private void PasswordBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                e.Handled = true;
                LoginButton_Click(sender, e);
            }
        }

        private void LoginButton_Click(object sender, EventArgs e)
        {
            string username = usernameBox.Text.Trim();
            string password = passwordBox.Text;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ShowError("Please enter both username and password.");
                return;
            }

            if (ValidateCredentials(username, password))
            {
                LoginSuccessful = true;

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                loginAttempts++;

                if (loginAttempts >= MAX_LOGIN_ATTEMPTS)
                {
                    MessageBox.Show(
                        "Maximum login attempts exceeded. Application will now close.",
                        "Access Denied",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                    Application.Exit();
                }
                else
                {
                    ShowError($"Invalid credentials. {MAX_LOGIN_ATTEMPTS - loginAttempts} attempt(s) remaining.");
                    passwordBox.Clear();
                    passwordBox.Focus();
                }
            }
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            LoginSuccessful = false;
            this.DialogResult = DialogResult.Cancel;
            Application.Exit();
        }
        #endregion
        #region Validation
        private bool ValidateCredentials(string username, string password)
        {
            try
            {
                string exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string scriptPath = Path.Combine(exeDirectory, "SecureServer", "adminPortal", "adminlogin.py");

                if (!File.Exists(scriptPath))
                {
                    MessageBox.Show(
                        $"Authentication script not found:\n{scriptPath}",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                    return false;
                }

                // Create process to run Python script
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{scriptPath}\" \"{username}\" \"{password}\"",
                    WorkingDirectory = exeDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (Process process = new Process { StartInfo = startInfo })
                {
                    process.Start();

                    // Read output
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();

                    process.WaitForExit();

                    string id = output.Trim();
                    MainForm.Session = new Session(id, username);

                    // Check exit code
                    if (process.ExitCode == 0)
                    {
                        MainForm.RootAuth = true;
                        return true;
                    }   
                    else if (process.ExitCode == 1)
                    {
                        MainForm.RootAuth = false;
                        return true;
                    }   
                    else if (process.ExitCode == 3)
                    {
                        // Root admin requires 2FA
                        string otp = PromptForOtp();
                        if (otp == null) return false;

                        return ValidateCredentialsWithOtp(username, password, otp);
                    }
                    else if (process.ExitCode == 5)
                    {
                        string qrData = output.Trim();
                        return Show2FASetup(username, password, qrData);
                    }
                    else
                    {
                        // Log error for debugging
                        if (!string.IsNullOrEmpty(error))
                        {
                            System.Diagnostics.Debug.WriteLine($"Auth script error: {error}");
                        }
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error running authentication: {ex.Message}\n\nMake sure Python is installed and in your PATH.",
                    "Authentication Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                return false;
            }
        }

        private bool Show2FASetup(string username, string password, string qrData)
        {
            using (var setupForm = new OtpForm(qrData))
            {
                if (setupForm.ShowDialog(this) != DialogResult.OK)
                    return false;

                return ValidateCredentialsWithOtp(
                    username,
                    password,
                    setupForm.OtpCode
                );
            }
        }

        private bool ValidateCredentialsWithOtp(string username, string password, string otp)
        {
            string exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string scriptPath = Path.Combine(exeDirectory, "SecureServer", "adminPortal", "adminlogin.py");

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"\"{scriptPath}\" \"{username}\" \"{password}\" \"{otp}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (Process p = Process.Start(startInfo))
            {
                string output = p.StandardOutput.ReadToEnd();

                p.WaitForExit();

                string id = output.Trim();
                MainForm.Session = new Session(id, username);

                if (p.ExitCode == 0)
                {
                    MainForm.RootAuth = true;
                    return true;
                }
                else if (p.ExitCode == 1)
                {
                    MainForm.RootAuth = false;
                    return true;
                }

                MessageBox.Show("Invalid 2FA code.", "Authentication Failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private string PromptForOtp()
        {
            using (OtpForm otpForm = new OtpForm())
            {
                return otpForm.ShowDialog(this) == DialogResult.OK
                    ? otpForm.OtpCode
                    : null;
            }
        }
        #endregion
        #region Helpers
        private void ShowError(string message)
        {
            errorLabel.Text = message;
            errorLabel.Visible = true;
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