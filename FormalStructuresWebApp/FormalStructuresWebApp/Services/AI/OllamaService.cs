using FormalStructuresWebApp.Services.Interfaces;

namespace FormalStructuresWebApp.Services.AI
{
    public class OllamaService : IOllamaService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _model;

        public OllamaService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _baseUrl = config["Ollama:BaseUrl"] ?? "http://localhost:11434";
            _model = config["Ollama:Model"] ?? "mistral";
            Console.WriteLine($"MODEL: {_model}");
        }

        public async Task<string> AskAsync(string prompt)
        {
            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/api/generate", new
            {
                model = _model,
                prompt = prompt,
                stream = false
            });

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Ollama error: {response.StatusCode} - {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<OllamaResponse>();

            return result?.response ?? "";
        }
    }

    public class OllamaResponse
    {
        public string response { get; set; }
    }
}
