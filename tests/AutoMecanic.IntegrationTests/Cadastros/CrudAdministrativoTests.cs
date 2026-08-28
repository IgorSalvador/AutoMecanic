using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AutoMecanic.IntegrationTests.Infraestrutura;

namespace AutoMecanic.IntegrationTests.Cadastros;

/// <summary>
/// Exercita a gestão administrativa exigida pelo requisito — CRUD de clientes, veículos,
/// serviços, peças e usuários — pelo mesmo caminho que a oficina usaria: requisição HTTP
/// autenticada contra a API e o banco reais.
/// </summary>
[Collection(ColecaoDeIntegracao.Nome)]
public sealed class CrudAdministrativoTests(AmbienteDaApi ambiente)
{
    // -----------------------------------------------------------------
    // Clientes
    // -----------------------------------------------------------------

    [Fact]
    public async Task Clientes_CicloCompletoDeCadastro()
    {
        var http = await ambiente.CriarClienteAutenticadoAsync();
        var documento = GeradorDeDadosValidos.ProximoCpf();

        // Criar
        var criado = await CriarAsync(http, "/api/v1/clientes", new
        {
            nome = "Cliente CRUD",
            documento,
            email = GeradorDeDadosValidos.ProximoEmail(),
            telefone = "11987654321",
            endereco = new
            {
                logradouro = "Rua das Oficinas",
                numero = "1500",
                complemento = "Sala 2",
                bairro = "Centro",
                cidade = "São Paulo",
                uf = "SP",
                cep = "01310-100"
            }
        });

        var id = criado.GetProperty("id").GetGuid();
        criado.GetProperty("endereco").GetProperty("uf").GetString().ShouldBe("SP");
        criado.GetProperty("tipoPessoa").GetString().ShouldBe("Fisica");

        // Ler por id e por documento
        (await GetAsync(http, $"/api/v1/clientes/{id}")).GetProperty("nome").GetString().ShouldBe("Cliente CRUD");
        (await GetAsync(http, $"/api/v1/clientes/documento/{documento}")).GetProperty("id").GetGuid().ShouldBe(id);

        // Listar com filtro
        var pagina = await GetAsync(http, "/api/v1/clientes?termoDeBusca=Cliente CRUD&apenasAtivos=true");
        pagina.GetProperty("totalDeItens").GetInt32().ShouldBeGreaterThanOrEqualTo(1);

        // Atualizar
        var atualizado = await PutAsync(http, $"/api/v1/clientes/{id}", new
        {
            nome = "Cliente CRUD Atualizado",
            email = GeradorDeDadosValidos.ProximoEmail(),
            telefone = "11912345678"
        });

        atualizado.GetProperty("nome").GetString().ShouldBe("Cliente CRUD Atualizado");

        // A API serializa com DefaultIgnoreCondition.WhenWritingNull: campo nulo é omitido
        // do JSON, não devolvido como null. Isso enxuga a resposta e é o contrato publicado.
        atualizado.TryGetProperty("endereco", out _).ShouldBeFalse();

        // Inativar e reativar — o cadastro nunca é excluído fisicamente.
        (await http.DeleteAsync($"/api/v1/clientes/{id}?motivo=Encerrou%20relacionamento"))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await GetAsync(http, $"/api/v1/clientes/{id}")).GetProperty("ativo").GetBoolean().ShouldBeFalse();

        (await http.PostAsync($"/api/v1/clientes/{id}/reativar", null))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await GetAsync(http, $"/api/v1/clientes/{id}")).GetProperty("ativo").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Clientes_BuscaPorDocumentoInexistente_Responde404()
    {
        var http = await ambiente.CriarClienteAutenticadoAsync();

        (await http.GetAsync($"/api/v1/clientes/documento/{GeradorDeDadosValidos.ProximoCpf()}"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // -----------------------------------------------------------------
    // Veículos
    // -----------------------------------------------------------------

    [Fact]
    public async Task Veiculos_CicloCompletoDeCadastro()
    {
        var http = await ambiente.CriarClienteAutenticadoAsync();

        var cliente = await CriarClienteAsync(http);
        var clienteId = cliente.GetProperty("id").GetGuid();
        var placa = GeradorDeDadosValidos.ProximaPlaca();

        var criado = await CriarAsync(http, "/api/v1/veiculos", new
        {
            clienteId,
            placa,
            marca = "Honda",
            modelo = "Civic",
            anoFabricacao = 2021,
            anoModelo = 2022,
            cor = "Preto",
            quilometragem = 30_000
        });

        var id = criado.GetProperty("id").GetGuid();
        criado.GetProperty("padraoPlaca").GetString().ShouldBe("Mercosul");

        (await GetAsync(http, $"/api/v1/veiculos/{id}")).GetProperty("nomeCliente").GetString()
            .ShouldBe(cliente.GetProperty("nome").GetString());

        (await GetAsync(http, $"/api/v1/veiculos/placa/{placa}")).GetProperty("id").GetGuid().ShouldBe(id);

        (await GetAsync(http, $"/api/v1/veiculos/cliente/{clienteId}")).GetArrayLength().ShouldBe(1);

        (await GetAsync(http, "/api/v1/veiculos?termoDeBusca=Civic"))
            .GetProperty("totalDeItens").GetInt32().ShouldBeGreaterThanOrEqualTo(1);

        // Atualizar dados descritivos.
        (await PutAsync(http, $"/api/v1/veiculos/{id}", new
        {
            marca = "Honda",
            modelo = "Civic Touring",
            anoFabricacao = 2021,
            anoModelo = 2022,
            cor = "Cinza"
        })).GetProperty("modelo").GetString().ShouldBe("Civic Touring");

        // Odômetro só avança.
        (await PatchAsync(http, $"/api/v1/veiculos/{id}/quilometragem", new { quilometragem = 35_000 }))
            .GetProperty("quilometragem").GetInt32().ShouldBe(35_000);

        var retrocesso = await http.PatchAsJsonAsync($"/api/v1/veiculos/{id}/quilometragem",
            new { quilometragem = 30_000 }, AmbienteDaApi.Json);

        retrocesso.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        // Transferência para outro cliente.
        var novoDono = await CriarClienteAsync(http);

        (await PostAsync(http, $"/api/v1/veiculos/{id}/transferir",
            new { novoClienteId = novoDono.GetProperty("id").GetGuid() }))
            .GetProperty("clienteId").GetGuid().ShouldBe(novoDono.GetProperty("id").GetGuid());

        (await http.DeleteAsync($"/api/v1/veiculos/{id}?motivo=Vendido"))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await http.PostAsync($"/api/v1/veiculos/{id}/reativar", null))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Veiculos_ComPlacaDuplicada_Responde409()
    {
        var http = await ambiente.CriarClienteAutenticadoAsync();
        var cliente = await CriarClienteAsync(http);
        var placa = GeradorDeDadosValidos.ProximaPlaca();

        var corpo = new
        {
            clienteId = cliente.GetProperty("id").GetGuid(),
            placa,
            marca = "Fiat",
            modelo = "Argo",
            anoFabricacao = 2022
        };

        (await http.PostAsJsonAsync("/api/v1/veiculos", corpo, AmbienteDaApi.Json))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        (await http.PostAsJsonAsync("/api/v1/veiculos", corpo, AmbienteDaApi.Json))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    // -----------------------------------------------------------------
    // Catálogo de serviços
    // -----------------------------------------------------------------

    [Fact]
    public async Task Servicos_CicloCompletoDeCadastro()
    {
        var http = await ambiente.CriarClienteAutenticadoAsync();
        var nome = $"Serviço de integração {Guid.CreateVersion7():N}"[..40];

        var criado = await CriarAsync(http, "/api/v1/servicos", new
        {
            nome,
            descricao = "Criado por teste automatizado.",
            categoria = "Diagnostico",
            preco = 199.90m,
            tempoEstimadoEmMinutos = 60
        });

        var id = criado.GetProperty("id").GetGuid();
        criado.GetProperty("categoria").GetString().ShouldBe("Diagnostico");

        (await GetAsync(http, $"/api/v1/servicos/{id}")).GetProperty("preco").GetDecimal().ShouldBe(199.90m);

        (await GetAsync(http, "/api/v1/servicos?categoria=Diagnostico&apenasAtivos=true"))
            .GetProperty("totalDeItens").GetInt32().ShouldBeGreaterThanOrEqualTo(1);

        (await PutAsync(http, $"/api/v1/servicos/{id}", new
        {
            nome = $"{nome} v2",
            descricao = "Atualizado.",
            categoria = "Eletrica",
            tempoEstimadoEmMinutos = 90
        })).GetProperty("categoria").GetString().ShouldBe("Eletrica");

        // O reajuste tem endpoint próprio para que a alteração de valor seja auditável.
        (await PatchAsync(http, $"/api/v1/servicos/{id}/preco", new { novoPreco = 249.90m }))
            .GetProperty("preco").GetDecimal().ShouldBe(249.90m);

        (await http.DeleteAsync($"/api/v1/servicos/{id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await http.PostAsync($"/api/v1/servicos/{id}/reativar", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Servicos_ComNomeDuplicado_Responde409()
    {
        var http = await ambiente.CriarClienteAutenticadoAsync();

        var corpo = new
        {
            nome = $"Duplicado {Guid.CreateVersion7():N}"[..30],
            descricao = (string?)null,
            categoria = "Outros",
            preco = 100m,
            tempoEstimadoEmMinutos = 30
        };

        (await http.PostAsJsonAsync("/api/v1/servicos", corpo, AmbienteDaApi.Json))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        (await http.PostAsJsonAsync("/api/v1/servicos", corpo, AmbienteDaApi.Json))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    // -----------------------------------------------------------------
    // Peças e estoque
    // -----------------------------------------------------------------

    [Fact]
    public async Task Pecas_CicloCompletoComMovimentacoesDeEstoque()
    {
        var http = await ambiente.CriarClienteAutenticadoAsync();
        var codigo = GeradorDeDadosValidos.ProximoCodigoDePeca();

        var criada = await CriarAsync(http, "/api/v1/pecas", new
        {
            codigo,
            nome = "Peça de integração",
            descricao = "Criada por teste.",
            unidadeMedida = "Unidade",
            precoUnitario = 75.50m,
            quantidadeInicial = 10,
            estoqueMinimo = 3
        });

        var id = criada.GetProperty("id").GetGuid();

        (await GetAsync(http, $"/api/v1/pecas/codigo/{codigo}")).GetProperty("id").GetGuid().ShouldBe(id);

        // Entrada de mercadoria.
        (await PostAsync(http, $"/api/v1/pecas/{id}/entradas", new { quantidade = 20, motivo = "NF 4567" }))
            .GetProperty("quantidadeEmEstoque").GetInt32().ShouldBe(30);

        // Perda.
        (await PostAsync(http, $"/api/v1/pecas/{id}/perdas", new { quantidade = 5, motivo = "Avaria no transporte" }))
            .GetProperty("quantidadeEmEstoque").GetInt32().ShouldBe(25);

        // Ajuste de inventário.
        (await PostAsync(http, $"/api/v1/pecas/{id}/ajustes", new { quantidadeApurada = 22, motivo = "Contagem física" }))
            .GetProperty("quantidadeEmEstoque").GetInt32().ShouldBe(22);

        // Reajuste de preço.
        (await PatchAsync(http, $"/api/v1/pecas/{id}/preco", new { novoPreco = 89.90m }))
            .GetProperty("precoUnitario").GetDecimal().ShouldBe(89.90m);

        // Atualização de dados.
        (await PutAsync(http, $"/api/v1/pecas/{id}", new
        {
            nome = "Peça de integração v2",
            descricao = "Atualizada.",
            unidadeMedida = "Unidade",
            estoqueMinimo = 5
        })).GetProperty("estoqueMinimo").GetInt32().ShouldBe(5);

        // O razão registrou os quatro lançamentos: inicial, entrada, perda e ajuste.
        var movimentos = await GetAsync(http, $"/api/v1/pecas/movimentos?pecaId={id}");
        movimentos.GetProperty("totalDeItens").GetInt32().ShouldBe(4);

        var tipos = movimentos.GetProperty("itens").EnumerateArray()
            .Select(m => m.GetProperty("tipo").GetString()!)
            .ToList();

        tipos.ShouldContain("Entrada");
        tipos.ShouldContain("Perda");
        tipos.ShouldContain("Ajuste");

        (await http.DeleteAsync($"/api/v1/pecas/{id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await http.PostAsync($"/api/v1/pecas/{id}/reativar", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Pecas_AlertaDeRessuprimento_ListaOQuePrecisaDeCompra()
    {
        var http = await ambiente.CriarClienteAutenticadoAsync();
        var codigo = GeradorDeDadosValidos.ProximoCodigoDePeca();

        await CriarAsync(http, "/api/v1/pecas", new
        {
            codigo,
            nome = "Peça crítica",
            descricao = (string?)null,
            unidadeMedida = "Unidade",
            precoUnitario = 50m,
            quantidadeInicial = 2,
            estoqueMinimo = 10
        });

        var alertas = await GetAsync(http, "/api/v1/pecas/alertas");

        var alerta = alertas.EnumerateArray()
            .FirstOrDefault(a => a.GetProperty("codigo").GetString() == codigo);

        alerta.ValueKind.ShouldNotBe(JsonValueKind.Undefined);
        alerta.GetProperty("quantidadeDisponivel").GetInt32().ShouldBe(2);

        // Sugestão de compra: repor até o dobro do ponto de ressuprimento.
        alerta.GetProperty("quantidadeSugeridaDeCompra").GetInt32().ShouldBe(18);
    }

    [Fact]
    public async Task Pecas_ListagemFiltrandoApenasAbaixoDoMinimo()
    {
        var http = await ambiente.CriarClienteAutenticadoAsync();

        var pagina = await GetAsync(http, "/api/v1/pecas?apenasAbaixoDoMinimo=true&apenasAtivas=true");

        foreach (var peca in pagina.GetProperty("itens").EnumerateArray())
        {
            peca.GetProperty("abaixoDoEstoqueMinimo").GetBoolean().ShouldBeTrue();
        }
    }

    // -----------------------------------------------------------------
    // Usuários
    // -----------------------------------------------------------------

    [Fact]
    public async Task Usuarios_CicloCompletoDeGestao()
    {
        var http = await ambiente.CriarClienteAutenticadoAsync();
        var email = GeradorDeDadosValidos.ProximoEmail();

        var criado = await CriarAsync(http, "/api/v1/usuarios", new
        {
            nome = "Usuário de Integração",
            email,
            senha = "Integracao@2026",
            perfil = "Estoquista"
        });

        var id = criado.GetProperty("id").GetGuid();
        criado.GetProperty("perfil").GetString().ShouldBe("Estoquista");

        (await GetAsync(http, $"/api/v1/usuarios/{id}")).GetProperty("email").GetString().ShouldBe(email);

        (await GetAsync(http, "/api/v1/usuarios?perfil=Estoquista&apenasAtivos=true"))
            .GetProperty("totalDeItens").GetInt32().ShouldBeGreaterThanOrEqualTo(1);

        (await PutAsync(http, $"/api/v1/usuarios/{id}", new { nome = "Usuário Renomeado", perfil = "Mecanico" }))
            .GetProperty("perfil").GetString().ShouldBe("Mecanico");

        // Redefinição administrativa: não exige a senha anterior.
        (await http.PostAsJsonAsync($"/api/v1/usuarios/{id}/senha/redefinir",
            new { novaSenha = "Redefinida@2026" }, AmbienteDaApi.Json))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // A nova senha funciona.
        var token = await AmbienteDaApi.ObterTokenAsync(ambiente.CriarClienteAnonimo(), email, "Redefinida@2026");
        token.ShouldNotBeNullOrWhiteSpace();

        (await http.PostAsync($"/api/v1/usuarios/{id}/desbloquear", null))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await http.DeleteAsync($"/api/v1/usuarios/{id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetAsync(http, $"/api/v1/usuarios/{id}")).GetProperty("ativo").GetBoolean().ShouldBeFalse();

        (await http.PostAsync($"/api/v1/usuarios/{id}/reativar", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Usuarios_TrocaDaPropriaSenha_ExigeASenhaAtual()
    {
        var admin = await ambiente.CriarClienteAutenticadoAsync();
        var email = GeradorDeDadosValidos.ProximoEmail();

        await CriarAsync(admin, "/api/v1/usuarios", new
        {
            nome = "Troca de Senha",
            email,
            senha = "Original@2026",
            perfil = "Atendente"
        });

        var http = ambiente.CriarClienteAnonimo();
        var token = await AmbienteDaApi.ObterTokenAsync(http, email, "Original@2026");
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Sem a senha atual, um token roubado permitiria tomar a conta permanentemente.
        (await http.PostAsJsonAsync("/api/v1/usuarios/eu/senha",
            new { senhaAtual = "Errada@2026", novaSenha = "Nova@2026" }, AmbienteDaApi.Json))
            .StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        (await http.PostAsJsonAsync("/api/v1/usuarios/eu/senha",
            new { senhaAtual = "Original@2026", novaSenha = "Nova@Senha2026" }, AmbienteDaApi.Json))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await http.GetAsync("/api/v1/usuarios/eu")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // -----------------------------------------------------------------
    // Ordens de serviço: consultas
    // -----------------------------------------------------------------

    [Fact]
    public async Task OrdensServico_ConsultaPorNumeroEListagemComFiltros()
    {
        var http = await ambiente.CriarClienteAutenticadoAsync();

        var os = await CriarAsync(http, "/api/v1/ordens-servico/recepcao", new
        {
            documentoCliente = GeradorDeDadosValidos.ProximoCpf(),
            nomeCliente = "Consulta de OS",
            emailCliente = GeradorDeDadosValidos.ProximoEmail(),
            telefoneCliente = "11987654321",
            placa = GeradorDeDadosValidos.ProximaPlaca(),
            marca = "Chevrolet",
            modelo = "Onix",
            anoFabricacao = 2020,
            descricaoProblema = "Revisão de 40 mil km."
        });

        var numero = os.GetProperty("numero").GetString()!;

        (await GetAsync(http, $"/api/v1/ordens-servico/numero/{numero}"))
            .GetProperty("id").GetGuid().ShouldBe(os.GetProperty("id").GetGuid());

        var pagina = await GetAsync(http,
            $"/api/v1/ordens-servico?status=Recebida&clienteId={os.GetProperty("clienteId").GetGuid()}");

        pagina.GetProperty("totalDeItens").GetInt32().ShouldBe(1);

        // Atribuição de responsável.
        var eu = await GetAsync(http, "/api/v1/usuarios/eu");

        (await PatchAsync(http, $"/api/v1/ordens-servico/{os.GetProperty("id").GetGuid()}/responsavel",
            new { responsavelId = eu.GetProperty("id").GetGuid() }))
            .GetProperty("responsavelId").GetGuid().ShouldBe(eu.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task OrdensServico_RevisaoDoOrcamentoDescongelaOsItens()
    {
        var http = await ambiente.CriarClienteAutenticadoAsync();
        var servico = (await GetAsync(http, "/api/v1/servicos?apenasAtivos=true&tamanhoPagina=1"))
            .GetProperty("itens")[0];

        var os = await CriarAsync(http, "/api/v1/ordens-servico/recepcao", new
        {
            documentoCliente = GeradorDeDadosValidos.ProximoCpf(),
            nomeCliente = "Revisão de Orçamento",
            emailCliente = GeradorDeDadosValidos.ProximoEmail(),
            telefoneCliente = "11987654321",
            placa = GeradorDeDadosValidos.ProximaPlaca(),
            marca = "Renault",
            modelo = "Kwid",
            anoFabricacao = 2022,
            descricaoProblema = "Barulho na suspensão."
        });

        var id = os.GetProperty("id").GetGuid();

        await PostAsync(http, $"/api/v1/ordens-servico/{id}/diagnostico/iniciar", null);

        var comItem = await PostAsync(http, $"/api/v1/ordens-servico/{id}/servicos",
            new { servicoId = servico.GetProperty("id").GetGuid(), quantidade = 2 });

        var itemId = comItem.GetProperty("servicos")[0].GetProperty("id").GetGuid();

        // Alterar quantidade antes do envio é permitido.
        (await PatchAsync(http, $"/api/v1/ordens-servico/{id}/servicos/{itemId}", new { quantidade = 3 }))
            .GetProperty("servicos")[0].GetProperty("quantidade").GetInt32().ShouldBe(3);

        await PostAsync(http, $"/api/v1/ordens-servico/{id}/orcamento", new { percentualDesconto = 0m });
        await PostAsync(http, $"/api/v1/ordens-servico/{id}/orcamento/enviar", new { validadeEmDias = 7 });

        // Devolver ao diagnóstico reabre o orçamento e descongela os itens.
        (await PostAsync(http, $"/api/v1/ordens-servico/{id}/orcamento/revisar?motivo=Cliente%20pediu%20revisao", null))
            .GetProperty("status").GetString().ShouldBe("EmDiagnostico");

        (await DeleteJsonAsync(http, $"/api/v1/ordens-servico/{id}/servicos/{itemId}"))
            .GetProperty("servicos").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task ExpiracaoDeOrcamentos_ExecutaSemErroQuandoNaoHaVencidos()
    {
        var http = await ambiente.CriarClienteAutenticadoAsync();

        var resposta = await http.PostAsync("/api/v1/ordens-servico/manutencao/expirar-orcamentos", null);

        resposta.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await resposta.Content.ReadFromJsonAsync<int>(AmbienteDaApi.Json)).ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Indicadores_PainelOperacionalReflitaAsOrdensEmAberto()
    {
        var http = await ambiente.CriarClienteAutenticadoAsync();

        var painel = await GetAsync(http, "/api/v1/indicadores/painel");

        painel.GetProperty("ordensEmAberto").GetInt32().ShouldBeGreaterThanOrEqualTo(0);
        painel.GetProperty("ordensPorStatus").ValueKind.ShouldBe(JsonValueKind.Object);
    }

    [Fact]
    public async Task Indicadores_TempoMedioAceitaPeriodoExplicito()
    {
        var http = await ambiente.CriarClienteAutenticadoAsync();

        var de = DateTimeOffset.UtcNow.AddDays(-7).ToString("O");
        var ate = DateTimeOffset.UtcNow.ToString("O");

        var indicador = await GetAsync(http,
            $"/api/v1/indicadores/tempo-medio-execucao?de={Uri.EscapeDataString(de)}&ate={Uri.EscapeDataString(ate)}");

        indicador.GetProperty("ordensFinalizadas").GetInt32().ShouldBeGreaterThanOrEqualTo(0);
    }

    // -----------------------------------------------------------------
    // Apoio
    // -----------------------------------------------------------------

    private static async Task<JsonElement> CriarClienteAsync(HttpClient http) =>
        await CriarAsync(http, "/api/v1/clientes", new
        {
            nome = "Cliente de Integração",
            documento = GeradorDeDadosValidos.ProximoCpf(),
            email = GeradorDeDadosValidos.ProximoEmail(),
            telefone = "11987654321"
        });

    private static async Task<JsonElement> CriarAsync(HttpClient http, string caminho, object corpo)
    {
        var resposta = await http.PostAsJsonAsync(caminho, corpo, AmbienteDaApi.Json);

        resposta.StatusCode.ShouldBe(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());

        return await resposta.Content.ReadFromJsonAsync<JsonElement>(AmbienteDaApi.Json);
    }

    private static async Task<JsonElement> GetAsync(HttpClient http, string caminho)
    {
        var resposta = await http.GetAsync(caminho);

        resposta.StatusCode.ShouldBe(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());

        return await resposta.Content.ReadFromJsonAsync<JsonElement>(AmbienteDaApi.Json);
    }

    private static async Task<JsonElement> PostAsync(HttpClient http, string caminho, object? corpo)
    {
        var resposta = await http.PostAsJsonAsync(caminho, corpo ?? new { }, AmbienteDaApi.Json);

        resposta.StatusCode.ShouldBe(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());

        return await resposta.Content.ReadFromJsonAsync<JsonElement>(AmbienteDaApi.Json);
    }

    private static async Task<JsonElement> PutAsync(HttpClient http, string caminho, object corpo)
    {
        var resposta = await http.PutAsJsonAsync(caminho, corpo, AmbienteDaApi.Json);

        resposta.StatusCode.ShouldBe(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());

        return await resposta.Content.ReadFromJsonAsync<JsonElement>(AmbienteDaApi.Json);
    }

    private static async Task<JsonElement> PatchAsync(HttpClient http, string caminho, object corpo)
    {
        var resposta = await http.PatchAsJsonAsync(caminho, corpo, AmbienteDaApi.Json);

        resposta.StatusCode.ShouldBe(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());

        return await resposta.Content.ReadFromJsonAsync<JsonElement>(AmbienteDaApi.Json);
    }

    private static async Task<JsonElement> DeleteJsonAsync(HttpClient http, string caminho)
    {
        var resposta = await http.DeleteAsync(caminho);

        resposta.StatusCode.ShouldBe(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());

        return await resposta.Content.ReadFromJsonAsync<JsonElement>(AmbienteDaApi.Json);
    }
}
