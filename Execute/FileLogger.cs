using UnityEngine;
using System.IO;
using System;
using System.Text;

namespace GradationBaker.Execute
{
    public static class FileLogger
    {
        private static string _logPath;

        private static string LogPath
        {
            get
            {
                if (string.IsNullOrEmpty(_logPath))
                {
                    // Find the path relative to the script or project
                    // Assuming Assets/Editor/GradationBaker/Log/
                    string root = "Assets/Editor/GradationBaker/Log";
                    if (!Directory.Exists(root))
                    {
                        Directory.CreateDirectory(root);
                    }
                    _logPath = Path.Combine(root, "debug_log.txt");
                }
                return _logPath;
            }
        }

        public static void Log(string message)
        {
            string log = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            Debug.Log(log);
            WriteToFile(log);
        }

        public static void LogError(string message)
        {
            string log = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [ERROR] {message}";
            Debug.LogError(log);
            WriteToFile(log);
        }

        private static void WriteToFile(string log)
        {
            try
            {
                File.AppendAllText(LogPath, log + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to write to log file: {ex.Message}");
            }
        }
        
        public static void Clear()
        {
             try
            {
                if (File.Exists(LogPath))
                {
                    File.WriteAllText(LogPath, string.Empty);
                }
            }
            catch {}
        }
    }
}
