using PQRS.Domain.Enums;

namespace PQRS.Application.Tickets;

// Datos que el widget envía al radicar un PQRS
public record CreateTicketRequest(
    string CustomerName,
    string CustomerEmail,
    string Subject,
    string Description,
    bool OriginatedFromRag = false);

// Datos que la API devuelve tras crear el Ticket
public record TicketResponse(
    Guid Id,
    string CustomerName,
    string CustomerEmail,
    string Subject,
    string Description,
    TicketType Type,
    TicketStatus Status,
    TicketPriority Priority,
    Sentiment? Sentiment,
    string? AiSummary,
    DateTime CreatedAtUtc);