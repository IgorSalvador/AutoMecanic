using AutoMecanic.Domain.Identidade;

namespace AutoMecanic.Api.Configuracao;

/// <summary>
/// Políticas de autorização do sistema, nomeadas pela <b>capacidade de negócio</b> que
/// concedem — e não pelo cargo de quem as usa.
/// <para>
/// A diferença importa: quando amanhã o Estoquista também puder abrir Ordens de Serviço,
/// muda-se a composição de uma política, e não dezenas de atributos espalhados pelos
/// controladores.
/// </para>
/// </summary>
public static class PoliticasDeAutorizacao
{
    /// <summary>Gerenciar usuários, catálogos e configurações. Exclusivo do administrador.</summary>
    public const string Administrar = "Administrar";

    /// <summary>Atender o cliente: cadastrar, abrir OS, enviar orçamento, entregar veículo.</summary>
    public const string Atender = "Atender";

    /// <summary>Executar serviços: diagnosticar, registrar laudo, finalizar.</summary>
    public const string ExecutarServico = "ExecutarServico";

    /// <summary>Movimentar peças e insumos no estoque.</summary>
    public const string GerenciarEstoque = "GerenciarEstoque";

    /// <summary>Consultar dados operacionais. Disponível a qualquer usuário autenticado.</summary>
    public const string Consultar = "Consultar";

    /// <summary>Perfis que satisfazem cada política.</summary>
    public static IReadOnlyDictionary<string, PerfilUsuario[]> PerfisPorPolitica { get; } =
        new Dictionary<string, PerfilUsuario[]>
        {
            [Administrar] = [PerfilUsuario.Administrador],

            [Atender] = [PerfilUsuario.Administrador, PerfilUsuario.Atendente],

            [ExecutarServico] = [PerfilUsuario.Administrador, PerfilUsuario.Mecanico],

            [GerenciarEstoque] = [PerfilUsuario.Administrador, PerfilUsuario.Estoquista],

            [Consultar] =
            [
                PerfilUsuario.Administrador,
                PerfilUsuario.Atendente,
                PerfilUsuario.Mecanico,
                PerfilUsuario.Estoquista
            ]
        };
}
