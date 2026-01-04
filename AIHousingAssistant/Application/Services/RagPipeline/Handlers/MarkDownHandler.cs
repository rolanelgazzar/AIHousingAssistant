

using AIHousingAssistant.Application.Services.RagPipeline.Abstractions;
using AIHousingAssistant.Application.Services.RagPipeline.Models;
using MarkItDownSharp; // The main namespace

namespace AIHousingAssistant.Application.Services.RagPipeline.Handlers
{
    public class MarkDownHandler : RagHandlerBase
    {
        public override async Task<RagPipelineRequest> HandleAsync(RagPipelineRequest request)
        {
            // The library usually uses 'MarkItDown' class as the entry point
            var markItDown = new MarkItDownConverter();

            // Convert the file to Markdown
            var result = await markItDown.ConvertLocalAsync(request.FilePath);

            // Access the content property from the result
            request.MarkdownContent = result.TextContent;

            // Continue the chain
            return await base.HandleAsync(request);
        }
    }
}