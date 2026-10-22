using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoMecanic.Infrastructure.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class CaixaDeSaidaDeEmails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mensagem_saida",
                schema: "automecanic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    destinatario = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    nome_do_destinatario = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    assunto = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    corpo_html = table.Column<string>(type: "text", nullable: false),
                    corpo_texto = table.Column<string>(type: "text", nullable: false),
                    referencia = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    situacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tentativas = table.Column<int>(type: "integer", nullable: false),
                    criada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    proxima_tentativa_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    enviada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ultimo_erro = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mensagem_saida", x => x.id);
                },
                comment: "Caixa de saída de e-mails (outbox): gravada na transação da mudança, enviada depois.");

            migrationBuilder.CreateIndex(
                name: "ix_mensagem_saida_referencia",
                schema: "automecanic",
                table: "mensagem_saida",
                column: "referencia");

            migrationBuilder.CreateIndex(
                name: "ix_mensagem_saida_situacao_proxima_tentativa_em",
                schema: "automecanic",
                table: "mensagem_saida",
                columns: new[] { "situacao", "proxima_tentativa_em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mensagem_saida",
                schema: "automecanic");
        }
    }
}
