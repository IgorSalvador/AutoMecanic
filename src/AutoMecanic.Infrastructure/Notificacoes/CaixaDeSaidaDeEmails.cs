using AutoMecanic.Application.Abstractions;
using AutoMecanic.Infrastructure.Persistencia;

namespace AutoMecanic.Infrastructure.Notificacoes;

/// <summary>
/// Registra o e-mail no mesmo <see cref="AutoMecanicDbContext"/> da operação: como os
/// manipuladores de eventos rodam antes do <c>SaveChanges</c>, a mensagem é gravada na mesma
/// transação da mudança que a originou.
/// </summary>
public sealed class CaixaDeSaidaDeEmails(AutoMecanicDbContext contexto, TimeProvider relogio) : ICaixaDeSaidaDeEmails
{
    public async Task EnfileirarAsync(EmailDeSaida email, CancellationToken cancellationToken = default) =>
        await contexto.MensagensDeSaida.AddAsync(MensagemDeSaida.De(email, relogio.GetUtcNow()), cancellationToken);
}
