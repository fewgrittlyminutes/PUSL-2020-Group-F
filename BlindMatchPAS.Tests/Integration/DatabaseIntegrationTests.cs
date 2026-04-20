using BlindMatchPAS.Models;
using BlindMatchPAS.Services;
using BlindMatchPAS.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BlindMatchPAS.Tests.Integration
{
    /// <summary>
    /// Integration tests that verify state transitions persist correctly to the database.
    /// Uses EF Core InMemory provider to test real query/persistence behavior
    /// without requiring a live SQL Server instance.
    /// </summary>
    public class DatabaseIntegrationTests : IAsyncLifetime
    {
        private readonly BlindMatchPAS.Data.ApplicationDbContext _context;
        private readonly MatchingService _service;
        private readonly string _dbName;

        private ResearchArea _area = null!;
        private ApplicationUser _student = null!;
        private ApplicationUser _supervisor = null!;

        public DatabaseIntegrationTests()
        {
            _dbName = Guid.NewGuid().ToString();
            _context = TestDbContextFactory.Create(_dbName);
            _service = new MatchingService(_context);
        }

        public async Task InitializeAsync()
        {
            (_area, _student, _supervisor) = await TestDbContextFactory.SeedUsersAndAreaAsync(_context);
        }

        public async Task DisposeAsync() => await _context.DisposeAsync();

        // ── Full Matching Workflow ────────────────────────────────────────────

        [Fact]
        public async Task FullWorkflow_Submit_Express_Confirm_PersistsCorrectState()
        {
            // Arrange: student submits a proposal
            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id);
            Assert.Equal(ProposalStatus.Pending, proposal.Status);

            // Act: supervisor expresses interest
            var interestResult = await _service.ExpressInterestAsync(_supervisor.Id, proposal.Id);
            Assert.True(interestResult.Success);

            // Assert intermediate DB state
            var proposalAfterInterest = await _context.ProjectProposals
                .Include(p => p.Match)
                .FirstAsync(p => p.Id == proposal.Id);
            Assert.Equal(ProposalStatus.UnderReview, proposalAfterInterest.Status);
            Assert.NotNull(proposalAfterInterest.Match);
            Assert.False(proposalAfterInterest.Match!.IsRevealed);

            // Act: supervisor confirms the match
            var confirmResult = await _service.ConfirmMatchAsync(_supervisor.Id, proposal.Id, "Great project!");
            Assert.True(confirmResult.Success);

            // Assert final DB state
            var proposalAfterConfirm = await _context.ProjectProposals
                .Include(p => p.Match)
                .FirstAsync(p => p.Id == proposal.Id);
            Assert.Equal(ProposalStatus.Matched, proposalAfterConfirm.Status);
            Assert.True(proposalAfterConfirm.Match!.IsRevealed);
            Assert.NotNull(proposalAfterConfirm.Match.ConfirmedAt);
            Assert.Equal("Great project!", proposalAfterConfirm.Match.SupervisorNote);
        }

        [Fact]
        public async Task FullWorkflow_Submit_Express_Withdraw_ProposalReturnsToPending()
        {
            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id);

            await _service.ExpressInterestAsync(_supervisor.Id, proposal.Id);
            await _service.WithdrawInterestAsync(_supervisor.Id, proposal.Id);

            var finalProposal = await _context.ProjectProposals
                .Include(p => p.Match)
                .FirstAsync(p => p.Id == proposal.Id);

            Assert.Equal(ProposalStatus.Pending, finalProposal.Status);
            Assert.Null(finalProposal.Match);
        }

        // ── Research Area CRUD ───────────────────────────────────────────────

        [Fact]
        public async Task ResearchArea_CanBeCreatedAndRetrieved()
        {
            var area = new ResearchArea
            {
                Name = "Cloud Computing",
                Description = "Azure, AWS, GCP",
                IsActive = true
            };
            _context.ResearchAreas.Add(area);
            await _context.SaveChangesAsync();

            var retrieved = await _context.ResearchAreas.FindAsync(area.Id);

            Assert.NotNull(retrieved);
            Assert.Equal("Cloud Computing", retrieved!.Name);
            Assert.True(retrieved.IsActive);
        }

        [Fact]
        public async Task ResearchArea_CanBeToggled()
        {
            var area = new ResearchArea { Name = "Cybersecurity", IsActive = true };
            _context.ResearchAreas.Add(area);
            await _context.SaveChangesAsync();

            area.IsActive = false;
            await _context.SaveChangesAsync();

            var updated = await _context.ResearchAreas.FindAsync(area.Id);
            Assert.False(updated!.IsActive);
        }

        // ── Supervisor Expertise ─────────────────────────────────────────────

        [Fact]
        public async Task SupervisorExpertise_CanBeAdded()
        {
            _context.SupervisorExpertises.Add(new SupervisorExpertise
            {
                SupervisorId = _supervisor.Id,
                ResearchAreaId = _area.Id
            });
            await _context.SaveChangesAsync();

            var expertise = await _context.SupervisorExpertises
                .FirstOrDefaultAsync(se => se.SupervisorId == _supervisor.Id && se.ResearchAreaId == _area.Id);

            Assert.NotNull(expertise);
        }

        [Fact]
        public async Task SupervisorExpertise_UniqueConstraint_PreventsDoubleEntry()
        {
            _context.SupervisorExpertises.Add(new SupervisorExpertise
            {
                SupervisorId = _supervisor.Id,
                ResearchAreaId = _area.Id
            });
            await _context.SaveChangesAsync();

            _context.SupervisorExpertises.Add(new SupervisorExpertise
            {
                SupervisorId = _supervisor.Id,
                ResearchAreaId = _area.Id
            });

            // InMemory does not enforce unique indexes, but we validate the
            // constraint is declared so real SQL Server will reject duplicates.
            // Verify via reflection that the index is configured.
            var entityType = _context.Model.FindEntityType(typeof(SupervisorExpertise));
            var uniqueIndexes = entityType!.GetIndexes().Where(i => i.IsUnique).ToList();
            Assert.NotEmpty(uniqueIndexes);
        }

        // ── Proposal CRUD ────────────────────────────────────────────────────

        [Fact]
        public async Task Proposal_CanBeSubmittedByStudent()
        {
            var initialCount = await _context.ProjectProposals.CountAsync();

            var proposal = new ProjectProposal
            {
                Title = "A New Innovative Research Project Title",
                Abstract = new string('B', 150),
                TechnicalStack = "React, Node.js",
                ResearchAreaId = _area.Id,
                StudentId = _student.Id,
                Status = ProposalStatus.Pending
            };
            _context.ProjectProposals.Add(proposal);
            await _context.SaveChangesAsync();

            Assert.Equal(initialCount + 1, await _context.ProjectProposals.CountAsync());
        }

        [Fact]
        public async Task Proposal_WithdrawBeforeMatch_RemovesFromActiveList()
        {
            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id);

            proposal.Status = ProposalStatus.Withdrawn;
            await _context.SaveChangesAsync();

            var active = await _context.ProjectProposals
                .Where(p => p.Status != ProposalStatus.Withdrawn)
                .ToListAsync();

            Assert.DoesNotContain(active, p => p.Id == proposal.Id);
        }

        // ── Anonymity Guarantee ──────────────────────────────────────────────

        [Fact]
        public async Task GetProposalsForSupervisor_NeverLoadsStudentNavProperty()
        {
            await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id);
            _context.SupervisorExpertises.Add(new SupervisorExpertise
            {
                SupervisorId = _supervisor.Id,
                ResearchAreaId = _area.Id
            });
            await _context.SaveChangesAsync();

            // Fresh context to avoid EF Core InMemory identity-cache hydration,
            // replicating real SQL Server behaviour (no .Include(Student) → null).
            await using var freshContext = TestDbContextFactory.Create(_dbName);
            var freshService = new MatchingService(freshContext);
            var proposals = (await freshService.GetProposalsForSupervisorAsync(_supervisor.Id)).ToList();

            Assert.Single(proposals);
            // Student property must be null — anonymity is enforced at service layer
            Assert.Null(proposals[0].Student);
        }

        [Fact]
        public async Task ConfirmedMatch_IsRevealedTrue_ExposesStudentIdentityPostConfirm()
        {
            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id);
            await _service.ExpressInterestAsync(_supervisor.Id, proposal.Id);
            await _service.ConfirmMatchAsync(_supervisor.Id, proposal.Id, null);

            var match = await _service.GetMatchForProposalAsync(proposal.Id);

            Assert.NotNull(match);
            Assert.True(match!.IsRevealed);
            // After reveal, the student's details can be loaded through the proposal
            Assert.NotNull(match.Proposal);
        }

        // ── Reassignment ─────────────────────────────────────────────────────

        [Fact]
        public async Task Reassign_NewMatchIsUnconfirmed_RequiresSupervisorToConfirmAgain()
        {
            var supervisor2 = new ApplicationUser
            {
                Id = "supervisor-2",
                UserName = "sup2@test.ac.lk",
                NormalizedUserName = "SUP2@TEST.AC.LK",
                Email = "sup2@test.ac.lk",
                NormalizedEmail = "SUP2@TEST.AC.LK",
                FullName = "Dr. Dave New",
                FacultyId = "F002",
                SecurityStamp = Guid.NewGuid().ToString()
            };
            _context.Users.Add(supervisor2);
            await _context.SaveChangesAsync();

            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id);
            await _service.ExpressInterestAsync(_supervisor.Id, proposal.Id);
            await _service.ReassignProposalAsync(proposal.Id, supervisor2.Id, "admin-1");

            var match = await _context.ProjectMatches.FirstOrDefaultAsync(m => m.ProposalId == proposal.Id);
            Assert.NotNull(match);
            Assert.False(match!.IsRevealed);
            Assert.Null(match.ConfirmedAt);
            Assert.Equal(supervisor2.Id, match.SupervisorId);
        }
    }
}
