namespace IdentityService.Api.Entities;

public class Role
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // Navigation property for many-to-many relationship
    public ICollection<User> Users { get; set; } = new List<User>();
}
