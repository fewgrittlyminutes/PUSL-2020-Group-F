using BlindMatchPAS.Models;

namespace BlindMatchPAS.ViewModels.Supervisor
{
    /// <summary>
    /// Anonymous view of a proposal — student identity deliberately omitted.
    /// </summary>
    public class AnonymousProposalViewModel
    {
        public int ProposalId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Abstract { get; set; } = string.Empty;
        public string TechnicalStack { get; set; } = string.Empty;
        public string ResearchAreaName { get; set; } = string.Empty;
        public DateTime SubmittedAt { get; set; }
        public ProposalStatus Status { get; set; }
    }

    public class ConfirmMatchViewModel
    {
        public int ProposalId { get; set; }
        public string ProposalTitle { get; set; } = string.Empty;
        public string? Note { get; set; }

        // Proposal detail fields for inline modal
        public string Abstract { get; set; } = string.Empty;
        public string TechnicalStack { get; set; } = string.Empty;
        public string ResearchAreaName { get; set; } = string.Empty;
        public DateTime SubmittedAt { get; set; }
    }

    public class RevealedMatchViewModel
    {
        public int ProposalId { get; set; }
        public string ProposalTitle { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string StudentEmail { get; set; } = string.Empty;
        public string? StudentId { get; set; }
        public DateTime ConfirmedAt { get; set; }
        public string? Note { get; set; }
    }
}
