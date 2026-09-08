namespace PQRS.Application.Tenants;

// Datos que el administrador envía para registrar una nueva empresa
public record CreateTenantRequest(string Name, string AllowedDomain);

// Datos que la API devuelve tras crear el Tenant, incluyendo su API Key del widget
public record TenantResponse(Guid Id, string Name, string AllowedDomain, string WidgetApiKey, bool IsActive);