using Microsoft.AspNetCore.SignalR;

namespace PQRS.Api.Hubs;

// Hub de SignalR para notificaciones en tiempo real de tickets críticos.
// Los agentes se conectan y se unen a un "grupo" por su TenantId, así
// solo reciben eventos de su propia empresa (aislamiento multi-tenant
// también a nivel de tiempo real).

public class TicketNotificationHub : Hub
{
    public async Task JoinTenantGroup(string tenantId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, tenantId);
    }
}