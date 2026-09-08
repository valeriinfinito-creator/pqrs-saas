using Microsoft.EntityFrameworkCore;
using PQRS.Infrastructure.Persistence;

namespace PQRS.Api.Middleware;

/// Autoriza peticiones CORS únicamente desde el dominio configurado en el
/// Tenant correspondiente
public class DynamicCorsMiddleware
{
    private readonly RequestDelegate _next;

    public DynamicCorsMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext db)
    {
        // El Hub de SignalR permite cualquier origen: solo transmite eventos
        // de notificación, no expone ni recibe datos sensibles directamente.
        if (context.Request.Path.StartsWithSegments("/hubs"))
        {
            var hubOrigin = context.Request.Headers.Origin.ToString();
            if (!string.IsNullOrEmpty(hubOrigin))
            {
                context.Response.Headers.AccessControlAllowOrigin = hubOrigin;
                context.Response.Headers.AccessControlAllowCredentials = "true";
                context.Response.Headers.AccessControlAllowHeaders = "Content-Type, X-Requested-With, x-signalr-user-agent";
                context.Response.Headers.Vary = "Origin";
            }

            if (HttpMethods.IsOptions(context.Request.Method))
            {
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return;
            }

            await _next(context);
            return;
        }

        // Panel de agentes: en este MVP se permite cualquier origen para las
        // rutas autenticadas (auth, tickets, kb-articles, tenants), ya que
        // quedan protegidas por JWT de todas formas 
        if (context.Request.Path.StartsWithSegments("/api/v1/auth")
            || context.Request.Path.StartsWithSegments("/api/v1/tickets")
            || context.Request.Path.StartsWithSegments("/api/v1/kb-articles")
            || context.Request.Path.StartsWithSegments("/api/v1/tenants"))
        {
            var agentOrigin = context.Request.Headers.Origin.ToString();
            if (!string.IsNullOrEmpty(agentOrigin))
            {
                context.Response.Headers.AccessControlAllowOrigin = agentOrigin;
                context.Response.Headers.AccessControlAllowHeaders = "Content-Type, Authorization";
                context.Response.Headers.AccessControlAllowMethods = "GET, POST, PUT, DELETE, OPTIONS";
                context.Response.Headers.Vary = "Origin";
            }

            if (HttpMethods.IsOptions(context.Request.Method))
            {
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return;
            }

            await _next(context);
            return;
        }

        if (!context.Request.Path.StartsWithSegments("/api/v1/widget"))
        {
            await _next(context);
            return;
        }

        var origin = context.Request.Headers.Origin.ToString();

        if (!string.IsNullOrEmpty(origin))
        {
            var isAllowed = await db.Tenants
                .AnyAsync(t => t.IsActive && t.AllowedDomain == origin);

            if (isAllowed)
            {
                context.Response.Headers.AccessControlAllowOrigin = origin;
                context.Response.Headers.AccessControlAllowHeaders = "Content-Type, X-Widget-Api-Key";
                context.Response.Headers.AccessControlAllowMethods = "POST, OPTIONS";
                context.Response.Headers.Vary = "Origin";
            }
        }

        // Las peticiones preflight (OPTIONS) del navegador terminan aquí
        // solo necesitan los headers de arriba, no llegan al endpoint real.
        if (HttpMethods.IsOptions(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return;
        }

        await _next(context);
    }
}