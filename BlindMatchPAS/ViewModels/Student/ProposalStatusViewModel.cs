using BlindMatchPAS.Models;

namespace BlindMatchPAS.ViewModels.Student
{
    public class ProposalStatusViewModel
    {
        public int ProposalId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Abstract { get; set; } = string.Empty;
        public string TechnicalStack { get; set; } = string.Empty;
        public string ResearchAreaName { get; set; } = string.Empty;
        public ProposalStatus Status { get; set; }
        public DateTime SubmittedAt { get; set; }

        // Revealed only after match is confirmed
        public bool IsRevealed { get; set; }
        public string? SupervisorName { get; set; }
        public string? SupervisorEmail { get; set; }
        public string? SupervisorDepartment { get; set; }
        public string? SupervisorNote { get; set; }
    }
}
