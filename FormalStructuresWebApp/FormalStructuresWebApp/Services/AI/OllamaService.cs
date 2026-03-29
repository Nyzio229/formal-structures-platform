using FormalStructuresWebApp.Services.Interfaces;

namespace FormalStructuresWebApp.Services.AI
{
    public class OllamaService : IOllamaService
    {
        private readonly HttpClient _httpClient;

        public OllamaService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string> AskAsync(string prompt)
        {
            var response = await _httpClient.PostAsJsonAsync("http://localhost:11434/api/generate", new
            {
                model = "lstar",
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
