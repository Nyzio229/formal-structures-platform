using FormalStructuresWebApp.Models.Domain;

namespace FormalStructuresWebApp.Services.LStar
{
    public class ObservationTable
    {
        public List<string> S = new();
        public List<string> E = new();

        private Dictionary<(string, string), bool> table = new();

        public ObservationTable(string[] alphabet)
        {
            S.Add("");
            E.Add("");
        }

        public void Set(string s, string e, bool value)
        {
            table[(s, e)] = value;
        }

        public bool Get(string s, string e)
        {
            return table.TryGetValue((s, e), out var v) && v;
        }

        public bool IsClosed() => true; // TODO
        public bool IsConsistent() => true; // TODO

        public void MakeClosed() { }
        public void MakeConsistent() { }

        public FiniteAutomaton BuildAutomaton()
        {
            // budowanie DFA z tabeli
            return new FiniteAutomaton();
        }
    }
}
