using System.ComponentModel.DataAnnotations;
using BlindMatchPAS.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BlindMatchPAS.ViewModels.Admin
{
    public class AllocationDashboardViewModel
    {
        public IEnumerable<MatchSummaryViewModel> ConfirmedMatches { get; set; } = new List<MatchSummaryViewModel>();
        public IEnumerable<MatchSummaryViewModel> PendingMatches { get; set; } = new List<MatchSummaryViewModel>();
        public IEnumerable<ProposalSummaryViewModel> UnmatchedProposals { get; set; } = new List<ProposalSummaryViewModel>();
    }

    public class MatchSummaryViewModel
    {
        public int MatchId { get; set; }
        public int ProposalId { get; set; }
        public string ProposalTitle { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string StudentEmail { get; set; } = string.Empty;
        public string SupervisorName { get; set; } = string.Empty;
        public string SupervisorEmail { get; set; } = string.Empty;
        public string ResearchArea { get; set; } = string.Empty;
        public bool IsConfirmed { get; set; }
        public DateTime? ConfirmedAt { get; set; }
    }

    public class ProposalSummaryViewModel
    {
        public int ProposalId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string ResearchArea { get; set; } = string.Empty;
        public ProposalStatus Status { get; set; }
    }

    public class ReassignProposalViewModel
    {
        [Required]
        public int ProposalId { get; set; }
        public string ProposalTitle { get; set; } = string.Empty;
        public string CurrentSupervisorName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a supervisor.")]
        [Display(Name = "New Supervisor")]
        public string NewSupervisorId { get; set; } = string.Empty;

        public IEnumerable<SelectListItem> Supervisors { get; set; } = new List<SelectListItem>();
    }

    public class CreateUserViewModel
    {
        [Required]
        [StringLength(100)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [StringLength(20)]
        [RegularExpression(@"^[A-Za-z]{1,3}\d{4,10}$",
            ErrorMessage = "Institution ID must start with 1–3 letters followed by 4–10 digits (e.g. ST20240001).")]
        [Display(Name = "Student / Faculty ID")]
        public string? InstitutionId { get; set; }

        [StringLength(200)]
        [RegularExpression(@"^[A-Za-z][A-Za-z0-9 &,()/-]{1,199}$",
            ErrorMessage = "Department name must start with a letter and contain only letters, digits, spaces and common punctuation.")]
        [Display(Name = "Department")]
        public string? Department { get; set; }

        [Required]
        [Display(Name = "Role")]
        public string Role { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 8)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        public IEnumerable<SelectListItem> Roles { get; set; } = new List<SelectListItem>();
    }
}
