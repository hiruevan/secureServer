using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Diagnostics;
using ServerLogViewer;
using CustomControls;

namespace SecureServerCommand
{
    public partial class MainForm : Form
    {
        #region Properties
        // Static Root Vars
        public static bool RootAuth = false;
        public static Session Session = new Session();
        public static RootForm RootForm;

        // Variables
        private List<LogEntry> allLogs = new List<LogEntry>();
        private List<LogEntry> filteredLogs = new List<LogEntry>();
        private FileSystemWatcher fileWatcher;
        private string currentFilePath;
        private Process serverProcess;

        // Form Controls
        private ListView logListView;
        private TextBox searchBox;
        private ComboBox levelFilter;
        private Button rootCommandButton;
        private Button refreshButton;
        private Button startServerButton;
        private Button stopServerButton;
        private CheckBox automaticallyUpdateCheckbox;
        private CheckBox autoScrollCheckbox;
        private Label statusLabel;
        private Label serverStatusLabel;
        private RichTextBox detailsBox;

        private MenuStrip navigationBar;
        private bool isFullscreen = false;
        private Rectangle windowedBounds;
        #endregion

        #region Constructors
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;
        public MainForm()
        {
            // Initialize window
            InitializeComponents();
            SetupFileWatcher();
            EnableDarkMode();
            SetControlDarkMode(this);

            this.Load += (_, __) => ApplyImmersiveScrollbars(this);

            // Check if server is already running
            string exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string pidFilePath = Path.Combine(exeDirectory, "SecureServer", "server.pid");
            DetectRunningServer(pidFilePath);

            // Load server.log
            string logFilePath = Path.Combine(exeDirectory, "SecureServer", "server.log");
            LoadLogFile(logFilePath);
            UpdateServerStatus();
            RefreshButton_Click(null, null);
        }
        private void DetectRunningServer(string pidFilePath)
        {
            try
            {
                // Check if PID file exists
                if (File.Exists(pidFilePath))
                {
                    string pidText = File.ReadAllText(pidFilePath).Trim();
                    if (int.TryParse(pidText, out int pid))
                    {
                        try
                        {
                            Process proc = Process.GetProcessById(pid);

                            // Verify it's still a python process
                            if (proc.ProcessName.ToLower().Contains("python"))
                            {
                                serverProcess = proc;
                                serverProcess.EnableRaisingEvents = true;
                                serverProcess.Exited += ServerProcess_Exited;
                                return;
                            }
                        }
                        catch (ArgumentException)
                        {
                            // Process doesn't exist anymore, clean up PID file
                            File.Delete(pidFilePath);
                        }
                    }
                }

                // No running process found
                serverProcess = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error detecting running server: {ex.Message}");
                serverProcess = null;
            }
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
            this.Size = new Size(1200, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(25, 25, 25);

            this.FormBorderStyle = FormBorderStyle.None;
            this.ControlBox = false;
            this.Text = "SecureServer Command Center";

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
            ToolStripMenuItem serverMenu = new ToolStripMenuItem("Server");

            fileMenu.DropDownItems.Add("Refresh", null, RefreshButton_Click);

            serverMenu.DropDownItems.Add("Start", null, StartServerButton_Click);
            serverMenu.DropDownItems.Add("Stop", null, StopServerButton_Click);
            serverMenu.DropDownItems.Add("Restart", null, (object sender, EventArgs e) => {
                StopServer();
                StartServer();
            });

            navigationBar.Items.Add(fileMenu);
            if (RootAuth)
                navigationBar.Items.Add(serverMenu);

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

            startServerButton = new Button
            {
                Text = "Start Server",
                Location = new Point(10, 10),
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(21, 79, 24),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9)
            };
            startServerButton.FlatAppearance.BorderSize = 0;
            startServerButton.Click += StartServerButton_Click;

            stopServerButton = new Button
            {
                Text = "Stop Server",
                Location = new Point(115, 10),
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(117, 18, 11),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9),
                Enabled = false
            };
            stopServerButton.FlatAppearance.BorderSize = 0;
            stopServerButton.Click += StopServerButton_Click;

            serverStatusLabel = new Label
            {
                Text = "Server: Stopped",
                Location = new Point(220, 16),
                AutoSize = true,
                ForeColor = Color.FromArgb(150, 150, 150),
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
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

            rootCommandButton = new Button
            {
                Text = "Root Command",
                Location = new Point(465, 10),
                Size = new Size(120, 30),
                BackColor = Color.FromArgb(0, 100, 180),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9)
            };
            rootCommandButton.FlatAppearance.BorderSize = 0;
            rootCommandButton.Click += RootButton_Click;

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
                Size = new Size(250, 25),
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9)
            };
            searchBox.TextChanged += SearchBox_TextChanged;

            Label filterLabel = new Label
            {
                Text = "Level:",
                Location = new Point(330, 48),
                AutoSize = true,
                ForeColor = Color.FromArgb(200, 200, 200),
                Font = new Font("Segoe UI", 9)
            };

            levelFilter = new DarkComboBox
            {
                Location = new Point(380, 45),
                Size = new Size(150, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9)
            };
            levelFilter.Items.AddRange(new object[] {
                "All",
                "INFO", "UPDATE", 
                "LOGIN", "ADMIN",
                "ENDPOINT",
                "WARNING",
                "NOTICE", "SECURITY",
                "ERROR", "CRITICAL",
            });
            levelFilter.SelectedIndex = 0;
            levelFilter.SelectedIndexChanged += LevelFilter_SelectedIndexChanged;

            automaticallyUpdateCheckbox = new CheckBox
            {
                Text = "Automatically Refresh",
                Location = new Point(540, 40),
                AutoSize = true,
                Checked = true,
                ForeColor = Color.FromArgb(200, 200, 200),
                Font = new Font("Segoe UI", 9)
            };

            autoScrollCheckbox = new CheckBox
            {
                Text = "Auto-scroll",
                Location = new Point(540, 55),
                AutoSize = true,
                Checked = true,
                ForeColor = Color.FromArgb(200, 200, 200),
                Font = new Font("Segoe UI", 9)
            };

            statusLabel = new Label
            {
                Text = "No file loaded",
                Location = new Point(700, 48),
                AutoSize = true,
                ForeColor = Color.FromArgb(150, 150, 150),
                Font = new Font("Segoe UI", 9)
            };

            topPanel.Controls.AddRange(new Control[] {
                refreshButton, searchLabel, searchBox,
                filterLabel, levelFilter, 
                automaticallyUpdateCheckbox, autoScrollCheckbox, 
                statusLabel
            });
            if (RootAuth)
            {
                refreshButton.Location = new Point(380, 10);
                topPanel.Controls.AddRange(new Control[] {rootCommandButton,
                     startServerButton, stopServerButton, serverStatusLabel 
                });
            }

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
            logListView = new ListView
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
            logListView.Columns.Add("Time", 150);
            logListView.Columns.Add("Level", 100);
            logListView.Columns.Add("Message", 400);
            logListView.Columns.Add("Endpoint", 200);
            logListView.Columns.Add("Status", 100);
            logListView.SelectedIndexChanged += LogListView_SelectedIndexChanged;
            logListView.DrawColumnHeader += LogListView_DrawColumnHeader;
            logListView.DrawSubItem += LogListView_DrawSubItem;
            logListView.ColumnWidthChanged += (s, e) => AutoSizeLastColumn();

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

            splitContainer.Panel1.Controls.Add(logListView);
            splitContainer.Panel2.Controls.AddRange(new Control[] { detailsBox, detailsLabel });

            this.Controls.Add(splitContainer);
            this.Controls.Add(topPanel);
            this.Controls.Add(titleBar);
        }
        #endregion

        #region Event Handlers
        private void RootButton_Click(object sender, EventArgs e)
        {
            if (!RootAuth)
            {
                MessageBox.Show(
                    "You do not have developer/root privileges.",
                    "Access Denied",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                return;
            }
            if (RootForm == null || RootForm.IsDisposed)
            {
                RootForm = new RootForm();
                RootForm.FormClosed += (_, __) => RootForm = null;
                RootForm.Show();
            }
            else
            {
                RootForm.Focus();
            }
        }
        private void StartServerButton_Click(object sender, EventArgs e)
        {
            StartServer();
        }
        private void StopServerButton_Click(object sender, EventArgs e)
        {
            StopServer();
        }
        private void SearchBox_TextChanged(object sender, EventArgs e)
        {
            ApplyFilters();
        }
        private void LevelFilter_SelectedIndexChanged(object sender, EventArgs e)
        {
            ApplyFilters();
        }
        private void RefreshButton_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(currentFilePath))
            {
                LoadLogFile(currentFilePath);
            }
        }
        private void LogListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (logListView.SelectedItems.Count > 0)
            {
                var log = (LogEntry)logListView.SelectedItems[0].Tag;
                DisplayLogDetails(log);
            }
        }
        #endregion
        #region Overrides
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            AutoSizeLastColumn();
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Logout self
            string exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string scriptPath = Path.Combine(
                exeDirectory,
                "SecureServer",
                "adminPortal",
                "logoutadmin.py"
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
            }

            base.OnFormClosing(e);
        }
        #endregion
        #region Server
        private void StartServer()
        {
            try
            {
                string exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string mainPyPath = Path.Combine(exeDirectory, "main.py");

                if (!File.Exists(mainPyPath))
                {
                    MessageBox.Show($"Could not find main.py in:\n{exeDirectory}",
                        "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Check if server is already running
                if (serverProcess != null && !serverProcess.HasExited)
                {
                    MessageBox.Show("Server is already running!", "Already Running",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Start the Python process
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{mainPyPath}\"",
                    WorkingDirectory = exeDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                serverProcess = new Process { StartInfo = startInfo };

                // Handle process exit
                serverProcess.EnableRaisingEvents = true;
                serverProcess.Exited += ServerProcess_Exited;

                serverProcess.Start();

                UpdateServerStatus();

                //MessageBox.Show("Server started successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting server: {ex.Message}\n\nMake sure Python is installed and in your PATH.",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(uint dwProcessId);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeConsole();
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCtrlHandler(IntPtr HandlerRoutine, bool Add);
        private const uint CTRL_C_EVENT = 0;
        private bool TryGracefulShutdown()
        {
            try
            {
                // Free our current console if we have one
                try { FreeConsole(); } catch { }

                // Attach to the server process console
                if (AttachConsole((uint)serverProcess.Id))
                {
                    // Disable Ctrl+C handling for our process
                    SetConsoleCtrlHandler(IntPtr.Zero, true);

                    // Send Ctrl+C event
                    GenerateConsoleCtrlEvent(CTRL_C_EVENT, 0);

                    // Wait a bit for the signal to be processed
                    System.Threading.Thread.Sleep(100);

                    // Detach from the console
                    FreeConsole();

                    // Re-enable Ctrl+C handling
                    SetConsoleCtrlHandler(IntPtr.Zero, false);

                    // Wait for graceful shutdown (1 seconds)
                    bool exited = serverProcess.WaitForExit(1000);

                    return exited;
                }

                return false;
            }
            catch (Exception ex)
            {
                // Clean up
                try
                {
                    FreeConsole();
                    SetConsoleCtrlHandler(IntPtr.Zero, false);
                }
                catch { }

                System.Diagnostics.Debug.WriteLine($"Graceful shutdown failed: {ex.Message}");
                return false;
            }
        }
        private void StopServer()
        {
            try
            {
                if (serverProcess != null && !serverProcess.HasExited)
                {
                    // Try graceful shutdown first
                    bool gracefulShutdown = TryGracefulShutdown();

                    // Check if still running
                    if (!gracefulShutdown && serverProcess != null && !serverProcess.HasExited)
                    {
                        var result = MessageBox.Show(
                            "Server did not respond to shutdown request. Force termination?",
                            "Force Stop",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning);

                        if (result == DialogResult.Yes)
                        {
                            serverProcess.Kill();
                            serverProcess.WaitForExit(2000);
                        }
                        else
                        {
                            return; // User cancelled
                        }
                    }

                    // Clean up
                    if (serverProcess != null)
                    {
                        try
                        {
                            if (!serverProcess.HasExited)
                            {
                                serverProcess.Kill();
                            }
                            serverProcess.Dispose();
                        }
                        catch { }
                        serverProcess = null;
                    }

                    UpdateServerStatus();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error stopping server: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);

                // Force cleanup
                if (serverProcess != null)
                {
                    try
                    {
                        serverProcess.Kill();
                        serverProcess.Dispose();
                    }
                    catch { }
                    serverProcess = null;
                }
                UpdateServerStatus();
            }
        }
        private void ServerProcess_Exited(object sender, EventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => UpdateServerStatus()));
            }
            else
            {
                UpdateServerStatus();
            }
        }
        private void UpdateServerStatus()
        {
            bool isRunning = serverProcess != null && !serverProcess.HasExited;

            startServerButton.Enabled = !isRunning;
            stopServerButton.Enabled = isRunning;

            if (isRunning)
            {
                serverStatusLabel.Text = "Server: Running";
                serverStatusLabel.ForeColor = Color.FromArgb(76, 175, 80); // Green
            }
            else
            {
                serverStatusLabel.Text = "Server: Stopped";
                serverStatusLabel.ForeColor = Color.FromArgb(150, 150, 150); // Gray
            }
        }
        #endregion
        #region File Handling
        private void SetupFileWatcher()
        {
            fileWatcher = new FileSystemWatcher
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
            };
            fileWatcher.Changed += FileWatcher_Changed;
        }
        private void LoadLogFile(string filePath)
        {
            try
            {
                currentFilePath = filePath;
                allLogs.Clear();
                logListView.Items.Clear();

                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        LogEntry entry = ParseLogLine(line);
                        if (entry != null)
                            allLogs.Add(entry);
                    }
                }

                ApplyFilters();
                statusLabel.Text = $"Loaded {allLogs.Count} log entries from {Path.GetFileName(filePath)}";
                statusLabel.ForeColor = Color.LightGreen;

                // Setup file watcher
                fileWatcher.Path = Path.GetDirectoryName(filePath);
                fileWatcher.Filter = Path.GetFileName(filePath);
                fileWatcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading file: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private LogEntry ParseLogLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return null;

            // Remove ANSI escape codes completely
            string cleanLine = Regex.Replace(line, @"\x1B\[[0-9;]*m", "");
            cleanLine = Regex.Replace(cleanLine, @"\[0m", "");

            // Parse: [2025-12-10 14:18:34] LEVEL: Message with possible protocol info
            var match = Regex.Match(cleanLine, @"\[(\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2})\]\s+([A-Z][A-Z\s]+):\s*(.*)");

            if (!match.Success)
                return null;

            string level = match.Groups[2].Value.Trim();
            string remainingText = match.Groups[3].Value.Trim();

            var entry = new LogEntry
            {
                Timestamp = DateTime.Parse(match.Groups[1].Value),
                LogLevel = level,
                LevelFilter = GetLevelFilter(level),
                FullLine = line,
                LevelColor = GetColorForLevel(level)
            };

            // Try to parse protocol information
            // Pattern: IP:PORT - "METHOD /endpoint HTTP/VERSION" STATUS - STATUS_TEXT
            var protocolMatch = Regex.Match(remainingText,
                @"^\s*([\d\.]+:\d+)\s+-\s+""(GET|POST|PUT|DELETE|PATCH|HEAD|OPTIONS)\s+([^\s""]+)\s+(HTTP/[\d\.]+)""\s*(\d+)?\s*-?\s*(.*)$");

            if (protocolMatch.Success)
            {
                entry.HasProtocolInfo = true;
                entry.IpAddress = protocolMatch.Groups[1].Value;
                entry.HttpMethod = protocolMatch.Groups[2].Value;
                entry.Endpoint = protocolMatch.Groups[3].Value;
                entry.HttpVersion = protocolMatch.Groups[4].Value;
                entry.StatusCode = protocolMatch.Groups[5].Value;
                entry.StatusText = protocolMatch.Groups[6].Value.Trim();

                // Message is just the main action description
                entry.Message = $"{entry.HttpMethod} {entry.Endpoint}";
            }
            else
            {
                // No protocol info, treat entire remaining text as message
                entry.HasProtocolInfo = false;
                entry.Message = remainingText;
            }

            return entry;
        }
        private void FileWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            if (!automaticallyUpdateCheckbox.Checked)
                return;
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => RefreshButton_Click(null, null)));
            }
        }
        #endregion
        #region Filtering
        private string GetLevelFilter(string level)
        {
            switch (level)
            {
                case "":
                case "INFO":
                    return "INFO";
                case "NOTICE":
                case "DEBUG":
                    return "NOTICE";
                case "WARNING":
                case "ISSUE":
                    return "WARNING";
                case "LOGIN":
                case "LOGOUT":
                    return "LOGIN";
                case "UPDATE":
                case "PASSWORD CHANGE":
                case "SIGNUP":
                    return "UPDATE";
                case "SECURITY NOTICE":
                case "SECURITY":
                case "CORRUPTED ENCRYPTED FILE":
                case "RESETTING ENCRYPTED FILE":
                    return "SECURITY";
                case "COMMAND":
                case "ADMIN":
                case "ADMIN ACTION":
                    return "ADMIN";
                case "ERROR":
                case "EXCEPTION":
                    return "ERROR";
                case "CRITICAL":
                    return "CRITICAL";
                default:
                    return "DEFAULT";
            }
        }
        private void ApplyFilters()
        {
            filteredLogs = allLogs.Where(log =>
            {
                // Level filter
                if (levelFilter.SelectedIndex > 0)
                {
                    string selectedLevel = levelFilter.SelectedItem.ToString();
                    if (log.LevelFilter != selectedLevel)
                        if (!log.LogLevel.Contains(selectedLevel))
                            if (!(selectedLevel == "ERROR" && log.LevelFilter == "CRITICAL"))
                                if (!(selectedLevel == "UPDATE" && log.LevelFilter == "LOGIN"))
                                    if (!(selectedLevel == "INFO" && log.LevelFilter == "DEFAULT"))
                                        if (!(selectedLevel == "ENDPOINT" && log.HasProtocolInfo))
                                            return false;
                }

                // Search filter
                if (!string.IsNullOrWhiteSpace(searchBox.Text))
                {
                    string search = searchBox.Text.ToLower();
                    if (!log.Message.ToLower().Contains(search) &&
                        !log.LogLevel.ToLower().Contains(search) &&
                        !(log.Endpoint?.ToLower().Contains(search) ?? false) &&
                        !(log.StatusCode?.ToLower().Contains(search) ?? false))
                        return false;
                }

                return true;
            }).ToList();

            UpdateListView();
        }
       
        #endregion
        #region Display
        private void UpdateListView()
        {
            logListView.Items.Clear();
            logListView.BeginUpdate();

            foreach (var log in filteredLogs)
            {
                var item = new ListViewItem(new string[]
                {
                    log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                    log.LogLevel,
                    log.HasProtocolInfo ? log.Message : log.Message,
                    log.Endpoint ?? "",
                    log.StatusCode != null ? $"{log.StatusCode} {log.StatusText}" : ""
                });
                item.Tag = log;
                item.ForeColor = log.LevelColor;
                logListView.Items.Add(item);
            }

            logListView.EndUpdate();
            AutoSizeLastColumn();

            if (autoScrollCheckbox.Checked && logListView.Items.Count > 0)
            {
                logListView.EnsureVisible(logListView.Items.Count - 1);
            }

            statusLabel.Text = $"Showing {filteredLogs.Count} of {allLogs.Count} entries";
        }
        private Color GetColorForLevel(string level)
        {
            switch (level)
            {
                case "INFO":
                    return Color.FromArgb(76, 175, 80);  // Green
                case "LOGIN":
                case "LOGOUT":
                case "COMMAND":
                case "ADMIN":
                case "ADMIN ACTION":
                    return Color.FromArgb(3, 169, 244);  // Blue
                case "UPDATE":
                case "SIGNUP":
                case "PASSWORD CHANGE":
                    return Color.FromArgb(156, 39, 176); // Purple
                case "NOTICE":
                case "WARNING":
                case "DEBUG":
                case "ISSUE":
                    return Color.FromArgb(255, 193, 7);  // Yellow
                case "SECURITY NOTICE":
                case "SECURITY":
                case "CORRUPTED ENCRYPTED FILE":
                case "RESETTING ENCRYPTED FILE":
                    return Color.FromArgb(255, 152, 0);  // Orange
                case "ERROR":
                case "CRITICAL":
                    return Color.FromArgb(244, 67, 54);  // Red
                default:
                    return Color.Gray;
            }
        }
        private Color GetColorForStatusCode(string statusCode)
        {
            if (string.IsNullOrEmpty(statusCode))
                return Color.Gray;

            if (statusCode.StartsWith("2"))
                return Color.FromArgb(76, 175, 80);  // Green - Success
            else if (statusCode.StartsWith("3"))
                return Color.FromArgb(156, 39, 176);  // Purple - Redirect
            else if (statusCode.StartsWith("4"))
                return Color.FromArgb(255, 193, 7);  // Yellow - Client Error
            else if (statusCode.StartsWith("5"))
                return Color.FromArgb(244, 67, 54);  // Red - Server Error
            else
                return Color.Gray;
        }
        
        private void DisplayLogDetails(LogEntry log)
        {
            detailsBox.Clear();

            // Add colored header
            detailsBox.SelectionColor = log.LevelColor;
            detailsBox.SelectionFont = new Font("Consolas", 12, FontStyle.Bold);
            detailsBox.AppendText($"{log.LogLevel}\n\n");

            // Reset to normal
            detailsBox.SelectionColor = Color.FromArgb(180, 180, 180);
            detailsBox.SelectionFont = new Font("Consolas", 10);

            detailsBox.AppendText($"Timestamp: ");
            detailsBox.SelectionColor = Color.White;
            detailsBox.AppendText($"{log.Timestamp:yyyy-MM-dd HH:mm:ss}\n\n");

            detailsBox.SelectionColor = Color.FromArgb(180, 180, 180);
            detailsBox.AppendText($"Message:\n");
            detailsBox.SelectionColor = Color.White;
            detailsBox.AppendText($"{log.Message}\n\n");

            // Show protocol information if available
            if (log.HasProtocolInfo)
            {
                detailsBox.SelectionColor = Color.FromArgb(180, 180, 180);
                detailsBox.AppendText($"Protocol Information:\n");

                detailsBox.SelectionColor = Color.FromArgb(100, 180, 255);
                detailsBox.AppendText($"  IP Address: ");
                detailsBox.SelectionColor = Color.White;
                detailsBox.AppendText($"{log.IpAddress}\n");

                detailsBox.SelectionColor = Color.FromArgb(100, 180, 255);
                detailsBox.AppendText($"  HTTP Method: ");
                detailsBox.SelectionColor = Color.White;
                detailsBox.AppendText($"{log.HttpMethod}\n");

                detailsBox.SelectionColor = Color.FromArgb(100, 180, 255);
                detailsBox.AppendText($"  Endpoint: ");
                detailsBox.SelectionColor = Color.White;
                detailsBox.AppendText($"{log.Endpoint}\n");

                detailsBox.SelectionColor = Color.FromArgb(100, 180, 255);
                detailsBox.AppendText($"  HTTP Version: ");
                detailsBox.SelectionColor = Color.White;
                detailsBox.AppendText($"{log.HttpVersion}\n");

                if (!string.IsNullOrEmpty(log.StatusCode))
                {
                    detailsBox.SelectionColor = Color.FromArgb(100, 180, 255);
                    detailsBox.AppendText($"  Status: ");
                    detailsBox.SelectionColor = GetColorForStatusCode(log.StatusCode);
                    detailsBox.AppendText($"{log.StatusCode} {log.StatusText}\n");
                }

                detailsBox.AppendText("\n");
            }

            detailsBox.SelectionColor = Color.FromArgb(180, 180, 180);
            detailsBox.AppendText($"Full Line:\n");
            detailsBox.SelectionColor = Color.FromArgb(120, 120, 120);
            detailsBox.SelectionFont = new Font("Consolas", 9);
            detailsBox.AppendText($"{log.FullLine}");
        }
        #endregion
        #region Custom Drawing
        // (paint)
        private void LogListView_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            // Fill the entire header area including the background
            e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(30, 30, 30)), e.Bounds);

            // Draw a subtle separator line at the bottom
            e.Graphics.DrawLine(new Pen(Color.FromArgb(50, 50, 50)),
                e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);

            e.Graphics.DrawString(e.Header.Text, new Font("Segoe UI", 9, FontStyle.Bold),
                new SolidBrush(Color.FromArgb(200, 200, 200)), e.Bounds.Left + 5, e.Bounds.Top + 5);
        }
        private void LogListView_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            // Alternate row colors
            Color bgColor = e.ItemIndex % 2 == 0 ? Color.FromArgb(30, 30, 30) : Color.FromArgb(35, 35, 35);

            if (e.Item.Selected)
            {
                bgColor = Color.FromArgb(0, 80, 140);
            }

            e.Graphics.FillRectangle(new SolidBrush(bgColor), e.Bounds);

            var log = (LogEntry)e.Item.Tag;
            Color textColor;

            // Column 4 is Status - color it based on status code
            if (e.ColumnIndex == 4 && !string.IsNullOrEmpty(log.StatusCode))
            {
                textColor = e.Item.Selected ? Color.White : GetColorForStatusCode(log.StatusCode);
            }
            else
            {
                textColor = e.Item.Selected ? Color.White : log.LevelColor;
            }

            TextRenderer.DrawText(e.Graphics, e.SubItem.Text, new Font("Consolas", 9),
                e.Bounds, textColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
        
        // (format)
        private bool resizingColumn = false;
        private void AutoSizeLastColumn()
        {
            if (logListView == null)
                return;
            if (logListView.Columns.Count == 0)
                return;

            if (resizingColumn)
                return; // Already resizing, skip to prevent recursion

            resizingColumn = true;

            try
            {
                int totalWidth = logListView.ClientSize.Width;

                // Subtract widths of all other columns
                for (int i = 0; i < logListView.Columns.Count - 1; i++)
                    totalWidth -= logListView.Columns[i].Width;

                if (totalWidth > 0)
                    logListView.Columns[logListView.Columns.Count - 1].Width = totalWidth;
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