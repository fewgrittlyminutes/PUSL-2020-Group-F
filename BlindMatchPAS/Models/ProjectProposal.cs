using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BlindMatchPAS.Models
{
    public class ProjectProposal
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Title is required.")]
        [StringLength(200, MinimumLength = 10, ErrorMessage = "Title must be between 10 and 200 characters.")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Abstract is required.")]
        [StringLength(2000, MinimumLength = 100, ErrorMessage = "Abstract must be between 100 and 2000 characters.")]
        public string Abstract { get; set; } = string.Empty;

        [Required(ErrorMessage = "Technical stack is required.")]
        [StringLength(500, ErrorMessage = "Technical stack must not exceed 500 characters.")]
        public string TechnicalStack { get; set; } = string.Empty;

        [Required(ErrorMessage = "Research area is required.")]
        public int ResearchAreaId { get; set; }

        [Required]
        public string StudentId { get; set; } = string.Empty;

        public ProposalStatus Status { get; set; } = ProposalStatus.Pending;

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Navigation
        [ForeignKey(nameof(ResearchAreaId))]
        public ResearchArea? ResearchArea { get; set; }

        [ForeignKey(nameof(StudentId))]
        public ApplicationUser? Student { get; set; }

        public ProjectMatch? Match { get; set; }
    }
}
