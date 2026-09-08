using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PQRS.Domain.Entities;

namespace PQRS.Infrastructure.Persistence.Configurations;

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("Tickets");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.CustomerName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.CustomerEmail)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(t => t.Subject)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(t => t.Description)
            .IsRequired();

        builder.Property(t => t.AiSummary)
            .HasMaxLength(500);

        // Los enums (Type, Status, Priority, Sentiment) se guardan como
        // enteros por defecto en PostgreSQL ya definimos sus valores en TicketEnums.cs

        builder.HasOne(t => t.Tenant)
            .WithMany()
            .HasForeignKey(t => t.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // Índices que pide el documento explícitamente:
        // (TenantId, Status) y (TenantId, Priority), para que el
        // dashboard de agentes pueda filtrar tickets rápido
        builder.HasIndex(t => new { t.TenantId, t.Status });
        builder.HasIndex(t => new { t.TenantId, t.Priority });

        // listar tickets de un tenant ordenados por fecha de creación 
        builder.HasIndex(t => new { t.TenantId, t.CreatedAtUtc });
    }
}