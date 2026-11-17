using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CopiaHistoricaCvPostulaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_preparaciones_postulacion_UsuarioId_EmpleoId",
                table: "preparaciones_postulacion");

            migrationBuilder.AddColumn<bool>(
                name: "CvNuevoIAPropuesto",
                table: "preparaciones_postulacion",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CvTextoIAPropuesto",
                table: "preparaciones_postulacion",
                type: "character varying(50000)",
                maxLength: 50000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CvTituloIAPropuesto",
                table: "preparaciones_postulacion",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CvVersionIAOriginal",
                table: "preparaciones_postulacion",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "CvDocumentoEnviado",
                table: "postulaciones",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CvDocumentoMime",
                table: "postulaciones",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CvDocumentoNombre",
                table: "postulaciones",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_preparaciones_postulacion_UsuarioId_EmpleoId",
                table: "preparaciones_postulacion",
                columns: new[] { "UsuarioId", "EmpleoId" },
                unique: true,
                filter: "\"Estado\" IN ('Preparando', 'EnModificacion', 'PendienteAprobacionCv', 'AplicandoCv', 'PendienteAprobacion', 'Enviando', 'CancelacionSolicitada')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_preparaciones_postulacion_UsuarioId_EmpleoId",
                table: "preparaciones_postulacion");

            migrationBuilder.DropColumn(
                name: "CvNuevoIAPropuesto",
                table: "preparaciones_postulacion");

            migrationBuilder.DropColumn(
                name: "CvTextoIAPropuesto",
                table: "preparaciones_postulacion");

            migrationBuilder.DropColumn(
                name: "CvTituloIAPropuesto",
                table: "preparaciones_postulacion");

            migrationBuilder.DropColumn(
                name: "CvVersionIAOriginal",
                table: "preparaciones_postulacion");

            migrationBuilder.DropColumn(
                name: "CvDocumentoEnviado",
                table: "postulaciones");

            migrationBuilder.DropColumn(
                name: "CvDocumentoMime",
                table: "postulaciones");

            migrationBuilder.DropColumn(
                name: "CvDocumentoNombre",
                table: "postulaciones");

            migrationBuilder.CreateIndex(
                name: "IX_preparaciones_postulacion_UsuarioId_EmpleoId",
                table: "preparaciones_postulacion",
                columns: new[] { "UsuarioId", "EmpleoId" },
                unique: true,
                filter: "\"Estado\" IN ('Preparando', 'EnModificacion', 'PendienteAprobacionCv', 'PendienteAprobacion', 'Enviando', 'CancelacionSolicitada')");
        }
    }
}
