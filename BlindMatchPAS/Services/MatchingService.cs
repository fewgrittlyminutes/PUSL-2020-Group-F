using BlindMatchPAS.Data;
using BlindMatchPAS.Models;
using Microsoft.EntityFrameworkCore;

namespace BlindMatchPAS.Services
{
    public class MatchingService : IMatchingService
    {
        private readonly ApplicationDbContext _context;

        public MatchingService(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Returns proposals filtered by the supervisor's research expertise,
        /// excluding proposals already matched or withdrawn.
        /// Student identity is NOT included — anonymity enforced at the service layer.
        /// </summary>
        public async Task<IEnumerable<ProjectProposal>> GetProposalsForSupervisorAsync(string supervisorId)
        {
            var expertiseAreaIds = await _context.SupervisorExpertises
                .Where(se => se.SupervisorId == supervisorId)
                .Select(se => se.ResearchAreaId)
                .ToListAsync();

            return await _context.ProjectProposals
                .Include(p => p.ResearchArea)
                .Where(p =>
                    expertiseAreaIds.Contains(p.ResearchAreaId) &&
                    (p.Status == ProposalStatus.Pending || p.Status == ProposalStatus.UnderReview) &&
                    p.Match == null)
                .OrderByDescending(p => p.SubmittedAt)
                .ToListAsync();
        }

        /// <summary>
        /// Supervisor expresses interest; proposal moves to UnderReview.
        /// A pending (unconfirmed) match record is created.
        /// </summary>
        public async Task<MatchResult> ExpressInterestAsync(string supervisorId, int proposalId)
        {
            var proposal = await _context.ProjectProposals
                .Include(p => p.Match)
                .FirstOrDefaultAsync(p => p.Id == proposalId);

            if (proposal == null)
                return new MatchResult(false, "Proposal not found.");

            if (!await IsProposalAvailableForMatchingAsync(proposalId))
                return new MatchResult(false, "Proposal is no longer available for matching.");

            if (proposal.Match != null)
                return new MatchResult(false, "Another supervisor has already expressed interest in this proposal.");

            var match = new ProjectMatch
            {
                ProposalId = proposalId,
                SupervisorId = supervisorId,
                InterestExpressedAt = DateTime.UtcNow,
                IsRevealed = false
            };

            proposal.Status = ProposalStatus.UnderReview;
            proposal.UpdatedAt = DateTime.UtcNow;

            _context.ProjectMatches.Add(match);
            await _context.SaveChangesAsync();

            return new MatchResult(true, "Interest expressed successfully. The proposal is now under review.", match);
        }

        /// <summary>
        /// Supervisor withdraws expressed interest; proposal returns to Pending.
        /// </summary>
        public async Task<MatchResult> WithdrawInterestAsync(string supervisorId, int proposalId)
        {
            var match = await _context.ProjectMatches
                .Include(m => m.Proposal)
                .FirstOrDefaultAsync(m => m.ProposalId == proposalId && m.SupervisorId == supervisorId);

            if (match == null)
                return new MatchResult(false, "No expressed interest found for this proposal.");

            if (match.IsRevealed)
                return new MatchResult(false, "Cannot withdraw after the match has been confirmed.");

            if (match.Proposal != null)
            {
                match.Proposal.Status = ProposalStatus.Pending;
                match.Proposal.UpdatedAt = DateTime.UtcNow;
            }

            _context.ProjectMatches.Remove(match);
            await _context.SaveChangesAsync();

            return new MatchResult(true, "Interest withdrawn. The proposal has been returned to the pool.");
        }

        /// <summary>
        /// Supervisor confirms the match. Identities are revealed to both parties.
        /// </summary>
        public async Task<MatchResult> ConfirmMatchAsync(string supervisorId, int proposalId, string? note)
        {
            var match = await _context.ProjectMatches
                .Include(m => m.Proposal)
                .FirstOrDefaultAsync(m => m.ProposalId == proposalId && m.SupervisorId == supervisorId);

            if (match == null)
                return new MatchResult(false, "No pending interest found for this proposal.");

            if (match.IsRevealed)
                return new MatchResult(false, "Match is already confirmed.");

            if (match.Proposal == null)
                return new MatchResult(false, "Associated proposal not found.");

            // Confirm the match and trigger Identity Reveal
            match.ConfirmedAt = DateTime.UtcNow;
            match.IsRevealed = true;
            match.SupervisorNote = note;

            match.Proposal.Status = ProposalStatus.Matched;
            match.Proposal.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return new MatchResult(true, "Match confirmed. Identities have been revealed to both parties.", match);
        }

        public async Task<bool> IsProposalAvailableForMatchingAsync(int proposalId)
        {
            var proposal = await _context.ProjectProposals.FindAsync(proposalId);
            return proposal != null &&
                   proposal.Status != ProposalStatus.Withdrawn &&
                   proposal.Status != ProposalStatus.Matched;
        }

        public async Task<ProjectMatch?> GetMatchForProposalAsync(int proposalId)
        {
            return await _context.ProjectMatches
                .Include(m => m.Supervisor)
                .Include(m => m.Proposal)
                    .ThenInclude(p => p!.Student)
                .FirstOrDefaultAsync(m => m.ProposalId == proposalId);
        }

        /// <summary>
        /// Module Leader manually reassigns a proposal to a different supervisor.
        /// Resets the match state for the new supervisor.
        /// </summary>
        public async Task<bool> ReassignProposalAsync(int proposalId, string newSupervisorId, string adminId)
        {
            var proposal = await _context.ProjectProposals
                .Include(p => p.Match)
                .FirstOrDefaultAsync(p => p.Id == proposalId);

            if (proposal == null) return false;

            // Remove existing match if any
            if (proposal.Match != null)
                _context.ProjectMatches.Remove(proposal.Match);

            // IsRevealed remains false — the new supervisor must still confirm the match.
            // Identity reveal only happens when the supervisor explicitly clicks Confirm.
            var newMatch = new ProjectMatch
            {
                ProposalId = proposalId,
                SupervisorId = newSupervisorId,
                InterestExpressedAt = DateTime.UtcNow,
                IsRevealed = false,
                SupervisorNote = $"Manually assigned by coordinator."
            };

            proposal.Status = ProposalStatus.UnderReview;
            proposal.UpdatedAt = DateTime.UtcNow;

            _context.ProjectMatches.Add(newMatch);
            await _context.SaveChangesAsync();

            return true;
        }
    }
}
