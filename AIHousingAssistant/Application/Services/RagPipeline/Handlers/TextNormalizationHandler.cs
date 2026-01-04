using AIHousingAssistant.Application.Services.RagPipeline.Abstractions;
using AIHousingAssistant.Application.Services.RagPipeline.Models;
using Markdig;
using Markdig.Renderers.Normalize;
using System.Text.RegularExpressions;

namespace AIHousingAssistant.Application.Services.RagPipeline.Handlers
{
    // English comment: This handler cleans duplication and formats markdown tables using Markdig.
    public class TextNormalizationHandler : RagHandlerBase
    {
        public override async Task<RagPipelineRequest> HandleAsync(RagPipelineRequest request)
        {
            try
            {
                // English comment: Check if there is content to normalize from the previous handler
                if (string.IsNullOrWhiteSpace(request.MarkdownContent))
                {
                    return await base.HandleAsync(request);
                }

                // 1. DEDUPLICATION LOGIC
                // English comment: In your file (Tamkeen Plus), sentences and links are repeated twice. 
                // This regex removes immediate identical repetitions of phrases (10+ characters).
                string rawText = request.MarkdownContent;
                string deduplicatedText = Regex.Replace(rawText, @"(.{10,})\1", "$1", RegexOptions.Singleline);

                // 2. MARKDIG CONFIGURATION
                // English comment: Configure the pipeline to recognize and fix Pipe Tables (| column |)
                var pipeline = new MarkdownPipelineBuilder()
                    .UseAdvancedExtensions()
                    .UsePipeTables()
                    .Build();

                // 3. NORMALIZATION RENDERING
                // English comment: Parse the cleaned text and use NormalizeRenderer to rebuild it.
                // This ensures the markdown is standard and readable by the LLM.
                var document = Markdown.Parse(deduplicatedText, pipeline);

                using (var writer = new StringWriter())
                {
                    var renderer = new NormalizeRenderer(writer);
                    renderer.Render(document);

                    string finalMarkdown = writer.ToString();

                    // 4. PREPARE FOR CHUNKING
                    // English comment: Standardize line breaks to prevent breaking tables later.
                    request.NormalizedContent = Regex.Replace(finalMarkdown, @"\n{3,}", "\n\n").Trim();
                }

                // English comment: Continue to the next handler in the RAG pipeline
                return await base.HandleAsync(request);
            }
            catch (Exception ex)
            {
                // English comment: Re-throw to ensure the error is captured by the Pipeline Manager
                throw;
            }
        }
    }
}