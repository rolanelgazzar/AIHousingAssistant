using AIHousingAssistant.Application.Enum;
using AIHousingAssistant.Application.Services.RagPipeline.Abstractions;
using AIHousingAssistant.Application.Services.RagPipeline.Models;
using AIHousingAssistant.Helper;
using System.Text;

namespace AIHousingAssistant.Application.Services.RagPipeline.Handlers
{
    public class FileStorageHandler : RagHandlerBase
    {
        public override async Task<RagPipelineRequest> HandleAsync(RagPipelineRequest request)
        {
            var settings = request.Settings ?? throw new Exception("Settings are missing from the request.");

            // English comment: Determine the language subfolder
            string langFolder = request.Language?.ToLower() == "arabic" ? "AR" : "EN";

            // English comment: Get the tool name from the enum (e.g., RagDocling, RagPandDoc)
            string toolName = System.Enum.GetName(typeof(SearchToolType), request.RagUiRequest.ToolsSearchBy) ?? "UnknownTool";

            // 1. Save Extracted Markdown Content
            if (!string.IsNullOrEmpty(request.MarkdownContent))
            {
                // Path: Processing/MD/RagDocling/AR/filename.txt
                string mdPath = Path.Combine(settings.ProcessingFolder, "MD", toolName, langFolder);
                string mdRoot = FileHelper.GetProcessingRoot(mdPath);

                string fileName = Path.GetFileNameWithoutExtension(request.FilePath) + ".txt";
                string fullPath = Path.Combine(mdRoot, fileName);

                await File.WriteAllTextAsync(fullPath, request.MarkdownContent, Encoding.UTF8);
            }

            // 2. Save Normalized Content (After cleaning/formatting)
            if (!string.IsNullOrEmpty(request.NormalizedContent))
            {
                // Path: Processing/Normalized/RagDocling/AR/filename_normalized.txt
                string normPath = Path.Combine(settings.ProcessingFolder, "Normalized", toolName, langFolder);
                string normRoot = FileHelper.GetProcessingRoot(normPath);

                string normFileName = Path.GetFileNameWithoutExtension(request.FilePath) + "_normalized.txt";
                string normFullPath = Path.Combine(normRoot, normFileName);

                await File.WriteAllTextAsync(normFullPath, request.NormalizedContent, Encoding.UTF8);
            }

            // 3. Save JSON Chunks
            if (request.Chunks != null && request.Chunks.Any())
            {
                // Path: Processing/Chunks/RagDocling/
                string chunkPath = Path.Combine(settings.ProcessingFolder, "Chunks", toolName);
                string chunkRoot = FileHelper.GetProcessingRoot(chunkPath);

                string chunkingMode = System.Enum.GetName(typeof(ChunkingMode), request.RagUiRequest.ChunkingMode) ?? "Default";

                // Detailed filename for history: Chunks--filename--Paragraph.json
                var detailedFileName = $"{settings.ChunksFileName}--{Path.GetFileNameWithoutExtension(request.FilePath)}--{chunkingMode}.json";

                // English comment: Save both the specific file chunks and the main chunks file
                await FileHelper.WriteJsonAsync(chunkRoot, detailedFileName, request.Chunks);
                await FileHelper.WriteJsonAsync(chunkRoot, settings.ChunksFileName, request.Chunks);
            }

            // English comment: Move to the next handler in the pipeline
            return await base.HandleAsync(request);
        }
    }
}