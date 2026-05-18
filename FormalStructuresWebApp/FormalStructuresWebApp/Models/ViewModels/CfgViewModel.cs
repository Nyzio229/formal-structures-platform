using FormalStructuresWebApp.Services.CFG;

namespace FormalStructuresWebApp.Models.ViewModels
{
    public class CfgViewModel
    {
        public string Description { get; set; } = "";
        public string Alphabet { get; set; } = "";
        public CfgLearningResult? Result { get; set; }
    }
}