using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace PT2
{
    public static class AppManager
    {
        public static readonly string AppDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PlayTimer");
        public static readonly string PasswordFile = Path.Combine(AppDataFolder, "PT.log");
        public static readonly string GameListFile = Path.Combine(AppDataFolder, "PT-list.txt");
        public static readonly string SettingsFile = Path.Combine(AppDataFolder, "PT-settings.txt");

        public static void EnsureDirectories()
        {
            if (!Directory.Exists(AppDataFolder))
            {
                Directory.CreateDirectory(AppDataFolder);
            }

            DirectoryInfo dirInfo = new DirectoryInfo(AppDataFolder);

            if ((dirInfo.Attributes & FileAttributes.Hidden) != FileAttributes.Hidden)
            {
                dirInfo.Attributes |= FileAttributes.Hidden;
            }
        }

        public static string HashPassword(string password)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();
                foreach (byte t in bytes)
                {
                    builder.Append(t.ToString("x2"));
                }
                return builder.ToString();
            }
        }

        public static bool IsPasswordSet() => File.Exists(PasswordFile);

        public static bool VerifyPassword(string inputPassword)
        {
            if (!IsPasswordSet()) return false;
            string savedHash = File.ReadAllText(PasswordFile);
            return HashPassword(inputPassword) == savedHash;
        }

        public static void SetPassword(string newPassword)
        {
            EnsureDirectories();
            File.WriteAllText(PasswordFile, HashPassword(newPassword));
        }

        public static void SetAutostart(bool enable)
        {
            string taskName = "PlayTimer";

            if (enable)
            {
                string exePath = Application.ExecutablePath;

                string createArguments = $"/Create /TN \"{taskName}\" /TR \"\\\"{exePath}\\\" /sillent\" /SC ONLOGON /RL HIGHEST /F";

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = createArguments,
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                using (Process process = Process.Start(psi))
                {
                    process?.WaitForExit();
                }
            }
            else
            {
                string deleteArguments = $"/Delete /TN \"{taskName}\" /F";

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = deleteArguments,
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                using (Process process = Process.Start(psi))
                {
                    process?.WaitForExit();
                }
            }
        }
    }
}