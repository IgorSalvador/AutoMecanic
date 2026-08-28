using System.Diagnostics;
using AutoMecanic.Application.Common;
using AutoMecanic.Domain.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutoMecanic.Api.Middlewares;

/// <summary>
/// Converte exceções em respostas <c>application/problem+json</c> (RFC 9457).
/// <para>
/// Concentrar a tradução aqui mantém os controladores livres de <c>try/catch</c> e — mais
/// importante do ponto de vista de segurança — garante que <b>nenhuma exceção inesperada
/// escape com pilha de chamadas ou mensagem interna</b> para o cliente. Erros conhecidos do
/// domínio recebem status e mensagem específicos; qualquer outra falha vira um 500 genérico,
/// com o detalhe registrado apenas no log do servidor.
/// </para>
/// </summary>
public sealed class MiddlewareDeTratamentoDeExcecoes(
    RequestDelegate proximo,
    ILogger<MiddlewareDeTratamentoDeExcecoes> logger,
    IHostEnvironment ambiente)
{
    public async Task InvokeAsync(HttpContext contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        try
        {
            await proximo(contexto);
        }
        catch (Exception excecao)
        {
            await TratarAsync(contexto, excecao);
        }
    }

    private async Task TratarAsync(HttpContext contexto, Exception excecao)
    {
        if (contexto.Response.HasStarted)
        {
            // A resposta já começou a ser enviada: não há como trocar o status.
            logger.LogError(excecao, "Exceção após o início da resposta. Não foi possível formatar o erro.");
            throw excecao;
        }

        var identificadorDaRequisicao = Activity.Current?.Id ?? contexto.TraceIdentifier;
        var detalhes = Traduzir(excecao, identificadorDaRequisicao);

        if (detalhes.Status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(excecao, "Falha não tratada na requisição {Requisicao}.", identificadorDaRequisicao);
        }
        else
        {
            logger.LogWarning(
                "Requisição {Requisicao} recusada: {Status} - {Titulo}",
                identificadorDaRequisicao, detalhes.Status, detalhes.Title);
        }

        contexto.Response.Clear();
        contexto.Response.StatusCode = detalhes.Status ?? StatusCodes.Status500InternalServerError;
        contexto.Response.ContentType = "application/problem+json";

        await contexto.Response.WriteAsJsonAsync(detalhes, detalhes.GetType());
    }

    private ProblemDetails Traduzir(Exception excecao, string identificadorDaRequisicao) => excecao switch
    {
        ValidacaoException validacao => new ValidationProblemDetails(validacao.Erros)
        {
            Title = "Requisição inválida",
            Status = StatusCodes.Status400BadRequest,
            Detail = validacao.Message,
            Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.1",
            Extensions = { ["requisicaoId"] = identificadorDaRequisicao }
        },

        NaoAutorizadoException naoAutorizado => Criar(
            "Não autorizado",
            naoAutorizado.Message,
            StatusCodes.Status401Unauthorized,
            identificadorDaRequisicao),

        RecursoNaoEncontradoException naoEncontrado => Criar(
            "Recurso não encontrado",
            naoEncontrado.Message,
            StatusCodes.Status404NotFound,
            identificadorDaRequisicao),

        ConflitoException conflito => Criar(
            "Conflito com o estado atual",
            conflito.Message,
            StatusCodes.Status409Conflict,
            identificadorDaRequisicao,
            conflito.Codigo),

        // Concorrência otimista: outra requisição alterou o mesmo registro entre a leitura
        // e a gravação. O cliente deve recarregar e tentar de novo.
        DbUpdateConcurrencyException => Criar(
            "Conflito de concorrência",
            "O registro foi alterado por outra operação. Recarregue os dados e tente novamente.",
            StatusCodes.Status409Conflict,
            identificadorDaRequisicao,
            "CONFLITO_DE_CONCORRENCIA"),

        // Violação de invariante do negócio: a requisição está sintaticamente correta,
        // mas a operação não é permitida no estado atual. 422 comunica exatamente isso.
        DomainException dominio => Criar(
            "Regra de negócio violada",
            dominio.Message,
            StatusCodes.Status422UnprocessableEntity,
            identificadorDaRequisicao,
            dominio.Codigo),

        OperationCanceledException => Criar(
            "Requisição cancelada",
            "A requisição foi cancelada pelo cliente.",
            StatusCodesExtras.ClientClosedRequest,
            identificadorDaRequisicao),

        _ => Criar(
            "Erro interno",
            // Em produção a mensagem é genérica de propósito: detalhes de exceção revelam
            // estrutura interna, versões de biblioteca e, por vezes, dados de outros usuários.
            ambiente.IsDevelopment() ? excecao.ToString() : "Ocorreu um erro inesperado ao processar a requisição.",
            StatusCodes.Status500InternalServerError,
            identificadorDaRequisicao)
    };

    private static ProblemDetails Criar(
        string titulo,
        string detalhe,
        int status,
        string identificadorDaRequisicao,
        string? codigo = null)
    {
        var problema = new ProblemDetails
        {
            Title = titulo,
            Detail = detalhe,
            Status = status,
            Extensions = { ["requisicaoId"] = identificadorDaRequisicao }
        };

        if (codigo is not null)
        {
            problema.Extensions["codigo"] = codigo;
        }

        return problema;
    }
}

/// <summary>Códigos de status usados que não existem em <see cref="StatusCodes"/>.</summary>
internal static class StatusCodesExtras
{
    /// <summary>499 — cliente fechou a conexão antes da resposta (convenção do nginx).</summary>
    public const int ClientClosedRequest = 499;
}
