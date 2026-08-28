using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoMecanic.Infrastructure.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class EsquemaInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "automecanic");

            migrationBuilder.CreateTable(
                name: "cliente",
                schema: "automecanic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    documento = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    telefone = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    endereco_logradouro = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    endereco_numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    endereco_complemento = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    endereco_bairro = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    endereco_cidade = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    endereco_uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    endereco_cep = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    cadastrado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cliente", x => x.id);
                },
                comment: "Clientes atendidos pela oficina, pessoa física ou jurídica.");

            migrationBuilder.CreateTable(
                name: "peca",
                schema: "automecanic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    unidade_medida = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    preco_unitario = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    quantidade_em_estoque = table.Column<int>(type: "integer", nullable: false),
                    quantidade_reservada = table.Column<int>(type: "integer", nullable: false),
                    estoque_minimo = table.Column<int>(type: "integer", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    cadastrado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_peca", x => x.id);
                    table.CheckConstraint("ck_peca_preco_positivo", "preco_unitario > 0");
                    table.CheckConstraint("ck_peca_reserva_menor_que_saldo", "quantidade_reservada <= quantidade_em_estoque");
                    table.CheckConstraint("ck_peca_reserva_nao_negativa", "quantidade_reservada >= 0");
                    table.CheckConstraint("ck_peca_saldo_nao_negativo", "quantidade_em_estoque >= 0");
                },
                comment: "Peças e insumos controlados pelo estoque da oficina.");

            migrationBuilder.CreateTable(
                name: "sequencia_ordem_servico",
                schema: "automecanic",
                columns: table => new
                {
                    ano = table.Column<int>(type: "integer", nullable: false),
                    ultimo_valor = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sequencia_ordem_servico", x => x.ano);
                },
                comment: "Contador do número sequencial de Ordem de Serviço, reiniciado a cada ano.");

            migrationBuilder.CreateTable(
                name: "servico",
                schema: "automecanic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    categoria = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    preco = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    tempo_estimado_em_minutos = table.Column<int>(type: "integer", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    cadastrado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_servico", x => x.id);
                },
                comment: "Catálogo de serviços prestados, com preço de tabela e tempo estimado.");

            migrationBuilder.CreateTable(
                name: "usuario",
                schema: "automecanic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    senha_hash = table.Column<string>(type: "character varying(72)", maxLength: 72, nullable: false),
                    perfil = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    tentativas_falhas = table.Column<int>(type: "integer", nullable: false),
                    bloqueado_ate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ultimo_acesso_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cadastrado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuario", x => x.id);
                },
                comment: "Usuários administrativos com acesso às APIs protegidas por JWT.");

            migrationBuilder.CreateTable(
                name: "veiculo",
                schema: "automecanic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    placa = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    marca = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    modelo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ano_fabricacao = table.Column<int>(type: "integer", nullable: false),
                    ano_modelo = table.Column<int>(type: "integer", nullable: false),
                    cor = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    quilometragem = table.Column<int>(type: "integer", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    cadastrado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_veiculo", x => x.id);
                    table.ForeignKey(
                        name: "fk_veiculo_cliente",
                        column: x => x.cliente_id,
                        principalSchema: "automecanic",
                        principalTable: "cliente",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "Veículos atendidos pela oficina, vinculados a um cliente.");

            migrationBuilder.CreateTable(
                name: "movimento_estoque",
                schema: "automecanic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    peca_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    quantidade = table.Column<int>(type: "integer", nullable: false),
                    saldo_anterior = table.Column<int>(type: "integer", nullable: false),
                    saldo_atual = table.Column<int>(type: "integer", nullable: false),
                    motivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ordem_servico_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ocorrido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_movimento_estoque", x => x.id);
                    table.ForeignKey(
                        name: "fk_movimento_estoque_peca",
                        column: x => x.peca_id,
                        principalSchema: "automecanic",
                        principalTable: "peca",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "Razão append-only de todas as movimentações de estoque.");

            migrationBuilder.CreateTable(
                name: "ordem_servico",
                schema: "automecanic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    veiculo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    descricao_problema = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    diagnostico_tecnico = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    quilometragem_entrada = table.Column<int>(type: "integer", nullable: true),
                    responsavel_id = table.Column<Guid>(type: "uuid", nullable: true),
                    motivo_cancelamento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    criada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    execucao_iniciada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finalizada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    entregue_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ordem_servico", x => x.id);
                    table.ForeignKey(
                        name: "fk_ordem_servico_cliente",
                        column: x => x.cliente_id,
                        principalSchema: "automecanic",
                        principalTable: "cliente",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ordem_servico_veiculo",
                        column: x => x.veiculo_id,
                        principalSchema: "automecanic",
                        principalTable: "veiculo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "Ordens de Serviço: o agregado central do sistema.");

            migrationBuilder.CreateTable(
                name: "orcamento",
                schema: "automecanic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordem_servico_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor_servicos = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    valor_pecas = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    percentual_desconto = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    valor_total = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    status = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    gerado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    enviado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    valido_ate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    respondido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    motivo_reprovacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_orcamento", x => x.id);
                    table.ForeignKey(
                        name: "fk_orcamento_ordem_servico_ordem_servico_id",
                        column: x => x.ordem_servico_id,
                        principalSchema: "automecanic",
                        principalTable: "ordem_servico",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Orçamento gerado automaticamente a partir dos itens da OS.");

            migrationBuilder.CreateTable(
                name: "ordem_servico_historico",
                schema: "automecanic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordem_servico_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status_anterior = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    status_atual = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    observacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ocorrido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ordem_servico_historico", x => x.id);
                    table.ForeignKey(
                        name: "fk_ordem_servico_historico_ordem_servico_ordem_servico_id",
                        column: x => x.ordem_servico_id,
                        principalSchema: "automecanic",
                        principalTable: "ordem_servico",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Linha do tempo de transições de status da OS.");

            migrationBuilder.CreateTable(
                name: "ordem_servico_item_peca",
                schema: "automecanic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordem_servico_id = table.Column<Guid>(type: "uuid", nullable: false),
                    peca_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_peca = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    nome_peca = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    preco_unitario = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    quantidade = table.Column<int>(type: "integer", nullable: false),
                    reservada = table.Column<bool>(type: "boolean", nullable: false),
                    consumida = table.Column<bool>(type: "boolean", nullable: false),
                    adicionado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ordem_servico_item_peca", x => x.id);
                    table.ForeignKey(
                        name: "fk_ordem_servico_item_peca_ordem_servico_ordem_servico_id",
                        column: x => x.ordem_servico_id,
                        principalSchema: "automecanic",
                        principalTable: "ordem_servico",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Peças previstas em uma OS, com preço congelado e situação de reserva.");

            migrationBuilder.CreateTable(
                name: "ordem_servico_item_servico",
                schema: "automecanic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordem_servico_id = table.Column<Guid>(type: "uuid", nullable: false),
                    servico_id = table.Column<Guid>(type: "uuid", nullable: false),
                    descricao = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    preco_unitario = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    quantidade = table.Column<int>(type: "integer", nullable: false),
                    tempo_estimado_em_minutos = table.Column<int>(type: "integer", nullable: false),
                    adicionado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ordem_servico_item_servico", x => x.id);
                    table.ForeignKey(
                        name: "fk_ordem_servico_item_servico_ordem_servico_ordem_servico_id",
                        column: x => x.ordem_servico_id,
                        principalSchema: "automecanic",
                        principalTable: "ordem_servico",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Serviços contratados em uma OS, com preço congelado na inclusão.");

            migrationBuilder.CreateIndex(
                name: "ix_cliente_ativo",
                schema: "automecanic",
                table: "cliente",
                column: "ativo");

            migrationBuilder.CreateIndex(
                name: "ix_cliente_nome",
                schema: "automecanic",
                table: "cliente",
                column: "nome");

            migrationBuilder.CreateIndex(
                name: "ux_cliente_documento",
                schema: "automecanic",
                table: "cliente",
                column: "documento",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_movimento_estoque_ordem_servico",
                schema: "automecanic",
                table: "movimento_estoque",
                column: "ordem_servico_id");

            migrationBuilder.CreateIndex(
                name: "ix_movimento_estoque_peca_data",
                schema: "automecanic",
                table: "movimento_estoque",
                columns: new[] { "peca_id", "ocorrido_em" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_orcamento_ordem_servico_id",
                schema: "automecanic",
                table: "orcamento",
                column: "ordem_servico_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_orcamento_status",
                schema: "automecanic",
                table: "orcamento",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_orcamento_valido_ate",
                schema: "automecanic",
                table: "orcamento",
                column: "valido_ate");

            migrationBuilder.CreateIndex(
                name: "ix_ordem_servico_cliente_id",
                schema: "automecanic",
                table: "ordem_servico",
                column: "cliente_id");

            migrationBuilder.CreateIndex(
                name: "ix_ordem_servico_criada_em",
                schema: "automecanic",
                table: "ordem_servico",
                column: "criada_em");

            migrationBuilder.CreateIndex(
                name: "ix_ordem_servico_finalizada_em",
                schema: "automecanic",
                table: "ordem_servico",
                column: "finalizada_em");

            migrationBuilder.CreateIndex(
                name: "ix_ordem_servico_status",
                schema: "automecanic",
                table: "ordem_servico",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_ordem_servico_veiculo_id",
                schema: "automecanic",
                table: "ordem_servico",
                column: "veiculo_id");

            migrationBuilder.CreateIndex(
                name: "ux_ordem_servico_numero",
                schema: "automecanic",
                table: "ordem_servico",
                column: "numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ordem_servico_historico_ordem_data",
                schema: "automecanic",
                table: "ordem_servico_historico",
                columns: new[] { "ordem_servico_id", "ocorrido_em" });

            migrationBuilder.CreateIndex(
                name: "ix_ordem_servico_item_peca_ordem_servico_id",
                schema: "automecanic",
                table: "ordem_servico_item_peca",
                column: "ordem_servico_id");

            migrationBuilder.CreateIndex(
                name: "ix_ordem_servico_item_peca_peca_id",
                schema: "automecanic",
                table: "ordem_servico_item_peca",
                column: "peca_id");

            migrationBuilder.CreateIndex(
                name: "ix_ordem_servico_item_servico_ordem_servico_id",
                schema: "automecanic",
                table: "ordem_servico_item_servico",
                column: "ordem_servico_id");

            migrationBuilder.CreateIndex(
                name: "ix_ordem_servico_item_servico_servico_id",
                schema: "automecanic",
                table: "ordem_servico_item_servico",
                column: "servico_id");

            migrationBuilder.CreateIndex(
                name: "ix_peca_ativo",
                schema: "automecanic",
                table: "peca",
                column: "ativo");

            migrationBuilder.CreateIndex(
                name: "ix_peca_nome",
                schema: "automecanic",
                table: "peca",
                column: "nome");

            migrationBuilder.CreateIndex(
                name: "ux_peca_codigo",
                schema: "automecanic",
                table: "peca",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_servico_categoria",
                schema: "automecanic",
                table: "servico",
                column: "categoria");

            migrationBuilder.CreateIndex(
                name: "ux_servico_nome",
                schema: "automecanic",
                table: "servico",
                column: "nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_usuario_perfil",
                schema: "automecanic",
                table: "usuario",
                column: "perfil");

            migrationBuilder.CreateIndex(
                name: "ux_usuario_email",
                schema: "automecanic",
                table: "usuario",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_veiculo_ativo",
                schema: "automecanic",
                table: "veiculo",
                column: "ativo");

            migrationBuilder.CreateIndex(
                name: "ix_veiculo_cliente_id",
                schema: "automecanic",
                table: "veiculo",
                column: "cliente_id");

            migrationBuilder.CreateIndex(
                name: "ux_veiculo_placa",
                schema: "automecanic",
                table: "veiculo",
                column: "placa",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "movimento_estoque",
                schema: "automecanic");

            migrationBuilder.DropTable(
                name: "orcamento",
                schema: "automecanic");

            migrationBuilder.DropTable(
                name: "ordem_servico_historico",
                schema: "automecanic");

            migrationBuilder.DropTable(
                name: "ordem_servico_item_peca",
                schema: "automecanic");

            migrationBuilder.DropTable(
                name: "ordem_servico_item_servico",
                schema: "automecanic");

            migrationBuilder.DropTable(
                name: "sequencia_ordem_servico",
                schema: "automecanic");

            migrationBuilder.DropTable(
                name: "servico",
                schema: "automecanic");

            migrationBuilder.DropTable(
                name: "usuario",
                schema: "automecanic");

            migrationBuilder.DropTable(
                name: "peca",
                schema: "automecanic");

            migrationBuilder.DropTable(
                name: "ordem_servico",
                schema: "automecanic");

            migrationBuilder.DropTable(
                name: "veiculo",
                schema: "automecanic");

            migrationBuilder.DropTable(
                name: "cliente",
                schema: "automecanic");
        }
    }
}
