using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventOps.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CrmOrcamentoFornecedor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FornecedorId",
                table: "Utilizadores",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Estado",
                table: "Despesas",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FornecedorId",
                table: "Despesas",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Fornecedores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nome = table.Column<string>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: true),
                    NIF = table.Column<string>(type: "TEXT", nullable: true),
                    Categoria = table.Column<string>(type: "TEXT", nullable: true),
                    EventoId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fornecedores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Fornecedores_Eventos_EventoId",
                        column: x => x.EventoId,
                        principalTable: "Eventos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Oradores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nome = table.Column<string>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: true),
                    Telefone = table.Column<string>(type: "TEXT", nullable: true),
                    Bio = table.Column<string>(type: "TEXT", nullable: true),
                    EventoId = table.Column<int>(type: "INTEGER", nullable: false),
                    EstadoContrato = table.Column<int>(type: "INTEGER", nullable: false),
                    Cache = table.Column<decimal>(type: "TEXT", nullable: false),
                    NotasContrato = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Oradores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Oradores_Eventos_EventoId",
                        column: x => x.EventoId,
                        principalTable: "Eventos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrcamentosCategoria",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EventoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Categoria = table.Column<string>(type: "TEXT", nullable: false),
                    ValorPrevisto = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrcamentosCategoria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrcamentosCategoria_Eventos_EventoId",
                        column: x => x.EventoId,
                        principalTable: "Eventos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sponsors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nome = table.Column<string>(type: "TEXT", nullable: false),
                    Empresa = table.Column<string>(type: "TEXT", nullable: true),
                    Email = table.Column<string>(type: "TEXT", nullable: true),
                    Nivel = table.Column<int>(type: "INTEGER", nullable: false),
                    ValorPatrocinio = table.Column<decimal>(type: "TEXT", nullable: false),
                    EstadoContrato = table.Column<int>(type: "INTEGER", nullable: false),
                    EventoId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sponsors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sponsors_Eventos_EventoId",
                        column: x => x.EventoId,
                        principalTable: "Eventos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FicheirosFornecedor",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FornecedorId = table.Column<int>(type: "INTEGER", nullable: false),
                    NomeOriginal = table.Column<string>(type: "TEXT", nullable: false),
                    Caminho = table.Column<string>(type: "TEXT", nullable: false),
                    Tipo = table.Column<int>(type: "INTEGER", nullable: false),
                    DataUpload = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TamanhoBytes = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FicheirosFornecedor", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FicheirosFornecedor_Fornecedores_FornecedorId",
                        column: x => x.FornecedorId,
                        principalTable: "Fornecedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RequisitosOrador",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OradorId = table.Column<int>(type: "INTEGER", nullable: false),
                    Tipo = table.Column<int>(type: "INTEGER", nullable: false),
                    Descricao = table.Column<string>(type: "TEXT", nullable: false),
                    Estado = table.Column<int>(type: "INTEGER", nullable: false),
                    Custo = table.Column<decimal>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequisitosOrador", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequisitosOrador_Oradores_OradorId",
                        column: x => x.OradorId,
                        principalTable: "Oradores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Utilizadores_FornecedorId",
                table: "Utilizadores",
                column: "FornecedorId");

            migrationBuilder.CreateIndex(
                name: "IX_Despesas_FornecedorId",
                table: "Despesas",
                column: "FornecedorId");

            migrationBuilder.CreateIndex(
                name: "IX_FicheirosFornecedor_FornecedorId",
                table: "FicheirosFornecedor",
                column: "FornecedorId");

            migrationBuilder.CreateIndex(
                name: "IX_Fornecedores_EventoId",
                table: "Fornecedores",
                column: "EventoId");

            migrationBuilder.CreateIndex(
                name: "IX_Oradores_EventoId",
                table: "Oradores",
                column: "EventoId");

            migrationBuilder.CreateIndex(
                name: "IX_OrcamentosCategoria_EventoId",
                table: "OrcamentosCategoria",
                column: "EventoId");

            migrationBuilder.CreateIndex(
                name: "IX_RequisitosOrador_OradorId",
                table: "RequisitosOrador",
                column: "OradorId");

            migrationBuilder.CreateIndex(
                name: "IX_Sponsors_EventoId",
                table: "Sponsors",
                column: "EventoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Despesas_Fornecedores_FornecedorId",
                table: "Despesas",
                column: "FornecedorId",
                principalTable: "Fornecedores",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Utilizadores_Fornecedores_FornecedorId",
                table: "Utilizadores",
                column: "FornecedorId",
                principalTable: "Fornecedores",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Despesas_Fornecedores_FornecedorId",
                table: "Despesas");

            migrationBuilder.DropForeignKey(
                name: "FK_Utilizadores_Fornecedores_FornecedorId",
                table: "Utilizadores");

            migrationBuilder.DropTable(
                name: "FicheirosFornecedor");

            migrationBuilder.DropTable(
                name: "OrcamentosCategoria");

            migrationBuilder.DropTable(
                name: "RequisitosOrador");

            migrationBuilder.DropTable(
                name: "Sponsors");

            migrationBuilder.DropTable(
                name: "Fornecedores");

            migrationBuilder.DropTable(
                name: "Oradores");

            migrationBuilder.DropIndex(
                name: "IX_Utilizadores_FornecedorId",
                table: "Utilizadores");

            migrationBuilder.DropIndex(
                name: "IX_Despesas_FornecedorId",
                table: "Despesas");

            migrationBuilder.DropColumn(
                name: "FornecedorId",
                table: "Utilizadores");

            migrationBuilder.DropColumn(
                name: "Estado",
                table: "Despesas");

            migrationBuilder.DropColumn(
                name: "FornecedorId",
                table: "Despesas");
        }
    }
}
