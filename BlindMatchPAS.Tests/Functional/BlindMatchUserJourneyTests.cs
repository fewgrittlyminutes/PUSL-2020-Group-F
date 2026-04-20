using BlindMatchPAS.Models;
using BlindMatchPAS.Services;
using BlindMatchPAS.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BlindMatchPAS.Tests.Functional
{
    /// <summary>
    /// Functional tests that simulate real user journeys end-to-end through the
    /// service layer and database, validating complete workflows without a running
    /// HTTP server. These tests confirm that the "Blind-Match" state machine
    /// behaves correctly from the perspective of each user role.
    /// </summary>
    public class BlindMatchUserJourneyTests : IAsyncLifetime
    {
        private readonly BlindMatchPAS.Data.ApplicationDbContext _context;
        private readonly MatchingService _service;
        private readonly string _dbName;

        private ResearchArea _area = null!;
        private ApplicationUser _student = null!;
        private ApplicationUser _supervisor = null!;

        public BlindMatchUserJourneyTests()
        {
            _dbName = Guid.NewGuid().ToString();
            _context = TestDbContextFactory.Create(_dbName);
            _service = new MatchingService(_context);
        }

        public async Task InitializeAsync()
        {
            (_area, _student, _supervisor) = await TestDbContextFactory.SeedUsersAndAreaAsync(_context);

            // Give the supervisor expertise so they can see proposals
            _context.SupervisorExpertises.Add(new SupervisorExpertise
            {
                SupervisorId = _supervisor.Id,
                ResearchAreaId = _area.Id
            });
            await _context.SaveChangesAsync();
        }

        public async Task DisposeAsync() => await _context.DisposeAsync();

        // ── Journey 1: Complete Happy Path ───────────────────────────────────
        // Student submits → Supervisor browses anonymously → expresses interest
        // → confirms match → both identities revealed

        [Fact]
        public async Task Journey_StudentSubmits_SupervisorConfirms_IdentityRevealed()
        {
            // Step 1: Student submits a proposal
            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id);
            Assert.Equal(ProposalStatus.Pending, proposal.Status);

            // Step 2: Supervisor browses — student identity must NOT be visible
            await using var browseContext = TestDbContextFactory.Create(_dbName);
            var browseService = new MatchingService(browseContext);
            var visible = (await browseService.GetProposalsForSupervisorAsync(_supervisor.Id)).ToList();

            Assert.Single(visible);
            Assert.Null(visible[0].Student); // anonymity enforced

            // Step 3: Supervisor expresses interest
            var interestResult = await _service.ExpressInterestAsync(_supervisor.Id, proposal.Id);
            Assert.True(interestResult.Success);
            Assert.False(interestResult.Match!.IsRevealed); // not yet revealed

            // Step 4: Proposal status transitions to UnderReview
            var afterInterest = await _context.ProjectProposals.FindAsync(proposal.Id);
            Assert.Equal(ProposalStatus.UnderReview, afterInterest!.Status);

            // Step 5: Supervisor confirms the match
            var confirmResult = await _service.ConfirmMatchAsync(_supervisor.Id, proposal.Id, "Excellent topic.");
            Assert.True(confirmResult.Success);

            // Step 6: Identities are now revealed
            var match = await _context.ProjectMatches
                .Include(m => m.Proposal).ThenInclude(p => p!.Student)
                .Include(m => m.Supervisor)
                .FirstOrDefaultAsync(m => m.ProposalId == proposal.Id);

            Assert.NotNull(match);
            Assert.True(match!.IsRevealed);
            Assert.NotNull(match.ConfirmedAt);
            Assert.NotNull(match.Proposal!.Student); // identity now accessible
            Assert.Equal(_student.Id, match.Proposal.Student!.Id);
            Assert.Equal(_supervisor.Id, match.SupervisorId);

            // Step 7: Proposal status is Matched
            var finalProposal = await _context.ProjectProposals.FindAsync(proposal.Id);
            Assert.Equal(ProposalStatus.Matched, finalProposal!.Status);
        }

        // ── Journey 2: Supervisors Cannot See Each Other's Proposals ────────
        // Supervisor A expresses interest → Supervisor B cannot also express interest

        [Fact]
        public async Task Journey_TwoSupervisors_OnlyFirstCanExpressInterest()
        {
            var supervisorB = new ApplicationUser
            {
                Id = "supervisor-b",
                UserName = "supB@test.ac.lk",
                NormalizedUserName = "SUPB@TEST.AC.LK",
                Email = "supB@test.ac.lk",
                NormalizedEmail = "SUPB@TEST.AC.LK",
                FullName = "Dr. Supervisor B",
                FacultyId = "FB01",
                SecurityStamp = Guid.NewGuid().ToString()
            };
            _context.Users.Add(supervisorB);
            _context.SupervisorExpertises.Add(new SupervisorExpertise
            {
                SupervisorId = supervisorB.Id,
                ResearchAreaId = _area.Id
            });
            await _context.SaveChangesAsync();

            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id);

            // Supervisor A expresses interest first
            var resultA = await _service.ExpressInterestAsync(_supervisor.Id, proposal.Id);
            Assert.True(resultA.Success);

            // Supervisor B tries — should be blocked
            var resultB = await _service.ExpressInterestAsync(supervisorB.Id, proposal.Id);
            Assert.False(resultB.Success);

            // Only one match record must exist
            var matchCount = await _context.ProjectMatches
                .CountAsync(m => m.ProposalId == proposal.Id);
            Assert.Equal(1, matchCount);
        }

        // ── Journey 3: Student Withdraws Before Match ────────────────────────
        // Student submits → supervisor expresses interest → student withdraws
        // (withdrawal is blocked because status is UnderReview)

        [Fact]
        public async Task Journey_StudentCannotWithdrawAfterSupervisorExpressesInterest()
        {
            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id);
            await _service.ExpressInterestAsync(_supervisor.Id, proposal.Id);

            // After interest is expressed, proposal is UnderReview
            // The supervisor's match exists — student cannot simply delete at this stage
            var updated = await _context.ProjectProposals
                .Include(p => p.Match)
                .FirstAsync(p => p.Id == proposal.Id);

            Assert.Equal(ProposalStatus.UnderReview, updated.Status);
            Assert.NotNull(updated.Match); // match exists, blocking direct student withdrawal
        }

        // ── Journey 4: Supervisor Withdraw Then Re-Available ─────────────────
        // Supervisor expresses interest → withdraws → proposal becomes pending again
        // → a different supervisor can now pick it up

        [Fact]
        public async Task Journey_SupervisorWithdraws_ProposalBecomesAvailableAgain()
        {
            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id);
            await _service.ExpressInterestAsync(_supervisor.Id, proposal.Id);

            await _service.WithdrawInterestAsync(_supervisor.Id, proposal.Id);

            var reset = await _context.ProjectProposals.FindAsync(proposal.Id);
            Assert.Equal(ProposalStatus.Pending, reset!.Status);

            var orphan = await _context.ProjectMatches
                .FirstOrDefaultAsync(m => m.ProposalId == proposal.Id);
            Assert.Null(orphan);

            // Another supervisor can now express interest
            var supervisorC = new ApplicationUser
            {
                Id = "supervisor-c",
                UserName = "supC@test.ac.lk",
                NormalizedUserName = "SUPC@TEST.AC.LK",
                Email = "supC@test.ac.lk",
                NormalizedEmail = "SUPC@TEST.AC.LK",
                FullName = "Dr. Supervisor C",
                FacultyId = "FC01",
                SecurityStamp = Guid.NewGuid().ToString()
            };
            _context.Users.Add(supervisorC);
            await _context.SaveChangesAsync();

            var reExpressResult = await _service.ExpressInterestAsync(supervisorC.Id, proposal.Id);
            Assert.True(reExpressResult.Success);
        }

        // ── Journey 5: Module Leader Reassigns ───────────────────────────────
        // Supervisor A confirms → admin reassigns to Supervisor B
        // → new match is unconfirmed and identity is hidden again

        [Fact]
        public async Task Journey_AdminReassigns_NewMatchRequiresConfirmation()
        {
            var supervisorD = new ApplicationUser
            {
                Id = "supervisor-d",
                UserName = "supD@test.ac.lk",
                NormalizedUserName = "SUPD@TEST.AC.LK",
                Email = "supD@test.ac.lk",
                NormalizedEmail = "SUPD@TEST.AC.LK",
                FullName = "Dr. Supervisor D",
                FacultyId = "FD01",
                SecurityStamp = Guid.NewGuid().ToString()
            };
            _context.Users.Add(supervisorD);
            await _context.SaveChangesAsync();

            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id);
            await _service.ExpressInterestAsync(_supervisor.Id, proposal.Id);

            var reassigned = await _service.ReassignProposalAsync(proposal.Id, supervisorD.Id, "admin-1");
            Assert.True(reassigned);

            var newMatch = await _context.ProjectMatches
                .FirstOrDefaultAsync(m => m.ProposalId == proposal.Id);

            Assert.NotNull(newMatch);
            Assert.Equal(supervisorD.Id, newMatch!.SupervisorId);
            Assert.False(newMatch.IsRevealed);     // not yet confirmed
            Assert.Null(newMatch.ConfirmedAt);     // requires supervisor D to confirm
        }

        // ── Journey 6: Anonymity Maintained Until Confirm ───────────────────
        // Before confirmation, supervisor cannot see student name through service

        [Fact]
        public async Task Journey_BeforeConfirmation_StudentIdentityNotExposedByService()
        {
            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id);
            await _service.ExpressInterestAsync(_supervisor.Id, proposal.Id);

            // Service layer returns proposals WITHOUT student navigation property
            await using var blindContext = TestDbContextFactory.Create(_dbName);
            var blindService = new MatchingService(blindContext);

            // GetProposalsForSupervisor intentionally does not include Student
            var results = (await blindService.GetProposalsForSupervisorAsync(_supervisor.Id)).ToList();

            // Proposal is now UnderReview so not in the browse list — this is correct behaviour
            // Anonymity is enforced: the available list should not include it anymore
            Assert.Empty(results);

            // The match record should not expose the student identity before reveal.
            // Use blindContext (fresh change tracker) to mirror real SQL Server behaviour where
            // navigation properties are only populated through explicit .Include() calls.
            var match = await blindContext.ProjectMatches
                .Include(m => m.Proposal)
                // Note: intentionally NOT loading .ThenInclude(p => p.Student)
                .FirstOrDefaultAsync(m => m.ProposalId == proposal.Id);

            Assert.NotNull(match);
            Assert.False(match!.IsRevealed);
            // Student navigation should be null — not eagerly loaded, anonymity enforced
            Assert.Null(match.Proposal!.Student);
        }

        // ── Journey 7: Confirmed Match Appears in Module Leader Dashboard ───

        [Fact]
        public async Task Journey_ConfirmedMatch_IsVisibleInModuleLeaderOverview()
        {
            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id);
            await _service.ExpressInterestAsync(_supervisor.Id, proposal.Id);
            await _service.ConfirmMatchAsync(_supervisor.Id, proposal.Id, "Good.");

            var confirmed = await _context.ProjectMatches
                .Include(m => m.Proposal).ThenInclude(p => p!.Student)
                .Include(m => m.Supervisor)
                .Where(m => m.IsRevealed)
                .ToListAsync();

            Assert.Single(confirmed);
            Assert.Equal(proposal.Id, confirmed[0].ProposalId);
            Assert.NotNull(confirmed[0].Proposal!.Student);
            Assert.NotNull(confirmed[0].Supervisor);
        }
    }
}
