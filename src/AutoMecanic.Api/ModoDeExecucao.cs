namespace AutoMecanic.Api;

/// <summary>
/// O que o processo foi iniciado para fazer. A mesma imagem de contêiner atende a mais de um
/// papel no Kubernetes: o servidor HTTP do Deployment e as tarefas pontuais dos Jobs.
/// </summary>
internal enum ModoDeExecucao
{
    /// <summary>Sobe o servidor HTTP. É o modo padrão, sem argumento.</summary>
    Servidor,

    /// <summary>
    /// Aplica as migrações pendentes, executa a carga inicial e encerra. É o comando do Job
    /// de migração: com várias réplicas sob o HPA, migrar na inicialização da API faria duas
    /// instâncias tentarem alterar o esquema ao mesmo tempo.
    /// </summary>
    Migrar,

    /// <summary>
    /// Expira os orçamentos vencidos, devolve as reservas de peça e encerra. É o comando do
    /// CronJob de manutenção: uma única execução por agendamento, em vez de um temporizador
    /// dentro de cada réplica disputando as mesmas Ordens de Serviço.
    /// </summary>
    ExpirarOrcamentos
}

/// <summary>Leitura do modo de execução a partir dos argumentos de linha de comando.</summary>
internal static class ArgumentosDeExecucao
{
    /// <summary>Argumento que ativa o modo <see cref="ModoDeExecucao.Migrar"/>.</summary>
    public const string Migrar = "--migrar";

    /// <summary>Argumento que ativa o modo <see cref="ModoDeExecucao.ExpirarOrcamentos"/>.</summary>
    public const string ExpirarOrcamentos = "--expirar-orcamentos";

    private static readonly Dictionary<string, ModoDeExecucao> Modos = new(StringComparer.OrdinalIgnoreCase)
    {
        [Migrar] = ModoDeExecucao.Migrar,
        [ExpirarOrcamentos] = ModoDeExecucao.ExpirarOrcamentos
    };

    /// <summary>
    /// Separa o modo de execução dos demais argumentos. O argumento de modo é removido para
    /// não chegar ao provedor de configuração por linha de comando, que o interpretaria como
    /// uma chave sem valor.
    /// </summary>
    /// <exception cref="ArgumentException">Mais de um modo informado.</exception>
    public static (ModoDeExecucao Modo, string[] Restantes) Interpretar(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var modos = args
            .Where(Modos.ContainsKey)
            .Select(argumento => Modos[argumento])
            .Distinct()
            .ToList();

        if (modos.Count > 1)
        {
            throw new ArgumentException(
                $"Informe apenas um modo de execução: {string.Join(" ou ", Modos.Keys)}.", nameof(args));
        }

        var restantes = args.Where(argumento => !Modos.ContainsKey(argumento)).ToArray();

        return (modos.Count == 1 ? modos[0] : ModoDeExecucao.Servidor, restantes);
    }
}
