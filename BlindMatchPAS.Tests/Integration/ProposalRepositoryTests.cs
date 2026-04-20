using BlindMatchPAS.Models;
using BlindMatchPAS.Services;
using BlindMatchPAS.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BlindMatchPAS.Tests.Integration
{
    /// <summary>
    /// Integration tests focused on the proposal repository layer — verifying that
    /// proposals are correctly persisted, queried, and filtered through EF Core.
    /// Each test uses its own isolated in-memory database.
    /// </summary>
    public class ProposalRepositoryTests : IAsyncLifetime
    {
        private readonly BlindMatchPAS.Data.ApplicationDbContext _context;
        private readonly MatchingService _service;
        private readonly string _dbName;

        private ResearchArea _area = null!;
        private ApplicationUser _student = null!;
        private ApplicationUser _supervisor = null!;

        public ProposalRepositoryTests()
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

        // ── Create / Persist ─────────────────────────────────────────────────

        [Fact]
        public async Task Proposal_IsPersistedWithCorrectFields()
        {
            var proposal = new ProjectProposal
            {
                Title = "Deep Learning For Image Classification",
                Abstract = new string('A', 150),
                TechnicalStack = "Python, TensorFlow",
                ResearchAreaId = _area.Id,
                StudentId = _student.Id,
                Status = ProposalStatus.Pending
            };
            _context.ProjectProposals.Add(proposal);
            await _context.SaveChangesAsync();

            var retrieved = await _context.ProjectProposals.FindAsync(proposal.Id);

            Assert.NotNull(retrieved);
            Assert.Equal("Deep Learning For Image Classification", retrieved!.Title);
            Assert.Equal(_student.Id, retrieved.StudentId);
            Assert.Equal(_area.Id, retrieved.ResearchAreaId);
            Assert.Equal(ProposalStatus.Pending, retrieved.Status);
        }

        [Fact]
        public async Task Proposal_SubmittedAt_IsSetAutomatically()
        {
            var before = DateTime.UtcNow.AddSeconds(-1);
            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id);
            var after = DateTime.UtcNow.AddSeconds(1);

            Assert.InRange(proposal.SubmittedAt, before, after);
        }

        // ── Query / Filter ───────────────────────────────────────────────────

        [Fact]
        public async Task Proposals_FilteredByStatus_ReturnOnlyMatchingRecords()
        {
            await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id, ProposalStatus.Pending);
            await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id, ProposalStatus.UnderReview);
            await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id, ProposalStatus.Matched);
            await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id, ProposalStatus.Withdrawn);

            var active = await _context.ProjectProposals
                .Where(p => p.Status == ProposalStatus.Pending || p.Status == ProposalStatus.UnderReview)
                .ToListAsync();

            Assert.Equal(2, active.Count);
            Assert.All(active, p => Assert.NotEqual(ProposalStatus.Withdrawn, p.Status));
        }

        [Fact]
        public async Task Proposals_AvailableForSupervisor_ExcludesAlreadyMatchedOnes()
        {
            // Available
            await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id, ProposalStatus.Pending);
            // Already matched – should be excluded
            var matched = await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id, ProposalStatus.Pending);
            _context.ProjectMatches.Add(new ProjectMatch
            {
                ProposalId = matched.Id,
                SupervisorId = _supervisor.Id,
                IsRevealed = true,
                ConfirmedAt = DateTime.UtcNow
            });
            matched.Status = ProposalStatus.Matched;
            await _context.SaveChangesAsync();

            var available = await _context.ProjectProposals
                .Where(p =>
                    (p.Status == ProposalStatus.Pending || p.Status == ProposalStatus.UnderReview) &&
                    p.Match == null)
                .ToListAsync();

            Assert.Single(available);
        }

        [Fact]
        public async Task Proposals_FilteredByResearchArea_ReturnOnlyMatchingArea()
        {
            var secondArea = new ResearchArea { Name = "Cybersecurity", IsActive = true };
            _context.ResearchAreas.Add(secondArea);
            await _context.SaveChangesAsync();

            await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id);
            await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, secondArea.Id);

            var aiProposals = await _context.ProjectProposals
                .Where(p => p.ResearchAreaId == _area.Id)
                .ToListAsync();

            Assert.Single(aiProposals);
        }

        // ── Update ───────────────────────────────────────────────────────────

        [Fact]
        public async Task Proposal_CanBeUpdatedBeforeMatch()
        {
            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id);

            proposal.Title = "Updated: Smart Healthcare Monitoring System";
            proposal.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var updated = await _context.ProjectProposals.FindAsync(proposal.Id);
            Assert.Equal("Updated: Smart Healthcare Monitoring System", updated!.Title);
            Assert.NotNull(updated.UpdatedAt);
        }

        [Fact]
        public async Task Proposal_StatusTransition_PendingToUnderReview_Persists()
        {
            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id);

            await _service.ExpressInterestAsync(_supervisor.Id, proposal.Id);

            var updated = await _context.ProjectProposals.FindAsync(proposal.Id);
            Assert.Equal(ProposalStatus.UnderReview, updated!.Status);
        }

        [Fact]
        public async Task Proposal_StatusTransition_UnderReviewToMatched_Persists()
        {
            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id);
            await _service.ExpressInterestAsync(_supervisor.Id, proposal.Id);

            await _service.ConfirmMatchAsync(_supervisor.Id, proposal.Id, null);

            var updated = await _context.ProjectProposals.FindAsync(proposal.Id);
            Assert.Equal(ProposalStatus.Matched, updated!.Status);
        }

        // ── Delete / Cascade ─────────────────────────────────────────────────

        [Fact]
        public async Task Proposal_WhenRemoved_MatchRecordIsAlsoDeleted()
        {
            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id);
            var interestResult = await _service.ExpressInterestAsync(_supervisor.Id, proposal.Id);
            Assert.NotNull(interestResult.Match);

            _context.ProjectProposals.Remove(proposal);
            await _context.SaveChangesAsync();

            var orphanMatch = await _context.ProjectMatches.FindAsync(interestResult.Match!.Id);
            Assert.Null(orphanMatch);
        }

        // ── Anonymity Guard ──────────────────────────────────────────────────

        [Fact]
        public async Task Proposal_LoadedWithoutStudentInclude_StudentPropertyIsNull()
        {
            await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id);

            // Intentionally do NOT include Student navigation
            await using var freshContext = TestDbContextFactory.Create(_dbName);
            var proposal = await freshContext.ProjectProposals
                .Include(p => p.ResearchArea)
                // No .Include(p => p.Student) — simulates blind browse
                .FirstOrDefaultAsync();

            Assert.NotNull(proposal);
            Assert.Null(proposal!.Student); // student identity is hidden
        }

        [Fact]
        public async Task GetProposalsForSupervisor_ReturnsOnlyProposalsMatchingExpertise()
        {
            var otherArea = new ResearchArea { Name = "Mobile Development", IsActive = true };
            _context.ResearchAreas.Add(otherArea);
            await _context.SaveChangesAsync();

            // Supervisor has expertise only in _area
            _context.SupervisorExpertises.Add(new SupervisorExpertise
            {
                SupervisorId = _supervisor.Id,
                ResearchAreaId = _area.Id
            });
            await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, _area.Id);       // matches expertise
            await TestDbContextFactory.SeedProposalAsync(_context, _student.Id, otherArea.Id);   // does NOT match
            await _context.SaveChangesAsync();

            var proposals = (await _service.GetProposalsForSupervisorAsync(_supervisor.Id)).ToList();

            Assert.Single(proposals);
            Assert.Equal(_area.Id, proposals[0].ResearchAreaId);
        }
    }
}
