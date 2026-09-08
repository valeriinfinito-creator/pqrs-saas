using PQRS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace PQRS.Api.Middleware;

/// Resuelve el Tenant activo a partir del header "X-Widget-Api-Key" en
/// peticiones públicas del widget (rutas bajo /api/v1/widget/*).
/// Si la key es válida, guarda el TenantId en HttpContext Items para que
/// los endpoints lo usen sin volver a consultar la base de datos.

public class WidgetTenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private const string ApiKeyHeaderName = "X-Widget-Api-Key";

    public WidgetTenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext db)
    {
        // Solo aplica a rutas públicas del widget; el resto de rutas
        // (auth, endpoints de agentes) siguen su propio camino.
        if (!context.Request.Path.StartsWithSegments("/api/v1/widget"))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKey) ||
            string.IsNullOrWhiteSpace(apiKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = $"Falta el header '{ApiKeyHeaderName}'." });
            return;
        }

        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.WidgetApiKey == apiKey.ToString());

        if (tenant is null || !tenant.IsActive)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "API Key inválida o Tenant inactivo." });
            return;
        }

        // Guardamos el TenantId resuelto para que los endpoints lo usen
        context.Items["TenantId"] = tenant.Id;

        await _next(context);
    }
}