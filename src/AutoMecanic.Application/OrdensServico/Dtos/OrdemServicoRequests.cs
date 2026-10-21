namespace AutoMecanic.Application.OrdensServico.Dtos;

/// <summary>Abertura de OS com cliente e veículo já cadastrados.</summary>
/// <param name="ClienteId">Cliente ativo previamente cadastrado.</param>
/// <param name="VeiculoId">Veículo ativo pertencente a esse cliente.</param>
/// <param name="DescricaoProblema">Relato do cliente na recepção.</param>
/// <param name="QuilometragemEntrada">Leitura do odômetro na entrada. Opcional.</param>
public sealed record AbrirOrdemServicoRequest(
    Guid ClienteId,
    Guid VeiculoId,
    string DescricaoProblema,
    int? QuilometragemEntrada = null);

/// <summary>
/// Abertura completa da Ordem de Serviço em uma única chamada, refletindo o balcão da oficina:
/// o atendente identifica o cliente pelo CPF/CNPJ, informa a placa, os serviços e as peças
/// combinados, e recebe a identificação da OS. Cliente e veículo são cadastrados
/// automaticamente quando ainda não existem; as peças são reservadas no estoque.
/// </summary>
/// <param name="DocumentoCliente">CPF ou CNPJ do cliente, com ou sem máscara.</param>
/// <param name="NomeCliente">Nome do cliente. Obrigatório apenas quando ele ainda não existe.</param>
/// <param name="EmailCliente">E-mail do cliente. Obrigatório apenas quando ele ainda não existe.</param>
/// <param name="TelefoneCliente">Telefone do cliente. Obrigatório apenas quando ele ainda não existe.</param>
/// <param name="Placa">Placa do veículo.</param>
/// <param name="Marca">Marca do veículo. Obrigatória apenas quando o veículo ainda não existe.</param>
/// <param name="Modelo">Modelo do veículo. Obrigatório apenas quando o veículo ainda não existe.</param>
/// <param name="AnoFabricacao">Ano de fabricação. Obrigatório apenas quando o veículo ainda não existe.</param>
/// <param name="AnoModelo">Ano-modelo. Opcional.</param>
/// <param name="Cor">Cor do veículo. Opcional.</param>
/// <param name="DescricaoProblema">Relato do cliente sobre o problema.</param>
/// <param name="QuilometragemEntrada">Leitura do odômetro na entrada. Opcional.</param>
/// <param name="Servicos">Serviços do catálogo já combinados com o cliente. Opcional.</param>
/// <param name="Pecas">Peças a reservar para a OS. Opcional; faltando saldo, nada é gravado.</param>
public sealed record ReceberVeiculoRequest(
    string DocumentoCliente,
    string? NomeCliente,
    string? EmailCliente,
    string? TelefoneCliente,
    string Placa,
    string? Marca,
    string? Modelo,
    int? AnoFabricacao,
    int? AnoModelo,
    string? Cor,
    string DescricaoProblema,
    int? QuilometragemEntrada = null,
    IReadOnlyList<AdicionarServicoRequest>? Servicos = null,
    IReadOnlyList<AdicionarPecaRequest>? Pecas = null);

/// <summary>Laudo técnico do mecânico.</summary>
/// <param name="Diagnostico">Texto do laudo.</param>
public sealed record RegistrarDiagnosticoRequest(string Diagnostico);

/// <summary>Inclusão de um serviço do catálogo na OS.</summary>
/// <param name="ServicoId">Serviço ativo do catálogo.</param>
/// <param name="Quantidade">Quantidade, de 1 a 999.</param>
public sealed record AdicionarServicoRequest(Guid ServicoId, int Quantidade = 1);

/// <summary>Inclusão de uma peça na OS. Reserva a quantidade no estoque.</summary>
/// <param name="PecaId">Peça ativa com saldo disponível.</param>
/// <param name="Quantidade">Quantidade, de 1 a 9.999.</param>
public sealed record AdicionarPecaRequest(Guid PecaId, int Quantidade = 1);

/// <summary>Alteração da quantidade de um item de serviço já incluído.</summary>
/// <param name="Quantidade">Nova quantidade.</param>
public sealed record AlterarQuantidadeRequest(int Quantidade);

/// <summary>Geração automática do orçamento a partir dos itens da OS.</summary>
/// <param name="PercentualDesconto">Desconto comercial de 0 a 100. Padrão zero.</param>
public sealed record GerarOrcamentoRequest(decimal PercentualDesconto = 0m);

/// <summary>Envio do orçamento ao cliente para aprovação.</summary>
/// <param name="ValidadeEmDias">Prazo de resposta, de 1 a 90 dias. Padrão 7.</param>
public sealed record EnviarOrcamentoRequest(int ValidadeEmDias = 7);

/// <summary>Reprovação do orçamento pelo cliente.</summary>
/// <param name="Motivo">Justificativa informada pelo cliente.</param>
public sealed record ReprovarOrcamentoRequest(string? Motivo);

/// <summary>Conclusão dos serviços.</summary>
/// <param name="Observacao">Observação do mecânico. Opcional.</param>
public sealed record FinalizarServicoRequest(string? Observacao = null);

/// <summary>Entrega do veículo ao cliente.</summary>
/// <param name="Observacao">Observação da entrega. Opcional.</param>
public sealed record EntregarVeiculoRequest(string? Observacao = null);

/// <summary>Cancelamento da Ordem de Serviço antes da execução.</summary>
/// <param name="Motivo">Justificativa obrigatória do cancelamento.</param>
public sealed record CancelarOrdemServicoRequest(string Motivo);

/// <summary>Atribuição do responsável técnico pela OS.</summary>
/// <param name="ResponsavelId">Usuário que passa a responder pela OS.</param>
public sealed record AtribuirResponsavelRequest(Guid ResponsavelId);

/// <summary>Aprovação do orçamento pelo próprio cliente, sem autenticação.</summary>
/// <param name="Numero">Número da OS (OS-AAAA-NNNNNN), informado no e-mail do orçamento.</param>
/// <param name="Documento">CPF ou CNPJ do cliente titular da OS.</param>
public sealed record RespostaAoOrcamentoRequest(string Numero, string Documento);

/// <summary>Recusa do orçamento pelo próprio cliente, sem autenticação.</summary>
/// <param name="Numero">Número da OS (OS-AAAA-NNNNNN), informado no e-mail do orçamento.</param>
/// <param name="Documento">CPF ou CNPJ do cliente titular da OS.</param>
/// <param name="Motivo">Justificativa da recusa. Opcional.</param>
public sealed record RecusaDoOrcamentoRequest(string Numero, string Documento, string? Motivo = null);
