using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PQRS.Domain.Entities;

namespace PQRS.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.FullName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(u => u.PasswordHash)
            .IsRequired();

        builder.Property(u => u.Role)
            .IsRequired()
            .HasMaxLength(20);

        // Relación: un Tenant tiene muchos Users. Si se borra el Tenant,
        // se borran en cascada sus usuarios (regla de negocio: no tiene
        // sentido un agente huérfano sin empresa).
        builder.HasOne(u => u.Tenant)
            .WithMany()
            .HasForeignKey(u => u.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // Un mismo correo no puede repetirse DENTRO del mismo tenant,
        // pero sí podría existir en tenants distintos (dos empresas
        // distintas podrían tener un agente con el mismo email).
        builder.HasIndex(u => new { u.TenantId, u.Email })
            .IsUnique();
    }
}