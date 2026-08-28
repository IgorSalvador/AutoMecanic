using AutoMecanic.Application.Common;
using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AutoMecanic.Api.Filtros;

/// <summary>
/// Executa automaticamente o validador FluentValidation correspondente a cada argumento da
/// ação, antes que o controlador seja invocado.
/// <para>
/// Com isso, nenhum controlador precisa lembrar de validar: a validação é garantida por
/// construção. Argumentos sem validador registrado passam direto — a regra vale para os
/// contratos que optamos por validar, sem impedir os demais.
/// </para>
/// </summary>
public sealed class FiltroDeValidacao(IServiceProvider provedor) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var erros = new Dictionary<string, List<string>>();

        foreach (var (_, argumento) in context.ActionArguments)
        {
            if (argumento is null)
            {
                continue;
            }

            var validador = provedor.GetService(typeof(IValidator<>).MakeGenericType(argumento.GetType()));

            if (validador is not IValidator instancia)
            {
                continue;
            }

            var contextoDeValidacao = new ValidationContext<object>(argumento);
            var resultado = await instancia.ValidateAsync(contextoDeValidacao, context.HttpContext.RequestAborted);

            if (resultado.IsValid)
            {
                continue;
            }

            foreach (var falha in resultado.Errors)
            {
                // Agrupa por campo para que o cliente receba todos os problemas de uma vez,
                // em vez de corrigir um erro por requisição.
                if (!erros.TryGetValue(falha.PropertyName, out var mensagens))
                {
                    mensagens = [];
                    erros[falha.PropertyName] = mensagens;
                }

                mensagens.Add(falha.ErrorMessage);
            }
        }

        if (erros.Count > 0)
        {
            throw new ValidacaoException(erros.ToDictionary(par => par.Key, par => par.Value.ToArray()));
        }

        await next();
    }
}
