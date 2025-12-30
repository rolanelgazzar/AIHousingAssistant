using AIHousingAssistant.Application.Services.DocumentProcessing.Abstractions;
using AIHousingAssistant.Application.Services.DocumentProcessing.Models;
using AIHousingAssistant.Helper;

public class FileStorageHandler : DocumentHandlerBase
{
    // Simple parameterless constructor
    public FileStorageHandler() { }

    public override async Task<DocumentProcessingRequest> HandleAsync(DocumentProcessingRequest request)
    {
        // 1. Get settings directly from the request object
        var settings = request.Settings ?? throw new Exception("Settings are missing from the request.");

        string langFolder = request.Language?.ToLower() == "arabic" ? "AR" : "EN";

        // 2. Build the path using settings from the request
        string storageSubPath = Path.Combine(settings.ProcessingFolder, "MD", langFolder);
        string rootPath = FileHelper.GetProcessingRoot(storageSubPath);

        string fileName = Path.GetFileNameWithoutExtension(request.FilePath) + ".md";
        string fullPath = Path.Combine(rootPath, fileName);

        // 3. Save the file
        await File.WriteAllTextAsync(fullPath, request.Content);
        request.FinalSavedPath = fullPath;

        return await base.HandleAsync(request);
    }
}