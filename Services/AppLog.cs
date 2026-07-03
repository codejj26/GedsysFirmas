using System.Diagnostics;
using System.IO;
using System.Text;

namespace FirmasApp.Services;

public static class AppLog
{
    private const string Category = "FirmasApp";
    private const long MaxFileBytes = 2 * 1024 * 1024; // 2 MB
    private static readonly object _fileLock = new();

    private static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FirmasApp");

    private static readonly string LogFilePath = Path.Combine(LogDir, "debug.log");

    public static string LogFile => LogFilePath;

    public static void Debug(string source, string message) => Write("DEBUG", source, message, null);

    public static void Info(string source, string message) => Write("INFO ", source, message, null);

    public static void Warn(string source, string message) => Write("WARN ", source, message, null);

    public static void Error(string source, string message, Exception? ex = null)
        => Write("ERROR", source, message, ex);

    private static void Write(string level, string source, string message, Exception? ex)
    {
        var line = $"[{Category}][{level}][{source}] {DateTime.Now:HH:mm:ss.fff} {message}"
            + (ex != null ? $" | {ex.GetType().Name}: {ex.Message}" : string.Empty);

        try
        {
            System.Diagnostics.Debug.WriteLine(line);
            Console.WriteLine(line);

            lock (_fileLock)
            {
                Directory.CreateDirectory(LogDir);
                RotateIfNeeded();
                File.AppendAllText(LogFilePath, line + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch
        {
            // Si falla el log a archivo, no queremos romper la app
        }
    }

    private static void RotateIfNeeded()
    {
        try
        {
            if (!File.Exists(LogFilePath)) return;

            var info = new FileInfo(LogFilePath);
            if (info.Length < MaxFileBytes) return;

            var oldPath = LogFilePath + ".old";
            if (File.Exists(oldPath)) File.Delete(oldPath);
            File.Move(LogFilePath, oldPath);
        }
        catch
        {
            // Ignorar errores de rotación
        }
    }
}
