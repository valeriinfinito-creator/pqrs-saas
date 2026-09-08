namespace PQRS.Application.Tickets;

// Versión resumida de un ticket para listados (dashboard de agentes)
public record TicketSummaryResponse(
    Guid Id,
    string CustomerName,
    string Subject,
    string Type,
    string Status,
    string Priority,
    string? Sentiment,
    string? AiSummary,
    DateTime CreatedAtUtc);