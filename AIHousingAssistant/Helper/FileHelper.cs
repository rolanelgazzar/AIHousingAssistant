// Use English comments in the code
using MarkItDownSharp;
using Microsoft.AspNetCore.Http;
using System.Text;
using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace AIHousingAssistant.Helper
{
    public static class FileHelper
    {
        private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
        private const string TimestampFormat = "yyyy-MM-dd_HH-mm-ss";

        

        public static async Task<string> SaveFileAsync(IFormFile file, string ProcessingFolderPath)
        {
            var rootPath = Path.Combine(Directory.GetCurrentDirectory(), ProcessingFolderPath);
            if (!Directory.Exists(rootPath)) Directory.CreateDirectory(rootPath);

            var safeFileName = Path.GetFileName(file.FileName);
            var filePath = Path.Combine(rootPath, safeFileName);

            using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                await file.CopyToAsync(stream);

            return filePath;
        }

        public static async Task WriteJsonAsync<T>(string folderPath, string fileName, T data)
        {
            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
            var path = Path.Combine(folderPath, fileName);
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

        public static string GetSafeFileNameFromPath(string filePath) => Path.GetFileName(filePath);

        public static string GetProcessingRoot(string processingFolderPath)
        {
            var rootPath = Path.Combine(Directory.GetCurrentDirectory(), processingFolderPath);
            if (!Directory.Exists(rootPath)) Directory.CreateDirectory(rootPath);
            return rootPath;
        }
    }
}