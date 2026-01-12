using System.Diagnostics;
using System.Text;
using AIHousingAssistant.Application.Services.RagPipeline.Abstractions;
using AIHousingAssistant.Application.Services.RagPipeline.Models;

namespace AIHousingAssistant.Application.Services.RagPipeline.Handlers.MarkupPandoc
{
    public class MarkDownPandocHandler : RagHandlerBase
    {
        public override async Task<RagPipelineRequest> HandleAsync(RagPipelineRequest request)
        {
            if (string.IsNullOrEmpty(request.FilePath))
                throw new ArgumentNullException(nameof(request.FilePath), "File path is missing.");

            string extension = Path.GetExtension(request.FilePath).ToLower();
            string content = string.Empty;

            // English comment: Route based on file extension to the appropriate extraction method
            switch (extension)
            {
                case ".docx":
                case ".doc":
                    content = await ConvertWordWithPandoc(request.FilePath);
                    break;

                case ".pdf":
                    // English comment: Placeholder for PDF logic (Integration with PdfPig or similar)
                    content = await ConvertPDFWithPandoc(request.FilePath);
                    break;

                case ".xlsx":
                case ".xls":
                    // English comment: Placeholder for Excel logic (Integration with ClosedXML)
                    content = "[Excel Content Extraction - Implementation Pending]";
                    break;

                default:
                    throw new NotSupportedException($"File type {extension} is not supported.");
            }

            request.MarkdownContent = content;

            // English comment: Move to the next handler in the pipeline
            return await base.HandleAsync(request);
        }
        private async Task<string> ConvertWordWithPandoc(string filePath)
        {
            // Updated command to ensure pipe-style Markdown tables
            // -f docx: Input format (DOCX)
            // -t markdown: Basic Markdown format (forces Markdown tables)
            // --wrap=none: Ensures that lines and tables are not wrapped automatically
            string pandocArgs = $"-f docx -t markdown --wrap=none \"{filePath}\"";
            //  string pandocArgs = $"-f docx -t gfm --wrap=none \"{filePath}\"";
            var startInfo = new ProcessStartInfo
            {
                FileName = "pandoc",
                Arguments = pandocArgs,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            using (var process = new Process { StartInfo = startInfo })
            {
                process.Start();

                string result = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    throw new Exception($"Pandoc Error: {error}");
                }

                return result;
            }
        }
        private async Task<string> ConvertPDFWithPandoc(string filePath)
        {
            return "";
        }


    }
}