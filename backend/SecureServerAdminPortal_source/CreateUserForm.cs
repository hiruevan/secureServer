using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace SecureServerCommand
{
    public class CreateUserForm : Form
    {
        private TextBox usernameBox;
        private TextBox passwordBox;
        private TextBox firstNameBox;
        private TextBox lastNameBox;
        private CheckBox devAdminBox;
        private CheckBox rootAuthBox;
        private CheckBox appAdminBox;
        private Button createButton;
        private Button cancelButton;

        public CreateUserForm()
        {
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            this.Text = "New User";
            this.Icon = Properties.Resources.favicon;
            this.Size = Size = new Size(420, 310);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(25, 25, 25);

            this.FormBorderStyle = FormBorderStyle.None;
            this.ControlBox = false;

            Label userLabel = new Label
            {
                Text = "Username:",
                ForeColor = Color.White,
                Location = new Point(20, 25),
                AutoSize = true
            };

            usernameBox = new TextBox
            {
                Location = new Point(120, 22),
                Width = 250,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            Label passLabel = new Label
            {
                Text = "Password:",
                ForeColor = Color.White,
                Location = new Point(20, 65),
                AutoSize = true
            };

            passwordBox = new TextBox
            {
                Location = new Point(120, 62),
                Width = 250,
                UseSystemPasswordChar = true,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            Label firstNameLabel = new Label
            {
                Text = "First Name:",
                ForeColor = Color.White,
                Location = new Point(20, 105),
                AutoSize = true
            };

            firstNameBox = new TextBox
            {
                Location = new Point(120, 102),
                Width = 250,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            Label lastNameLabel = new Label
            {
                Text = "Last Name:",
                ForeColor = Color.White,
                Location = new Point(20, 145),
                AutoSize = true
            };

            lastNameBox = new TextBox
            {
                Location = new Point(120, 142),
                Width = 250,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            devAdminBox = new CheckBox
            {
                Text = "Dev Admin",
                Location = new Point(120, 205),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(25, 25, 25),
                AutoSize = true
            };

            rootAuthBox = new CheckBox
            {
                Text = "Root Auth",
                Location = new Point(220, 205),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(25, 25, 25),
                AutoSize = true
            };

            appAdminBox = new CheckBox
            {
                Text = "App Admin",
                Location = new Point(120, 180),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(25, 25, 25),
                AutoSize = true
            };

            createButton = new Button
            {
                Text = "Create",
                Location = new Point(120, 260),
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(0, 120, 200),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            createButton.FlatAppearance.BorderSize = 0;
            createButton.Click += CreateButton_Click;

            cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(230, 260),
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(70, 70, 70),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            cancelButton.FlatAppearance.BorderSize = 0;
            cancelButton.Click += (_, __) => DialogResult = DialogResult.Cancel;

            Controls.AddRange(new Control[]
            {
                userLabel, usernameBox,
                passLabel, passwordBox,
                firstNameLabel, firstNameBox,
                lastNameLabel, lastNameBox,
                devAdminBox, appAdminBox, rootAuthBox,
                createButton, cancelButton
            });
        }

        private void CreateButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(usernameBox.Text) ||
                string.IsNullOrWhiteSpace(passwordBox.Text))
            {
                MessageBox.Show("Username and password are required.",
                    "Invalid Input",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            RunPythonScript(usernameBox.Text.Trim(), passwordBox.Text);
            this.Close();
        }

        private void RunPythonScript(string username, string password)
        {
            string exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string scriptPath = Path.Combine(
                exeDirectory,
                "SecureServer",
                "adminPortal",
                "createuser.py"
            );

            if (!File.Exists(scriptPath))
            {
                MessageBox.Show($"Script not found:\n{scriptPath}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            var args = new List<string>
            {
                $"\"{scriptPath}\"",
                $"\"{MainForm.Session.Id}\"",
                $"\"{username}\"",
                $"\"{password}\""
            };

            // Optional overrides
            if (!string.IsNullOrWhiteSpace(firstNameBox.Text))
            {
                args.Add("first_name");
                args.Add($"\"{firstNameBox.Text.Trim()}\"");
            }

            if (!string.IsNullOrWhiteSpace(lastNameBox.Text))
            {
                args.Add("last_name");
                args.Add($"\"{lastNameBox.Text.Trim()}\"");
            }

            if (devAdminBox.Checked)
            {
                args.Add("dev_admin");
                args.Add("true");
            }

            if (rootAuthBox.Checked)
            {
                args.Add("root_auth");
                args.Add("true");
            }

            if (appAdminBox.Checked)
            {
                args.Add("admin");
                args.Add("true");
            }

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = string.Join(" ", args),
                WorkingDirectory = exeDirectory,
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(psi))
            {
                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    MessageBox.Show(
                        $"Failed to create user.\n\n{stderr}",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                    return;
                }
            }
        }

    }
}
