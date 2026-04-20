using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace BlindMatchPAS.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [StringLength(20)]
        public string? StudentId { get; set; }

        [StringLength(20)]
        public string? FacultyId { get; set; }

        [StringLength(200)]
        public string? Department { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<ProjectProposal> ProjectProposals { get; set; } = new List<ProjectProposal>();
        public ICollection<SupervisorExpertise> SupervisorExpertises { get; set; } = new List<SupervisorExpertise>();
        public ICollection<ProjectMatch> SupervisedMatches { get; set; } = new List<ProjectMatch>();
    }
}
