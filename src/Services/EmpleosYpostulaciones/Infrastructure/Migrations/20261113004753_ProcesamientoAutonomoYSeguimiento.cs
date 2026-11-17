using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProcesamientoAutonomoYSeguimiento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ContabilizadaEnCupo",
                table: "preparaciones_postulacion",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaContabilizacionUtc",
                table: "preparaciones_postulacion",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModoEjecucion",
                table: "preparaciones_postulacion",
                type: "character varying(25)",
                maxLength: 25,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UltimoError",
                table: "preparaciones_postulacion",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Origen",
                table: "postulaciones",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "intentos_postulacion_ia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmpleoId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreparacionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Resultado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FechaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intentos_postulacion_ia", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_preparaciones_postulacion_UsuarioId_FechaContabilizacionUtc",
                table: "preparaciones_postulacion",
                columns: new[] { "UsuarioId", "FechaContabilizacionUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_intentos_postulacion_ia_UsuarioId_EmpleoId_FechaUtc",
                table: "intentos_postulacion_ia",
                columns: new[] { "UsuarioId", "EmpleoId", "FechaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_intentos_postulacion_ia_UsuarioId_FechaUtc",
                table: "intentos_postulacion_ia",
                columns: new[] { "UsuarioId", "FechaUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "intentos_postulacion_ia");

            migrationBuilder.DropIndex(
                name: "IX_preparaciones_postulacion_UsuarioId_FechaContabilizacionUtc",
                table: "preparaciones_postulacion");

            migrationBuilder.DropColumn(
                name: "ContabilizadaEnCupo",
                table: "preparaciones_postulacion");

            migrationBuilder.DropColumn(
                name: "FechaContabilizacionUtc",
                table: "preparaciones_postulacion");

            migrationBuilder.DropColumn(
                name: "ModoEjecucion",
                table: "preparaciones_postulacion");

            migrationBuilder.DropColumn(
                name: "UltimoError",
                table: "preparaciones_postulacion");

            migrationBuilder.DropColumn(
                name: "Origen",
                table: "postulaciones");
        }
    }
}
