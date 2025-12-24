using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Plugins.Web;
using System.ComponentModel;
using System.Linq;
using SKGoogle = Microsoft.SemanticKernel.Plugins.Web.Google.GoogleConnector;

// Disable experimental warnings for the entire file
#pragma warning disable SKEXP0050 

namespace AIHousingAssistant.Application.SemanticKernel.Plugins
{
    public class WebSearchPlugin
    {
        private readonly SKGoogle _googleConnector;

        public WebSearchPlugin(SKGoogle googleConnector)
        {
            _googleConnector = googleConnector;
        }

        [KernelFunction]
        [Description("Searches the web to answer questions about real-time events, weather, news, and current world information.")]
        public async Task<string> SearchAsync(
            [Description("The specific search query")] string query)
        {
            try
            {
                // Explicitly use <WebPage> to get structured results
                var results = await _googleConnector.SearchAsync<WebPage>(
                    query,
                    count: 5,
                    offset: 0,
                    cancellationToken: default);

                if (results == null || !results.Any())
                {
                    return "No relevant web results were found.";
                }

                // English comments as per your instructions
                // Format results into a readable string for the LLM
                var formattedResults = results.Select(r =>
                    $"Title: {r.Name}\n" +
                    $"Snippet: {r.Snippet}\n" +
                    $"URL: {r.Url}");

                return string.Join("\n\n---\n\n", formattedResults);
            }
            catch (Exception ex)
            {
                return $"Error occurred while searching: {ex.Message}";
            }
        }
    }
}
#pragma warning restore SKEXP0050