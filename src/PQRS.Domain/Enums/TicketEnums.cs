namespace PQRS.Domain.Enums;

//Tipo de solicitud PQRS
public enum TicketType
{
    Peticion = 0,
    Queja = 1,
    Reclamo = 2,
    Sugerencia = 3
}

//Estado del ciclo de vida del ticket
public enum TicketStatus
{
    Pendiente = 0,
    EnProceso = 1,
    Resuelto = 2
}

//Prioridad asignada manual o automáticamente (IA) al ticket
public enum TicketPriority
{
    Baja = 0,
    Media = 1,
    Alta = 2
}

//Sentimiento detectado por el módulo de IA en el texto del ticket
public enum Sentiment
{
    Positivo = 0,
    Neutro = 1,
    Negativo = 2
}