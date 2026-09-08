namespace PQRS.Application.KnowledgeBase;

//Datos para crear o actualizar un artículo de la base de conocimiento
public record UpsertKnowledgeBaseArticleRequest(string Title, string Content);

//Respuesta con los datos del artículo (sin exponer el vector crudo)
public record KnowledgeBaseArticleResponse(
    Guid Id,
    string Title,
    string Content,
    bool IsActive,
    bool HasEmbedding,
    DateTime CreatedAtUtc);