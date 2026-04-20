using BlindMatchPAS.Data;
using BlindMatchPAS.Models;
using BlindMatchPAS.Services;
using BlindMatchPAS.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BlindMatchPAS.Controllers
{
    [Authorize(Roles = "SystemAdmin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IMatchingService _matchingService;

        public AdminController(
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
            var totalUsers = await _userManager.Users.CountAsync();
            var totalProposals = await _context.ProjectProposals.CountAsync();
            var totalMatches = await _context.ProjectMatches.CountAsync();

            var roleNames = new[] { "Student", "Supervisor", "ModuleLeader", "SystemAdmin" };
            var roleCounts = new Dictionary<string, int>();
            foreach (var role in roleNames)
            {
                var usersInRole = await _userManager.GetUsersInRoleAsync(role);
                roleCounts[role] = usersInRole.Count;
            }

            ViewBag.TotalUsers = totalUsers;
            ViewBag.TotalProposals = totalProposals;
            ViewBag.TotalMatches = totalMatches;
            ViewBag.RoleCounts = roleCounts;

            return View();
        }

        // --- User Management ---

        [HttpGet]
        public async Task<IActionResult> Users(string? role, string? search)
        {
            var users = _userManager.Users.OrderBy(u => u.FullName);
            var allUsers = await users.ToListAsync();
            var userRoles = new Dictionary<string, IList<string>>();
            foreach (var user in allUsers)
                userRoles[user.Id] = await _userManager.GetRolesAsync(user);

            IEnumerable<ApplicationUser> filtered = allUsers;
            if (!string.IsNullOrWhiteSpace(role))
                filtered = filtered.Where(u => userRoles.GetValueOrDefault(u.Id, new List<string>()).Contains(role));
            if (!string.IsNullOrWhiteSpace(search))
                filtered = filtered.Where(u =>
                    (u.FullName != null && u.FullName.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (u.Email != null && u.Email.Contains(search, StringComparison.OrdinalIgnoreCase)));

            ViewBag.UserRoles = userRoles;
            ViewBag.CurrentRole = role;
            ViewBag.CurrentSearch = search;
            return View(filtered.ToList());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            var currentId = _userManager.GetUserId(User);
            if (user.Id == currentId)
            {
                TempData["Error"] = "Cannot delete your own account.";
                return RedirectToAction(nameof(Users));
            }

            // Delete related matches first, then proposals (FK constraint)
            var proposals = await _context.ProjectProposals
                .Include(p => p.Match)
                .Where(p => p.StudentId == id)
                .ToListAsync();
            foreach (var proposal in proposals)
            {
                if (proposal.Match != null)
                    _context.ProjectMatches.Remove(proposal.Match);
                _context.ProjectProposals.Remove(proposal);
            }
            // Also remove supervised matches
            var supervisedMatches = await _context.ProjectMatches
                .Where(m => m.SupervisorId == id)
                .ToListAsync();
            _context.ProjectMatches.RemoveRange(supervisedMatches);

            await _context.SaveChangesAsync();
            await _userManager.DeleteAsync(user);
            TempData["Success"] = $"User '{user.FullName}' deleted.";
            return RedirectToAction(nameof(Users));
        }

        [HttpGet]
        public async Task<IActionResult> CreateUser()
        {
            var vm = new CreateUserViewModel
            {
                Roles = GetRoleSelectList()
            };
            ViewBag.Departments = await _context.ResearchAreas
                .Where(r => r.IsActive)
                .OrderBy(r => r.Name)
                .Select(r => r.Name)
                .ToListAsync();
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(CreateUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.Roles = GetRoleSelectList();
                ViewBag.Departments = await _context.ResearchAreas
                    .Where(r => r.IsActive)
                    .OrderBy(r => r.Name)
                    .Select(r => r.Name)
                    .ToListAsync();
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                Department = model.Department,
                EmailConfirmed = true
            };

            if (model.Role == "Student")
                user.StudentId = model.InstitutionId;
            else
                user.FacultyId = model.InstitutionId;

            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                model.Roles = GetRoleSelectList();
                return View(model);
            }

            await _userManager.AddToRoleAsync(user, model.Role);
            TempData["Success"] = $"User {model.Email} created with role {model.Role}.";
            return RedirectToAction(nameof(Users));
        }

        // --- Research Area Management ---

        [HttpGet]
        public async Task<IActionResult> ResearchAreas()
        {
            var areas = await _context.ResearchAreas.OrderBy(r => r.Name).ToListAsync();
            return View(areas);
        }

        [HttpGet]
        public IActionResult CreateResearchArea() => View(new ResearchArea());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateResearchArea(ResearchArea model)
        {
            if (!ModelState.IsValid) return View(model);

            _context.ResearchAreas.Add(model);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Research area created.";
            return RedirectToAction(nameof(ResearchAreas));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleResearchArea(int id)
        {
            var area = await _context.ResearchAreas.FindAsync(id);
            if (area == null) return NotFound();

            area.IsActive = !area.IsActive;
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(ResearchAreas));
        }

        // --- Reassignment ---

        [HttpGet]
        public async Task<IActionResult> Reassign(int proposalId)
        {
            var proposal = await _context.ProjectProposals
                .Include(p => p.Match).ThenInclude(m => m!.Supervisor)
                .FirstOrDefaultAsync(p => p.Id == proposalId);

            if (proposal == null) return NotFound();

            var supervisors = await _userManager.GetUsersInRoleAsync("Supervisor");
            var vm = new ReassignProposalViewModel
            {
                ProposalId = proposalId,
                ProposalTitle = proposal.Title,
                CurrentSupervisorName = proposal.Match?.Supervisor?.FullName ?? "Unassigned",
                Supervisors = supervisors.Select(s => new SelectListItem(s.FullName, s.Id))
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reassign(ReassignProposalViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var supervisors = await _userManager.GetUsersInRoleAsync("Supervisor");
                model.Supervisors = supervisors.Select(s => new SelectListItem(s.FullName, s.Id));
                return View(model);
            }

            var adminId = _userManager.GetUserId(User)!;
            var success = await _matchingService.ReassignProposalAsync(model.ProposalId, model.NewSupervisorId, adminId);

            TempData[success ? "Success" : "Error"] = success
                ? "Proposal reassigned successfully."
                : "Failed to reassign proposal.";

            return RedirectToAction(nameof(Dashboard));
        }

        private static IEnumerable<SelectListItem> GetRoleSelectList() =>
            new List<SelectListItem>
            {
                new("Student", "Student"),
                new("Supervisor", "Supervisor"),
                new("Module Leader", "ModuleLeader")
            };
    }
}
