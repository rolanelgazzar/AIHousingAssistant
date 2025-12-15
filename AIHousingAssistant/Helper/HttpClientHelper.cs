namespace AIHousingAssistant.Helper
{
    using System;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    public class HttpClientHelper
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonSerializerOptions;

        /// <summary>
        /// Initializes the HttpClientHelper. It must receive an HttpClient instance 
        /// (preferably injected via IHttpClientFactory) to manage resources correctly.
        /// </summary>
        public HttpClientHelper(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

            // General JSON serialization settings
            _jsonSerializerOptions = new JsonSerializerOptions
            {
                // Use camelCase for JSON keys (standard for most APIs, including Qdrant)
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                // Allows reading string numbers/longs for more flexibility during deserialization
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
            };

            // Configure standard request headers
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }

        // -----------------------------------------------------------
        // CORE SEND METHOD (Private)
        // -----------------------------------------------------------

        /// <summary>
        /// The core method to send an HTTP request, handle JSON serialization/deserialization, and error reporting.
        /// </summary>
        private async Task<TResponse?> SendJsonAsync<TRequest, TResponse>(
            HttpMethod method,
            string url,
            TRequest? requestData)
            where TRequest : class
            where TResponse : class
        {
            using var request = new HttpRequestMessage(method, url);

            // 1. Serialize C# object to JSON string if requestData is provided
            if (requestData != null)
            {
                var json = JsonSerializer.Serialize(requestData, _jsonSerializerOptions);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            // 2. Send the request
            using var response = await _httpClient.SendAsync(request);

            // 3. Handle errors
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    $"API call to {url} failed with status {response.StatusCode}. Details: {errorContent}",
                    null,
                    response.StatusCode);
            }

            // 4. Deserialize JSON response to C# object if content exists
            if (response.Content.Headers.ContentLength == 0 || response.Content.Headers.ContentType?.MediaType != "application/json")
            {
                return default; // Return default if there is no content (e.g., 204 No Content)
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TResponse>(jsonResponse, _jsonSerializerOptions);
        }

        // -----------------------------------------------------------
        // PUBLIC WRAPPER METHODS (POST, PUT, GET, DELETE)
        // -----------------------------------------------------------

        /// <summary>
        /// Sends an HTTP POST request with a JSON payload and expects a JSON response.
        /// </summary>
        public Task<TResponse?> PostAsync<TRequest, TResponse>(string url, TRequest requestData)
            where TRequest : class
            where TResponse : class
        {
            return SendJsonAsync<TRequest, TResponse>(HttpMethod.Post, url, requestData);
        }

        /// <summary>
        /// Sends an HTTP PUT request with a JSON payload and expects a JSON response.
        /// </summary>
        public Task<TResponse?> PutAsync<TRequest, TResponse>(string url, TRequest requestData)
            where TRequest : class
            where TResponse : class
        {
            return SendJsonAsync<TRequest, TResponse>(HttpMethod.Put, url, requestData);
        }

        /// <summary>
        /// Sends an HTTP GET request and deserializes the JSON response.
        /// </summary>
        public async Task<TResponse?> GetAsync<TResponse>(string url)
            where TResponse : class
        {
            // We use the core SendJsonAsync method but pass null for requestData.
            // We use a dummy object for TRequest to satisfy the generic constraint.
            // Alternatively, we use the original, more explicit GET implementation for clarity:

            using var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    $"API GET call to {url} failed with status {response.StatusCode}. Details: {errorContent}",
                    null,
                    response.StatusCode);
            }

            if (response.Content.Headers.ContentLength == 0)
            {
                return default;
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TResponse>(jsonResponse, _jsonSerializerOptions);
        }

        /// <summary>
        /// Sends an HTTP DELETE request.
        /// </summary>
        public async Task DeleteAsync(string url)
        {
            using var response = await _httpClient.DeleteAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    $"API DELETE call to {url} failed with status {response.StatusCode}. Details: {errorContent}",
                    null,
                    response.StatusCode);
            }
            // Success (2xx status codes) handled by not throwing an exception
        }
    }
}