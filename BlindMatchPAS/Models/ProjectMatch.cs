using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BlindMatchPAS.Models
{
    public class ProjectMatch
    {
        public int Id { get; set; }

        [Required]
        public int ProposalId { get; set; }

        [Required]
        public string SupervisorId { get; set; } = string.Empty;

        public DateTime InterestExpressedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ConfirmedAt { get; set; }

        /// <summary>
        /// Controls whether student/supervisor identities are visible to each other.
        /// Becomes true only after supervisor confirms the match.
        /// </summary>
        public bool IsRevealed { get; set; } = false;

        [StringLength(1000)]
        public string? SupervisorNote { get; set; }

        // Navigation
        [ForeignKey(nameof(ProposalId))]
        public ProjectProposal? Proposal { get; set; }

        [ForeignKey(nameof(SupervisorId))]
        public ApplicationUser? Supervisor { get; set; }
    }
}
