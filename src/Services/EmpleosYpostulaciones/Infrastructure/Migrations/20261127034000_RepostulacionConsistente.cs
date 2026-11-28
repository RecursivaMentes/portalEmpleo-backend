using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

public partial class RepostulacionConsistente : Migration
{
    protected override void Up(MigrationBuilder b)
    {
        b.DropIndex(name: "IX_postulaciones_UsuarioId_EmpleoId", table: "postulaciones");
        b.CreateIndex(name: "IX_postulaciones_UsuarioId_EmpleoId", table: "postulaciones",
            columns: new[] { "UsuarioId", "EmpleoId" }, unique: true, filter: "\"Estado\" <> 'Retirada'");
        // Recuperar registros ocultos; no borrar información histórica durante la migración.
        b.Sql("UPDATE postulaciones SET \"OcultaParaUsuario\" = FALSE WHERE \"OcultaParaUsuario\" = TRUE");
        // Propuestas antiguas contradictorias: la postulación vigente es la fuente de verdad.
        b.Sql("""
            UPDATE preparaciones_postulacion AS p
            SET "Estado" = 'Cancelada', "UltimoError" = 'Ya existe una postulación vigente para esta oferta.',
                "VersionEdicion" = p."VersionEdicion" + 1, "FechaActualizacion" = CURRENT_TIMESTAMP
            WHERE p."Estado" IN ('Preparando', 'EnModificacion', 'PendienteAprobacionCv', 'AplicandoCv', 'PendienteAprobacion', 'Enviando', 'CancelacionSolicitada')
              AND EXISTS (SELECT 1 FROM postulaciones AS s WHERE s."UsuarioId" = p."UsuarioId"
                AND s."EmpleoId" = p."EmpleoId" AND s."Estado" <> 'Retirada');
            """);
    }
    protected override void Down(MigrationBuilder b)
    {
        // No se destruyen postulaciones para forzar el índice anterior.
        b.Sql("""
            DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM postulaciones GROUP BY "UsuarioId", "EmpleoId" HAVING COUNT(*) > 1)
                THEN RAISE EXCEPTION 'No se puede revertir el indice sin perder postulaciones historicas'; END IF;
            END; $$;
            """);
        b.DropIndex(name: "IX_postulaciones_UsuarioId_EmpleoId", table: "postulaciones");
        b.CreateIndex(name: "IX_postulaciones_UsuarioId_EmpleoId", table: "postulaciones",
            columns: new[] { "UsuarioId", "EmpleoId" }, unique: true);
    }
}
