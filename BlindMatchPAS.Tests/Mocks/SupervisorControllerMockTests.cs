using BlindMatchPAS.Controllers;
using BlindMatchPAS.Data;
using BlindMatchPAS.Models;
using BlindMatchPAS.Services;
using BlindMatchPAS.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using System.Security.Claims;
using Xunit;

namespace BlindMatchPAS.Tests.Mocks
{
    /// <summary>
    /// Controller-level tests that use Moq to mock IMatchingService.
    /// These tests verify that the SupervisorController correctly delegates to the
    /// matching service and handles the results (success/failure TempData and redirects).
    /// No real database or HTTP server is required.
    /// </summary>
    public class SupervisorControllerMockTests : IAsyncLifetime
    {
        private readonly BlindMatchPAS.Data.ApplicationDbContext _context;
        private readonly Mock<IMatchingService> _mockMatchingService;
        private readonly Mock<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>> _mockUserManager;
        private SupervisorController _controller = null!;

        private const string SupervisorId = "supervisor-mock-1";

        public SupervisorControllerMockTests()
        {
            _context = TestDbContextFactory.Create();
            _mockMatchingService = new Mock<IMatchingService>();
            _mockUserManager = MockUserManagerHelper.Create();
        }

        public async Task InitializeAsync()
        {
            await TestDbContextFactory.SeedUsersAndAreaAsync(_context);
            _controller = BuildController();
        }

        public async Task DisposeAsync() => await _context.DisposeAsync();

        /// <summary>
        /// Creates the controller with a fake authenticated supervisor identity
        /// and a stub ITempDataDictionary so TempData assignments do not throw.
        /// </summary>
        private SupervisorController BuildController()
        {
            _mockUserManager
                .Setup(um => um.GetUserId(It.IsAny<ClaimsPrincipal>()))
                .Returns(SupervisorId);

            var controller = new SupervisorController(_context, _mockUserManager.Object, _mockMatchingService.Object);

            // Wire up a real ClaimsPrincipal so the controller's User property is set
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, SupervisorId),
                new Claim(ClaimTypes.Name, "supervisor@test.ac.lk"),
                new Claim(ClaimTypes.Role, "Supervisor")
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };

            // TempData requires an ITempDataDictionary
            controller.TempData = new TempDataDictionary(
                controller.ControllerContext.HttpContext,
                Mock.Of<ITempDataProvider>());

            return controller;
        }

        // ── ExpressInterest ──────────────────────────────────────────────────

        [Fact]
        public async Task ExpressInterest_CallsServiceWithCorrectSupervisorIdAndProposalId()
        {
            const int proposalId = 42;
            _mockMatchingService
                .Setup(s => s.ExpressInterestAsync(SupervisorId, proposalId))
                .ReturnsAsync(new MatchResult(true, "Interest expressed successfully."));

            await _controller.ExpressInterest(proposalId);

            _mockMatchingService.Verify(
                s => s.ExpressInterestAsync(SupervisorId, proposalId),
                Times.Once);
        }

        [Fact]
        public async Task ExpressInterest_OnSuccess_RedirectsToBrowse()
        {
            _mockMatchingService
                .Setup(s => s.ExpressInterestAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(new MatchResult(true, "OK"));

            var result = await _controller.ExpressInterest(1);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Browse", redirect.ActionName);
        }

        [Fact]
        public async Task ExpressInterest_OnSuccess_SetsTempDataSuccess()
        {
            const string successMessage = "Interest expressed successfully.";
            _mockMatchingService
                .Setup(s => s.ExpressInterestAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(new MatchResult(true, successMessage));

            await _controller.ExpressInterest(1);

            Assert.Equal(successMessage, _controller.TempData["Success"]);
        }

        [Fact]
        public async Task ExpressInterest_OnFailure_SetsTempDataError()
        {
            const string errorMessage = "Proposal is no longer available.";
            _mockMatchingService
                .Setup(s => s.ExpressInterestAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(new MatchResult(false, errorMessage));

            await _controller.ExpressInterest(1);

            Assert.Equal(errorMessage, _controller.TempData["Error"]);
        }

        [Fact]
        public async Task ExpressInterest_OnFailure_StillRedirectsToBrowse()
        {
            _mockMatchingService
                .Setup(s => s.ExpressInterestAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(new MatchResult(false, "Can't."));

            var result = await _controller.ExpressInterest(1);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Browse", redirect.ActionName);
        }

        // ── WithdrawInterest ─────────────────────────────────────────────────

        [Fact]
        public async Task WithdrawInterest_CallsServiceWithCorrectIds()
        {
            const int proposalId = 7;
            _mockMatchingService
                .Setup(s => s.WithdrawInterestAsync(SupervisorId, proposalId))
                .ReturnsAsync(new MatchResult(true, "Withdrawn."));

            await _controller.WithdrawInterest(proposalId);

            _mockMatchingService.Verify(
                s => s.WithdrawInterestAsync(SupervisorId, proposalId),
                Times.Once);
        }

        [Fact]
        public async Task WithdrawInterest_OnSuccess_RedirectsToDashboard()
        {
            _mockMatchingService
                .Setup(s => s.WithdrawInterestAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(new MatchResult(true, "OK"));

            var result = await _controller.WithdrawInterest(1);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Dashboard", redirect.ActionName);
        }

        [Fact]
        public async Task WithdrawInterest_WhenAlreadyConfirmed_SetsTempDataError()
        {
            const string errorMessage = "Cannot withdraw after the match has been confirmed.";
            _mockMatchingService
                .Setup(s => s.WithdrawInterestAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(new MatchResult(false, errorMessage));

            await _controller.WithdrawInterest(1);

            Assert.Equal(errorMessage, _controller.TempData["Error"]);
        }

        // ── ConfirmMatch ─────────────────────────────────────────────────────

        [Fact]
        public async Task ConfirmMatch_Post_CallsServiceWithCorrectIds()
        {
            var viewModel = new BlindMatchPAS.ViewModels.Supervisor.ConfirmMatchViewModel
            {
                ProposalId = 15,
                Note = "Excellent work"
            };

            _mockMatchingService
                .Setup(s => s.ConfirmMatchAsync(SupervisorId, 15, "Excellent work"))
                .ReturnsAsync(new MatchResult(true, "Match confirmed."));

            await _controller.ConfirmMatch(viewModel);

            _mockMatchingService.Verify(
                s => s.ConfirmMatchAsync(SupervisorId, 15, "Excellent work"),
                Times.Once);
        }

        [Fact]
        public async Task ConfirmMatch_OnSuccess_RedirectsToDashboard()
        {
            var viewModel = new BlindMatchPAS.ViewModels.Supervisor.ConfirmMatchViewModel
            {
                ProposalId = 1
            };

            _mockMatchingService
                .Setup(s => s.ConfirmMatchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>()))
                .ReturnsAsync(new MatchResult(true, "Confirmed."));

            var result = await _controller.ConfirmMatch(viewModel);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Dashboard", redirect.ActionName);
        }

        [Fact]
        public async Task ConfirmMatch_OnFailure_SetsTempDataError()
        {
            var viewModel = new BlindMatchPAS.ViewModels.Supervisor.ConfirmMatchViewModel
            {
                ProposalId = 1
            };
            const string errorMessage = "Match is already confirmed.";

            _mockMatchingService
                .Setup(s => s.ConfirmMatchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>()))
                .ReturnsAsync(new MatchResult(false, errorMessage));

            await _controller.ConfirmMatch(viewModel);

            Assert.Equal(errorMessage, _controller.TempData["Error"]);
        }

        // ── Service Interaction Counts ───────────────────────────────────────

        [Fact]
        public async Task ExpressInterest_CalledOnlyOncePerRequest()
        {
            _mockMatchingService
                .Setup(s => s.ExpressInterestAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(new MatchResult(true, "OK"));

            await _controller.ExpressInterest(5);

            // Ensures there's no duplicate service call
            _mockMatchingService.Verify(
                s => s.ExpressInterestAsync(It.IsAny<string>(), It.IsAny<int>()),
                Times.Exactly(1));
        }

        [Fact]
        public async Task WithdrawInterest_CalledOnlyOncePerRequest()
        {
            _mockMatchingService
                .Setup(s => s.WithdrawInterestAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(new MatchResult(true, "OK"));

            await _controller.WithdrawInterest(5);

            _mockMatchingService.Verify(
                s => s.WithdrawInterestAsync(It.IsAny<string>(), It.IsAny<int>()),
                Times.Exactly(1));
        }

        [Fact]
        public async Task ConfirmMatch_NeverCallsExpressInterest()
        {
            var viewModel = new BlindMatchPAS.ViewModels.Supervisor.ConfirmMatchViewModel { ProposalId = 1 };

            _mockMatchingService
                .Setup(s => s.ConfirmMatchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>()))
                .ReturnsAsync(new MatchResult(true, "OK"));

            await _controller.ConfirmMatch(viewModel);

            // Confirm action must never trigger an ExpressInterest side-effect
            _mockMatchingService.Verify(
                s => s.ExpressInterestAsync(It.IsAny<string>(), It.IsAny<int>()),
                Times.Never);
        }
    }
}
