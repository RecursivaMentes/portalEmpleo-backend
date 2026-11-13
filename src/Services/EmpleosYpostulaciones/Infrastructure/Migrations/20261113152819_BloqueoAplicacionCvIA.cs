using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BloqueoAplicacionCvIA : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_preparaciones_postulacion_UsuarioId",
                table: "preparaciones_postulacion");

            migrationBuilder.CreateIndex(
                name: "IX_preparaciones_postulacion_UsuarioId",
                table: "preparaciones_postulacion",
                column: "UsuarioId",
                unique: true,
                filter: "\"Estado\" IN ('Preparando', 'EnModificacion', 'AplicandoCv', 'Enviando', 'CancelacionSolicitada')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_preparaciones_postulacion_UsuarioId",
                table: "preparaciones_postulacion");

            migrationBuilder.CreateIndex(
                name: "IX_preparaciones_postulacion_UsuarioId",
                table: "preparaciones_postulacion",
                column: "UsuarioId",
                unique: true,
                filter: "\"Estado\" IN ('Preparando', 'EnModificacion', 'Enviando', 'CancelacionSolicitada')");
        }
    }
}
