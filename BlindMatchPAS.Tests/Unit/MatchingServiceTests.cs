using BlindMatchPAS.Models;
using BlindMatchPAS.Services;
using BlindMatchPAS.Tests.Helpers;
using Xunit;

namespace BlindMatchPAS.Tests.Unit
{
    /// <summary>
    /// Unit tests for MatchingService business logic.
    /// Each test uses an isolated in-memory database to guarantee independence.
    /// </summary>
    public class MatchingServiceTests : IAsyncLifetime
    {
        private readonly BlindMatchPAS.Data.ApplicationDbContext _context;
        private readonly MatchingService _service;
        private readonly string _dbName;

        private ResearchArea _area = null!;
        private ApplicationUser _student = null!;
        private ApplicationUser _supervisor = null!;

        public MatchingServiceTests()
        {
            _dbName = Guid.NewGuid().ToString();
            _context = TestDbContextFactory.Create(_dbName);
            _service = new MatchingService(_context);
        }

        public async Task InitializeAsync()
        {
            (_area, _student, _supervisor) = await TestDbContextFactory.SeedUsersAndAreaAsync(_context);
        }

        public async Task DisposeAsync()
        {
            await _context.DisposeAsync();
        }

        // ── IsProposalAvailableForMatchingAsync ──────────────────────────────

        [Fact]
        public async Task IsProposalAvailable_WhenProposalDoesNotExist_ReturnsFalse()
        {
            var result = await _service.IsProposalAvailableForMatchingAsync(9999);

            Assert.False(result);
        }

        [Fact]
        public async Task IsProposalAvailable_WhenProposalIsPending_ReturnsTrue()
        {
            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id, ProposalStatus.Pending);

            var result = await _service.IsProposalAvailableForMatchingAsync(proposal.Id);

            Assert.True(result);
        }

        [Fact]
        public async Task IsProposalAvailable_WhenProposalIsUnderReview_ReturnsTrue()
        {
            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id, ProposalStatus.UnderReview);

            var result = await _service.IsProposalAvailableForMatchingAsync(proposal.Id);

            Assert.True(result);
        }

        [Theory]
        [InlineData(ProposalStatus.Matched)]
        [InlineData(ProposalStatus.Withdrawn)]
        public async Task IsProposalAvailable_WhenFinalStatus_ReturnsFalse(ProposalStatus status)
        {
            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id, status);

            var result = await _service.IsProposalAvailableForMatchingAsync(proposal.Id);

            Assert.False(result);
        }

        // ── ExpressInterestAsync ─────────────────────────────────────────────

        [Fact]
        public async Task ExpressInterest_WhenProposalNotFound_ReturnsFailed()
        {
            var result = await _service.ExpressInterestAsync(_supervisor.Id, 9999);

            Assert.False(result.Success);
            Assert.Equal("Proposal not found.", result.Message);
        }

        [Fact]
        public async Task ExpressInterest_WhenProposalIsWithdrawn_ReturnsFailed()
        {
            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id, ProposalStatus.Withdrawn);

            var result = await _service.ExpressInterestAsync(_supervisor.Id, proposal.Id);

            Assert.False(result.Success);
        }

        [Fact]
        public async Task ExpressInterest_WhenProposalIsMatched_ReturnsFailed()
        {
            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id, ProposalStatus.Matched);

            var result = await _service.ExpressInterestAsync(_supervisor.Id, proposal.Id);

            Assert.False(result.Success);
        }

        [Fact]
        public async Task ExpressInterest_WhenProposalAvailable_ReturnsSuccess()
        {
            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id);

            var result = await _service.ExpressInterestAsync(_supervisor.Id, proposal.Id);

            Assert.True(result.Success);
            Assert.NotNull(result.Match);
        }

        [Fact]
        public async Task ExpressInterest_WhenProposalAvailable_CreatesMatchRecord()
        {
            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id);

            await _service.ExpressInterestAsync(_supervisor.Id, proposal.Id);

            var match = _context.ProjectMatches.FirstOrDefault(m => m.ProposalId == proposal.Id);
            Assert.NotNull(match);
            Assert.Equal(_supervisor.Id, match!.SupervisorId);
            Assert.False(match.IsRevealed);
            Assert.Null(match.ConfirmedAt);
        }

        [Fact]
        public async Task ExpressInterest_WhenProposalAvailable_SetsStatusToUnderReview()
        {
            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id);

            await _service.ExpressInterestAsync(_supervisor.Id, proposal.Id);

            var updated = await _context.ProjectProposals.FindAsync(proposal.Id);
            Assert.Equal(ProposalStatus.UnderReview, updated!.Status);
        }

        [Fact]
        public async Task ExpressInterest_WhenAnotherSupervisorAlreadyExpressed_ReturnsFailed()
        {
            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id);
            // A second supervisor
            var supervisor2 = new ApplicationUser
            {
                Id = "supervisor-2",
                UserName = "sup2@test.ac.lk",
                NormalizedUserName = "SUP2@TEST.AC.LK",
                Email = "sup2@test.ac.lk",
                NormalizedEmail = "SUP2@TEST.AC.LK",
                FullName = "Dr. Carol Second",
                FacultyId = "F002",
                SecurityStamp = Guid.NewGuid().ToString()
            };
            _context.Users.Add(supervisor2);
            await _context.SaveChangesAsync();

            // First supervisor expresses interest
            await _service.ExpressInterestAsync(_supervisor.Id, proposal.Id);

            // Second supervisor also tries
            var result = await _service.ExpressInterestAsync(supervisor2.Id, proposal.Id);

            Assert.False(result.Success);
        }

        // ── WithdrawInterestAsync ────────────────────────────────────────────

        [Fact]
        public async Task WithdrawInterest_WhenNoMatchExists_ReturnsFailed()
        {
            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id);

            var result = await _service.WithdrawInterestAsync(_supervisor.Id, proposal.Id);

            Assert.False(result.Success);
            Assert.Equal("No expressed interest found for this proposal.", result.Message);
        }

        [Fact]
        public async Task WithdrawInterest_AfterExpressingInterest_ReturnsSuccess()
        {
            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id);
            await _service.ExpressInterestAsync(_supervisor.Id, proposal.Id);

            var result = await _service.WithdrawInterestAsync(_supervisor.Id, proposal.Id);

            Assert.True(result.Success);
        }

        [Fact]
        public async Task WithdrawInterest_AfterWithdraw_MatchRecordIsRemoved()
        {
            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id);
            await _service.ExpressInterestAsync(_supervisor.Id, proposal.Id);

            await _service.WithdrawInterestAsync(_supervisor.Id, proposal.Id);

            var match = _context.ProjectMatches.FirstOrDefault(m => m.ProposalId == proposal.Id);
            Assert.Null(match);
        }

        [Fact]
        public async Task WithdrawInterest_AfterWithdraw_ProposalStatusRevertsToPending()
        {
            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id);
            await _service.ExpressInterestAsync(_supervisor.Id, proposal.Id);

            await _service.WithdrawInterestAsync(_supervisor.Id, proposal.Id);

            var updated = await _context.ProjectProposals.FindAsync(proposal.Id);
            Assert.Equal(ProposalStatus.Pending, updated!.Status);
        }

        [Fact]
        public async Task WithdrawInterest_AfterConfirmation_ReturnsFailed()
        {
            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id);
            await _service.ExpressInterestAsync(_supervisor.Id, proposal.Id);
            await _service.ConfirmMatchAsync(_supervisor.Id, proposal.Id, null);

            var result = await _service.WithdrawInterestAsync(_supervisor.Id, proposal.Id);

            Assert.False(result.Success);
            Assert.Equal("Cannot withdraw after the match has been confirmed.", result.Message);
        }

        // ── ConfirmMatchAsync ────────────────────────────────────────────────

        [Fact]
        public async Task ConfirmMatch_WhenNoMatchExists_ReturnsFailed()
        {
            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id);

            var result = await _service.ConfirmMatchAsync(_supervisor.Id, proposal.Id, null);

            Assert.False(result.Success);
            Assert.Equal("No pending interest found for this proposal.", result.Message);
        }

        [Fact]
        public async Task ConfirmMatch_AfterExpressingInterest_ReturnsSuccess()
        {
            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id);
            await _service.ExpressInterestAsync(_supervisor.Id, proposal.Id);

            var result = await _service.ConfirmMatchAsync(_supervisor.Id, proposal.Id, null);

            Assert.True(result.Success);
        }

        [Fact]
        public async Task ConfirmMatch_SetsIsRevealedToTrue()
        {
            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id);
            await _service.ExpressInterestAsync(_supervisor.Id, proposal.Id);

            await _service.ConfirmMatchAsync(_supervisor.Id, proposal.Id, null);

            var match = _context.ProjectMatches.FirstOrDefault(m => m.ProposalId == proposal.Id);
            Assert.NotNull(match);
            Assert.True(match!.IsRevealed);
            Assert.NotNull(match.ConfirmedAt);
        }

        [Fact]
        public async Task ConfirmMatch_SetsProposalStatusToMatched()
        {
            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id);
            await _service.ExpressInterestAsync(_supervisor.Id, proposal.Id);

            await _service.ConfirmMatchAsync(_supervisor.Id, proposal.Id, null);

            var updated = await _context.ProjectProposals.FindAsync(proposal.Id);
            Assert.Equal(ProposalStatus.Matched, updated!.Status);
        }

        [Fact]
        public async Task ConfirmMatch_WithNote_PersistsNoteOnMatch()
        {
            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id);
            await _service.ExpressInterestAsync(_supervisor.Id, proposal.Id);
            const string note = "Looking forward to working with you.";

            await _service.ConfirmMatchAsync(_supervisor.Id, proposal.Id, note);

            var match = _context.ProjectMatches.FirstOrDefault(m => m.ProposalId == proposal.Id);
            Assert.Equal(note, match!.SupervisorNote);
        }

        [Fact]
        public async Task ConfirmMatch_WhenAlreadyConfirmed_ReturnsFailed()
        {
            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id);
            await _service.ExpressInterestAsync(_supervisor.Id, proposal.Id);
            await _service.ConfirmMatchAsync(_supervisor.Id, proposal.Id, null);

            // Attempt to confirm again
            var result = await _service.ConfirmMatchAsync(_supervisor.Id, proposal.Id, null);

            Assert.False(result.Success);
            Assert.Equal("Match is already confirmed.", result.Message);
        }

        // ── GetProposalsForSupervisorAsync ───────────────────────────────────

        [Fact]
        public async Task GetProposalsForSupervisor_WithNoExpertise_ReturnsEmpty()
        {
            await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id);

            var results = await _service.GetProposalsForSupervisorAsync(_supervisor.Id);

            Assert.Empty(results);
        }

        [Fact]
        public async Task GetProposalsForSupervisor_WithMatchingExpertise_ReturnsProposal()
        {
            await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id);
            _context.SupervisorExpertises.Add(new SupervisorExpertise
            {
                SupervisorId = _supervisor.Id,
                ResearchAreaId = _area.Id
            });
            await _context.SaveChangesAsync();

            var results = await _service.GetProposalsForSupervisorAsync(_supervisor.Id);

            Assert.Single(results);
        }

        [Fact]
        public async Task GetProposalsForSupervisor_ExcludesWithdrawnProposals()
        {
            await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id, ProposalStatus.Withdrawn);
            _context.SupervisorExpertises.Add(new SupervisorExpertise
            {
                SupervisorId = _supervisor.Id,
                ResearchAreaId = _area.Id
            });
            await _context.SaveChangesAsync();

            var results = await _service.GetProposalsForSupervisorAsync(_supervisor.Id);

            Assert.Empty(results);
        }

        [Fact]
        public async Task GetProposalsForSupervisor_ExcludesMatchedProposals()
        {
            await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id, ProposalStatus.Matched);
            _context.SupervisorExpertises.Add(new SupervisorExpertise
            {
                SupervisorId = _supervisor.Id,
                ResearchAreaId = _area.Id
            });
            await _context.SaveChangesAsync();

            var results = await _service.GetProposalsForSupervisorAsync(_supervisor.Id);

            Assert.Empty(results);
        }

        [Fact]
        public async Task GetProposalsForSupervisor_DoesNotIncludeStudentIdentity()
        {
            // Seed via the write context
            await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id);
            _context.SupervisorExpertises.Add(new SupervisorExpertise
            {
                SupervisorId = _supervisor.Id,
                ResearchAreaId = _area.Id
            });
            await _context.SaveChangesAsync();

            // Use a FRESH context (same InMemory DB) so no entities are in the
            // identity cache. This mirrors real SQL Server behaviour where
            // Student is null unless explicitly .Include()-d.
            await using var freshContext = TestDbContextFactory.Create(_dbName);
            var freshService = new MatchingService(freshContext);
            var results = (await freshService.GetProposalsForSupervisorAsync(_supervisor.Id)).ToList();

            // The service must NOT eagerly load the Student navigation property
            Assert.Single(results);
            Assert.Null(results[0].Student);
        }

        // ── GetMatchForProposalAsync ─────────────────────────────────────────

        [Fact]
        public async Task GetMatchForProposal_WhenNoMatch_ReturnsNull()
        {
            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id);

            var result = await _service.GetMatchForProposalAsync(proposal.Id);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetMatchForProposal_AfterExpressingInterest_ReturnsMatch()
        {
            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id);
            await _service.ExpressInterestAsync(_supervisor.Id, proposal.Id);

            var result = await _service.GetMatchForProposalAsync(proposal.Id);

            Assert.NotNull(result);
            Assert.Equal(proposal.Id, result!.ProposalId);
        }

        // ── ReassignProposalAsync ────────────────────────────────────────────

        [Fact]
        public async Task ReassignProposal_WhenProposalNotFound_ReturnsFalse()
        {
            var result = await _service.ReassignProposalAsync(9999, _supervisor.Id, "admin-1");

            Assert.False(result);
        }

        [Fact]
        public async Task ReassignProposal_WhenValidProposal_ReturnsTrue()
        {
            var supervisor2 = new ApplicationUser
            {
                Id = "supervisor-2",
                UserName = "sup2@test.ac.lk",
                NormalizedUserName = "SUP2@TEST.AC.LK",
                Email = "sup2@test.ac.lk",
                NormalizedEmail = "SUP2@TEST.AC.LK",
                FullName = "Dr. Carol Second",
                FacultyId = "F002",
                SecurityStamp = Guid.NewGuid().ToString()
            };
            _context.Users.Add(supervisor2);
            await _context.SaveChangesAsync();

            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id);
            await _service.ExpressInterestAsync(_supervisor.Id, proposal.Id);

            var result = await _service.ReassignProposalAsync(proposal.Id, supervisor2.Id, "admin-1");

            Assert.True(result);
        }

        [Fact]
        public async Task ReassignProposal_ReplacesPreviousMatchWithNewSupervisor()
        {
            var supervisor2 = new ApplicationUser
            {
                Id = "supervisor-2",
                UserName = "sup2@test.ac.lk",
                NormalizedUserName = "SUP2@TEST.AC.LK",
                Email = "sup2@test.ac.lk",
                NormalizedEmail = "SUP2@TEST.AC.LK",
                FullName = "Dr. Carol Second",
                FacultyId = "F002",
                SecurityStamp = Guid.NewGuid().ToString()
            };
            _context.Users.Add(supervisor2);
            await _context.SaveChangesAsync();

            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id);
            await _service.ExpressInterestAsync(_supervisor.Id, proposal.Id);

            await _service.ReassignProposalAsync(proposal.Id, supervisor2.Id, "admin-1");

            var match = _context.ProjectMatches.FirstOrDefault(m => m.ProposalId == proposal.Id);
            Assert.NotNull(match);
            Assert.Equal(supervisor2.Id, match!.SupervisorId);
            Assert.False(match.IsRevealed);
        }
    }
}
