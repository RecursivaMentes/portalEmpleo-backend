using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MigracionInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "empleos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: false),
                    Requisitos = table.Column<string>(type: "text", nullable: true),
                    Ubicacion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Modalidad = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TipoContrato = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SalarioMin = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    SalarioMax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Estado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FechaPublicacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaCierre = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_empleos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "postulaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmpleoId = table.Column<Guid>(type: "uuid", nullable: false),
                    CvId = table.Column<Guid>(type: "uuid", nullable: false),
                    Estado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Observaciones = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TextoPresentacion = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    RequiereAprobacion = table.Column<bool>(type: "boolean", nullable: false),
                    HistorialEstadosJson = table.Column<string>(type: "jsonb", nullable: false),
                    FechaPostulacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_postulaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_postulaciones_empleos_EmpleoId",
                        column: x => x.EmpleoId,
                        principalTable: "empleos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_empleos_EmpresaId",
                table: "empleos",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_empleos_Estado",
                table: "empleos",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_empleos_UsuarioId",
                table: "empleos",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_postulaciones_CvId",
                table: "postulaciones",
                column: "CvId");

            migrationBuilder.CreateIndex(
                name: "IX_postulaciones_EmpleoId",
                table: "postulaciones",
                column: "EmpleoId");

            migrationBuilder.CreateIndex(
                name: "IX_postulaciones_UsuarioId_EmpleoId",
                table: "postulaciones",
                columns: new[] { "UsuarioId", "EmpleoId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "postulaciones");

            migrationBuilder.DropTable(
                name: "empleos");
        }
    }
}
