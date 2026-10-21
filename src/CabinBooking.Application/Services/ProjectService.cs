using CabinBooking.Application.Abstractions;
using CabinBooking.Application.Common;
using CabinBooking.Application.Dtos;
using CabinBooking.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CabinBooking.Application.Services;

public class ProjectService(IAppDbContext db, IAuditLogger audit)
{
    public async Task<IReadOnlyList<ProjectDto>> GetAllAsync(CancellationToken ct)
    {
        var projects = await db.Projects.AsNoTracking().Include(p => p.Members).OrderBy(p => p.Name).ToListAsync(ct);
        return projects.Select(ToDto).ToList();
    }

    public async Task<ProjectDto> CreateAsync(UpsertProjectRequest request, CancellationToken ct)
    {
        var name = ValidateName(request.Name);
        if (await db.Projects.AnyAsync(p => p.Name == name, ct))
            throw new ConflictException("A project with this name already exists.");

        var project = new Project { Name = name };
        await SetMembersAsync(project, request.MemberUserIds, ct);
        db.Projects.Add(project);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("ProjectCreated", "Project", project.Id.ToString(), $"Created project '{name}'", ct);
        return ToDto(project);
    }

    public async Task<ProjectDto> UpdateAsync(int id, UpsertProjectRequest request, CancellationToken ct)
    {
        var project = await db.Projects.Include(p => p.Members).FirstOrDefaultAsync(p => p.Id == id, ct)
                      ?? throw new NotFoundException("Project not found.");
        var name = ValidateName(request.Name);
        if (await db.Projects.AnyAsync(p => p.Name == name && p.Id != id, ct))
            throw new ConflictException("A project with this name already exists.");

        project.Name = name;
        db.ProjectMembers.RemoveRange(project.Members);
        project.Members = new List<ProjectMember>();
        await SetMembersAsync(project, request.MemberUserIds, ct);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("ProjectUpdated", "Project", id.ToString(), $"Updated project '{name}'", ct);
        return ToDto(project);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct)
                      ?? throw new NotFoundException("Project not found.");
        db.Projects.Remove(project);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("ProjectDeleted", "Project", id.ToString(), $"Deleted project '{project.Name}'", ct);
    }

    /// <summary>First run: copy the configured project names into the table so existing setups keep their list.</summary>
    public async Task SeedAsync(IEnumerable<string> names, CancellationToken ct)
    {
        if (await db.Projects.AnyAsync(ct)) return;
        foreach (var n in names.Select(n => n.Trim()).Where(n => n.Length > 0).Distinct())
            db.Projects.Add(new Project { Name = n });
        await db.SaveChangesAsync(ct);
    }

    private async Task SetMembersAsync(Project project, Guid[]? userIds, CancellationToken ct)
    {
        var ids = (userIds ?? []).Distinct().ToList();
        if (ids.Count == 0) return;
        var valid = await db.Users.Where(u => ids.Contains(u.Id)).Select(u => u.Id).ToListAsync(ct);
        foreach (var uid in valid)
            project.Members.Add(new ProjectMember { Project = project, UserId = uid });
    }

    private static string ValidateName(string? name)
    {
        var n = name?.Trim() ?? "";
        if (n.Length is < 2 or > 120) throw new AppValidationException("Project name must be 2–120 characters.");
        return n;
    }

    private static ProjectDto ToDto(Project p) => new(p.Id, p.Name, p.Members.Select(m => m.UserId).ToArray());
}
