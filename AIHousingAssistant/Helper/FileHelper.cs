using AIHousingAssistant.Models.Settings;
using System.Text.Json;

namespace AIHousingAssistant.Helper
{
    public static class FileHelper
    {
        private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

        public static async Task<string> SaveFileAsync(IFormFile file,string ProcessingFolderPath)
        {
            // Create or use the folder where processed files and chunks will be stored
            var rootPath = Path.Combine(
            Directory.GetCurrentDirectory(),
               ProcessingFolderPath
            );

            if (!Directory.Exists(rootPath))
                Directory.CreateDirectory(rootPath);

            var safeFileName = Path.GetFileName(file.FileName);
            var filePath = Path.Combine(rootPath, safeFileName);

            using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                await file.CopyToAsync(stream);

            return filePath;
        }

       
    }
}
