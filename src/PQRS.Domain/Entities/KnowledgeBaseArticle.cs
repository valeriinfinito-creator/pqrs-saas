using Pgvector;

namespace PQRS.Domain.Entities;

// Artículo de la base de conocimiento (FAQ/documentación) usado para RAG.
// El campo Embedding almacena el vector semántico del contenido para
// búsquedas por similitud coseno filtradas por TenantId.

public class KnowledgeBaseArticle
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public Vector? Embedding { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime? UpdatedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}