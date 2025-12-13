using AIHousingAssistant.Models.Settings;
using System.Text.Json;

namespace AIHousingAssistant.Helper
{
    public static class FileHelper
    {
        private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

        // Timestamp format with underscores (safe for Windows file names)
        private const string TimestampFormat = "yyyy-MM-dd_HH-mm-ss";

        // Build a timestamped file name:
        // example: report.pdf -> report_2025-12-13_14-07-55.pdf
        private static string AddTimestampToFileName(string fileName)
        {
            var name = Path.GetFileNameWithoutExtension(fileName);
            var ext = Path.GetExtension(fileName);

            var ts = DateTime.Now.ToString(TimestampFormat);
            return $"{name}_{ts}{ext}";
        }

        public static async Task<string> SaveFileAsync(IFormFile file, string ProcessingFolderPath)
        {
            // Create or use the folder where processed files and chunks will be stored
            var rootPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                ProcessingFolderPath
            );

            if (!Directory.Exists(rootPath))
                Directory.CreateDirectory(rootPath);

            var safeFileName = Path.GetFileName(file.FileName);

            // NEW: always save with timestamp to avoid overwrite/locking issues
            var timestampedFileName = AddTimestampToFileName(safeFileName);

            var filePath = Path.Combine(rootPath, timestampedFileName);

            using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                await file.CopyToAsync(stream);

            return filePath;
        }

        public static async Task WriteJsonAsync<T>(string folderPath, string fileName, T data)
        {
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            // NEW: always write JSON with timestamp to avoid overwrite/locking issues
            // example: chunks.json -> chunks_2025-12-13_14-07-55.json
            var safeName = Path.GetFileName(fileName);
            var timestampedFileName = AddTimestampToFileName(safeName);

            var path = Path.Combine(folderPath, timestampedFileName);

            var json = JsonSerializer.Serialize(data, SerializerOptions);
            await File.WriteAllTextAsync(path, json);
        }

        public static async Task<T?> ReadJsonAsync<T>(string folderPath, string fileName)
        {
            var path = Path.Combine(folderPath, fileName);
            if (!File.Exists(path)) return default;

            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<T>(json);
        }

        public static string GetSafeFileName(IFormFile file)
            => Path.GetFileName(file.FileName);

        public static string GetSafeFileNameFromPath(string filePath)
            => Path.GetFileName(filePath);

        public static string GetFileNameWithoutExtension(string filePathOrName)
            => Path.GetFileNameWithoutExtension(filePathOrName);
    }
}
