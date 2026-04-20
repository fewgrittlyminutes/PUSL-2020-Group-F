# BlindMatchPAS

Blind matching system for student project proposals and supervisors. Built with ASP.NET Core 8 MVC + SQLite + Identity.

## Prerequisites

| Tool | Version |
|------|---------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 8.0+ |
| Git | any |

No database server needed — uses SQLite (file-based).

## Quick Start

```bash
# 1. Clone
git clone https://github.com/Methul25/Software-tools-project.git
cd Software-tools-project/BlindMatchPAS

# 2. Restore & run
dotnet run
```

App starts at **http://localhost:5000**

The database (`BlindMatchPAS.db`) is created and seeded automatically on first run.

## Default Accounts

| Role | Email | Password | Notes |
|------|-------|----------|-------|
| SystemAdmin | admin@blindmatch.ac.lk | Admin@12345 | Seeded on first run |
| ModuleLeader | moduleleader@blindmatch.ac.lk | Leader@12345 | Seeded on first run |
| Supervisor | *(set on creation)* | *(set on creation)* | Created via ModuleLeader dashboard |
| Student | *(set on creation)* | *(set on creation)* | Created via ModuleLeader dashboard |

> Supervisor and Student accounts are created by the ModuleLeader (or SystemAdmin) via `/ModuleLeader/CreateUser`.

## Project Structure

```
BlindMatchPAS/
├── Controllers/       # Admin, Student, Supervisor, Home
├── Models/            # EF Core entities
├── Views/             # Razor views per role
├── ViewModels/        # Typed view models
├── Services/          # Matching logic
├── Data/              # DbContext + seeder
└── Areas/Identity/    # Auth (login only)
```

## Useful Commands

```bash
# Apply migrations manually (not normally needed)
dotnet ef database update

# Add a new migration
dotnet ef migrations add <Name>

# Build only
dotnet build
```
