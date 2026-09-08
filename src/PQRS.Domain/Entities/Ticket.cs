using PQRS.Domain.Enums;

namespace PQRS.Domain.Entities;

//Petición, Queja, Reclamo o Sugerencia radicada por un cliente final

public class Ticket
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public TicketType Type { get; set; }
    public TicketStatus Status { get; set; } = TicketStatus.Pendiente;
    public TicketPriority Priority { get; set; } = TicketPriority.Media;
    public Sentiment? Sentiment { get; set; }

    //Resumen ejecutivo generado por la IA (1-2 oraciones)
    public string? AiSummary { get; set; }

    // True si el ticket nació de una consulta RAG que el usuario marcó como "No resuelta"
    public bool OriginatedFromRag { get; set; }

    //Agente asignado (opcional). Null = sin asignar
    public Guid? AssignedUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}