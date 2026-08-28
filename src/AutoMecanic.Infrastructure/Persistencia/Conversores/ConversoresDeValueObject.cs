using AutoMecanic.Domain.Clientes.ValueObjects;
using AutoMecanic.Domain.OrdensServico.ValueObjects;
using AutoMecanic.Domain.SharedKernel;
using AutoMecanic.Domain.Veiculos.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AutoMecanic.Infrastructure.Persistencia.Conversores;

/// <summary>
/// Conversores que traduzem Objetos de Valor de coluna única para tipos primitivos do banco.
/// <para>
/// A opção por conversor (e não por tipo <i>owned</i>) é deliberada para os VOs cujo estado
/// cabe em um único campo: mantém o esquema simples, permite indexar e comparar diretamente
/// (<c>WHERE documento = '...'</c>) e reconstrói o objeto pela fábrica do domínio, de modo
/// que <b>nenhum valor inválido entra na memória</b>, nem vindo do banco.
/// </para>
/// </summary>
internal static class ConversoresDeValueObject
{
    /// <summary>CPF/CNPJ ⇄ <c>varchar(14)</c>.</summary>
    public static readonly ValueConverter<Documento, string> Documento =
        new(vo => vo.Numero, valor => Domain.Clientes.ValueObjects.Documento.Criar(valor));

    /// <summary>E-mail ⇄ <c>varchar(254)</c>.</summary>
    public static readonly ValueConverter<Email, string> Email =
        new(vo => vo.Endereco, valor => Domain.Clientes.ValueObjects.Email.Criar(valor));

    /// <summary>Telefone ⇄ <c>varchar(11)</c>.</summary>
    public static readonly ValueConverter<Telefone, string> Telefone =
        new(vo => vo.Numero, valor => Domain.Clientes.ValueObjects.Telefone.Criar(valor));

    /// <summary>Placa ⇄ <c>varchar(7)</c>.</summary>
    public static readonly ValueConverter<Placa, string> Placa =
        new(vo => vo.Valor, valor => Domain.Veiculos.ValueObjects.Placa.Criar(valor));

    /// <summary>Número da OS ⇄ <c>varchar(14)</c>.</summary>
    public static readonly ValueConverter<NumeroOrdemServico, string> NumeroOrdemServico =
        new(vo => vo.Valor, valor => Domain.OrdensServico.ValueObjects.NumeroOrdemServico.Analisar(valor));

    /// <summary>
    /// Dinheiro ⇄ <c>numeric(14,2)</c>. O tipo <c>numeric</c> do PostgreSQL é decimal exato,
    /// sem o erro de representação de ponto flutuante — obrigatório para valores financeiros.
    /// </summary>
    public static readonly ValueConverter<Dinheiro, decimal> Dinheiro =
        new(vo => vo.Valor, valor => Domain.SharedKernel.Dinheiro.De(valor));
}
