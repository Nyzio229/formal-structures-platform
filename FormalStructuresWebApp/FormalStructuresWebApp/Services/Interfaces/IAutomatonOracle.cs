namespace FormalStructuresWebApp.Services.Interfaces
{
    public interface IAutomatonOracle
    {
        Task<bool> MembershipQuery(string word);
    }
}
