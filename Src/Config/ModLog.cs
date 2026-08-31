using System;
using System.IO;
using TaleWorlds.Library;

namespace AICompanion.Config
{
    /// <summary>
    /// Tiny file logger, separate from Debug.Print (which only shows in a dev console nobody
    /// has open during normal play). Writes to Modules/AICompanion/aicompanion.log so issues —
    /// especially interactions with other mods like TOR — can be inspected after the fact.
    /// </summary>
    public static class ModLog
    {
        private static readonly string LogPath =
            Path.Combine(BasePath.Name, "Modules", "AICompanion", "aicompanion.log");

        private static readonly object Lock = new object();

        public static void Info(string message) => Write("INFO", message);

        public static void Error(string message) => Write("ERROR", message);

        public static void Error(string message, Exception ex) => Write("ERROR", $"{message}: {ex}");

        private static void Write(string level, string message)
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}{Environment.NewLine}";

            // Best-effort only — logging must never be the thing that breaks the mod.
            try
            {
                lock (Lock)
                {
                    File.AppendAllText(LogPath, line);
                }
            }
            catch
            {
                // Nothing sensible to do if the log itself can't be written.
            }

            Debug.Print($"[AICompanion] {message}");
        }
    }
}
