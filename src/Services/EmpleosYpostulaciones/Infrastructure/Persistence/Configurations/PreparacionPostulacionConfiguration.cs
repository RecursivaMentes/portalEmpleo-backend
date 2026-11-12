using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpleosYpostulaciones.Infrastructure.Persistence.Configurations;

public sealed class PreparacionPostulacionConfiguration : IEntityTypeConfiguration<PreparacionPostulacion>
{
    // En espera de aprobacion la IA ya esta libre; una propuesta sigue abierta para esa oferta.
    public const string FiltroEjecucion = "\"Estado\" IN ('Preparando', 'EnModificacion', 'Enviando', 'CancelacionSolicitada')";
    public const string FiltroNoFinalizada = "\"Estado\" IN ('Preparando', 'EnModificacion', 'PendienteAprobacionCv', 'PendienteAprobacion', 'Enviando', 'CancelacionSolicitada')";

    public void Configure(EntityTypeBuilder<PreparacionPostulacion> builder)
    {
        builder.ToTable("preparaciones_postulacion");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Estado).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.Origen).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.TextoPresentacion).HasMaxLength(4000);
        builder.Property(x => x.FechaCreacion).IsRequired();
        builder.Property(x => x.FechaActualizacion).IsRequired();
        builder.HasOne<Empleo>().WithMany().HasForeignKey(x => x.EmpleoId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Postulacion>().WithMany().HasForeignKey(x => x.PostulacionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.UsuarioId).IsUnique().HasFilter(FiltroEjecucion);
        builder.HasIndex(x => new { x.UsuarioId, x.EmpleoId }).IsUnique().HasFilter(FiltroNoFinalizada);
    }
}
