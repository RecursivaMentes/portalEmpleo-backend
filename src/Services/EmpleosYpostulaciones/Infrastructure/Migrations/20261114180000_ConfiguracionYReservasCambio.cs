using System;
using EmpleosYpostulaciones.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261114180000_ConfiguracionYReservasCambio")]
public sealed class ConfiguracionYReservasCambio : Migration
{
    protected override void Up(MigrationBuilder b)
    {
        b.CreateTable(name: "configuraciones_postulacion", columns: t => new
        {
            UsuarioId = t.Column<Guid>(type: "uuid", nullable: false),
            Modo = t.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
            CompatibilidadMinima = t.Column<int>(type: "integer", nullable: false),
            LimiteDiario = t.Column<int>(type: "integer", nullable: false),
            OrdenPendientes = t.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
            ZonaHoraria = t.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
            Version = t.Column<int>(type: "integer", nullable: false)
        }, constraints: t => t.PrimaryKey("PK_configuraciones_postulacion", x => x.UsuarioId));
        b.CreateTable(name: "reservas_cambio_usuario", columns: t => new
        {
            UsuarioId = t.Column<Guid>(type: "uuid", nullable: false),
            OperacionId = t.Column<Guid>(type: "uuid", nullable: false),
            ExpiraUtc = t.Column<DateTime>(type: "timestamp with time zone", nullable: false)
        }, constraints: t => t.PrimaryKey("PK_reservas_cambio_usuario", x => x.UsuarioId));
        b.CreateIndex(name: "IX_reservas_cambio_usuario_ExpiraUtc",
            table: "reservas_cambio_usuario", column: "ExpiraUtc");
    }

    protected override void Down(MigrationBuilder b)
    {
        b.DropTable(name: "reservas_cambio_usuario");
        b.DropTable(name: "configuraciones_postulacion");
    }
}
