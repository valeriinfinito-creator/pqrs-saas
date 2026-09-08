namespace PQRS.Application.Tickets;

//Datos que un agente puede actualizar en un ticket existente
public record UpdateTicketStatusRequest(string? Status, Guid? AssignedUserId);