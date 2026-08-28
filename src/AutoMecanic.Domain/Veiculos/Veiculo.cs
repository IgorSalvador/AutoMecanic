using AutoMecanic.Domain.Abstractions;
using AutoMecanic.Domain.Veiculos.Events;
using AutoMecanic.Domain.Veiculos.ValueObjects;

namespace AutoMecanic.Domain.Veiculos;

/// <summary>
/// <b>Raiz de Agregado</b> que representa o veículo atendido pela oficina.
/// <para>
/// É um agregado próprio — e não uma entidade interna de <c>Cliente</c> — porque tem ciclo
/// de vida independente: pode ser transferido entre clientes, é consultado e alterado sem
/// carregar o cliente, e é a ele que a Ordem de Serviço se refere. A associação com o
/// cliente é feita <b>por identidade</b> (<see cref="ClienteId"/>), respeitando a regra de
/// que agregados só se referenciam por Id.
/// </para>
/// <para><b>Invariantes:</b> placa válida e única; ano de fabricação plausível; quilometragem
/// nunca retrocede.</para>
/// </summary>
public sealed class Veiculo : AggregateRoot
{
    private const int AnoMinimoFabricacao = 1900;
    private const int QuilometragemMaxima = 3_000_000;

    private Veiculo()
    {
        Placa = null!;
        Marca = null!;
        Modelo = null!;
    }

    private Veiculo(
        Guid id,
        Guid clienteId,
        Placa placa,
        string marca,
        string modelo,
        int anoFabricacao,
        int anoModelo,
        string? cor,
        int quilometragem)
        : base(id)
    {
        ClienteId = clienteId;
        Placa = placa;
        Marca = marca;
        Modelo = modelo;
        AnoFabricacao = anoFabricacao;
        AnoModelo = anoModelo;
        Cor = cor;
        Quilometragem = quilometragem;
        Ativo = true;
        CadastradoEm = DateTimeOffset.UtcNow;
    }

    /// <summary>Referência por identidade ao agregado Cliente (proprietário atual).</summary>
    public Guid ClienteId { get; private set; }

    public Placa Placa { get; private set; }

    public string Marca { get; private set; }

    public string Modelo { get; private set; }

    public int AnoFabricacao { get; private set; }

    /// <summary>Ano-modelo, que pode ser o ano de fabricação ou o seguinte.</summary>
    public int AnoModelo { get; private set; }

    public string? Cor { get; private set; }

    /// <summary>Odômetro em quilômetros. Monotônico: só aceita valores maiores ou iguais ao atual.</summary>
    public int Quilometragem { get; private set; }

    public bool Ativo { get; private set; }

    public DateTimeOffset CadastradoEm { get; private set; }

    public DateTimeOffset? AtualizadoEm { get; private set; }

    /// <summary>Descrição amigável usada em listagens e no corpo do orçamento.</summary>
    public string Descricao => $"{Marca} {Modelo} {AnoModelo} - {Placa.Formatada}";

    public static Veiculo Cadastrar(
        Guid clienteId,
        string? placa,
        string? marca,
        string? modelo,
        int anoFabricacao,
        int? anoModelo = null,
        string? cor = null,
        int quilometragem = 0)
    {
        if (clienteId == Guid.Empty)
        {
            throw new DomainException("CLIENTE_OBRIGATORIO", "O veículo deve estar vinculado a um cliente.");
        }

        var anoModeloEfetivo = anoModelo ?? anoFabricacao;

        ValidarAnos(anoFabricacao, anoModeloEfetivo);
        ValidarQuilometragem(quilometragem);

        var veiculo = new Veiculo(
            NovoId(),
            clienteId,
            ValueObjects.Placa.Criar(placa),
            ExigirTexto(marca, "marca", 50),
            ExigirTexto(modelo, "modelo", 80),
            anoFabricacao,
            anoModeloEfetivo,
            string.IsNullOrWhiteSpace(cor) ? null : cor.Trim(),
            quilometragem);

        veiculo.RegistrarEvento(new VeiculoCadastrado(veiculo.Id, clienteId, veiculo.Placa.Valor));

        return veiculo;
    }

    /// <summary>
    /// Atualiza os dados descritivos. A placa é imutável: outra placa significa outro veículo
    /// — o caminho correto é inativar este e cadastrar o novo, preservando o histórico de OS.
    /// </summary>
    public void AtualizarDados(string? marca, string? modelo, int anoFabricacao, int anoModelo, string? cor)
    {
        GarantirVeiculoAtivo();
        ValidarAnos(anoFabricacao, anoModelo);

        Marca = ExigirTexto(marca, "marca", 50);
        Modelo = ExigirTexto(modelo, "modelo", 80);
        AnoFabricacao = anoFabricacao;
        AnoModelo = anoModelo;
        Cor = string.IsNullOrWhiteSpace(cor) ? null : cor.Trim();
        AtualizadoEm = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Registra a leitura do odômetro. Rejeita retrocesso, que na prática indicaria erro de
    /// digitação na recepção ou adulteração — em ambos os casos o dado não deve ser aceito.
    /// </summary>
    public void RegistrarQuilometragem(int novaQuilometragem)
    {
        GarantirVeiculoAtivo();
        ValidarQuilometragem(novaQuilometragem);

        if (novaQuilometragem < Quilometragem)
        {
            throw new DomainException(
                "QUILOMETRAGEM_RETROATIVA",
                $"A quilometragem informada ({novaQuilometragem} km) é menor que a última registrada ({Quilometragem} km).");
        }

        if (novaQuilometragem == Quilometragem)
        {
            return;
        }

        var anterior = Quilometragem;
        Quilometragem = novaQuilometragem;
        AtualizadoEm = DateTimeOffset.UtcNow;

        RegistrarEvento(new QuilometragemAtualizada(Id, anterior, novaQuilometragem));
    }

    /// <summary>Transfere a titularidade do veículo para outro cliente da oficina.</summary>
    public void TransferirPara(Guid novoClienteId)
    {
        GarantirVeiculoAtivo();

        if (novoClienteId == Guid.Empty)
        {
            throw new DomainException("CLIENTE_OBRIGATORIO", "O novo proprietário é obrigatório.");
        }

        if (novoClienteId == ClienteId)
        {
            return;
        }

        var anterior = ClienteId;
        ClienteId = novoClienteId;
        AtualizadoEm = DateTimeOffset.UtcNow;

        RegistrarEvento(new VeiculoTransferido(Id, anterior, novoClienteId));
    }

    public void Inativar(string? motivo = null)
    {
        if (!Ativo)
        {
            return;
        }

        Ativo = false;
        AtualizadoEm = DateTimeOffset.UtcNow;

        RegistrarEvento(new VeiculoInativado(Id, motivo ?? "Não informado"));
    }

    public void Reativar()
    {
        if (Ativo)
        {
            return;
        }

        Ativo = true;
        AtualizadoEm = DateTimeOffset.UtcNow;
    }

    public void GarantirVeiculoAtivo()
    {
        if (!Ativo)
        {
            throw new DomainException(
                "VEICULO_INATIVO",
                $"O veículo de placa '{Placa.Formatada}' está inativo e não pode ser utilizado.");
        }
    }

    private static void ValidarAnos(int anoFabricacao, int anoModelo)
    {
        var anoLimite = DateTimeOffset.UtcNow.Year + 1;

        if (anoFabricacao < AnoMinimoFabricacao || anoFabricacao > anoLimite)
        {
            throw new DomainException(
                "ANO_INVALIDO",
                $"O ano de fabricação deve estar entre {AnoMinimoFabricacao} e {anoLimite}.");
        }

        // O ano-modelo acompanha o de fabricação ou é o imediatamente seguinte;
        // qualquer outra combinação é erro de cadastro.
        if (anoModelo != anoFabricacao && anoModelo != anoFabricacao + 1)
        {
            throw new DomainException(
                "ANO_MODELO_INVALIDO",
                "O ano-modelo deve ser igual ao ano de fabricação ou o ano seguinte.");
        }
    }

    private static void ValidarQuilometragem(int quilometragem)
    {
        if (quilometragem is < 0 or > QuilometragemMaxima)
        {
            throw new DomainException(
                "QUILOMETRAGEM_INVALIDA",
                $"A quilometragem deve estar entre 0 e {QuilometragemMaxima:N0} km.");
        }
    }

    private static string ExigirTexto(string? valor, string campo, int comprimentoMaximo)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new DomainException("CAMPO_OBRIGATORIO", $"O campo '{campo}' do veículo é obrigatório.");
        }

        var limpo = valor.Trim();

        if (limpo.Length > comprimentoMaximo)
        {
            throw new DomainException("CAMPO_INVALIDO", $"O campo '{campo}' excede {comprimentoMaximo} caracteres.");
        }

        return limpo;
    }
}
