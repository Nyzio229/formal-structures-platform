using FormalStructuresWebApp.Models.Domain;

namespace FormalStructuresWebApp.Services.Interfaces
{
    public interface IPdaSessionService
    {
        PushdownAutomaton GetPda();
        void SetPda(PushdownAutomaton pda);
    }
}