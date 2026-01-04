using AIHousingAssistant.Application.Enum;
using AIHousingAssistant.Application.Services.RagPipeline.Abstractions;
using AIHousingAssistant.Application.Services.RagPipeline.Models;
using AIHousingAssistant.Helper;

public class FileStorageHandler : RagHandlerBase
{
    public override async Task<RagPipelineRequest> HandleAsync(RagPipelineRequest request)
    {
        var settings = request.Settings ?? throw new Exception("Settings are missing from the request.");
        string langFolder = request.Language?.ToLower() == "arabic" ? "AR" : "EN";

        // 1. Save Extracted Content (The initial raw markdown/text)
        if (!string.IsNullOrEmpty(request.MarkdownContent))
        {
            string mdPath = Path.Combine(settings.ProcessingFolder, "MD", langFolder);
            string mdRoot = FileHelper.GetProcessingRoot(mdPath);

            string fileName = Path.GetFileNameWithoutExtension(request.FilePath) + ".txt";
            string fullPath = Path.Combine(mdRoot, fileName);

            await File.WriteAllTextAsync(fullPath, request.MarkdownContent, System.Text.Encoding.UTF8);
           // request.ExtractedFilePath = fullPath;
        }

        // 2. Save Normalized Content (The cleaned version)
        if (!string.IsNullOrEmpty(request.NormalizedContent))
        {
            string normPath = Path.Combine(settings.ProcessingFolder, "Normalized", langFolder);
            string normRoot = FileHelper.GetProcessingRoot(normPath);

            if (!Directory.Exists(normRoot)) Directory.CreateDirectory(normRoot);

            string normFileName = Path.GetFileNameWithoutExtension(request.FilePath) + "_normalized.txt";
            string normFullPath = Path.Combine(normRoot, normFileName);

            await File.WriteAllTextAsync(normFullPath, request.NormalizedContent, System.Text.Encoding.UTF8);
          //  request.NormalizedFilePath = normFullPath;
        }

        // 3. Save JSON Chunks
        if (request.Chunks != null && request.Chunks.Any())
        {
            string mainUploadFolder = FileHelper.GetProcessingRoot(settings.ProcessingFolder);
            string chunkingMode = System.Enum.GetName(typeof(ChunkingMode), request.RagUiRequest.ChunkingMode);
            var detailedFileName = $"{settings.ChunksFileName}--{FileHelper.GetFileNameWithoutExtension(request.FilePath)}--{chunkingMode}.json";

            await FileHelper.WriteJsonAsync(mainUploadFolder, detailedFileName, request.Chunks);
            await FileHelper.WriteJsonAsync(mainUploadFolder, settings.ChunksFileName, request.Chunks);
        }

        return await base.HandleAsync(request);
    }
}