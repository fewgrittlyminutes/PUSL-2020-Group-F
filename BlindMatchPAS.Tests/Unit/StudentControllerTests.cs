using BlindMatchPAS.Controllers;
using BlindMatchPAS.Models;
using BlindMatchPAS.Tests.Helpers;
using BlindMatchPAS.ViewModels.Student;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using System.Security.Claims;
using Xunit;

namespace BlindMatchPAS.Tests.Unit
{
    /// <summary>
    /// Unit tests for StudentController.
    /// Uses Moq to fake UserManager so no real ASP.NET Identity infrastructure is needed.
    /// The in-memory database is used for EF Core queries.
    /// </summary>
    public class StudentControllerTests : IAsyncLifetime
    {
        private readonly BlindMatchPAS.Data.ApplicationDbContext _context;
        private readonly Mock<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>> _mockUserManager;
        private StudentController _controller = null!;

        private const string StudentId = "student-unit-1";

        private BlindMatchPAS.Models.ResearchArea _area = null!;
        private ApplicationUser _student = null!;

        public StudentControllerTests()
        {
            _context = TestDbContextFactory.Create();
            _mockUserManager = MockUserManagerHelper.Create();
        }

        public async Task InitializeAsync()
        {
            (_area, _student, _) = await TestDbContextFactory.SeedUsersAndAreaAsync(_context);

            // Override student Id to match StudentId constant used in tests
            _student = new ApplicationUser
            {
                Id = StudentId,
                UserName = "student-unit@test.ac.lk",
                NormalizedUserName = "STUDENT-UNIT@TEST.AC.LK",
                Email = "student-unit@test.ac.lk",
                NormalizedEmail = "STUDENT-UNIT@TEST.AC.LK",
                FullName = "Test Student Unit",
                StudentId = "SU001",
                SecurityStamp = Guid.NewGuid().ToString()
            };
            _context.Users.Add(_student);
            await _context.SaveChangesAsync();

            _controller = BuildController();
        }

        public async Task DisposeAsync() => await _context.DisposeAsync();

        private StudentController BuildController()
        {
            _mockUserManager
                .Setup(um => um.GetUserId(It.IsAny<ClaimsPrincipal>()))
                .Returns(StudentId);

            var controller = new StudentController(_context, _mockUserManager.Object);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, StudentId),
                new Claim(ClaimTypes.Name, "student-unit@test.ac.lk"),
                new Claim(ClaimTypes.Role, "Student")
            };
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
                }
            };
            controller.TempData = new TempDataDictionary(
                controller.ControllerContext.HttpContext,
                Mock.Of<ITempDataProvider>());

            return controller;
        }

        // ── Dashboard ──────────────────────────────────────────────────────────

        [Fact]
        public async Task Dashboard_ReturnsViewResult()
        {
            var result = await _controller.Dashboard();

            Assert.IsType<ViewResult>(result);
        }

        [Fact]
        public async Task Dashboard_ReturnsOnlyCurrentStudentsProposals()
        {
            // Seed two proposals for the test student
            await TestDbContextFactory.SeedProposalAsync(_context, StudentId, _area.Id);
            await TestDbContextFactory.SeedProposalAsync(_context, StudentId, _area.Id);

            // Seed one proposal for a different student — should NOT appear
            var other = new ApplicationUser
            {
                Id = "other-student",
                UserName = "other@test.ac.lk",
                NormalizedUserName = "OTHER@TEST.AC.LK",
                Email = "other@test.ac.lk",
                NormalizedEmail = "OTHER@TEST.AC.LK",
                FullName = "Other Student",
                SecurityStamp = Guid.NewGuid().ToString()
            };
            _context.Users.Add(other);
            await _context.SaveChangesAsync();
            await TestDbContextFactory.SeedProposalAsync(_context, other.Id, _area.Id);

            var result = await _controller.Dashboard() as ViewResult;
            var model = result!.Model as List<ProposalStatusViewModel>;

            Assert.NotNull(model);
            Assert.Equal(2, model!.Count);
            Assert.All(model, vm => Assert.DoesNotContain("other", vm.Title, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task Dashboard_ExcludesWithdrawnProposals()
        {
            await TestDbContextFactory.SeedProposalAsync(_context, StudentId, _area.Id, ProposalStatus.Pending);
            await TestDbContextFactory.SeedProposalAsync(_context, StudentId, _area.Id, ProposalStatus.Withdrawn);

            var result = await _controller.Dashboard() as ViewResult;
            var model = result!.Model as List<ProposalStatusViewModel>;

            Assert.Single(model!);
        }

        // ── Submit (GET) ───────────────────────────────────────────────────────

        [Fact]
        public async Task Submit_Get_ReturnsViewWithResearchAreas()
        {
            var result = await _controller.Submit() as ViewResult;
            var model = result!.Model as SubmitProposalViewModel;

            Assert.NotNull(model);
            Assert.NotEmpty(model!.ResearchAreas);
        }

        // ── Submit (POST) ──────────────────────────────────────────────────────

        [Fact]
        public async Task Submit_Post_WithValidModel_CreatesProposalAndRedirects()
        {
            var vm = new SubmitProposalViewModel
            {
                Title = "A Valid Research Proposal Title Here",
                Abstract = new string('X', 150),
                TechnicalStack = "ASP.NET Core, EF Core",
                ResearchAreaId = _area.Id
            };

            var result = await _controller.Submit(vm);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Dashboard", redirect.ActionName);

            var saved = _context.ProjectProposals.FirstOrDefault(p => p.StudentId == StudentId);
            Assert.NotNull(saved);
            Assert.Equal(ProposalStatus.Pending, saved!.Status);
        }

        [Fact]
        public async Task Submit_Post_WithInvalidModel_ReturnsViewWithErrors()
        {
            var vm = new SubmitProposalViewModel
            {
                Title = "", // invalid: empty
                Abstract = new string('X', 150),
                TechnicalStack = "ASP.NET Core",
                ResearchAreaId = _area.Id
            };
            _controller.ModelState.AddModelError("Title", "Title is required.");

            var result = await _controller.Submit(vm);

            Assert.IsType<ViewResult>(result);
        }

        // ── Edit ───────────────────────────────────────────────────────────────

        [Fact]
        public async Task Edit_Get_WithValidPendingProposal_ReturnsView()
        {
            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, StudentId, _area.Id);

            var result = await _controller.Edit(proposal.Id) as ViewResult;

            Assert.NotNull(result);
        }

        [Fact]
        public async Task Edit_Get_ProposalNotOwnedByStudent_ReturnsNotFound()
        {
            var other = new ApplicationUser
            {
                Id = "other-2",
                UserName = "other2@test.ac.lk",
                NormalizedUserName = "OTHER2@TEST.AC.LK",
                Email = "other2@test.ac.lk",
                NormalizedEmail = "OTHER2@TEST.AC.LK",
                FullName = "Other 2",
                SecurityStamp = Guid.NewGuid().ToString()
            };
            _context.Users.Add(other);
            await _context.SaveChangesAsync();
            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, other.Id, _area.Id);

            var result = await _controller.Edit(proposal.Id);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Edit_Get_MatchedProposal_RedirectsToDashboard()
        {
            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, StudentId, _area.Id, ProposalStatus.Matched);

            var result = await _controller.Edit(proposal.Id) as RedirectToActionResult;

            Assert.NotNull(result);
            Assert.Equal("Dashboard", result!.ActionName);
        }

        [Fact]
        public async Task Edit_Post_WithValidModel_UpdatesAndRedirects()
        {
            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, StudentId, _area.Id);
            var vm = new SubmitProposalViewModel
            {
                Title = "Updated Research Proposal Title Here",
                Abstract = new string('U', 150),
                TechnicalStack = "React, Node.js",
                ResearchAreaId = _area.Id
            };

            var result = await _controller.Edit(proposal.Id, vm) as RedirectToActionResult;

            Assert.NotNull(result);
            Assert.Equal("Dashboard", result!.ActionName);

            var updated = await _context.ProjectProposals.FindAsync(proposal.Id);
            Assert.Equal("Updated Research Proposal Title Here", updated!.Title);
        }

        // ── Withdraw ───────────────────────────────────────────────────────────

        [Fact]
        public async Task Withdraw_PendingProposal_RemovesAndRedirects()
        {
            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, StudentId, _area.Id);

            var result = await _controller.Withdraw(proposal.Id) as RedirectToActionResult;

            Assert.NotNull(result);
            Assert.Equal("Dashboard", result!.ActionName);

            var deleted = await _context.ProjectProposals.FindAsync(proposal.Id);
            Assert.Null(deleted);
        }

        [Fact]
        public async Task Withdraw_MatchedProposal_RedirectsWithError()
        {
            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, StudentId, _area.Id, ProposalStatus.Matched);

            var result = await _controller.Withdraw(proposal.Id) as RedirectToActionResult;

            Assert.NotNull(result);
            Assert.Equal("Dashboard", result!.ActionName);
            Assert.Equal("Cannot withdraw an already matched proposal.", _controller.TempData["Error"]);
        }

        [Fact]
        public async Task Withdraw_ProposalNotOwned_ReturnsNotFound()
        {
            var other = new ApplicationUser
            {
                Id = "other-3",
                UserName = "other3@test.ac.lk",
                NormalizedUserName = "OTHER3@TEST.AC.LK",
                Email = "other3@test.ac.lk",
                NormalizedEmail = "OTHER3@TEST.AC.LK",
                FullName = "Other 3",
                SecurityStamp = Guid.NewGuid().ToString()
            };
            _context.Users.Add(other);
            await _context.SaveChangesAsync();
            var proposal = await TestDbContextFactory.SeedProposalAsync(_context, other.Id, _area.Id);

            var result = await _controller.Withdraw(proposal.Id);

            Assert.IsType<NotFoundResult>(result);
        }

        // ── GetUserId is called, not direct User.Id ────────────────────────────

        [Fact]
        public async Task Dashboard_InvokesGetUserIdOnUserManager()
        {
            await _controller.Dashboard();

            _mockUserManager.Verify(
                um => um.GetUserId(It.IsAny<ClaimsPrincipal>()),
                Times.AtLeastOnce);
        }
    }
}
