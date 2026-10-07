using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Apellido = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Telefono = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    PreferenciasJson = table.Column<string>(type: "jsonb", nullable: false),
                    ReglasPostulacion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Rol = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Estado = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "chats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FechaInicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_chats_usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cvs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UrlArchivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TextoPlano = table.Column<string>(type: "text", nullable: true),
                    EsPrincipal = table.Column<bool>(type: "boolean", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaActualizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cvs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cvs_usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "empresas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: true),
                    Rubro = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Ubicacion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SitioWeb = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaAlta = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_empresas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_empresas_usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "perfiles_laborales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Resumen = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ExperienciasJson = table.Column<string>(type: "jsonb", nullable: false),
                    FormacionJson = table.Column<string>(type: "jsonb", nullable: false),
                    HabilidadesJson = table.Column<string>(type: "jsonb", nullable: false),
                    IdiomasJson = table.Column<string>(type: "jsonb", nullable: false),
                    FechaActualizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_perfiles_laborales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_perfiles_laborales_usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mensajes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChatId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rol = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Contenido = table.Column<string>(type: "text", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mensajes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mensajes_chats_ChatId",
                        column: x => x.ChatId,
                        principalTable: "chats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notificaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversacionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Mensaje = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Leido = table.Column<bool>(type: "boolean", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notificaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notificaciones_chats_ConversacionId",
                        column: x => x.ConversacionId,
                        principalTable: "chats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                    table.ForeignKey(
                        name: "FK_empleos_empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_empleos_usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "postulaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmpleoId = table.Column<Guid>(type: "uuid", nullable: false),
                    CvId = table.Column<Guid>(type: "uuid", nullable: true),
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
                        name: "FK_postulaciones_cvs_CvId",
                        column: x => x.CvId,
                        principalTable: "cvs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_postulaciones_empleos_EmpleoId",
                        column: x => x.EmpleoId,
                        principalTable: "empleos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_postulaciones_usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recomendaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmpleoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Score = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Motivo = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recomendaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_recomendaciones_empleos_EmpleoId",
                        column: x => x.EmpleoId,
                        principalTable: "empleos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_recomendaciones_usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_chats_UsuarioId",
                table: "chats",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_cvs_UsuarioId",
                table: "cvs",
                column: "UsuarioId");

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
                name: "IX_empresas_UsuarioId",
                table: "empresas",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_mensajes_ChatId_Fecha",
                table: "mensajes",
                columns: new[] { "ChatId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_notificaciones_ConversacionId_Leido",
                table: "notificaciones",
                columns: new[] { "ConversacionId", "Leido" });

            migrationBuilder.CreateIndex(
                name: "IX_perfiles_laborales_UsuarioId",
                table: "perfiles_laborales",
                column: "UsuarioId",
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_recomendaciones_EmpleoId",
                table: "recomendaciones",
                column: "EmpleoId");

            migrationBuilder.CreateIndex(
                name: "IX_recomendaciones_UsuarioId_EmpleoId",
                table: "recomendaciones",
                columns: new[] { "UsuarioId", "EmpleoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_Email",
                table: "usuarios",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mensajes");

            migrationBuilder.DropTable(
                name: "notificaciones");

            migrationBuilder.DropTable(
                name: "perfiles_laborales");

            migrationBuilder.DropTable(
                name: "postulaciones");

            migrationBuilder.DropTable(
                name: "recomendaciones");

            migrationBuilder.DropTable(
                name: "chats");

            migrationBuilder.DropTable(
                name: "cvs");

            migrationBuilder.DropTable(
                name: "empleos");

            migrationBuilder.DropTable(
                name: "empresas");

            migrationBuilder.DropTable(
                name: "usuarios");
        }
    }
}
