using BlindMatchPAS.Data;
using BlindMatchPAS.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BlindMatchPAS.Tests.Helpers
{
    /// <summary>
    /// Creates a fresh in-memory ApplicationDbContext for each test to ensure isolation.
    /// </summary>
    public static class TestDbContextFactory
    {
        public static ApplicationDbContext Create(string? dbName = null)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        /// <summary>
        /// Seeds a research area, a student, and a supervisor into the context.
        /// </summary>
        public static async Task<(ResearchArea area, ApplicationUser student, ApplicationUser supervisor)>
            SeedUsersAndAreaAsync(ApplicationDbContext context)
        {
            var area = new ResearchArea
            {
                Name = "Artificial Intelligence",
                Description = "AI and Machine Learning research",
                IsActive = true
            };
            context.ResearchAreas.Add(area);

            var student = new ApplicationUser
            {
                Id = "student-1",
                UserName = "student@test.ac.lk",
                NormalizedUserName = "STUDENT@TEST.AC.LK",
                Email = "student@test.ac.lk",
                NormalizedEmail = "STUDENT@TEST.AC.LK",
                FullName = "Alice Student",
                StudentId = "S001",
                SecurityStamp = Guid.NewGuid().ToString()
            };

            var supervisor = new ApplicationUser
            {
                Id = "supervisor-1",
                UserName = "supervisor@test.ac.lk",
                NormalizedUserName = "SUPERVISOR@TEST.AC.LK",
                Email = "supervisor@test.ac.lk",
                NormalizedEmail = "SUPERVISOR@TEST.AC.LK",
                FullName = "Dr. Bob Supervisor",
                FacultyId = "F001",
                Department = "Computer Science",
                SecurityStamp = Guid.NewGuid().ToString()
            };

            context.Users.AddRange(student, supervisor);
            await context.SaveChangesAsync();

            return (area, student, supervisor);
        }

        /// <summary>
        /// Seeds a complete proposal with a student and research area.
        /// </summary>
        public static async Task<ProjectProposal> SeedProposalAsync(
            ApplicationDbContext context,
            string studentId,
            int researchAreaId,
            ProposalStatus status = ProposalStatus.Pending)
        {
            var proposal = new ProjectProposal
            {
                Title = "Machine Learning for Fraud Detection System",
                Abstract = new string('A', 150),
                TechnicalStack = "Python, TensorFlow, ASP.NET Core",
                ResearchAreaId = researchAreaId,
                StudentId = studentId,
                Status = status,
                SubmittedAt = DateTime.UtcNow
            };

            context.ProjectProposals.Add(proposal);
            await context.SaveChangesAsync();
            return proposal;
        }
    }
}
