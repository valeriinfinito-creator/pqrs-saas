namespace PQRS.Domain.Entities;

// Agente o administrador que gestiona los PQRS de un Tenant.

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// Empresa a la que pertenece este usuario
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    public string Role { get; set; } = "Agent";

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}