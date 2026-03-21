using FormalStructuresWebApp.Models.DTOs;

namespace FormalStructuresWebApp.Services.Interfaces
{
    public interface IAiGenerationService
    {
        Task<GenerateStructureResponse> GenerateAutomatonFromDescriptionAsync(string description);
    }
}