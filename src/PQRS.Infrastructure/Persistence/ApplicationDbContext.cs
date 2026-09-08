using Microsoft.EntityFrameworkCore;
using PQRS.Domain.Entities;

namespace PQRS.Infrastructure.Persistence;


// Contexto EF Core principal. El aislamiento multi-tenant se garantiza
// a nivel de aplicación (filtrando por TenantId en cada consulta) y
// se refuerza con índices compuestos que definimos más adelante

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<KnowledgeBaseArticle> KnowledgeBaseArticles => Set<KnowledgeBaseArticle>();
    public DbSet<Ticket> Tickets => Set<Ticket>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Habilita la extensión pgvector en PostgreSQL (necesaria para el
        // tipo 'vector' usado en KnowledgeBaseArticle.Embedding).
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}