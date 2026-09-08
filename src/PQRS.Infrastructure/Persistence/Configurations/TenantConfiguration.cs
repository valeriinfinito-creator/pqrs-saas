using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PQRS.Domain.Entities;

namespace PQRS.Infrastructure.Persistence.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.AllowedDomain)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(t => t.WidgetApiKey)
            .IsRequired()
            .HasMaxLength(64);

        // La API Key del widget debe ser única es la forma en que elwidget se identifica públicamente ante el backend.
        builder.HasIndex(t => t.WidgetApiKey)
            .IsUnique();
    }
}