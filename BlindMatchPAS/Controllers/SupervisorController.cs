using BlindMatchPAS.Data;
using BlindMatchPAS.Models;
using BlindMatchPAS.Services;
using BlindMatchPAS.ViewModels.Supervisor;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlindMatchPAS.Controllers
{
    [Authorize(Roles = "Supervisor")]
    public class SupervisorController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IMatchingService _matchingService;

        public SupervisorController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IMatchingService matchingService)
        {
            _context = context;
            _userManager = userManager;
            _matchingService = matchingService;
        }

        public async Task<IActionResult> Dashboard()
        {
            var supervisorId = _userManager.GetUserId(User)!;

            var availableProposals = await _matchingService.GetProposalsForSupervisorAsync(supervisorId);
            var interestedProposals = await GetInterestedProposalsAsync(supervisorId);
            var confirmedMatches = await GetConfirmedMatchesAsync(supervisorId);

            ViewBag.AvailableProposals = availableProposals.Select(ToAnonymousViewModel).ToList();
            ViewBag.InterestedProposals = interestedProposals;
            ViewBag.ConfirmedMatches = confirmedMatches;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Browse(int? researchAreaId)
        {
            var supervisorId = _userManager.GetUserId(User)!;

            var proposals = await _context.ProjectProposals
                .Include(p => p.ResearchArea)
                .Where(p =>
                    (p.Status == ProposalStatus.Pending || p.Status == ProposalStatus.UnderReview) &&
                    p.Match == null)
                .OrderByDescending(p => p.SubmittedAt)
                .ToListAsync();

            if (researchAreaId.HasValue)
                proposals = proposals.Where(p => p.ResearchAreaId == researchAreaId.Value).ToList();

            var viewModels = proposals.Select(ToAnonymousViewModel).ToList();

            // All active research areas for the filter dropdown
            ViewBag.ResearchAreas = await _context.ResearchAreas
                .Where(r => r.IsActive)
                .OrderBy(r => r.Name)
                .Select(r => r.Name)
                .ToListAsync();

            ViewBag.SelectedAreaId = researchAreaId;
            return View(viewModels);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExpressInterest(int proposalId)
        {
            var supervisorId = _userManager.GetUserId(User)!;
            var result = await _matchingService.ExpressInterestAsync(supervisorId, proposalId);

            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Browse));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> WithdrawInterest(int proposalId)
        {
            var supervisorId = _userManager.GetUserId(User)!;
            var result = await _matchingService.WithdrawInterestAsync(supervisorId, proposalId);

            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Dashboard));
        }

        [HttpGet]
        public async Task<IActionResult> ProposalDetail(int id)
        {
            // Fetch directly — no expertise filter, student identity excluded via ViewModel
            var proposal = await _context.ProjectProposals
                .Include(p => p.ResearchArea)
                .Where(p => p.Id == id &&
                            (p.Status == ProposalStatus.Pending || p.Status == ProposalStatus.UnderReview) &&
                            p.Match == null)
                .FirstOrDefaultAsync();

            if (proposal == null) return NotFound();
            return View(ToAnonymousViewModel(proposal));
        }

        [HttpGet]
        public async Task<IActionResult> ConfirmedProposalDetail(int proposalId)
        {
            var supervisorId = _userManager.GetUserId(User)!;

            // Only reachable by the supervisor who owns the confirmed match
            var match = await _context.ProjectMatches
                .Include(m => m.Proposal).ThenInclude(p => p!.ResearchArea)
                .Include(m => m.Proposal).ThenInclude(p => p!.Student)
                .FirstOrDefaultAsync(m => m.ProposalId == proposalId &&
                                          m.SupervisorId == supervisorId &&
                                          m.IsRevealed);

            if (match == null || match.Proposal == null) return NotFound();

            ViewBag.StudentName  = match.Proposal.Student?.FullName ?? "Unknown";
            ViewBag.StudentEmail = match.Proposal.Student?.Email ?? string.Empty;
            ViewBag.StudentIdNo  = match.Proposal.Student?.StudentId;
            ViewBag.Note         = match.SupervisorNote;
            ViewBag.ConfirmedAt  = match.ConfirmedAt;

            return View(match.Proposal);
        }

        [HttpGet]
        public async Task<IActionResult> ConfirmMatch(int proposalId)
        {
            var supervisorId = _userManager.GetUserId(User)!;
            var match = await _context.ProjectMatches
                .Include(m => m.Proposal).ThenInclude(p => p!.ResearchArea)
                .FirstOrDefaultAsync(m => m.ProposalId == proposalId && m.SupervisorId == supervisorId);

            if (match == null || match.IsRevealed) return NotFound();

            var vm = new ConfirmMatchViewModel
            {
                ProposalId    = proposalId,
                ProposalTitle = match.Proposal?.Title ?? string.Empty,
                Abstract      = match.Proposal?.Abstract ?? string.Empty,
                TechnicalStack = match.Proposal?.TechnicalStack ?? string.Empty,
                ResearchAreaName = match.Proposal?.ResearchArea?.Name ?? string.Empty,
                SubmittedAt   = match.Proposal?.SubmittedAt ?? DateTime.UtcNow
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmMatch(ConfirmMatchViewModel model)
        {
            var supervisorId = _userManager.GetUserId(User)!;
            var result = await _matchingService.ConfirmMatchAsync(supervisorId, model.ProposalId, model.Note);

            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Dashboard));
        }

        [HttpGet]
        public async Task<IActionResult> ManageExpertise()
        {
            var supervisorId = _userManager.GetUserId(User)!;
            var allAreas = await _context.ResearchAreas.Where(r => r.IsActive).OrderBy(r => r.Name).ToListAsync();
            var selectedIds = await _context.SupervisorExpertises
                .Where(se => se.SupervisorId == supervisorId)
                .Select(se => se.ResearchAreaId)
                .ToListAsync();

            ViewBag.AllAreas = allAreas;
            ViewBag.SelectedIds = selectedIds;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManageExpertise(List<int> selectedAreaIds)
        {
            var supervisorId = _userManager.GetUserId(User)!;

            var existing = await _context.SupervisorExpertises
                .Where(se => se.SupervisorId == supervisorId)
                .ToListAsync();

            _context.SupervisorExpertises.RemoveRange(existing);

            var newEntries = selectedAreaIds.Distinct().Select(id => new SupervisorExpertise
            {
                SupervisorId = supervisorId,
                ResearchAreaId = id
            });
            _context.SupervisorExpertises.AddRange(newEntries);

            await _context.SaveChangesAsync();
            TempData["Success"] = "Expertise preferences updated.";
            return RedirectToAction(nameof(Dashboard));
        }

        // --- Helpers ---

        private static AnonymousProposalViewModel ToAnonymousViewModel(ProjectProposal p) => new()
        {
            ProposalId = p.Id,
            Title = p.Title,
            Abstract = p.Abstract,
            TechnicalStack = p.TechnicalStack,
            ResearchAreaName = p.ResearchArea?.Name ?? string.Empty,
            SubmittedAt = p.SubmittedAt,
            Status = p.Status
            // Student identity intentionally excluded
        };

        private async Task<List<AnonymousProposalViewModel>> GetInterestedProposalsAsync(string supervisorId)
        {
            return await _context.ProjectMatches
                .Include(m => m.Proposal).ThenInclude(p => p!.ResearchArea)
                .Where(m => m.SupervisorId == supervisorId && !m.IsRevealed)
                .Select(m => new AnonymousProposalViewModel
                {
                    ProposalId = m.ProposalId,
                    Title = m.Proposal!.Title,
                    Abstract = m.Proposal.Abstract,
                    TechnicalStack = m.Proposal.TechnicalStack,
                    ResearchAreaName = m.Proposal.ResearchArea!.Name,
                    SubmittedAt = m.Proposal.SubmittedAt,
                    Status = m.Proposal.Status
                })
                .ToListAsync();
        }

        private async Task<List<RevealedMatchViewModel>> GetConfirmedMatchesAsync(string supervisorId)
        {
            return await _context.ProjectMatches
                .Include(m => m.Proposal).ThenInclude(p => p!.Student)
                .Where(m => m.SupervisorId == supervisorId && m.IsRevealed)
                .Select(m => new RevealedMatchViewModel
                {
                    ProposalId = m.ProposalId,
                    ProposalTitle = m.Proposal!.Title,
                    StudentName = m.Proposal.Student!.FullName,
                    StudentEmail = m.Proposal.Student.Email!,
                    StudentId = m.Proposal.Student.StudentId,
                    ConfirmedAt = m.ConfirmedAt!.Value,
                    Note = m.SupervisorNote
                })
                .ToListAsync();
        }

        private async Task<List<string>> GetSupervisorExpertiseAreasAsync(string supervisorId)
        {
            return await _context.SupervisorExpertises
                .Include(se => se.ResearchArea)
                .Where(se => se.SupervisorId == supervisorId)
                .Select(se => se.ResearchArea!.Name)
                .ToListAsync();
        }
    }
}
