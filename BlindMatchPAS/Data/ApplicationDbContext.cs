using BlindMatchPAS.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BlindMatchPAS.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<ResearchArea> ResearchAreas { get; set; }
        public DbSet<ProjectProposal> ProjectProposals { get; set; }
        public DbSet<ProjectMatch> ProjectMatches { get; set; }
        public DbSet<SupervisorExpertise> SupervisorExpertises { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // ProjectProposal → Student (restrict delete to avoid cascade cycles)
            builder.Entity<ProjectProposal>()
                .HasOne(p => p.Student)
                .WithMany(u => u.ProjectProposals)
                .HasForeignKey(p => p.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            // ProjectProposal → ResearchArea
            builder.Entity<ProjectProposal>()
                .HasOne(p => p.ResearchArea)
                .WithMany(r => r.ProjectProposals)
                .HasForeignKey(p => p.ResearchAreaId)
                .OnDelete(DeleteBehavior.Restrict);

            // ProjectMatch → Proposal (one-to-one)
            builder.Entity<ProjectMatch>()
                .HasOne(m => m.Proposal)
                .WithOne(p => p.Match)
                .HasForeignKey<ProjectMatch>(m => m.ProposalId)
                .OnDelete(DeleteBehavior.Cascade);

            // ProjectMatch → Supervisor
            builder.Entity<ProjectMatch>()
                .HasOne(m => m.Supervisor)
                .WithMany(u => u.SupervisedMatches)
                .HasForeignKey(m => m.SupervisorId)
                .OnDelete(DeleteBehavior.Restrict);

            // SupervisorExpertise → Supervisor
            builder.Entity<SupervisorExpertise>()
                .HasOne(se => se.Supervisor)
                .WithMany(u => u.SupervisorExpertises)
                .HasForeignKey(se => se.SupervisorId)
                .OnDelete(DeleteBehavior.Cascade);

            // SupervisorExpertise → ResearchArea
            builder.Entity<SupervisorExpertise>()
                .HasOne(se => se.ResearchArea)
                .WithMany(r => r.SupervisorExpertises)
                .HasForeignKey(se => se.ResearchAreaId)
                .OnDelete(DeleteBehavior.Restrict);

            // Unique constraint: one expertise entry per supervisor per area
            builder.Entity<SupervisorExpertise>()
                .HasIndex(se => new { se.SupervisorId, se.ResearchAreaId })
                .IsUnique();
        }
    }
}
