using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PrioridadYRevisionPreparaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CambiosPendientes",
                table: "preparaciones_postulacion",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "CvIdPropuesto",
                table: "preparaciones_postulacion",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TextoPresentacionPropuesto",
                table: "preparaciones_postulacion",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VersionEdicion",
                table: "preparaciones_postulacion",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CambiosPendientes",
                table: "preparaciones_postulacion");

            migrationBuilder.DropColumn(
                name: "CvIdPropuesto",
                table: "preparaciones_postulacion");

            migrationBuilder.DropColumn(
                name: "TextoPresentacionPropuesto",
                table: "preparaciones_postulacion");

            migrationBuilder.DropColumn(
                name: "VersionEdicion",
                table: "preparaciones_postulacion");
        }
    }
}
