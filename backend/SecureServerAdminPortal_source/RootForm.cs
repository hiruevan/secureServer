using CustomControls;
using ServerLogViewer;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SecureServerCommand
{
    public class RootForm : Form
    {
        #region Properties
        private ListView entryListView;
        private TextBox searchBox;
        private ComboBox databaseSelected;
        private Button refreshButton;
        private Button createUserButton;
        private RichTextBox detailsBox;
        private Label statusLabel;

        private MenuStrip navigationBar;
        private bool isFullscreen = false;
        private Rectangle windowedBounds;

        private User selectedUser;
        private Token selectedSession;
        #endregion

        #region Constructors
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;
        public RootForm()
        {
            if (!MainForm.RootAuth)
                Application.Exit(); // Fail safe if somehow the form is opened.

            // Initialize window
            InitializeComponents();
            EnableDarkMode();
            SetControlDarkMode(this);

            this.Load += (_, __) => ApplyImmersiveScrollbars(this);

            RefreshButton_Click(null, null);
        }
        private void EnableDrag(Control control)
        {
            control.MouseDown += (_, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(this.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
                }
            };
        }
        private void InitializeComponents()
        {
            this.Icon = Properties.Resources.favicon;
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.WindowsDefaultLocation;
            this.BackColor = Color.FromArgb(25, 25, 25);

            this.FormBorderStyle = FormBorderStyle.None;
            this.ControlBox = false;
            this.Text = "Root Control Panel";

            // --- Custom Title Bar ---
            Panel titleBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 32,
                BackColor = Color.FromArgb(35, 35, 35)
            };

            Button closeButton = new Button
            {
                Text = "✕",
                Dock = DockStyle.Right,
                Width = 40,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(35, 35, 35)
            };
            closeButton.FlatAppearance.BorderSize = 0;
            closeButton.Click += (_, __) => this.Close();

            Button fullscreenButton = new Button
            {
                Text = "⬜", // restore icon
                Dock = DockStyle.Right,
                Width = 40,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(35, 35, 35)
            };
            fullscreenButton.FlatAppearance.BorderSize = 0;
            fullscreenButton.Click += (_, __) =>
            {
                if (!isFullscreen)
                {
                    windowedBounds = this.Bounds;

                    this.TopMost = false; // optional
                    this.FormBorderStyle = FormBorderStyle.None;
                    this.Bounds = Screen.FromControl(this).Bounds;

                    fullscreenButton.Text = "❐"; // restore icon
                    isFullscreen = true;
                }
                else
                {
                    this.Bounds = windowedBounds;

                    fullscreenButton.Text = "⬜";
                    isFullscreen = false;
                }
            };

            Button minimizeButton = new Button
            {
                Text = "–",
                Dock = DockStyle.Right,
                Width = 40,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(35, 35, 35)
            };
            minimizeButton.FlatAppearance.BorderSize = 0;
            minimizeButton.Click += (_, __) => this.WindowState = FormWindowState.Minimized;

            
            titleBar.Controls.Add(minimizeButton);
            titleBar.Controls.Add(fullscreenButton);
            titleBar.Controls.Add(closeButton);

            // --- Navigation bar ---
            navigationBar = new MenuStrip
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(35, 35, 35),
                ForeColor = Color.White,
                Renderer = new DarkMenuRenderer(),
            };

            ToolStripMenuItem fileMenu = new ToolStripMenuItem("File");
            ToolStripMenuItem databaseMenu = new ToolStripMenuItem("Database");

            ToolStripMenuItem userMenu = new ToolStripMenuItem("User");
            ToolStripMenuItem permissionsMenu = new ToolStripMenuItem("Permissions");
            ToolStripMenuItem perAppMenu = new ToolStripMenuItem("App");
            ToolStripMenuItem perDevMenu = new ToolStripMenuItem("Dev");

            ToolStripMenuItem attemptsMenu = new ToolStripMenuItem("Failed Attempts");

            ToolStripMenuItem sessionsMenu = new ToolStripMenuItem("Sessions");

            fileMenu.DropDownItems.Add(databaseMenu);
            fileMenu.DropDownItems.Add("Refresh", null, RefreshButton_Click);

            userMenu.DropDownItems.Add("Create New", null, createUserButton_Click);
            userMenu.DropDownItems.Add("Freeze", null, FreezeUser_Click);
            userMenu.DropDownItems.Add("Unfreeze", null, UnfreezeUser_Click);
            userMenu.DropDownItems.Add(permissionsMenu);
            userMenu.DropDownItems.Add("Clear Attempts", null, FreezeUser_Click);
            userMenu.DropDownItems.Add("Logout", null, LogoutSelected_Click);

            permissionsMenu.DropDownItems.Add(perAppMenu);
            permissionsMenu.DropDownItems.Add(perDevMenu);

            perAppMenu.DropDownItems.Add("Make Admin", null, AppAdmin_Click);
            perAppMenu.DropDownItems.Add("Revoke Admin", null, TakeAppAdmin_Click);

            perDevMenu.DropDownItems.Add("Make Developer", null, DevAdmin_Click);
            perDevMenu.DropDownItems.Add("Revoke Developer", null, TakeDevAdmin_Click);
            perDevMenu.DropDownItems.Add("Grant Root", null, RootAuth_Click);
            perDevMenu.DropDownItems.Add("Revoke Root", null, TakeRootAuth_Click);

            attemptsMenu.DropDownItems.Add("Clear All", null, ClearAllAttempts_Click);

            sessionsMenu.DropDownItems.Add("Logout All", null, LogoutAll_Click);
            sessionsMenu.DropDownItems.Add("Logout Selected", null, LogoutSelected_Click);
            sessionsMenu.DropDownItems.Add("View Self", null, ViewOwnSession_Click);

            navigationBar.Items.Add(fileMenu);
            navigationBar.Items.Add(userMenu);
            navigationBar.Items.Add(attemptsMenu);
            navigationBar.Items.Add(sessionsMenu);

            navigationBar.Padding = new Padding(8, 3, 8, 3);
            navigationBar.Font = new Font("Segoe UI", 9);

            EnableDrag(titleBar);
            EnableDrag(navigationBar);

            foreach (ToolStripMenuItem menu in navigationBar.Items)
            {
                ApplyDarkMenu(menu);
            }

            void ApplyDarkMenu(ToolStripMenuItem item)
            {
                item.ForeColor = Color.White;
                item.DropDown.BackColor = Color.FromArgb(30, 30, 30);

                foreach (ToolStripItem sub in item.DropDownItems)
                {
                    sub.ForeColor = Color.White;
                    if (sub is ToolStripMenuItem subMenu)
                        ApplyDarkMenu(subMenu);
                }
            }

            titleBar.Controls.Add(navigationBar);

            // Top panel with controls
            Panel topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                Padding = new Padding(10),
                BackColor = Color.FromArgb(35, 35, 35)
            };

            refreshButton = new Button
            {
                Text = "Refresh",
                Location = new Point(10, 10),
                Size = new Size(80, 30),
                BackColor = Color.FromArgb(0, 100, 180),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9)
            };
            refreshButton.FlatAppearance.BorderSize = 0;
            refreshButton.Click += RefreshButton_Click;

            createUserButton = new Button
            {
                Text = "Create New User",
                Location = new Point(100, 10),
                Size = new Size(150, 30),
                BackColor = Color.FromArgb(120, 60, 180),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9)
            };
            createUserButton.FlatAppearance.BorderSize = 0;
            createUserButton.Click += createUserButton_Click;
            
            

            Label searchLabel = new Label
            {
                Text = "Search:",
                Location = new Point(10, 48),
                AutoSize = true,
                ForeColor = Color.FromArgb(200, 200, 200),
                Font = new Font("Segoe UI", 9)
            };

            searchBox = new TextBox
            {
                Location = new Point(70, 45),
                Size = new Size(150, 25),
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9)
            };
            searchBox.TextChanged += SearchBox_TextChanged;

            Label filterLabel = new Label
            {
                Text = "Type:",
                Location = new Point(230, 48),
                AutoSize = true,
                ForeColor = Color.FromArgb(200, 200, 200),
                Font = new Font("Segoe UI", 9)
            };

            databaseSelected = new DarkComboBox
            {
                Location = new Point(280, 45),
                Size = new Size(135, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9)
            };
            databaseSelected.Items.AddRange(new object[] {
                "USERS", "FAILED ATTEMPTS", "SESSIONS"
            });
            databaseSelected.SelectedIndex = 0;
            databaseSelected.SelectedIndexChanged += databaseSelected_SelectedIndexChanged;

            statusLabel = new Label
            {
                Text = "No file loaded",
                Location = new Point(430, 48),
                AutoSize = true,
                ForeColor = Color.FromArgb(150, 150, 150),
                Font = new Font("Segoe UI", 9)
            };

            topPanel.Controls.AddRange(new Control[] {
                refreshButton, createUserButton,
                searchLabel, searchBox,
                filterLabel, databaseSelected,
                statusLabel
            });

            // Split container for list and details
            SplitContainer splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 500,
                BackColor = Color.FromArgb(25, 25, 25),
                Panel1MinSize = 200,
                Panel2MinSize = 100
            };
            splitContainer.Panel1.BackColor = Color.FromArgb(30, 30, 30);
            splitContainer.Panel2.BackColor = Color.FromArgb(25, 25, 25);

            // Log list view
            entryListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                MultiSelect = false,
                Font = new Font("Consolas", 9),
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                OwnerDraw = true
            };
            entryListView.SelectedIndexChanged += entryListView_SelectedIndexChanged;
            entryListView.DrawColumnHeader += entryListView_DrawColumnHeader;
            entryListView.DrawSubItem += entryListView_DrawSubItem;
            entryListView.ColumnWidthChanged += (s, e) => AutoSizeLastColumn();

            // Details box
            detailsBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new Font("Consolas", 10),
                BackColor = Color.FromArgb(25, 25, 25),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None
            };

            Label detailsLabel = new Label
            {
                Text = "Details:",
                Dock = DockStyle.Top,
                Padding = new Padding(5),
                BackColor = Color.FromArgb(35, 35, 35),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };

            splitContainer.Panel1.Controls.Add(entryListView);
            splitContainer.Panel2.Controls.AddRange(new Control[] { detailsBox, detailsLabel });

            this.Controls.Add(splitContainer);
            this.Controls.Add(topPanel);
            this.Controls.Add(titleBar);
        }

        #endregion

        #region Event Handlers

        private void RefreshButton_Click(object sender, EventArgs e)
        {
            GetDatabase(databaseSelected.Text);
        }
        private void SearchBox_TextChanged(object sender, EventArgs e) 
        {

        }
        private void databaseSelected_SelectedIndexChanged(object sender, EventArgs e)
        {
            RefreshButton_Click(null, null);
        }
        private void entryListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (entryListView.SelectedItems.Count == 0)
            {
                selectedUser = null;
                selectedSession = null;
                return;
            }

            selectedUser = entryListView.SelectedItems[0].Tag as User;
            selectedSession = entryListView.SelectedItems[0].Tag as Token;

            // Update details view
            UpdateDetailsView();
        }
        private void createUserButton_Click(object sender, EventArgs e)
        {
            using (var form = new CreateUserForm())
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    RefreshButton_Click(null, null); // refresh USERS list
                }
            }
        }
        private void ClearAllAttempts_Click(object sender, EventArgs e)
        {
            string exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string scriptPath = Path.Combine(
                exeDirectory,
                "SecureServer",
                "adminPortal",
                "clearallattempts.py"
            );

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "python",
                Arguments =
                    $"\"{scriptPath}\" \"{MainForm.Session.Id}\"",
                WorkingDirectory = exeDirectory,
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = Process.Start(psi))
            {
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    MessageBox.Show(error, "Error while clearing attempts.",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            RefreshButton_Click(null, null);
        }
        private void FreezeUser_Click(object sender, EventArgs e)
        {
            RunUserAction("freeze");
        }
        private void UnfreezeUser_Click(object sender, EventArgs e)
        {
            RunUserAction("unfreeze");
        }
        private void AppAdmin_Click(object sender, EventArgs e)
        {
            RunUserAction("promote_app_admin");
        }
        private void TakeAppAdmin_Click(object sender, EventArgs args)
        {
            RunUserAction("demote_app_admin");
        }
        private void DevAdmin_Click(object sender, EventArgs e)
        {
            RunUserAction("promote_dev_admin");
        }
        private void TakeDevAdmin_Click(object sender, EventArgs e)
        {
            RunUserAction("demote_dev_admin");
        }
        private void RootAuth_Click(object sender, EventArgs e)
        {
            RunUserAction("grant_root_auth");
        }
        private void TakeRootAuth_Click(object sender, EventArgs e)
        {
            RunUserAction("revoke_root_auth");
        }
        private void ClearAttempts_Click(object sender, EventArgs e)
        {
            RunUserAction("clear_attempts");
        }
        private void ViewOwnSession_Click(object sender, EventArgs e)
        {
            MessageBox.Show(MainForm.Session.ToString());
        }
        private void LogoutAll_Click(object sender, EventArgs e)
        {
            string exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string scriptPath = Path.Combine(
                exeDirectory,
                "SecureServer",
                "adminPortal",
                "logoutall.py"
            );

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "python",
                Arguments =
                    $"\"{scriptPath}\" \"{MainForm.Session.Id}\"",
                WorkingDirectory = exeDirectory,
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = Process.Start(psi))
            {
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    MessageBox.Show(error, "Error loging out sessions.",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            Application.Exit();
        }
        private void LogoutSelected_Click(object sender, EventArgs e)
        {
            if (entryListView.SelectedItems.Count == 0)
                return;

            string id = null;

            if (entryListView.SelectedItems[0].Tag is User)
            {
                id = selectedUser.Id;
            } else if (entryListView.SelectedItems[0].Tag is Token)
            {
                id = selectedSession.UserId;
            }

            if (id == null)
                return;

            string exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string scriptPath = Path.Combine(
                exeDirectory,
                "SecureServer",
                "adminPortal",
                "logout.py"
            );

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "python",
                Arguments =
                    $"\"{scriptPath}\" \"{MainForm.Session.Id}\" \"{id}\"",
                WorkingDirectory = exeDirectory,
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = Process.Start(psi))
            {
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    MessageBox.Show(error, "Error loging out sessions.",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            RefreshButton_Click(null, null);

        }
        #endregion
        #region Overrides
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            AutoSizeLastColumn();
        }
        #endregion
        #region Details View
        private void UpdateDetailsView()
        {
            detailsBox.Clear();

            if (selectedUser != null)
            {
                DisplayUserDetails(selectedUser);
            }
            else if (selectedSession != null)
            {
                DisplaySessionDetails(selectedSession);
            }
            else if (entryListView.SelectedItems.Count > 0 &&
                     entryListView.SelectedItems[0].Tag is AttemptEntry attempt)
            {
                DisplayAttemptDetails(attempt);
            }
        }

        private void DisplayUserDetails(User user)
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════");
            sb.AppendLine("                    USER DETAILS                   ");
            sb.AppendLine("═══════════════════════════════════════════════════");
            sb.AppendLine();

            sb.AppendLine($"User ID:            {user.Id}");
            sb.AppendLine($"Username:        {user.Username}");
            sb.AppendLine($"Full Name:        {user.Name}");
            sb.AppendLine();

            sb.AppendLine("───────────────────────────────────────────────────");
            sb.AppendLine("  PERMISSIONS & STATUS");
            sb.AppendLine("───────────────────────────────────────────────────");
            sb.AppendLine($"App Admin:         {(user.IsAdmin ? "✓ YES" : "✗ NO")}");
            sb.AppendLine($"Dev Admin:         {(user.DevAdmin ? "✓ YES" : "✗ NO")}");
            sb.AppendLine($"Root Access:       {(user.RootAuth ? "✓ YES" : "✗ NO")}");
            sb.AppendLine($"Account Status:   {(user.Disabled ? "⚠ DISABLED" : "✓ ACTIVE")}");
            sb.AppendLine();

            sb.AppendLine("───────────────────────────────────────────────────");
            sb.AppendLine("  SECURITY");
            sb.AppendLine("───────────────────────────────────────────────────");
            sb.AppendLine($"2FA Enabled:      {(user.TwoFAEnabled ? "✓ YES" : "✗ NO")}");
            sb.AppendLine($"Failed Attempts:   {user.FailedAttempts}");
            sb.AppendLine();

            sb.AppendLine("───────────────────────────────────────────────────");
            sb.AppendLine("  STORAGE");
            sb.AppendLine("───────────────────────────────────────────────────");
            sb.AppendLine($"Vault Size:       {FormatBytes(user.VaultSize)}");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(user.Email) || !string.IsNullOrEmpty(user.Phone))
            {
                sb.AppendLine("───────────────────────────────────────────────────");
                sb.AppendLine("  CONTACT");
                sb.AppendLine("───────────────────────────────────────────────────");
                sb.AppendLine($"Email:            {user.Email}");
                sb.AppendLine($"Phone:            {user.Phone}");
                sb.AppendLine($"Preferred Method: {user.PreferredContact}");
                sb.AppendLine();
            }

            detailsBox.Text = sb.ToString();
            ColorizeDetails();
        }

        private void DisplaySessionDetails(Token session)
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════");
            sb.AppendLine("                   SESSION DETAILS                  ");
            sb.AppendLine("═══════════════════════════════════════════════════");
            sb.AppendLine();

            sb.AppendLine($"Session ID:        {session.Id}");
            sb.AppendLine($"Token Value:      {session.Value}");
            sb.AppendLine();

            sb.AppendLine("───────────────────────────────────────────────────");
            sb.AppendLine("  USER INFORMATION");
            sb.AppendLine("───────────────────────────────────────────────────");
            sb.AppendLine($"User ID:             {session.UserId}");
            sb.AppendLine($"Username:         {session.Username}");
            sb.AppendLine();

            sb.AppendLine("───────────────────────────────────────────────────");
            sb.AppendLine("  TIMING");
            sb.AppendLine("───────────────────────────────────────────────────");
            sb.AppendLine($"Login Time:       {session.LoginTime}");

            if (DateTime.TryParse(session.LoginTime, out DateTime loginTime))
            {
                var duration = DateTime.Now - loginTime;
                sb.AppendLine($"Duration:          {FormatDuration(duration)}");
            }
            sb.AppendLine();

            detailsBox.Text = sb.ToString();
            ColorizeDetails();
        }

        private void DisplayAttemptDetails(AttemptEntry attempt)
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════");
            sb.AppendLine("                FAILED ATTEMPT DETAILS              ");
            sb.AppendLine("═══════════════════════════════════════════════════");
            sb.AppendLine();

            sb.AppendLine($"User:                 {(string.IsNullOrEmpty(attempt.User) ? "Unknown" : attempt.User)}");
            sb.AppendLine($"Attempt Time:     {attempt.Time}");
            sb.AppendLine();

            if (DateTime.TryParse(attempt.Time, out DateTime attemptTime))
            {
                var elapsed = DateTime.Now - attemptTime;
                sb.AppendLine($"Time Elapsed:     {FormatDuration(elapsed)} ago");
            }

            detailsBox.Text = sb.ToString();
            ColorizeDetails();
        }

        private void ColorizeDetails()
        {
            // Color key headers
            ColorizeText("USER DETAILS", Color.FromArgb(100, 200, 255));
            ColorizeText("SESSION DETAILS", Color.FromArgb(3, 169, 244));
            ColorizeText("FAILED ATTEMPT DETAILS", Color.FromArgb(255, 100, 100));

            // Color section headers
            ColorizeText("PERMISSIONS & STATUS", Color.FromArgb(200, 200, 200));
            ColorizeText("SECURITY", Color.FromArgb(200, 200, 200));
            ColorizeText("STORAGE", Color.FromArgb(200, 200, 200));
            ColorizeText("CONTACT", Color.FromArgb(200, 200, 200));
            ColorizeText("USER INFORMATION", Color.FromArgb(200, 200, 200));
            ColorizeText("TIMING", Color.FromArgb(200, 200, 200));

            // Color status indicators
            ColorizeText("✓ YES", Color.LightGreen);
            ColorizeText("✗ NO", Color.FromArgb(255, 100, 100));
            ColorizeText("✓ ACTIVE", Color.LightGreen);
            ColorizeText("⚠ DISABLED", Color.Orange);
        }

        private void ColorizeText(string text, Color color)
        {
            int start = 0;
            while (start < detailsBox.TextLength)
            {
                int index = detailsBox.Find(text, start, RichTextBoxFinds.None);
                if (index == -1) break;

                detailsBox.Select(index, text.Length);
                detailsBox.SelectionColor = color;
                start = index + text.Length;
            }

            // Reset selection
            detailsBox.Select(0, 0);
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalDays >= 1)
                return $"{(int)duration.TotalDays}d {duration.Hours}h {duration.Minutes}m";
            else if (duration.TotalHours >= 1)
                return $"{(int)duration.TotalHours}h {duration.Minutes}m";
            else if (duration.TotalMinutes >= 1)
                return $"{(int)duration.TotalMinutes}m {duration.Seconds}s";
            else
                return $"{duration.Seconds}s";
        }
        #endregion
        #region Database Parsing
        private void GetDatabase(string databaseName)
        {
            loadingDatabase = true;
            entryListView.BeginUpdate(); // prevent flickering

            try
            {
                // Clear existing items
                entryListView.Items.Clear();

                if (databaseName == "USERS")
                {
                    // Update columns for USERS
                    entryListView.Columns.Clear();
                    entryListView.Columns.Add("ID", 120);
                    entryListView.Columns.Add("Username", 150);
                    entryListView.Columns.Add("Name", 200);
                    entryListView.Columns.Add("Admin", 80);
                    entryListView.Columns.Add("2FA Enabled", 80);
                    entryListView.Columns.Add("Vault Size", 80);
                    entryListView.Columns.Add("Disabled", 80);

                    // Run Python script
                    string exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    string scriptPath = Path.Combine(exeDirectory, "SecureServer", "adminPortal", "listusers.py");

                    if (!File.Exists(scriptPath))
                    {
                        MessageBox.Show(
                            $"List Users database script not found:\n{scriptPath}",
                            "Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error
                        );
                        throw new Exception();
                    }

                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = "python",
                        Arguments = $"\"{scriptPath}\" \"{MainForm.Session.Id}\"",
                        WorkingDirectory = exeDirectory,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    using (Process process = Process.Start(psi))
                    {
                        string output = process.StandardOutput.ReadToEnd();
                        process.WaitForExit();

                        if (process.ExitCode != 0)
                        {
                            MessageBox.Show(
                                $"Error fetching the users database.",
                                "Error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error
                            );
                            throw new Exception();
                        }

                        var users = JsonSerializer.Deserialize<List<User>>(output);

                        foreach (var user in users)
                        {
                            ListViewItem item = new ListViewItem(user.Id);
                            item.SubItems.Add(user.Username);
                            item.SubItems.Add(user.Name);
                            item.SubItems.Add(user.IsAdmin.ToString());
                            item.SubItems.Add(user.TwoFAEnabled.ToString());
                            item.SubItems.Add(user.VaultSize.ToString());
                            item.SubItems.Add(user.Disabled.ToString());

                            item.Tag = user;
                            entryListView.Items.Add(item);
                        }
                        statusLabel.Text = $"Loaded {users.Count} log entries from users.json";
                        statusLabel.ForeColor = Color.LightGreen;
                    }
                }
                else if (databaseName == "FAILED ATTEMPTS")
                {
                    entryListView.Columns.Clear();
                    entryListView.Columns.Add("User", 150);
                    entryListView.Columns.Add("Attempt Time", 300);

                    string exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    string scriptPath = Path.Combine(exeDirectory, "SecureServer", "adminPortal", "listattempts.py");

                    if (!File.Exists(scriptPath))
                    {
                        MessageBox.Show(
                            $"List Attempts database script not found:\n{scriptPath}",
                            "Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error
                        );
                        throw new Exception();
                    }
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = "python",
                        Arguments = $"\"{scriptPath}\" \"{MainForm.Session.Id}\"",
                        WorkingDirectory = exeDirectory,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    using (Process process = Process.Start(psi))
                    {
                        string output = process.StandardOutput.ReadToEnd();
                        process.WaitForExit();

                        if (process.ExitCode != 0)
                        {
                            MessageBox.Show(
                                $"Error fetching the attempts database.",
                                "Error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error
                            );
                            throw new Exception();
                        }

                        var attempts = JsonSerializer.Deserialize<List<AttemptEntry>>(output);

                        foreach (var attempt in attempts)
                        {
                            ListViewItem item;
                            if (attempt.Time == null)
                                item = new ListViewItem(attempt.User);
                            else
                                item = new ListViewItem();
                            item.SubItems.Add(attempt.Time);

                            item.Tag = attempt;
                            entryListView.Items.Add(item);
                        }
                        statusLabel.Text = $"Loaded {attempts.Count} log entries from failed_attempts.json";
                        statusLabel.ForeColor = Color.LightGreen;
                    }
                }
                else if (databaseName == "SESSIONS")
                {
                    entryListView.Columns.Clear();
                    entryListView.Columns.Add("Session ID", 250);
                    entryListView.Columns.Add("Value", 100);
                    entryListView.Columns.Add("Username", 150);
                    entryListView.Columns.Add("Login Time", 200);


                    string exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    string scriptPath = Path.Combine(exeDirectory, "SecureServer", "adminPortal", "listsessions.py");

                    if (!File.Exists(scriptPath))
                    {
                        MessageBox.Show(
                            $"List Sessions database script not found:\n{scriptPath}",
                            "Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error
                        );
                        throw new Exception();
                    }
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = "python",
                        Arguments = $"\"{scriptPath}\" \"{MainForm.Session.Id}\"",
                        WorkingDirectory = exeDirectory,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    using (Process process = Process.Start(psi))
                    {
                        string output = process.StandardOutput.ReadToEnd();
                        process.WaitForExit();

                        if (process.ExitCode != 0)
                        {
                            MessageBox.Show(
                                $"Error fetching the sessions database.",
                                "Error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error
                            );
                            throw new Exception();
                        }

                        var tokens = JsonSerializer.Deserialize<List<Token>>(output);

                        foreach (var token in tokens)
                        {
                            ListViewItem item = new ListViewItem(token.Id);
                            item.SubItems.Add(token.Value);
                            item.SubItems.Add(token.Username);
                            item.SubItems.Add(token.LoginTime);

                            item.Tag = token;
                            entryListView.Items.Add(item);
                        }
                        statusLabel.Text = $"Loaded {tokens.Count} log entries from tokens.json";
                        statusLabel.ForeColor = Color.LightGreen;
                    }
                }
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Failed to load database '{databaseName}'";
                statusLabel.ForeColor = Color.Red;
            }
            finally
            {
                entryListView.EndUpdate();
                loadingDatabase = false;
                AutoSizeLastColumn();
            }
        }

        #endregion
        #region Commands
        private void RunUserAction(string action)
        {
            if (selectedUser == null)
                return;

            string exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string scriptPath = Path.Combine(
                exeDirectory,
                "SecureServer",
                "adminPortal",
                "useraction.py"
            );

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "python",
                Arguments =
                    $"\"{scriptPath}\" \"{MainForm.Session.Id}\" \"{action}\" \"{selectedUser.Id}\"",
                WorkingDirectory = exeDirectory,
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = Process.Start(psi))
            {
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    MessageBox.Show(error, "Command Failed",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            RefreshButton_Click(null, null);
        }

        #endregion
        #region Custom Drawing
        // (paint)
        private void entryListView_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            // Fill the entire header area including the background
            e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(30, 30, 30)), e.Bounds);

            // Draw a subtle separator line at the bottom
            e.Graphics.DrawLine(new Pen(Color.FromArgb(50, 50, 50)),
                e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);

            e.Graphics.DrawString(e.Header.Text, new Font("Segoe UI", 9, FontStyle.Bold),
                new SolidBrush(Color.FromArgb(200, 200, 200)), e.Bounds.Left + 5, e.Bounds.Top + 5);
        }
        private void entryListView_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            // Alternate row colors
            Color bgColor = e.ItemIndex % 2 == 0 ? Color.FromArgb(30, 30, 30) : Color.FromArgb(35, 35, 35);

            if (e.Item.Selected)
            {
                bgColor = Color.FromArgb(0, 80, 140);
            }

            e.Graphics.FillRectangle(new SolidBrush(bgColor), e.Bounds);

            Color textColor = Color.White; // default color

            // Detect type
            if (e.Item.Tag is LogEntry log)
            {
                // existing logic for LogEntry
                if (e.ColumnIndex == 4 && !string.IsNullOrEmpty(log.StatusCode))
                {
                    textColor = e.Item.Selected ? Color.White : Color.Blue;
                }
                else
                {
                    textColor = e.Item.Selected ? Color.White : log.LevelColor;
                }
            }
            else if (e.Item.Tag is User user)
                textColor = e.Item.Selected ? Color.White : Color.LightGreen;
            else if (e.Item.Tag is AttemptEntry attempt)
                textColor = e.Item.Selected ? Color.White : attempt.Time != null ? Color.PaleVioletRed : Color.LightGreen;
            else if (e.Item.Tag is Token)
                textColor = e.Item.Selected ? Color.White : Color.FromArgb(3, 169, 244);

            TextRenderer.DrawText(e.Graphics, e.SubItem.Text, new Font("Consolas", 9),
                e.Bounds, textColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }


        // (format)
        private bool resizingColumn = false;
        private bool loadingDatabase = false;
        private void AutoSizeLastColumn()
        {
            if (loadingDatabase)
                return;
            if (entryListView == null || entryListView.Columns.Count == 0) return;
            if (resizingColumn) return;

            resizingColumn = true;
            try
            {
                int totalWidth = entryListView.ClientSize.Width;

                // Subtract widths of all columns except the last
                for (int i = 0; i < entryListView.Columns.Count - 1; i++)
                    totalWidth -= entryListView.Columns[i].Width;

                if (totalWidth > 50) // minimum width
                    entryListView.Columns[entryListView.Columns.Count - 1].Width = totalWidth;
            }
            finally
            {
                resizingColumn = false;
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

        // DWMWINDOWATTRIBUTE values
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20; // Windows 10 1809+
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_NEW = 38; // Windows 11 (sometimes 1903+)
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
            catch
            {
                // If Windows version doesn't support it, just ignore.
            }
        }
        private void SetControlDarkMode(Control control)
        {
            // Apply DWM Dark Mode to the control's handle
            if (control.Handle != IntPtr.Zero)
            {
                int useDarkMode = 1;

                // Try the newer attribute first (Win 11)
                int result = DwmSetWindowAttribute(control.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE_NEW, ref useDarkMode, sizeof(int));

                // Fallback to the older attribute (Win 10 1809+)
                if (result != 0)
                {
                    DwmSetWindowAttribute(control.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));
                }
            }

            // Recursively apply to child controls
            foreach (Control child in control.Controls)
            {
                SetControlDarkMode(child);
            }
        }
        #region Windows 11 Immersive Scrollbars
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int SetWindowTheme(IntPtr hwnd, string pszSubAppName, string pszSubIdList);

        [StructLayout(LayoutKind.Sequential)]
        public struct WTA_OPTIONS
        {
            public uint Flags;
            public uint Mask;
        }

        // Window attribute enum
        public enum WindowThemeAttributeType : uint
        {
            WTA_NONCLIENT = 1
        }

        [DllImport("uxtheme.dll", SetLastError = true)]
        private static extern int SetWindowThemeAttribute(
            IntPtr hwnd,
            WindowThemeAttributeType eAttribute,
            ref WTA_OPTIONS pvAttribute,
            uint cbAttribute);

        private const uint WTNCA_NODRAWCAPTION = 0x00000001;
        private const uint WTNCA_NODRAWICON = 0x00000002;
        private const uint WTNCA_NOSYSMENU = 0x00000004;
        private const uint WTNCA_NOMIRRORHELP = 0x00000008;
        private const uint WTNCA_VALIDBITS = 0x0000000F;

        private void ApplyImmersiveScrollbars(Control control)
        {
            if (control == null) return;

            // Apply Windows 11 scrollbar theme
            SetWindowTheme(control.Handle, "DarkMode_Explorer", null);

            // Apply to children recursively
            foreach (Control child in control.Controls)
                ApplyImmersiveScrollbars(child);
        }
        #endregion
        #endregion
    }
}
