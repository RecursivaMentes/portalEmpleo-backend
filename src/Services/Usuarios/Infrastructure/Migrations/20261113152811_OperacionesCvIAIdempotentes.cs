using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OperacionesCvIAIdempotentes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "operaciones_cv_ia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    CvId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionResultado = table.Column<long>(type: "bigint", nullable: false),
                    HuellaSolicitud = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operaciones_cv_ia", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_operaciones_cv_ia_UsuarioId",
                table: "operaciones_cv_ia",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "operaciones_cv_ia");
        }
    }
}
