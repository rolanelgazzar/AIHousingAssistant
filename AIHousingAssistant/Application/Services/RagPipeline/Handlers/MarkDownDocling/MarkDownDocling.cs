using System.Diagnostics;
using System.Text;
using AIHousingAssistant.Application.Services.RagPipeline.Abstractions;
using AIHousingAssistant.Application.Services.RagPipeline.Models;

namespace AIHousingAssistant.Application.Services.RagPipeline.Handlers.MarkDownSharp
{
    public class MarkDownDocling : RagHandlerBase
    {
        public override async Task<RagPipelineRequest> HandleAsync(RagPipelineRequest request)
        {
            if (string.IsNullOrEmpty(request.FilePath))
                throw new ArgumentNullException(nameof(request.FilePath), "File path is missing.");

            string extension = Path.GetExtension(request.FilePath).ToLower();

            // English comment: Check if the extension is supported by Docling
            //string[] supportedExtensions = { ".docx", ".doc", ".pdf", ".pptx", ".html" };

            //if (!supportedExtensions.Contains(extension))
            //{
            //    // English comment: For Excel, you might still need a custom logic or skip
            //    if (extension == ".xlsx" || extension == ".xls")
            //    {
            //        request.MarkdownContent = "[Excel Content Extraction - Implementation Pending]";
            //        return await base.HandleAsync(request);
            //    }
            //    throw new NotSupportedException($"File type {extension} is not supported by Docling.");
            //}

            // English comment: Use Docling to convert supported files to Markdown
            request.MarkdownContent = await ConvertWithDocling(request.FilePath);

            // English comment: Move to the next handler in the pipeline
            return await base.HandleAsync(request);
        }

        private async Task<string> ConvertWithDocling(string filePath)
        {
            // English comment: Prepare temp path for the bridge script
            string scriptPath = Path.Combine(Path.GetTempPath(), "docling_converter.py");

            // English comment: Inline script for direct library access
            string pythonCode = $@"
import sys
import io
from docling.document_converter import DocumentConverter

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

def convert():
    try:
        source = r'{filePath}'
        converter = DocumentConverter()
        result = converter.convert(source)
        print(result.document.export_to_markdown())
    except Exception as e:
        print(f'PYTHON_INTERNAL_ERROR: {{str(e)}}', file=sys.stderr)
        sys.exit(1)

if __name__ == '__main__':
    convert()
";

            try
            {
                await File.WriteAllTextAsync(scriptPath, pythonCode, Encoding.UTF8);

                var startInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{scriptPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                };

                using (var process = new Process { StartInfo = startInfo })
                {
                    process.Start();

                    string markdownResult = await process.StandardOutput.ReadToEndAsync();
                    string errorLogs = await process.StandardError.ReadToEndAsync();

                    await process.WaitForExitAsync();

                    // English comment: Cleanup temp file immediately
                    if (File.Exists(scriptPath)) File.Delete(scriptPath);

                    // English comment: If process failed or returned internal python error, throw exception
                    if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(markdownResult))
                    {
                        string detailedError = !string.IsNullOrEmpty(errorLogs) ? errorLogs : "Unknown Docling Error";
                        throw new Exception($"Docling Conversion Failed: {detailedError}");
                    }

                    return markdownResult.Trim();
                }
            }
            catch (Exception ex)
            {
                // English comment: Ensure temp file is deleted even on crash
                if (File.Exists(scriptPath)) File.Delete(scriptPath);

                // English comment: Re-throw the exception to be handled by the Pipeline Processor
                throw new Exception($"[DoclingHandler Error]: {ex.Message}", ex);
            }
        }
    }
}