using AutoMecanic.Application.Common;
using AutoMecanic.Domain.Clientes;
using AutoMecanic.Domain.Clientes.ValueObjects;
using AutoMecanic.Domain.Estoque;
using AutoMecanic.Domain.Identidade;
using AutoMecanic.Domain.OrdensServico;
using AutoMecanic.Domain.Servicos;
using AutoMecanic.Domain.Veiculos;
using AutoMecanic.Domain.Veiculos.ValueObjects;

namespace AutoMecanic.Application.Abstractions;

/// <summary>
/// Contrato comum de repositório. Um repositório existe <b>por raiz de agregado</b> — nunca
/// por tabela — porque é o agregado, e não a linha, que é carregado e salvo como uma unidade.
/// </summary>
/// <typeparam name="TAgregado">Raiz do agregado gerenciado.</typeparam>
public interface IRepositorio<TAgregado>
    where TAgregado : class
{
    /// <summary>Carrega o agregado completo pela identidade.</summary>
    Task<TAgregado?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Adiciona um novo agregado ao contexto. A persistência ocorre no commit da Unidade de Trabalho.</summary>
    Task AdicionarAsync(TAgregado agregado, CancellationToken cancellationToken = default);

    /// <summary>Marca o agregado como alterado.</summary>
    void Atualizar(TAgregado agregado);

    /// <summary>Remove o agregado. Usado apenas onde a exclusão física é aceitável pelo negócio.</summary>
    void Remover(TAgregado agregado);
}

/// <summary>Repositório do agregado <see cref="Cliente"/>.</summary>
public interface IRepositorioDeClientes : IRepositorio<Cliente>
{
    /// <summary>Busca pela chave natural — o caminho usado na abertura da Ordem de Serviço.</summary>
    Task<Cliente?> ObterPorDocumentoAsync(Documento documento, CancellationToken cancellationToken = default);

    /// <summary>Verifica duplicidade de CPF/CNPJ antes do cadastro.</summary>
    Task<bool> ExisteComDocumentoAsync(Documento documento, Guid? ignorarId = null, CancellationToken cancellationToken = default);

    /// <summary>Listagem paginada com filtro livre por nome ou documento.</summary>
    Task<ResultadoPaginado<Cliente>> ListarAsync(
        string? termoDeBusca,
        bool? apenasAtivos,
        ParametrosDePaginacao paginacao,
        CancellationToken cancellationToken = default);
}

/// <summary>Repositório do agregado <see cref="Veiculo"/>.</summary>
public interface IRepositorioDeVeiculos : IRepositorio<Veiculo>
{
    Task<Veiculo?> ObterPorPlacaAsync(Placa placa, CancellationToken cancellationToken = default);

    Task<bool> ExisteComPlacaAsync(Placa placa, Guid? ignorarId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Veiculo>> ListarPorClienteAsync(Guid clienteId, CancellationToken cancellationToken = default);

    Task<ResultadoPaginado<Veiculo>> ListarAsync(
        string? termoDeBusca,
        Guid? clienteId,
        bool? apenasAtivos,
        ParametrosDePaginacao paginacao,
        CancellationToken cancellationToken = default);
}

/// <summary>Repositório do catálogo de <see cref="Servico"/>.</summary>
public interface IRepositorioDeServicos : IRepositorio<Servico>
{
    Task<bool> ExisteComNomeAsync(string nome, Guid? ignorarId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Servico>> ObterPorIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    Task<ResultadoPaginado<Servico>> ListarAsync(
        string? termoDeBusca,
        CategoriaServico? categoria,
        bool? apenasAtivos,
        ParametrosDePaginacao paginacao,
        CancellationToken cancellationToken = default);
}

/// <summary>Repositório do agregado <see cref="Peca"/>.</summary>
public interface IRepositorioDePecas : IRepositorio<Peca>
{
    Task<Peca?> ObterPorCodigoAsync(string codigo, CancellationToken cancellationToken = default);

    Task<bool> ExisteComCodigoAsync(string codigo, Guid? ignorarId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Carrega várias peças de uma vez. Evita o problema de N+1 consultas ao montar um
    /// orçamento com muitos itens.
    /// </summary>
    Task<IReadOnlyList<Peca>> ObterPorIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>Peças cujo saldo disponível está no ponto de ressuprimento ou abaixo dele.</summary>
    Task<IReadOnlyList<Peca>> ListarAbaixoDoEstoqueMinimoAsync(CancellationToken cancellationToken = default);

    Task<ResultadoPaginado<Peca>> ListarAsync(
        string? termoDeBusca,
        bool? apenasAtivas,
        bool? apenasAbaixoDoMinimo,
        ParametrosDePaginacao paginacao,
        CancellationToken cancellationToken = default);
}

/// <summary>Repositório do razão de estoque (append-only).</summary>
public interface IRepositorioDeMovimentosDeEstoque : IRepositorio<MovimentoEstoque>
{
    Task<ResultadoPaginado<MovimentoEstoque>> ListarAsync(
        Guid? pecaId,
        Guid? ordemServicoId,
        TipoMovimentoEstoque? tipo,
        DateTimeOffset? de,
        DateTimeOffset? ate,
        ParametrosDePaginacao paginacao,
        CancellationToken cancellationToken = default);
}

/// <summary>Repositório do agregado <see cref="OrdemServico"/>.</summary>
public interface IRepositorioDeOrdensServico : IRepositorio<OrdemServico>
{
    /// <summary>Carrega a OS com itens, orçamento e histórico — o agregado inteiro.</summary>
    Task<OrdemServico?> ObterCompletaPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Busca pelo número legível informado pelo cliente (OS-AAAA-NNNNNN).</summary>
    Task<OrdemServico?> ObterPorNumeroAsync(string numero, CancellationToken cancellationToken = default);

    Task<ResultadoPaginado<OrdemServico>> ListarAsync(
        StatusOrdemServico? status,
        Guid? clienteId,
        Guid? veiculoId,
        DateTimeOffset? de,
        DateTimeOffset? ate,
        ParametrosDePaginacao paginacao,
        CancellationToken cancellationToken = default);

    /// <summary>Ordens finalizadas no período, usadas no cálculo do tempo médio de execução.</summary>
    Task<IReadOnlyList<OrdemServico>> ListarFinalizadasNoPeriodoAsync(
        DateTimeOffset de,
        DateTimeOffset ate,
        CancellationToken cancellationToken = default);

    /// <summary>Ordens com orçamento vencido, candidatas a expiração automática.</summary>
    Task<IReadOnlyList<OrdemServico>> ListarComOrcamentoVencidoAsync(
        DateTimeOffset referencia,
        CancellationToken cancellationToken = default);

    /// <summary>Contagem por status, para o painel operacional.</summary>
    Task<IReadOnlyDictionary<StatusOrdemServico, int>> ContarPorStatusAsync(CancellationToken cancellationToken = default);
}

/// <summary>Repositório do agregado <see cref="Usuario"/>.</summary>
public interface IRepositorioDeUsuarios : IRepositorio<Usuario>
{
    Task<Usuario?> ObterPorEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<bool> ExisteComEmailAsync(string email, Guid? ignorarId = null, CancellationToken cancellationToken = default);

    Task<bool> ExisteAlgumUsuarioAsync(CancellationToken cancellationToken = default);

    Task<ResultadoPaginado<Usuario>> ListarAsync(
        string? termoDeBusca,
        PerfilUsuario? perfil,
        bool? apenasAtivos,
        ParametrosDePaginacao paginacao,
        CancellationToken cancellationToken = default);
}
