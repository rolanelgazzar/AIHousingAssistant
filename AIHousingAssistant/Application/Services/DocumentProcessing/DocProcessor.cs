using AIHousingAssistant.Application.Services.DocumentProcessing.Abstractions;
using AIHousingAssistant.Application.Services.DocumentProcessing.Handlers;
using AIHousingAssistant.Application.Services.DocumentProcessing.Models;
using AIHousingAssistant.Helper;
using AIHousingAssistant.Models.Settings;
using Microsoft.Extensions.Options;

public class DocProcessor : IDocProcessor
{
    private readonly IOptions<Settings> _settings;

    public DocProcessor(IOptions<Settings> settings)
    {
        _settings = settings;
    }

    public async Task<DocumentProcessingRequest> ProcessAndSaveAsync(IFormFile file)
    {
        // 1. Save temp file and prepare the request with settings
        var tempPath = await FileHelper.SaveFileAsync(file, _settings.Value.ProcessingFolder);

        var request = new DocumentProcessingRequest
        {
            FilePath = tempPath,
            Settings = _settings.Value // Put settings in the bag here
        };

        // 2. Initialize all handlers - Perfectly consistent!
        var h1 = new MarkItDownHandler();
        var h2 = new FileMetaDataHandler();
        var h3 = new TextNormalizationHandler();
        var h4 = new FileStorageHandler();

        // 3. Chain and Execute
        h1.SetNext(h2).SetNext(h3).SetNext(h4);

        var result = await h1.HandleAsync(request);

        return result;
    }
}