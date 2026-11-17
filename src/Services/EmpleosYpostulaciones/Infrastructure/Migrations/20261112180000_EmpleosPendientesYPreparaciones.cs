using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class EmpleosPendientesYPreparaciones : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "empleos_pendientes",
                columns: table => new
                {
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmpleoId = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaAgregado = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_empleos_pendientes", x => new { x.UsuarioId, x.EmpleoId });
                    table.ForeignKey(
                        name: "FK_empleos_pendientes_empleos_EmpleoId",
                        column: x => x.EmpleoId,
                        principalTable: "empleos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "preparaciones_postulacion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmpleoId = table.Column<Guid>(type: "uuid", nullable: false),
                    CvId = table.Column<Guid>(type: "uuid", nullable: true),
                    TextoPresentacion = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Origen = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Estado = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PostulacionId = table.Column<Guid>(type: "uuid", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaActualizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_preparaciones_postulacion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_preparaciones_postulacion_empleos_EmpleoId",
                        column: x => x.EmpleoId,
                        principalTable: "empleos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_preparaciones_postulacion_postulaciones_PostulacionId",
                        column: x => x.PostulacionId,
                        principalTable: "postulaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_empleos_pendientes_EmpleoId",
                table: "empleos_pendientes", column: "EmpleoId");
            migrationBuilder.CreateIndex(
                name: "IX_preparaciones_postulacion_EmpleoId",
                table: "preparaciones_postulacion", column: "EmpleoId");
            migrationBuilder.CreateIndex(
                name: "IX_preparaciones_postulacion_PostulacionId",
                table: "preparaciones_postulacion", column: "PostulacionId");
            migrationBuilder.CreateIndex(
                name: "IX_preparaciones_postulacion_UsuarioId",
                table: "preparaciones_postulacion", column: "UsuarioId", unique: true,
                filter: "\"Estado\" IN ('Preparando', 'EnModificacion', 'Enviando', 'CancelacionSolicitada')");
            migrationBuilder.CreateIndex(
                name: "IX_preparaciones_postulacion_UsuarioId_EmpleoId",
                table: "preparaciones_postulacion", columns: new[] { "UsuarioId", "EmpleoId" }, unique: true,
                filter: "\"Estado\" IN ('Preparando', 'EnModificacion', 'PendienteAprobacionCv', 'PendienteAprobacion', 'Enviando', 'CancelacionSolicitada')");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "preparaciones_postulacion");
            migrationBuilder.DropTable(name: "empleos_pendientes");
        }
    }
}
