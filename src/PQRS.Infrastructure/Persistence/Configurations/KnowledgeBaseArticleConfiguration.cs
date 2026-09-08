using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pgvector.EntityFrameworkCore;
using PQRS.Domain.Entities;

namespace PQRS.Infrastructure.Persistence.Configurations;

public class KnowledgeBaseArticleConfiguration : IEntityTypeConfiguration<KnowledgeBaseArticle>
{
    public void Configure(EntityTypeBuilder<KnowledgeBaseArticle> builder)
    {
        builder.ToTable("KnowledgeBaseArticles");

        builder.HasKey(k => k.Id);

        builder.Property(k => k.Title)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(k => k.Content)
            .IsRequired();

        builder.Property(k => k.Embedding)
            .HasColumnType("vector(384)");

        builder.HasOne(k => k.Tenant)
            .WithMany()
            .HasForeignKey(k => k.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // Índice vectorial HNSW: acelera drásticamente la búsqueda por
        // similitud Sin este índice cada búsqueda RAG tendría
        // que comparar el vector de la consulta contra TODOS los
        // artículos uno por uno
        builder.HasIndex(k => k.Embedding)
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops");

        // Índice para filtrar rápido 
        builder.HasIndex(k => new { k.TenantId, k.IsActive });
    }
}