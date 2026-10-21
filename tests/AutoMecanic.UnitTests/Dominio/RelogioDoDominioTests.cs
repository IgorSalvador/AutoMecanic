using AutoMecanic.Application.Veiculos.Dtos;
using AutoMecanic.Application.Validacao;
using AutoMecanic.Domain.Abstractions;
using AutoMecanic.Domain.OrdensServico;
using AutoMecanic.Domain.OrdensServico.Events;
using AutoMecanic.Domain.OrdensServico.ValueObjects;

namespace AutoMecanic.UnitTests.Dominio;

/// <summary>
/// Com o relógio do domínio controlável, as regras que dependem de tempo — duração da
/// execução, validade do orçamento, limite de ano do veículo — passam a ser verificadas com
/// valores exatos, e não com tolerâncias do tipo "foi há menos de um minuto".
/// </summary>
public sealed class RelogioDoDominioTests
{
    private static readonly DateTimeOffset Inicio = new(2026, 3, 10, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Congelar_SubstituiOAgoraERestauraAoDescartar()
    {
        var relogio = new RelogioAjustavel(Inicio);

        using (RelogioDoDominio.Congelar(relogio))
        {
            RelogioDoDominio.Agora.ShouldBe(Inicio);
        }

        RelogioDoDominio.Agora.ShouldNotBe(Inicio);
    }

    [Fact]
    public async Task Congelar_NaoVazaParaOutroFluxoAssincrono()
    {
        // Os testes rodam em paralelo: o relógio congelado por um não pode aparecer no outro.
        using var _ = RelogioDoDominio.Congelar(new RelogioAjustavel(Inicio));

        Task<DateTimeOffset> outroFluxo;

        using (ExecutionContext.SuppressFlow())
        {
            outroFluxo = Task.Run(() => RelogioDoDominio.Agora);
        }

        (await outroFluxo).ShouldNotBe(Inicio);
        RelogioDoDominio.Agora.ShouldBe(Inicio);
    }

    [Fact]
    public void FinalizarServico_DuracaoDaExecucaoEhExata()
    {
        var relogio = new RelogioAjustavel(Inicio);
        using var _ = RelogioDoDominio.Congelar(relogio);

        var ordem = OrdemComOrcamentoEnviado();
        ordem.AprovarOrcamento();

        relogio.Avancar(TimeSpan.FromMinutes(95));
        ordem.FinalizarServico();

        ordem.ExecucaoIniciadaEm.ShouldBe(Inicio);
        ordem.FinalizadaEm.ShouldBe(Inicio.AddMinutes(95));
        ordem.DuracaoDaExecucao.ShouldBe(TimeSpan.FromMinutes(95));
        ordem.EventosDeDominio.OfType<ServicoFinalizado>().ShouldHaveSingleItem().DuracaoEmMinutos.ShouldBe(95);
    }

    [Fact]
    public void EnviarOrcamento_ValidadeContaAPartirDoEnvio()
    {
        var relogio = new RelogioAjustavel(Inicio);
        using var _ = RelogioDoDominio.Congelar(relogio);

        var ordem = OrdemComOrcamentoEnviado(validadeEmDias: 7);

        ordem.Orcamento!.EnviadoEm.ShouldBe(Inicio);
        ordem.Orcamento.ValidoAte.ShouldBe(Inicio.AddDays(7));
        ordem.Orcamento.EstaVencido(Inicio.AddDays(7)).ShouldBeFalse();
        ordem.Orcamento.EstaVencido(Inicio.AddDays(7).AddSeconds(1)).ShouldBeTrue();
    }

    [Fact]
    public void EventoDeDominio_RegistraOInstanteDoRelogio()
    {
        using var _ = RelogioDoDominio.Congelar(new RelogioAjustavel(Inicio));

        var ordem = Abrir();

        ordem.CriadaEm.ShouldBe(Inicio);
        ordem.EventosDeDominio.OfType<OrdemDeServicoAberta>().ShouldHaveSingleItem().OcorridoEm.ShouldBe(Inicio);
    }

    [Theory]
    [InlineData(2031, true)]
    [InlineData(2032, false)]
    public void ValidadorDeVeiculo_LimiteDeAnoAcompanhaORelogio(int anoFabricacao, bool valido)
    {
        // O limite é calculado a cada validação: com o relógio em 2030, aceita-se até 2031.
        using var _ = RelogioDoDominio.Congelar(new RelogioAjustavel(new DateTimeOffset(2030, 6, 1, 0, 0, 0, TimeSpan.Zero)));

        var resultado = new ValidadorDeCriarVeiculo().Validate(
            new CriarVeiculoRequest(Guid.CreateVersion7(), "ABC1234", "Fiat", "Argo", anoFabricacao));

        resultado.IsValid.ShouldBe(valido);
    }

    private static OrdemServico Abrir() =>
        OrdemServico.Abrir(NumeroOrdemServico.Gerar(2026, 1), Guid.CreateVersion7(), Guid.CreateVersion7(), "Barulho ao frear");

    private static OrdemServico OrdemComOrcamentoEnviado(int validadeEmDias = 7)
    {
        var ordem = Abrir();
        ordem.IniciarDiagnostico();
        ordem.AdicionarServico(Guid.CreateVersion7(), "Troca de pastilhas", 160m, 1, 75);
        ordem.GerarOrcamento();
        ordem.EnviarOrcamentoParaAprovacao(validadeEmDias);

        return ordem;
    }
}
