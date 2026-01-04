using System.Text.RegularExpressions;
using System.Text;
using AIHousingAssistant.Application.Services.RagPipeline.Abstractions;
using AIHousingAssistant.Application.Services.RagPipeline.Models;

namespace AIHousingAssistant.Application.Services.RagPipeline.Handlers
{
    public class TextNormalizationHandler : RagHandlerBase
    {
        public override async Task<RagPipelineRequest> HandleAsync(RagPipelineRequest request)
        {               return await base.HandleAsync(request);
            if (string.IsNullOrEmpty(request.Content))
                return await base.HandleAsync(request);

            // 1. Repair reversed Arabic text caused by PDF extraction
            request.Content = FixArabicText(request.Content);

            // 2. Identify and format potential tables (Words or Numbers)
            request.Content = FormatGenericTables(request.Content);

            // 3. Clean unnecessary artifacts while preserving table pipes
            request.Content = FinalCleanup(request.Content);
  

        }

        private string FixArabicText(string input)
        {
            var lines = input.Split(new[] { "\n", "\r\n" }, StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++)
            {
                // If the line is primarily Arabic, it's likely reversed in PDFs
                if (IsPrimarilyArabic(lines[i]))
                {
                    char[] charArray = lines[i].ToCharArray();
                    Array.Reverse(charArray);
                    lines[i] = new string(charArray);
                }
            }
            return string.Join(Environment.NewLine, lines);
        }

        private string FormatGenericTables(string input)
        {
            var lines = input.Split(new[] { "\n", "\r\n" }, StringSplitOptions.None);
            var result = new StringBuilder();

            foreach (var line in lines)
            {
                // If a line has multiple spaces between words/numbers, it's likely a table row
                // This regex finds 3 or more spaces as a column separator
                if (Regex.IsMatch(line, @"\w+\s{3,}\w+") || Regex.IsMatch(line, @"\d+\s{3,}\d+"))
                {
                    // Replace multiple spaces with Markdown pipe separator
                    string formattedLine = "|" + Regex.Replace(line.Trim(), @"\s{3,}", "|") + "|";
                    result.AppendLine(formattedLine);
                }
                else
                {
                    result.AppendLine(line);
                }
            }
            return result.ToString();
        }

        private bool IsPrimarilyArabic(string text)
        {
            // Regex for Arabic Unicode range
            return Regex.IsMatch(text, @"[\u0600-\u06FF]");
        }

        private string FinalCleanup(string text)
        {
            // Remove multiple empty lines but keep single ones
            text = Regex.Replace(text, @"(\r\n|\n){3,}", Environment.NewLine + Environment.NewLine);
            return text.Trim();
        }
    }
}