
using AIHousingAssistant.Application.Services.DocumentProcessing.Abstractions;
using AIHousingAssistant.Application.Services.DocumentProcessing.Models;
using MarkItDownSharp; // The main namespace

namespace AIHousingAssistant.Application.Services.DocumentProcessing.Handlers
{
    public class MarkItDownHandler : DocumentHandlerBase
    {
        public override async Task<DocumentProcessingRequest> HandleAsync(DocumentProcessingRequest request)
        {
            // The library usually uses 'MarkItDown' class as the entry point
            var markItDown = new MarkItDownConverter();

            // Convert the file to Markdown
            var result = await markItDown.ConvertLocalAsync(request.FilePath);

            // Access the content property from the result
            request.Content = result.TextContent;

            // Continue the chain
            return await base.HandleAsync(request);
        }
    }
}