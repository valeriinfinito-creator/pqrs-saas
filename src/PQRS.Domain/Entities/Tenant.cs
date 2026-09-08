namespace PQRS.Domain.Entities;

//Representa una empresa suscriptora de la plataforma SaaS.

public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    // Dominio autorizado para peticiones CORS del widget (ej: https://miempresa.com)
    public string AllowedDomain { get; set; } = string.Empty;

    // API Key pública usada por el widget para identificar al tenant
    public string WidgetApiKey { get; set; } = Guid.NewGuid().ToString("N");

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}