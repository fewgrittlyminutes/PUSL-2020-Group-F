using BlindMatchPAS.Data;
using BlindMatchPAS.Models;
using BlindMatchPAS.ViewModels.Student;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BlindMatchPAS.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public StudentController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Dashboard()
        {
            var userId = _userManager.GetUserId(User)!;
            var proposals = await _context.ProjectProposals
                .Include(p => p.ResearchArea)
                .Include(p => p.Match)
                    .ThenInclude(m => m!.Supervisor)
                .Where(p => p.StudentId == userId && p.Status != ProposalStatus.Withdrawn)
                .OrderByDescending(p => p.SubmittedAt)
                .ToListAsync();

            var viewModels = proposals.Select(p => BuildStatusViewModel(p)).ToList();
            return View(viewModels);
        }

        [HttpGet]
        public async Task<IActionResult> Submit()
        {
            var vm = new SubmitProposalViewModel
            {
                ResearchAreas = await GetResearchAreaSelectListAsync()
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(SubmitProposalViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.ResearchAreas = await GetResearchAreaSelectListAsync();
                return View(model);
            }

            var userId = _userManager.GetUserId(User)!;
            var proposal = new ProjectProposal
            {
                Title = model.Title,
                Abstract = model.Abstract,
                TechnicalStack = model.TechnicalStack,
                ResearchAreaId = model.ResearchAreaId,
                StudentId = userId,
                Status = ProposalStatus.Pending
            };

            _context.ProjectProposals.Add(proposal);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Proposal submitted successfully!";
            return RedirectToAction(nameof(Dashboard));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var proposal = await _context.ProjectProposals
                .FirstOrDefaultAsync(p => p.Id == id && p.StudentId == userId);

            if (proposal == null) return NotFound();
            if (proposal.Status == ProposalStatus.Matched || proposal.Status == ProposalStatus.Withdrawn)
            {
                TempData["Error"] = "Cannot edit a matched or withdrawn proposal.";
                return RedirectToAction(nameof(Dashboard));
            }

            var vm = new SubmitProposalViewModel
            {
                Title = proposal.Title,
                Abstract = proposal.Abstract,
                TechnicalStack = proposal.TechnicalStack,
                ResearchAreaId = proposal.ResearchAreaId,
                ResearchAreas = await GetResearchAreaSelectListAsync()
            };
            ViewBag.ProposalId = id;
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SubmitProposalViewModel model)
        {
            var userId = _userManager.GetUserId(User)!;
            var proposal = await _context.ProjectProposals
                .FirstOrDefaultAsync(p => p.Id == id && p.StudentId == userId);

            if (proposal == null) return NotFound();
            if (proposal.Status == ProposalStatus.Matched || proposal.Status == ProposalStatus.Withdrawn)
            {
                TempData["Error"] = "Cannot edit a matched or withdrawn proposal.";
                return RedirectToAction(nameof(Dashboard));
            }

            if (!ModelState.IsValid)
            {
                model.ResearchAreas = await GetResearchAreaSelectListAsync();
                ViewBag.ProposalId = id;
                return View(model);
            }

            proposal.Title = model.Title;
            proposal.Abstract = model.Abstract;
            proposal.TechnicalStack = model.TechnicalStack;
            proposal.ResearchAreaId = model.ResearchAreaId;
            proposal.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Proposal updated successfully.";
            return RedirectToAction(nameof(Dashboard));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Withdraw(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var proposal = await _context.ProjectProposals
                .Include(p => p.Match)
                .FirstOrDefaultAsync(p => p.Id == id && p.StudentId == userId);

            if (proposal == null) return NotFound();
            if (proposal.Status == ProposalStatus.Matched)
            {
                TempData["Error"] = "Cannot withdraw an already matched proposal.";
                return RedirectToAction(nameof(Dashboard));
            }

            if (proposal.Match != null)
                _context.ProjectMatches.Remove(proposal.Match);

            _context.ProjectProposals.Remove(proposal);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Proposal withdrawn and removed.";
            return RedirectToAction(nameof(Dashboard));
        }

        // --- Helpers ---

        private static ProposalStatusViewModel BuildStatusViewModel(ProjectProposal p)
        {
            var vm = new ProposalStatusViewModel
            {
                ProposalId = p.Id,
                Title = p.Title,
                Abstract = p.Abstract,
                TechnicalStack = p.TechnicalStack,
                ResearchAreaName = p.ResearchArea?.Name ?? string.Empty,
                Status = p.Status,
                SubmittedAt = p.SubmittedAt,
                IsRevealed = p.Match?.IsRevealed ?? false
            };

            if (vm.IsRevealed && p.Match?.Supervisor != null)
            {
                vm.SupervisorName = p.Match.Supervisor.FullName;
                vm.SupervisorEmail = p.Match.Supervisor.Email;
                vm.SupervisorDepartment = p.Match.Supervisor.Department;
                vm.SupervisorNote = p.Match.SupervisorNote;
            }

            return vm;
        }

        private async Task<IEnumerable<SelectListItem>> GetResearchAreaSelectListAsync()
        {
            return await _context.ResearchAreas
                .Where(r => r.IsActive)
                .OrderBy(r => r.Name)
                .Select(r => new SelectListItem(r.Name, r.Id.ToString()))
                .ToListAsync();
        }
    }
}
