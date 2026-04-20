using BlindMatchPAS.Models;

namespace BlindMatchPAS.Services
{
    public interface IMatchingService
    {
        Task<IEnumerable<ProjectProposal>> GetProposalsForSupervisorAsync(string supervisorId);
        Task<MatchResult> ExpressInterestAsync(string supervisorId, int proposalId);
        Task<MatchResult> WithdrawInterestAsync(string supervisorId, int proposalId);
        Task<MatchResult> ConfirmMatchAsync(string supervisorId, int proposalId, string? note);
        Task<bool> IsProposalAvailableForMatchingAsync(int proposalId);
        Task<ProjectMatch?> GetMatchForProposalAsync(int proposalId);
        Task<bool> ReassignProposalAsync(int proposalId, string newSupervisorId, string adminId);
    }

    public record MatchResult(bool Success, string Message, ProjectMatch? Match = null);
}
