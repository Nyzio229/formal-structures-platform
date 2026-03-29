using FormalStructuresWebApp.Models.Domain;
using FormalStructuresWebApp.Services.Interfaces;

namespace FormalStructuresWebApp.Services.LStar
{
    public class LStarService
    {


        public async Task<FiniteAutomaton> LearnAsync(IAutomatonOracle oracle)
        {
            var automaton = new FiniteAutomaton();

            // NA RAZIE PROSTA WERSJA TESTOWA
            var test = await oracle.MembershipQuery("a");

            Console.WriteLine($"Oracle answer for 'a': {test}");

            return automaton;
        }

        //private async Task FillTable(ObservationTable table)
        //{
        //    foreach (var s in table.S)
        //    {
        //        foreach (var e in table.E)
        //        {
        //            var word = s + e;
        //            var result = await _oracle.MembershipQuery(word);
        //            table.Set(s, e, result);
        //        }
        //    }
        //}
    }
}
