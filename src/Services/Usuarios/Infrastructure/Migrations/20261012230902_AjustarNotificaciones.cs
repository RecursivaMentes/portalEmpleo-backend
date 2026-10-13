using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AjustarNotificaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "ConversacionId",
                table: "notificaciones",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "UsuarioId",
                table: "notificaciones",
                type: "uuid",
                nullable: false);

            migrationBuilder.CreateIndex(
                name: "IX_notificaciones_UsuarioId_Leido",
                table: "notificaciones",
                columns: new[] { "UsuarioId", "Leido" });

            migrationBuilder.AddForeignKey(
                name: "FK_notificaciones_usuarios_UsuarioId",
                table: "notificaciones",
                column: "UsuarioId",
                principalTable: "usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_notificaciones_usuarios_UsuarioId",
                table: "notificaciones");

            migrationBuilder.DropIndex(
                name: "IX_notificaciones_UsuarioId_Leido",
                table: "notificaciones");

            migrationBuilder.DropColumn(
                name: "UsuarioId",
                table: "notificaciones");

            migrationBuilder.AlterColumn<Guid>(
                name: "ConversacionId",
                table: "notificaciones",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
