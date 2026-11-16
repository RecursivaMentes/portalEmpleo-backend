using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConcurrenciaCv : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "cvs",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.Sql(@"DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM cvs WHERE ""EsPrincipal"" = TRUE
                           GROUP BY ""UsuarioId"" HAVING COUNT(*) > 1) THEN
                    RAISE EXCEPTION 'Existen usuarios con varios CV principales. Corregir datos antes de continuar.';
                END IF;
            END $$;");
            migrationBuilder.CreateIndex(
                name: "IX_cvs_usuario_principal",
                table: "cvs",
                column: "UsuarioId",
                unique: true,
                filter: "\"EsPrincipal\" = TRUE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_cvs_usuario_principal",
                table: "cvs");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "cvs");
        }
    }
}
