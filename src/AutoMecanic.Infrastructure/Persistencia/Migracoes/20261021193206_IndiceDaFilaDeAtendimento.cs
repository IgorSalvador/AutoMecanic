using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoMecanic.Infrastructure.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class IndiceDaFilaDeAtendimento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_ordem_servico_status",
                schema: "automecanic",
                table: "ordem_servico");

            migrationBuilder.CreateIndex(
                name: "ix_ordem_servico_status_criada_em",
                schema: "automecanic",
                table: "ordem_servico",
                columns: new[] { "status", "criada_em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_ordem_servico_status_criada_em",
                schema: "automecanic",
                table: "ordem_servico");

            migrationBuilder.CreateIndex(
                name: "ix_ordem_servico_status",
                schema: "automecanic",
                table: "ordem_servico",
                column: "status");
        }
    }
}
