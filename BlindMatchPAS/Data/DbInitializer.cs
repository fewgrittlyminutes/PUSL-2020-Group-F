using BlindMatchPAS.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BlindMatchPAS.Data
{
    public static class DbInitializer
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            await context.Database.MigrateAsync();

            // Seed roles
            string[] roles = { "Student", "Supervisor", "ModuleLeader", "SystemAdmin" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }

            // Seed research areas
            if (!await context.ResearchAreas.AnyAsync())
            {
                var areas = new List<ResearchArea>
                {
                    new() { Name = "Artificial Intelligence", Description = "Machine learning, deep learning, neural networks." },
                    new() { Name = "Web Development", Description = "Frontend, backend, full-stack web technologies." },
                    new() { Name = "Cybersecurity", Description = "Network security, ethical hacking, cryptography." },
                    new() { Name = "Data Science", Description = "Big data, analytics, data visualisation." },
                    new() { Name = "Cloud Computing", Description = "AWS, Azure, GCP, distributed systems." },
                    new() { Name = "Mobile Development", Description = "iOS, Android, cross-platform applications." },
                    new() { Name = "Internet of Things", Description = "Embedded systems, sensor networks, smart devices." },
                    new() { Name = "Software Engineering", Description = "Design patterns, agile, DevOps, testing." }
                };
                context.ResearchAreas.AddRange(areas);
                await context.SaveChangesAsync();
            }

            // Seed system admin account
            if (await userManager.FindByEmailAsync("admin@blindmatch.ac.lk") == null)
            {
                var admin = new ApplicationUser
                {
                    UserName = "admin@blindmatch.ac.lk",
                    Email = "admin@blindmatch.ac.lk",
                    FullName = "System Administrator",
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(admin, "Admin@12345");
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(admin, "SystemAdmin");
            }

            // Seed module leader
            if (await userManager.FindByEmailAsync("moduleleader@blindmatch.ac.lk") == null)
            {
                var leader = new ApplicationUser
                {
                    UserName = "moduleleader@blindmatch.ac.lk",
                    Email = "moduleleader@blindmatch.ac.lk",
                    FullName = "Module Leader",
                    FacultyId = "ML001",
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(leader, "Leader@12345");
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(leader, "ModuleLeader");
            }
        }
    }
}
