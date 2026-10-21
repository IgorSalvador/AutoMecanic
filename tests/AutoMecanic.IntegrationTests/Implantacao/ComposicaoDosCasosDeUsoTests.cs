using AutoMecanic.Application.OrdensServico.CasosDeUso;
using AutoMecanic.Infrastructure.Persistencia.Seed;
using AutoMecanic.IntegrationTests.Infraestrutura;
using Microsoft.Extensions.DependencyInjection;

namespace AutoMecanic.IntegrationTests.Implantacao;

/// <summary>
/// Os casos de uso são registrados por convenção. Este teste resolve cada um no contêiner real
/// da aplicação, para que um colaborador esquecido apareça aqui, e não na primeira requisição
/// em produção.
/// </summary>
[Collection(ColecaoDeIntegracao.Nome)]
public sealed class ComposicaoDosCasosDeUsoTests(AmbienteDaApi ambiente)
{
    public static TheoryData<Type> CasosDeUso()
    {
        var dados = new TheoryData<Type>();

        foreach (var tipo in typeof(ReceberVeiculo).Assembly.GetTypes()
                     .Where(tipo => tipo is { IsClass: true, IsPublic: true }
                                    && tipo.Namespace == typeof(ReceberVeiculo).Namespace))
        {
            dados.Add(tipo);
        }

        return dados;
    }

    [Theory]
    [MemberData(nameof(CasosDeUso))]
    public void CasoDeUso_EhResolvidoPeloConteiner(Type casoDeUso)
    {
        using var escopo = ambiente.Services.CreateScope();

        escopo.ServiceProvider.GetRequiredService(casoDeUso).ShouldNotBeNull();
    }

    [Fact]
    public void SemeadorDeOrdensDeServico_EhResolvidoPeloConteiner()
    {
        using var escopo = ambiente.Services.CreateScope();

        escopo.ServiceProvider.GetRequiredService<SemeadorDeOrdensDeServico>().ShouldNotBeNull();
    }
}
