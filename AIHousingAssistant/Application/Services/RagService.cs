using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using System.Text.Json;
using AIHousingAssistant.Models;
using AIHousingAssistant.Models.Settings;
using Microsoft.Extensions.Options;

using LangChain.Splitters.Text;
using System.Linq;
using AIHousingAssistant.Helper;
using OllamaSharp;
using System.Text;
using AIHousingAssistant.Application.Services.Interfaces;
using AIHousingAssistant.Application.Enum;

namespace AIHousingAssistant.Application.Services
{
    public class RagService : IRagService
    {
        private readonly string _uploadFolder;
        private readonly IInMemoryVectorStore _memoryVectorStore;
        private readonly ProviderSettings _providerSettings;
        private readonly IChunkService _chunkService;
        private readonly OllamaApiClient _ollamaClient;
        private readonly IQDrantVectorStore _QDrantVectorStore;
        private readonly IQDrantVectorStoreEF _QDrantVectorStoreEF;

        public RagService(IOptions<ProviderSettings> providerSettings,
            IInMemoryVectorStore memoryStore,
                 IQDrantVectorStore QDrantVectorStore,
                 IQDrantVectorStoreEF qDrantVectorStoreEF,
            IChunkService chunkService)
        {
            if (providerSettings == null)
                throw new ArgumentNullException(nameof(providerSettings));
            _providerSettings = providerSettings.Value;

            //// Create or use the folder where processed files and chunks will be stored
            //_uploadFolder = Path.Combine(
            //    Directory.GetCurrentDirectory(),
            //   _providerSettings.ProcessingFolder
            //);

            //if (!Directory.Exists(_uploadFolder))
            //    Directory.CreateDirectory(_uploadFolder);

            _memoryVectorStore = memoryStore ?? throw new ArgumentNullException(nameof(memoryStore));
            _QDrantVectorStore = QDrantVectorStore ?? throw new ArgumentNullException(nameof(QDrantVectorStore));
            _QDrantVectorStoreEF = qDrantVectorStoreEF ?? throw new ArgumentNullException(nameof(QDrantVectorStoreEF));

            _chunkService = chunkService;

            // Initialize Ollama client for answer generation (llama3)
            _ollamaClient = new OllamaApiClient(new Uri(_providerSettings.Ollama.Endpoint));
            _ollamaClient.SelectedModel = _providerSettings.Ollama.Model; // "llama3"
        }

        public async Task ProcessDocumentAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File is empty", nameof(file));

            string text = string.Empty;

            // 1️⃣ Save uploaded document locally
            var filePath = await FileHelper.SaveFileAsync(file, _providerSettings.ProcessingFolder);
            var source = FileHelper.GetSafeFileNameFromPath(filePath);

            // 2️⃣ Extract text based on file type
            var fileNameLower = file.FileName.ToLowerInvariant();

            if (fileNameLower.EndsWith(".docx") || fileNameLower.EndsWith(".doc"))
            {
                text = ExtractWordText(filePath);
            }
            else if (fileNameLower.EndsWith(".pdf"))
            {
                text = ExtractPdfText(filePath);
            }
            else
            {
                throw new Exception("Unsupported file type");
            }

            // Stop if the file does not contain any readable text
            if (string.IsNullOrWhiteSpace(text))
                throw new InvalidOperationException("No readable text found in the uploaded document.");

            // 3️⃣ Split the extracted text into chunks
            var chunks = await _chunkService.CreateChunksAsync(text, ChunkingMode.LangChainRecursiveTextSplitter, source);

            if (chunks == null || chunks.Count == 0)
                throw new InvalidOperationException("No text chunks were generated from the document.");

            // 4️⃣ Convert all chunks to embeddings and store inside the Json 
            await _memoryVectorStore .StoreTextChunksAsVectorsAsync(chunks);

            // 4️⃣ Convert all chunks to embeddings and store inside the QDrantVectorStore
               await _QDrantVectorStore.StoreTextChunksAsVectorsAsync(chunks);

         //   await _QDrantVectorStoreEF.StoreTextChunksAsVectorsAsync(chunks);

        }


        // -----------------------------------------------------------
        // Extract plain text from a Word document (.docx)
        private string ExtractWordText(string filePath)
        {
            using var doc = WordprocessingDocument.Open(filePath, false);
            var body = doc.MainDocumentPart?.Document?.Body;

            return body?.InnerText ?? string.Empty;
        }

        // -----------------------------------------------------------
        // Extract text from a PDF document using PdfPig
        private string ExtractPdfText(string filePath)
        {
            string text = "";

            using var pdf = PdfDocument.Open(filePath);
            foreach (Page page in pdf.GetPages())
            {
                if (!string.IsNullOrWhiteSpace(page.Text))
                    text += page.Text + "\n";
            }

            return text;
        }

        // -----------------------------------------------------------
        // Perform a RAG query: embed the question, search for the closest chunk, return the text
        public async Task<string> AskRagJsonAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return "Query is empty.";

            // 1️⃣ Get the closest chunk using the vector store
            var resultChunk = await _memoryVectorStore.SearchClosest(query);

            if (resultChunk == null || string.IsNullOrWhiteSpace(resultChunk.Content))
                return "No related answer found.";

            // 2️⃣ Extract the most relevant answer snippet from the chunk
            var answer =await ExtractAnswerFromChunkAsync(query, resultChunk.Content);

            return string.IsNullOrWhiteSpace(answer)
                ? "No related answer found."
                : answer;
        }


        // -----------------------------------------------------------
        public async Task<string> AskRagQDrantAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return "Query is empty.";

            // 1️⃣ Get the closest chunk using the vector store
            var resultChunk = await _QDrantVectorStore.SearchClosest(query);

            if (resultChunk == null || string.IsNullOrWhiteSpace(resultChunk.Content))
                return "No related answer found.";

            // 2️⃣ Extract the most relevant answer snippet from the chunk
            var answer = await ExtractAnswerFromChunkAsync(query, resultChunk.Content);

            return string.IsNullOrWhiteSpace(answer)
                ? "No related answer found."
                : answer;
        }
        // -----------------------------------------------------------
        // Use llama3 via Ollama to extract ONLY the answer from the chunk (G in RAG)

        public async Task<string> AskRagQDrantEFAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return "Query is empty.";

            // 1️⃣ Get the closest chunk using the vector store
            var resultChunk = await _QDrantVectorStoreEF.SearchClosest(query);

            if (resultChunk == null || string.IsNullOrWhiteSpace(resultChunk.Content))
                return "No related answer found.";

            // 2️⃣ Extract the most relevant answer snippet from the chunk
            var answer = await ExtractAnswerFromChunkAsync(query, resultChunk.Content);

            return string.IsNullOrWhiteSpace(answer)
                ? "No related answer found."
                : answer;
        }

        private async Task<string> ExtractAnswerFromChunkAsync(string query, string chunkContent)
        {
            return chunkContent;
            if (string.IsNullOrWhiteSpace(chunkContent))
                return string.Empty;

            if (string.IsNullOrWhiteSpace(query))
                return chunkContent.Trim();

            // 2️⃣ Build a focused prompt for llama3 to extract ONLY the answer (G in RAG)
            var prompt = $@"
You are an AI assistant for a housing and maintenance system.

You will receive:
- CONTEXT: a piece of a knowledge base.
- QUESTION: a user question.

Your task:
- Answer the QUESTION using ONLY the information in the CONTEXT.
- If the answer is present, respond with a short, direct answer (one or two sentences).
- Do NOT mention the word CONTEXT.
- Do NOT include unrelated information.
- If the answer is not in the CONTEXT, say: ""I don't know based on the provided information.""

CONTEXT:
{chunkContent}

QUESTION:
{query}

ANSWER:
";

            // 3️⃣ Call llama3 via OllamaSharp and stream the response into a single string
            var sb = new StringBuilder();

            await foreach (var response in _ollamaClient.GenerateAsync(prompt))
            {
                if (!string.IsNullOrEmpty(response.Response))
                    sb.Append(response.Response);
            }

            var answer = sb.ToString().Trim();

            if (string.IsNullOrWhiteSpace(answer))
                return string.Empty;

            return answer;
        }

       
    }
}
