using AIHousingAssistant.Application.Services.RagPipeline.Abstractions;
using AIHousingAssistant.Application.Services.RagPipeline.Models;
using AIHousingAssistant.Helper;
using AIHousingAssistant.Application.Enum;

namespace AIHousingAssistant.Application.Services.RagPipeline.Handlers.Indexing
{
    // English comment: This handler saves the Markdown content in language-specific folders, 
    // but saves the JSON chunks in the main processing folder as per the original logic.
    public class FileStorageHandler : RagHandlerBase
    {
        public override async Task<RagPipelineRequest> HandleAsync(RagPipelineRequest request)
        {
            var settings = request.Settings ?? throw new Exception("Settings are missing from the request.");

            // 1. Path for Clean Text (Stays inside MD/AR or MD/EN)
            string langFolder = request.Language?.ToLower() == "arabic" ? "AR" : "EN";
            string mdStorageSubPath = Path.Combine(settings.ProcessingFolder, "MD", langFolder);
            string mdRootPath = FileHelper.GetProcessingRoot(mdStorageSubPath);

            if (!string.IsNullOrEmpty(request.Content))
            {
                string txtFileName = Path.GetFileNameWithoutExtension(request.FilePath) + ".txt";
                string txtFullPath = Path.Combine(mdRootPath, txtFileName);
                await File.WriteAllTextAsync(txtFullPath, request.Content, System.Text.Encoding.UTF8);
                request.FinalSavedPath = txtFullPath;
            }

            // 2. Path for Chunks (Back to the Main Processing Folder)
            // This matches your original logic: _uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), settings.ProcessingFolder)
            if (request.Chunks != null && request.Chunks.Any())
            {
                string chunkingMode = System.Enum.GetName(typeof(ChunkingMode), request.RagUiRequest.ChunkingMode);

                // Get the main processing folder root (without MD/AR/EN)
                string mainUploadFolder = FileHelper.GetProcessingRoot(settings.ProcessingFolder);

                // Use the exact naming pattern you requested
                var detailedFileName = $"{settings.ChunksFileName}--{FileHelper.GetFileNameWithoutExtension(request.FilePath)}--{chunkingMode}.json";

                // Save 1: Detailed version in the main folder
                await FileHelper.WriteJsonAsync(mainUploadFolder, detailedFileName, request.Chunks);

                // Save 2: Global version in the main folder
                await FileHelper.WriteJsonAsync(mainUploadFolder, settings.ChunksFileName, request.Chunks);
            }

            return await base.HandleAsync(request);
        }
    }
}