using AIHousingAssistant.Models.Settings;
using AIHousingAssistant.Models;
using LangChain.Splitters.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace AIHousingAssistant.Application.Services
{
 
    public class ChunkService: IChunkService
    {
        // -----------------------------------------------------------
        // Save file locally to the processing folder

        private readonly ProviderSettings _providerSettings;
        private readonly string _uploadFolder;
        private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

        public ChunkService(IOptions<ProviderSettings> providerSettings)
        {
            if (providerSettings == null)
                throw new ArgumentNullException(nameof(providerSettings));
            _providerSettings = providerSettings.Value;

            // Create or use the folder where processed files and chunks will be stored
            _uploadFolder = Path.Combine(
                Directory.GetCurrentDirectory(),
               _providerSettings.ProcessingFolder
            );

            if (!Directory.Exists(_uploadFolder))
                Directory.CreateDirectory(_uploadFolder);

        }

        // -----------------------------------------------------------
        // Split text into overlapping chunks for better retrieval quality
               public async Task<List<TextChunk>> CreateChunksAsync(string text)

        {
            // 1️⃣ Return empty list if input text is null or whitespace
            if (string.IsNullOrWhiteSpace(text))
                return new List<TextChunk>();

            // 2️⃣ Initialize LangChain.NET text splitter
            var splitter = new RecursiveCharacterTextSplitter(
                chunkSize: 1000,   // max characters per chunk
                chunkOverlap: 100  // small overlap for better context
            );

            // 3️⃣ Split text into raw chunks (synchronous API)
            var rawChunks = splitter.SplitText(text); // List<string>

            // 4️⃣ Map raw chunks to TextChunk objects, trim and skip empty ones
            var chunks = rawChunks
                .Select((c, i) => new TextChunk
                {
                    Index = i,
                    Content = c.Trim()
                })
                .Where(tc => !string.IsNullOrWhiteSpace(tc.Content))
                .ToList();

            // 5️⃣ Save chunks to JSON file for debugging or transparency
            SaveChunksAsync(chunks);

            // 6️⃣ Return the final list of TextChunk objects
            return chunks;
        }

        public async Task SaveChunksAsync(List<TextChunk> chunks)
        {
            string filePath = Path.Combine(_uploadFolder, _providerSettings.VectorStoreFilename);
            string json = JsonSerializer.Serialize(chunks, SerializerOptions);
            await File.WriteAllTextAsync(filePath, json);
        }

        // Shared JSON options for writing files

    }
}
