namespace FormalStructuresWebApp.Services.Interfaces
{
    public interface IOllamaService
    {
        Task<string> AskAsync(string prompt);
    }
}
