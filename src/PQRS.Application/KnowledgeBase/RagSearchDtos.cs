namespace PQRS.Application.KnowledgeBase;

// Lo que envía el widget cuando el usuario escribe una pregunta
public record RagSearchRequest(string Question);

// Lo que devolvemos si encontramos respuesta o no, y el texto sintetizado
public record RagSearchResponse(bool Found, string? Answer);