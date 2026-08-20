using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace SolidTweaks.Infrastructure.Diagnostics
{
    internal static class ApplicationLog
    {
        private const long MaximumLogSize = 5L * 1024L * 1024L;
        private static readonly object SyncRoot = new object();
        private static string logPath;
        private static bool initialized;

        public static string CurrentPath
        {
            get
            {
                EnsureInitialized();
                return logPath ?? "Log unavailable";
            }
        }

        public static void Initialize(string[] arguments)
        {
            lock (SyncRoot)
            {
                if (initialized)
                {
                    return;
                }
                initialized = true;

                var failures = new List<string>();
                foreach (string candidate in GetCandidatePaths())
                {
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(candidate));
                        AppendRaw(candidate, string.Empty);
                        logPath = candidate;
                        break;
                    }
                    catch (Exception exception)
                    {
                        failures.Add(candidate + " => " + exception.GetType().Name + ": " + exception.Message);
                    }
                }

                if (logPath == null)
                {
                    return;
                }

                RotateIfNeeded();
                WriteCore("INFO", "================ New run ================", null);
                WriteCore(
                    "INFO",
                    "EXE=" + SafeExecutablePath() +
                    "; version=" + Assembly.GetExecutingAssembly().GetName().Version +
                    "; PID=" + Process.GetCurrentProcess().Id +
                    "; OS=" + Environment.OSVersion +
                    "; CLR=" + Environment.Version +
                    "; 64bit=" + Environment.Is64BitProcess +
                    "; build=" + GetExecutableFingerprint() +
                    "; arguments=" + FormatArguments(arguments),
                    null);
                if (failures.Count > 0)
                {
                    WriteCore("WARN", "Failures before selecting the log destination: " + string.Join(" | ", failures), null);
                }
                WriteCore("INFO", "Active log at " + logPath, null);
            }
        }

        public static void Info(string message)
        {
            Write("INFO", message, null);
        }

        public static void Warning(string message)
        {
            Write("WARN", message, null);
        }

        public static void Error(string message, Exception exception = null)
        {
            Write("ERROR", message, exception);
        }

        private static void Write(string level, string message, Exception exception)
        {
            EnsureInitialized();
            lock (SyncRoot)
            {
                WriteCore(level, message, exception);
            }
        }

        private static void WriteCore(string level, string message, Exception exception)
        {
            if (logPath == null)
            {
                return;
            }
            try
            {
                var text = new StringBuilder();
                text.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                text.Append(" [").Append(level).Append("]");
                text.Append(" [PID ").Append(Process.GetCurrentProcess().Id).Append("]");
                text.Append(" [T ").Append(Thread.CurrentThread.ManagedThreadId).Append("] ");
                text.AppendLine(message ?? string.Empty);
                if (exception != null)
                {
                    text.AppendLine(exception.ToString());
                }
                AppendRaw(logPath, text.ToString());
            }
            catch
            {
                // O diagnóstico nunca deve derrubar a aplicação.
            }
        }

        private static void EnsureInitialized()
        {
            if (!initialized)
            {
                Initialize(new string[0]);
            }
        }

        private static IEnumerable<string> GetCandidatePaths()
        {
            yield return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SolidTweaks",
                "Logs",
                "SolidTweaks.log");
            yield return Path.Combine(Path.GetTempPath(), "SolidTweaks", "Logs", "SolidTweaks.log");
            yield return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SolidTweaks.log");
        }

        private static void RotateIfNeeded()
        {
            try
            {
                var current = new FileInfo(logPath);
                if (!current.Exists || current.Length < MaximumLogSize)
                {
                    return;
                }
                string previous = Path.Combine(current.DirectoryName, "SolidTweaks.previous.log");
                if (File.Exists(previous))
                {
                    File.Delete(previous);
                }
                File.Move(logPath, previous);
                AppendRaw(logPath, string.Empty);
            }
            catch
            {
                // Se a rotação falhar, continua anexando ao arquivo atual.
            }
        }

        private static void AppendRaw(string path, string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
            Exception lastError = null;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                    {
                        stream.Write(bytes, 0, bytes.Length);
                        stream.Flush();
                    }
                    return;
                }
                catch (IOException exception)
                {
                    lastError = exception;
                    Thread.Sleep(40 * (attempt + 1));
                }
            }
            if (lastError != null)
            {
                throw lastError;
            }
        }

        private static string SafeExecutablePath()
        {
            try { return Process.GetCurrentProcess().MainModule.FileName; }
            catch { return AppDomain.CurrentDomain.BaseDirectory; }
        }

        private static string GetExecutableFingerprint()
        {
            try
            {
                string executable = SafeExecutablePath();
                using (SHA256 algorithm = SHA256.Create())
                using (FileStream stream = new FileStream(executable, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    byte[] hash = algorithm.ComputeHash(stream);
                    var value = new StringBuilder(hash.Length * 2);
                    for (int index = 0; index < hash.Length; index++)
                    {
                        value.Append(hash[index].ToString("X2"));
                    }
                    return "SHA256:" + value;
                }
            }
            catch (Exception exception)
            {
                return "unavailable:" + exception.GetType().Name;
            }
        }

        private static string FormatArguments(string[] arguments)
        {
            return arguments == null || arguments.Length == 0
                ? "<none>"
                : string.Join(" ", arguments);
        }
    }
}
