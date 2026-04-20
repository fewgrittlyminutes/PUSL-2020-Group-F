using BlindMatchPAS.ViewModels.Admin;

namespace BlindMatchPAS.ViewModels.ModuleLeader
{
    public class ModuleLeaderDashboardViewModel
    {
        public int TotalUsers { get; set; }
        public int TotalProposals { get; set; }
        public int ActiveResearchAreas { get; set; }

        public IEnumerable<MatchSummaryViewModel> ConfirmedMatches { get; set; } = new List<MatchSummaryViewModel>();
        public IEnumerable<MatchSummaryViewModel> PendingMatches { get; set; } = new List<MatchSummaryViewModel>();
        public IEnumerable<ProposalSummaryViewModel> UnmatchedProposals { get; set; } = new List<ProposalSummaryViewModel>();
    }
}
