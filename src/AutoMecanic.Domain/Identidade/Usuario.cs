using AutoMecanic.Domain.Abstractions;
using AutoMecanic.Domain.Clientes.ValueObjects;
using AutoMecanic.Domain.Identidade.Events;

namespace AutoMecanic.Domain.Identidade;

/// <summary>
/// Perfil de acesso do usuário administrativo. Define o que cada pessoa pode fazer nas
/// APIs protegidas por JWT.
/// </summary>
public enum PerfilUsuario
{
    /// <summary>Acesso total, incluindo gestão de usuários e catálogos.</summary>
    Administrador = 1,

    /// <summary>Recepção: abre OS, cadastra clientes e veículos, envia orçamentos.</summary>
    Atendente = 2,

    /// <summary>Oficina: registra diagnóstico, executa e finaliza serviços.</summary>
    Mecanico = 3,

    /// <summary>Almoxarifado: gerencia peças, insumos e movimentações de estoque.</summary>
    Estoquista = 4
}

/// <summary>
/// <b>Raiz de Agregado</b> do contexto de Autenticação &amp; Acesso.
/// <para>
/// Guarda apenas o <b>hash</b> da senha — a senha em claro nunca entra no domínio, nunca é
/// persistida e nunca é registrada em log. O cálculo do hash é responsabilidade da
/// infraestrutura (BCrypt), injetada como função para manter o domínio livre de dependências.
/// </para>
/// <para>
/// O bloqueio por tentativas malsucedidas é regra de negócio, não detalhe técnico: após
/// <see cref="MaximoTentativasFalhas"/> erros consecutivos a conta é bloqueada
/// temporariamente, mitigando ataques de força bruta.
/// </para>
/// </summary>
public sealed class Usuario : AggregateRoot
{
    /// <summary>Tentativas consecutivas de login malsucedidas toleradas antes do bloqueio.</summary>
    public const int MaximoTentativasFalhas = 5;

    /// <summary>Duração do bloqueio temporário após exceder as tentativas.</summary>
    public static readonly TimeSpan DuracaoDoBloqueio = TimeSpan.FromMinutes(15);

    private Usuario()
    {
        Nome = null!;
        Email = null!;
        SenhaHash = null!;
    }

    private Usuario(Guid id, string nome, Email email, string senhaHash, PerfilUsuario perfil)
        : base(id)
    {
        Nome = nome;
        Email = email;
        SenhaHash = senhaHash;
        Perfil = perfil;
        Ativo = true;
        CadastradoEm = DateTimeOffset.UtcNow;
    }

    public string Nome { get; private set; }

    /// <summary>E-mail de login. Chave natural única do usuário.</summary>
    public Email Email { get; private set; }

    /// <summary>Hash BCrypt da senha. Nunca exposto em DTO nem serializado em resposta.</summary>
    public string SenhaHash { get; private set; }

    public PerfilUsuario Perfil { get; private set; }

    public bool Ativo { get; private set; }

    public int TentativasFalhas { get; private set; }

    /// <summary>Preenchido enquanto a conta estiver bloqueada por excesso de tentativas.</summary>
    public DateTimeOffset? BloqueadoAte { get; private set; }

    public DateTimeOffset? UltimoAcessoEm { get; private set; }

    public DateTimeOffset CadastradoEm { get; private set; }

    public DateTimeOffset? AtualizadoEm { get; private set; }

    /// <summary>
    /// Cria um usuário administrativo.
    /// </summary>
    /// <param name="nome">Nome completo do usuário.</param>
    /// <param name="email">E-mail de login, único no sistema.</param>
    /// <param name="senha">Senha em claro. É consumida apenas para gerar o hash e descartada.</param>
    /// <param name="perfil">Perfil de acesso que define as permissões do usuário.</param>
    /// <param name="gerarHash">Função de hash fornecida pela infraestrutura (BCrypt).</param>
    public static Usuario Criar(
        string? nome,
        string? email,
        string? senha,
        PerfilUsuario perfil,
        Func<string, string> gerarHash)
    {
        ArgumentNullException.ThrowIfNull(gerarHash);

        var usuario = new Usuario(
            NovoId(),
            ValidarNome(nome),
            Clientes.ValueObjects.Email.Criar(email),
            gerarHash(ValidarPolitcaDeSenha(senha)),
            perfil);

        usuario.RegistrarEvento(new UsuarioCriado(usuario.Id, usuario.Email.Endereco, perfil));

        return usuario;
    }

    /// <summary>
    /// Valida a senha informada no login e atualiza o estado de bloqueio.
    /// </summary>
    /// <param name="senha">Senha em claro fornecida na tentativa de login.</param>
    /// <param name="verificarHash">Função de verificação fornecida pela infraestrutura.</param>
    /// <param name="agora">Instante da tentativa. Injetado para tornar o comportamento testável.</param>
    /// <returns><see langword="true"/> quando a autenticação é bem-sucedida.</returns>
    public bool TentarAutenticar(string? senha, Func<string, string, bool> verificarHash, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(verificarHash);

        if (!Ativo)
        {
            throw new DomainException("USUARIO_INATIVO", "Usuário inativo. Procure o administrador do sistema.");
        }

        if (EstaBloqueado(agora))
        {
            throw new DomainException(
                "USUARIO_BLOQUEADO",
                $"Conta temporariamente bloqueada por excesso de tentativas. Tente novamente após {BloqueadoAte:HH:mm} (UTC).");
        }

        // O bloqueio anterior já expirou: zera o contador antes de avaliar esta tentativa.
        if (BloqueadoAte is not null)
        {
            BloqueadoAte = null;
            TentativasFalhas = 0;
        }

        if (string.IsNullOrEmpty(senha) || !verificarHash(senha, SenhaHash))
        {
            RegistrarTentativaFalha(agora);
            return false;
        }

        TentativasFalhas = 0;
        BloqueadoAte = null;
        UltimoAcessoEm = agora;
        AtualizadoEm = agora;

        RegistrarEvento(new UsuarioAutenticado(Id, Email.Endereco, agora));

        return true;
    }

    public bool EstaBloqueado(DateTimeOffset agora) => BloqueadoAte is not null && agora < BloqueadoAte;

    /// <summary>Troca a senha, exigindo a senha atual — protege contra sequestro de sessão.</summary>
    public void AlterarSenha(
        string? senhaAtual,
        string? novaSenha,
        Func<string, string, bool> verificarHash,
        Func<string, string> gerarHash)
    {
        ArgumentNullException.ThrowIfNull(verificarHash);
        ArgumentNullException.ThrowIfNull(gerarHash);

        if (string.IsNullOrEmpty(senhaAtual) || !verificarHash(senhaAtual, SenhaHash))
        {
            throw new DomainException("SENHA_ATUAL_INVALIDA", "A senha atual informada está incorreta.");
        }

        var validada = ValidarPolitcaDeSenha(novaSenha);

        if (verificarHash(validada, SenhaHash))
        {
            throw new DomainException("SENHA_REPETIDA", "A nova senha deve ser diferente da atual.");
        }

        SenhaHash = gerarHash(validada);
        AtualizadoEm = DateTimeOffset.UtcNow;

        RegistrarEvento(new SenhaAlterada(Id));
    }

    /// <summary>Redefinição feita por um administrador, sem exigir a senha anterior.</summary>
    public void RedefinirSenha(string? novaSenha, Func<string, string> gerarHash)
    {
        ArgumentNullException.ThrowIfNull(gerarHash);

        SenhaHash = gerarHash(ValidarPolitcaDeSenha(novaSenha));
        TentativasFalhas = 0;
        BloqueadoAte = null;
        AtualizadoEm = DateTimeOffset.UtcNow;

        RegistrarEvento(new SenhaAlterada(Id));
    }

    public void AlterarPerfil(PerfilUsuario novoPerfil)
    {
        if (Perfil == novoPerfil)
        {
            return;
        }

        Perfil = novoPerfil;
        AtualizadoEm = DateTimeOffset.UtcNow;
    }

    public void AtualizarNome(string? nome)
    {
        Nome = ValidarNome(nome);
        AtualizadoEm = DateTimeOffset.UtcNow;
    }

    public void Inativar()
    {
        if (!Ativo)
        {
            return;
        }

        Ativo = false;
        AtualizadoEm = DateTimeOffset.UtcNow;

        RegistrarEvento(new UsuarioInativado(Id, Email.Endereco));
    }

    public void Reativar()
    {
        if (Ativo)
        {
            return;
        }

        Ativo = true;
        TentativasFalhas = 0;
        BloqueadoAte = null;
        AtualizadoEm = DateTimeOffset.UtcNow;
    }

    /// <summary>Desbloqueio manual por um administrador.</summary>
    public void Desbloquear()
    {
        TentativasFalhas = 0;
        BloqueadoAte = null;
        AtualizadoEm = DateTimeOffset.UtcNow;
    }

    private void RegistrarTentativaFalha(DateTimeOffset agora)
    {
        TentativasFalhas++;
        AtualizadoEm = agora;

        if (TentativasFalhas >= MaximoTentativasFalhas)
        {
            BloqueadoAte = agora.Add(DuracaoDoBloqueio);
            RegistrarEvento(new UsuarioBloqueado(Id, Email.Endereco, BloqueadoAte.Value));
        }
    }

    private static string ValidarNome(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new DomainException("NOME_OBRIGATORIO", "O nome do usuário é obrigatório.");
        }

        var limpo = nome.Trim();

        if (limpo.Length is < 3 or > 150)
        {
            throw new DomainException("NOME_INVALIDO", "O nome do usuário deve ter entre 3 e 150 caracteres.");
        }

        return limpo;
    }

    /// <summary>
    /// Política mínima de senha: 8 caracteres, com maiúscula, minúscula, dígito e símbolo.
    /// A regra vive no domínio porque é uma decisão de negócio sobre risco, não de tecnologia.
    /// </summary>
    private static string ValidarPolitcaDeSenha(string? senha)
    {
        if (string.IsNullOrWhiteSpace(senha))
        {
            throw new DomainException("SENHA_OBRIGATORIA", "A senha é obrigatória.");
        }

        if (senha.Length < 8)
        {
            throw new DomainException("SENHA_FRACA", "A senha deve ter no mínimo 8 caracteres.");
        }

        if (senha.Length > 128)
        {
            throw new DomainException("SENHA_INVALIDA", "A senha excede 128 caracteres.");
        }

        var temMaiuscula = senha.Any(char.IsUpper);
        var temMinuscula = senha.Any(char.IsLower);
        var temDigito = senha.Any(char.IsDigit);
        var temSimbolo = senha.Any(c => !char.IsLetterOrDigit(c));

        if (!temMaiuscula || !temMinuscula || !temDigito || !temSimbolo)
        {
            throw new DomainException(
                "SENHA_FRACA",
                "A senha deve conter ao menos uma letra maiúscula, uma minúscula, um dígito e um caractere especial.");
        }

        return senha;
    }
}
