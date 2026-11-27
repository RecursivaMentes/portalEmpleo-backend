using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

public partial class SeleccionAutonomaOfertas : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "FuenteOferta", table: "preparaciones_postulacion",
            type: "character varying(30)", maxLength: 30, nullable: true);
        migrationBuilder.AddColumn<string>(name: "FuenteOferta", table: "postulaciones",
            type: "character varying(30)", maxLength: 30, nullable: true);
        // Antes de esta migración toda iniciativa autónoma provenía de pendientes.
        migrationBuilder.Sql("""
            UPDATE preparaciones_postulacion SET "FuenteOferta" = 'Pendientes' WHERE "Origen" = 'IniciativaIA';
            UPDATE postulaciones p SET "FuenteOferta" = 'Pendientes'
            WHERE EXISTS (SELECT 1 FROM preparaciones_postulacion pr
                WHERE pr."PostulacionId" = p."Id" AND pr."Origen" = 'IniciativaIA' AND pr."Estado" = 'Enviada');
            """);
        migrationBuilder.CreateTable(name: "ofertas_descartadas", columns: t => new
        {
            UsuarioId = t.Column<Guid>(type: "uuid", nullable: false),
            EmpleoId = t.Column<Guid>(type: "uuid", nullable: false),
            FechaUtc = t.Column<DateTime>(type: "timestamp with time zone", nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_ofertas_descartadas", x => new { x.UsuarioId, x.EmpleoId });
            t.ForeignKey("FK_ofertas_descartadas_empleos_EmpleoId", x => x.EmpleoId,
                "empleos", "Id", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateIndex(name: "IX_ofertas_descartadas_EmpleoId",
            table: "ofertas_descartadas", column: "EmpleoId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ofertas_descartadas");
        migrationBuilder.DropColumn(name: "FuenteOferta", table: "postulaciones");
        migrationBuilder.DropColumn(name: "FuenteOferta", table: "preparaciones_postulacion");
    }
}
