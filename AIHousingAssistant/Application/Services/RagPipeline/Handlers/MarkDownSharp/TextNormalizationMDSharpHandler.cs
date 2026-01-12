using System.Text.RegularExpressions;
using AIHousingAssistant.Application.Services.RagPipeline.Abstractions;
using AIHousingAssistant.Application.Services.RagPipeline.Models;

namespace AIHousingAssistant.Application.Services.RagPipeline.Handlers
{
    // English comment: This handler cleans duplication and normalizes text/tables for LLM consumption.
    public class TextNormalizationMDSharpHandler : RagHandlerBase
    {
        public override async Task<RagPipelineRequest> HandleAsync(RagPipelineRequest request)
        {
            request.NormalizedContent=request.MarkdownContent;
            return await base.HandleAsync(request);

            //if (string.IsNullOrWhiteSpace(request.MarkdownContent))
            //    return await base.HandleAsync(request);

            //string text = request.MarkdownContent;

            //// 1. CLEANING ARTIFACTS
            //// English comment: This Regex keeps English, Arabic, Numbers, and essential HTML/Markdown symbols
            //// It specifically removes weird artifacts like ΓÇï and ┬á
            //text = Regex.Replace(text, @"[^\u0000-\u007F\u0600-\u06FF\s\t\n\r<>=""/|#*-_]", "");

            //// 2. WHITESPACE NORMALIZATION
            //// English comment: Standardize line breaks
            //text = text.Replace("\r\n", "\n");

            //// English comment: Remove excessive newlines (keep max 2 for paragraph separation)
            //text = Regex.Replace(text, @"\n{3,}", "\n\n");

            //// English comment: Trim trailing spaces on each line to save tokens
            //text = Regex.Replace(text, @"[ \t]+$", "", RegexOptions.Multiline);

            //// 3. FINAL ASSIGNMENT
            //request.NormalizedContent = text.Trim();

            //// English comment: Continue to the next handler (e.g., FileMetaDataHandler or ChunkingHandler)
            //return await base.HandleAsync(request);
        }
    }
}