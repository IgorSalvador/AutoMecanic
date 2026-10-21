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
    Migrar
}

/// <summary>Leitura do modo de execução a partir dos argumentos de linha de comando.</summary>
internal static class ArgumentosDeExecucao
{
    /// <summary>Argumento que ativa o modo <see cref="ModoDeExecucao.Migrar"/>.</summary>
    public const string Migrar = "--migrar";

    /// <summary>
    /// Separa o modo de execução dos demais argumentos. O argumento de modo é removido para
    /// não chegar ao provedor de configuração por linha de comando, que o interpretaria como
    /// uma chave sem valor.
    /// </summary>
    public static (ModoDeExecucao Modo, string[] Restantes) Interpretar(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var migrar = args.Contains(Migrar, StringComparer.OrdinalIgnoreCase);

        var restantes = args
            .Where(argumento => !string.Equals(argumento, Migrar, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return (migrar ? ModoDeExecucao.Migrar : ModoDeExecucao.Servidor, restantes);
    }
}
