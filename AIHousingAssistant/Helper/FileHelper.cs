using AIHousingAssistant.Models.Settings;
using DocumentFormat.OpenXml.Packaging;
using System.Text;
using System.Text.Json;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
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
            var timestampedFileName = safeFileName;// AddTimestampToFileName(safeFileName);

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
            var timestampedFileName = safeName;// AddTimestampToFileName(safeName);

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


        public static string ExtractWordText(string filePath)
        {
            using var doc = WordprocessingDocument.Open(filePath, false);
            var body = doc.MainDocumentPart?.Document?.Body;
            return body?.InnerText ?? string.Empty;
        }

        public static string ExtractPdfText(string filePath)
        {
            var sb = new StringBuilder();

            using var pdf = PdfDocument.Open(filePath);
            foreach (Page page in pdf.GetPages())
            {
                if (!string.IsNullOrWhiteSpace(page.Text))
                    sb.AppendLine(page.Text);
            }

            return sb.ToString();
        }

        public static async Task<string> ExtractDocumentAsync(string filePath, string fileName)
        {
            // 2) Extract
            var fileNameLower = fileName.ToLowerInvariant();
            if (fileNameLower.EndsWith(".docx") || fileNameLower.EndsWith(".doc"))
                return ExtractWordText(filePath);
            else if (fileNameLower.EndsWith(".pdf"))
                return ExtractPdfText(filePath);
            else
                throw new Exception("Unsupported file type");

        }
    }
}
