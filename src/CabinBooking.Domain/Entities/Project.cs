namespace CabinBooking.Domain.Entities;

public class Project
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();
}

public class ProjectMember
{
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
}
