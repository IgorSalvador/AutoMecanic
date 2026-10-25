# Arquitetura e decisões técnicas

> **O que é.** Como o código está organizado, por que cada decisão foi tomada e o que ela
> custa. Toda escolha aqui tem uma alternativa razoável que foi descartada — e o motivo do
> descarte está registrado.

## Arquitetura em camadas

O requisito pede um **monolito em arquitetura de camadas**. A regra de dependência é única e
inegociável: **as setas apontam para dentro**.

```mermaid
flowchart TB
    subgraph EXT["🌐 Mundo externo"]
        CLI["Cliente HTTP<br/><i>navegador, app, Swagger</i>"]
        DB[("PostgreSQL 17")]
    end

    subgraph API["📡 AutoMecanic.Api — Apresentação"]
        CTRL["Controladores REST"]
        MW["Middlewares<br/><i>exceções, cabeçalhos</i>"]
        FLT["Filtro de validação"]
        AUTH["JWT · políticas · limite de taxa"]
    end

    subgraph APP["⚙️ AutoMecanic.Application — Casos de uso"]
        SVC["Casos de uso e serviços de aplicação<br/><i>orquestração</i>"]
        DTO["DTOs de entrada e saída"]
        VAL["Validadores"]
        PORT["<b>Portas</b><br/><i>interfaces de repositório,<br/>UoW, hash, token, relógio</i>"]
        HAND["Manipuladores de evento"]
    end

    subgraph DOM["💎 AutoMecanic.Domain — Regras de negócio"]
        AGG["Agregados"]
        VO["Objetos de Valor"]
        EVT["Eventos de Domínio"]
        ABS["Blocos de construção"]
    end

    subgraph INFRA["🔌 AutoMecanic.Infrastructure — Detalhes técnicos"]
        EF["EF Core · DbContext · mapeamentos"]
        REPO["Repositórios"]
        UOW["Unidade de Trabalho"]
        SEG["BCrypt · JWT"]
    end

    CLI --> CTRL
    CTRL --> SVC
    MW -.-> CTRL
    FLT -.-> CTRL
    AUTH -.-> CTRL

    SVC --> AGG
    SVC --> PORT
    HAND --> AGG
    VAL --> DTO

    REPO -.->|implementa| PORT
    UOW -.->|implementa| PORT
    SEG -.->|implementa| PORT
    EF --> AGG
    EF --> DB

    API --> APP --> DOM
    INFRA --> APP

    classDef dominio fill:#FFE0B2,stroke:#E65100,stroke-width:3px,color:#111
    classDef aplicacao fill:#E1F5FE,stroke:#0277BD,color:#111
    classDef infra fill:#F3E5F5,stroke:#6A1B9A,color:#111
    classDef apresentacao fill:#E8F5E9,stroke:#2E7D32,color:#111
    classDef externo fill:#EEEEEE,stroke:#757575,color:#111
    class AGG,VO,EVT,ABS dominio
    class SVC,DTO,VAL,PORT,HAND aplicacao
    class EF,REPO,UOW,SEG infra
    class CTRL,MW,FLT,AUTH apresentacao
    class CLI,DB externo
```

### A regra de dependência, verificada

| Projeto | Referencia | Pacotes NuGet |
|---|---|---|
| `AutoMecanic.Domain` | **nada** | **nenhum** |
| `AutoMecanic.Application` | Domain | FluentValidation, abstrações de DI e log |
| `AutoMecanic.Infrastructure` | Application | EF Core, Npgsql, BCrypt, JWT |
| `AutoMecanic.Api` | Infrastructure | ASP.NET Core, Swashbuckle, Serilog |

O `.csproj` do domínio carrega um comentário explicando o vazio:

```xml
<!--
  A camada de Domínio é deliberadamente livre de dependências de infraestrutura.
  Nenhum PackageReference é adicionado aqui: o modelo de domínio é POCO puro.
-->
```

**Por que isso importa na prática:** as 439 verificações unitárias do domínio rodam em ~3
segundos, sem banco, sem contêiner e sem configuração. Uma regra de negócio testável em
milissegundos é uma regra que a equipe realmente testa.

### A inversão que sustenta o desenho

A Infraestrutura depende da Aplicação, e não o contrário. As **portas** (interfaces) são
declaradas onde são consumidas; as implementações ficam do lado de fora:

```csharp
// Application/Abstractions/Repositorios.cs — a porta, declarada onde é usada
public interface IRepositorioDePecas : IRepositorio<Peca>
{
    Task<Peca?> ObterPorCodigoAsync(string codigo, CancellationToken ct = default);
    Task<IReadOnlyList<Peca>> ListarAbaixoDoEstoqueMinimoAsync(CancellationToken ct = default);
}

// Infrastructure/.../RepositorioDePecas.cs — o adaptador, do lado de fora
public sealed class RepositorioDePecas(AutoMecanicDbContext contexto)
    : RepositorioBase<Peca>(contexto), IRepositorioDePecas { … }
```

Trocar PostgreSQL por outro banco significa reescrever um projeto. Domínio e Aplicação não
mudam uma linha.

---

## Decisões de arquitetura

### ADR-01 · PostgreSQL como banco de dados

**Contexto.** O requisito deixa a escolha livre, mas exige justificativa.

**Decisão.** PostgreSQL 17.

**Por quê.**

| Critério | PostgreSQL | SQL Server |
|---|---|---|
| Licença | Livre, sem custo em produção | Developer grátis; Standard licenciado por núcleo |
| Imagem Docker | `postgres:17-alpine`, ~90 MB | ~1,5 GB, mais lenta para subir |
| Tipo monetário | `numeric` — decimal exato | `decimal` — equivalente |
| Concorrência otimista | **`xmin` nativo**, sem coluna extra | Exige coluna `rowversion` |
| Busca sem acento/caixa | `ILIKE` nativo | Depende de *collation* |
| Alocação atômica | `INSERT … ON CONFLICT … RETURNING` | Exige `MERGE` ou `sp_getapplock` |
| Suporte no EF Core | Npgsql, provedor maduro e ativo | Provedor oficial |

Os desempatadores concretos foram três: **`xmin`**, que dá controle de concorrência sem
poluir o modelo com uma coluna de versão; **`ON CONFLICT … RETURNING`**, que resolve a
numeração sequencial de OS em um único comando atômico; e o **tamanho da imagem**, que torna
`docker compose up` viável em qualquer máquina de avaliação.

**Custo.** Sem integração nativa com o ecossistema Microsoft (SSIS, Power BI direto). Nenhum
deles está no escopo do MVP.

---

### ADR-02 · Um caso de uso por classe, sem mediador

> **Revisado na Fase 2.** A decisão original — um serviço de aplicação por contexto — está
> preservada abaixo, porque explica por que o mediador continua de fora.

**Contexto.** A alternativa popular seria MediatR com um *handler* por comando.

**Decisão original (Fase 1).** Um serviço de aplicação por contexto, com um método por caso de uso.

**Por quê.** O requisito pede *arquitetura em camadas*, não CQRS. Com este volume de casos de
uso, o mediador acrescentaria uma classe e um arquivo por operação sem resolver nenhum
problema real — a única motivação seria a moda. O ganho de um pipeline de *behaviors* já é
obtido, mais simplesmente, com um filtro de ação para validação e um middleware para exceções.

**Custo.** Serviços maiores. `ServicoDeOrdensServico` chegou a ~665 linhas e 22 operações.

**Revisão (Fase 2).** O contexto central, Ordem de Serviço, passou a ter **uma classe por caso
de uso** (`Application/OrdensServico/CasosDeUso`), agrupadas em pastas por etapa do fluxo
(abertura, diagnóstico, composição, orçamento, execução, consultas). Cada classe expõe uma
operação (`ExecutarAsync`) e recebe só o que usa; o que várias compartilham — carregar e
persistir a OS, reservar e devolver peças, abrir a OS — fica em três colaboradores do
namespace `Apoio`. O controlador recebe o caso de uso por `[FromServices]` na própria ação.

Continua **sem mediador**: a chamada é direta, o tipo do caso de uso aparece na assinatura e o
"vá para a definição" leva ao código que executa. Os testes de arquitetura garantem a forma —
casos de uso selados, sem dependência entre si, com poucos métodos.

Os demais contextos (clientes, veículos, catálogo, estoque, identidade) mantêm serviços de
aplicação: são coesos, com menos de 230 linhas cada, e dividi-los agora seria cerimônia.

---

### ADR-03 · Objetos de valor por conversor, não por tipo *owned*

**Contexto.** VOs de campo único (`Documento`, `Placa`, `Email`) podem ser mapeados como tipo
*owned* ou por `ValueConverter`.

**Decisão.** `ValueConverter` para os de campo único; tipo *owned* apenas para `Endereco`.

**Por quê.** O conversor mantém o esquema plano e indexável — `documento` é uma coluna
`varchar(14)` com índice único, e não uma tabela à parte. A reconstrução passa pela fábrica do
domínio, o que garante que **nenhum valor inválido entra na memória, nem vindo do banco**.

**Custo — e ele é real.** Um tipo convertido é opaco para o tradutor de consultas: não é
possível escrever `ILIKE` sobre ele. A busca por documento e placa é, portanto, por
**igualdade do próprio Objeto de Valor**:

```csharp
// Infrastructure/.../RepositorioDeClientes.cs
return (achouDocumento, achouEmail) switch
{
    (true, _) => consulta.Where(c => EF.Functions.ILike(c.Nome, termo) || c.Documento == documento),
    (_, true) => consulta.Where(c => EF.Functions.ILike(c.Nome, termo) || c.Email == email),
    _         => consulta.Where(c => EF.Functions.ILike(c.Nome, termo))
};
```

Na prática o comportamento é o esperado: busca-se um CPF inteiro, não um pedaço dele. Se a
busca parcial por documento virar requisito, a saída é uma coluna auxiliar normalizada.

> Esta limitação foi descoberta por um teste ponta a ponta, não por leitura de código: a
> primeira versão usava `EF.Property<string>(c, "documento")` e falhava em tempo de execução.

---

### ADR-04 · Eventos de domínio despachados **antes** do commit

**Contexto.** Os eventos podem ser despachados antes ou depois de `SaveChanges`.

**Decisão.** Antes, em laço, até não haver mais eventos pendentes.

**Por quê.** É o que torna atômico o par saldo + lançamento no razão. O manipulador cria a
entidade `MovimentoEstoque`, ela entra no mesmo `SaveChanges` e, portanto, na mesma transação.
Se um falhar, o outro é desfeito. **Não existe saldo sem lançamento correspondente.**

```csharp
// Infrastructure/Persistencia/UnitOfWork.cs
public async Task<int> SalvarAlteracoesAsync(CancellationToken ct = default)
{
    await DespacharEventosPendentesAsync(ct);   // manipuladores criam entidades…
    return await contexto.SaveChangesAsync(ct); // …que entram nesta mesma gravação
}
```

**Custo.** Manipuladores lentos ou que chamem serviços externos seguram a transação. Por isso
os manipuladores só gravam dados no próprio banco — o do razão de estoque e, na Fase 2, o que
grava a mensagem de e-mail na caixa de saída (ADR-10) — e o de alerta apenas registra em log.
O envio do e-mail em si **não** acontece aqui: ele sai depois do commit, por outro processo.

Um laço com limite de 10 rodadas trata a cascata (um evento gerando outro) e impede que um
ciclo entre manipuladores trave a requisição.

---

### ADR-05 · Concorrência otimista com `xmin`

**Contexto.** Dois atendentes podem editar a mesma OS ao mesmo tempo.

**Decisão.** Token de concorrência mapeado para a coluna de sistema `xmin` do PostgreSQL.

**Por quê.** Sem isso, a última gravação sobrescreve silenciosamente a primeira — e ninguém
percebe. Com isso, a segunda falha com `DbUpdateConcurrencyException`, traduzida para
**HTTP 409** com uma instrução clara ao cliente.

```csharp
builder.Property(a => a.Versao)
    .HasColumnName("xmin").HasColumnType("xid")
    .ValueGeneratedOnAddOrUpdate()
    .IsConcurrencyToken();
```

O PostgreSQL já mantém `xmin` em toda linha: o controle sai **de graça**, sem coluna extra e
sem código de aplicação.

---

### ADR-06 · Modelo de reserva de estoque

**Contexto.** O saldo poderia ser baixado na inclusão da peça ou apenas na aprovação.

**Decisão.** Nem um nem outro: **reservar** na inclusão, **consumir** na aprovação.

**Por quê.** Baixar na inclusão faria o estoque sumir por causa de um orçamento que talvez
seja reprovado. Baixar só na aprovação permitiria que duas OS prometessem a mesma última peça
ao cliente — exatamente o problema de "falhas no controle de peças" descrito no enunciado.

A reserva resolve os dois: o saldo físico permanece intacto até a decisão, mas deixa de ser
prometível a outra OS.

**Custo.** Um estado a mais para reconciliar. Reservas de orçamentos abandonados prenderiam
estoque indefinidamente — daí a validade do orçamento e a rotina de expiração.

---

### ADR-07 · Migração automática na inicialização

> **Revisado na Fase 2.** Em Kubernetes a migração passou a ser um Job (ADR-11). A decisão
> abaixo continua valendo para o `docker compose`, que tem uma única instância.

**Contexto.** As migrações podem ser aplicadas pela aplicação ou por um passo de implantação.

**Decisão.** Pela aplicação, controlado por configuração (`BancoDeDados:MigrarNaInicializacao`).

**Por quê.** `docker compose up -d --build` precisa entregar um sistema pronto para uso, sem
passo manual. É o que o requisito de "execução local simples" pede, e a API é a única
escritora do esquema neste MVP.

**Custo — e ele está documentado no código.** Com múltiplas réplicas, duas instâncias tentariam
migrar simultaneamente. Em produção real, este passo migraria para um *job* de implantação
dedicado. O sinalizador de configuração existe precisamente para permitir essa mudança sem
alterar código.

---

### ADR-08 · Testes de integração contra PostgreSQL real

**Contexto.** A alternativa seria o provedor em memória do EF Core.

**Decisão.** Testcontainers, com um PostgreSQL descartável por execução.

**Por quê.** Boa parte do que precisa ser testado **não existe** fora do PostgreSQL:
conversores de VO, restrições `CHECK`, índices únicos, `xmin`, tradução de `ILIKE`,
`ON CONFLICT … RETURNING`. Um teste que passa em memória e falha em produção não é um teste.

A decisão se pagou: **dois defeitos reais** foram encontrados por esses testes — a consulta
por Objeto de Valor não traduzível (ADR-03) e o bug de autorização do autoatendimento
descrito abaixo.

**Custo.** ~11 segundos para subir o contêiner. Aceitável; os testes unitários continuam
rodando em 3 segundos e são o laço rápido do dia a dia.

---

### ADR-09 · Autoatendimento em controlador separado

**Contexto.** `GET /usuarios/eu` e `POST /usuarios/eu/senha` estavam em `UsuariosController`,
cuja classe exige a política `Administrar`, com um `[Authorize(Policy = Consultar)]` na ação.

**Problema encontrado.** O ASP.NET Core **combina** os atributos `[Authorize]` de classe e
ação exigindo que **ambas** as políticas passem. A política permissiva da ação nunca era
alcançada: qualquer perfil que não fosse Administrador recebia **403** ao tentar trocar a
própria senha.

**Decisão.** As duas ações passaram para `MeuPerfilController`, com política própria.

**Como foi descoberto.** Por um teste de integração que autentica como atendente e tenta trocar
a senha — não por leitura de código. É o tipo de defeito que só aparece quando o teste percorre
o pipeline de autorização de verdade.

---

### ADR-10 · E-mail ao cliente por caixa de saída (*outbox*) transacional

**Contexto.** A cada mudança de situação da OS o cliente recebe um e-mail. Enviar dentro da
requisição acopla a resposta ao servidor SMTP: se ele estiver lento ou fora, a oficina não
consegue abrir uma OS. Enviar depois do commit, direto do manipulador, perde a mensagem se o
processo cair entre o commit e o envio — ou envia um e-mail de algo que depois foi desfeito.

**Decisão.** O manipulador do evento de domínio grava uma `MensagemDeSaida` na **mesma
transação** da mudança de status (aproveitando o despacho antes do commit do ADR-04). Um
`BackgroundService` lê a tabela e envia por SMTP, com `FOR UPDATE SKIP LOCKED`, novas
tentativas com intervalo crescente e limite de tentativas.

**Por quê.** O e-mail sai **se, e somente se,** a mudança foi confirmada. `SKIP LOCKED` deixa
várias réplicas dividirem a fila sem enviar a mesma mensagem duas vezes. A porta
`IEnviadorDeEmail` isola o MailKit: trocar a caixa de saída por envio direto mudaria só o
despachante, sem tocar nos casos de uso.

**Custo.** Uma tabela e um processo a mais, e a entrega é *pelo menos uma vez* — uma falha entre
o envio e a marcação da mensagem pode repetir o e-mail. É preferível a perdê-lo. O envio fica
desligado por padrão (`Notificacoes:Habilitadas`) e, quando ligado, a configuração SMTP é
validada na subida: um servidor mal configurado impede o início, em vez de acumular falhas.

---

### ADR-11 · Migração do banco como Job do Kubernetes

**Contexto.** O ADR-07 deixou registrado o limite: com várias réplicas sob o HPA, duas
instâncias tentariam alterar o esquema ao mesmo tempo.

**Decisão.** O binário ganhou dois modos de tarefa avulsa, no mesmo padrão do `--health-check`:
`--migrar` (migra, semeia e encerra) e `--expirar-orcamentos`. No Kubernetes o Deployment roda
com `BancoDeDados__MigrarNaInicializacao=false`; a migração é um **Job** executado antes de
cada atualização, e a expiração de orçamentos é um **CronJob**.

**Por quê.** Uma única imagem atende os três papéis, e o código de saída (0 ou 1) é o que o
orquestrador lê para tentar de novo. O CronJob executa uma vez por agendamento, sem um
temporizador dentro de cada réplica disputando as mesmas OS. No `docker compose` nada muda.

**Custo.** O template de um Job é imutável: o fluxo de deploy precisa apagar o Job anterior
antes de reaplicar os manifestos. A ordem importa — migrar, esperar a conclusão e só então
atualizar a API (o CD faz nessa sequência).

---

### ADR-12 · Manifestos em Kustomize, com um *base* e um overlay por ambiente

**Contexto.** O mesmo conjunto de recursos precisa rodar num cluster local (kind) e na AWS
(EKS), mudando só o que é do ambiente.

**Decisão.** `k8s/base` com tudo que é comum — Namespace, ConfigMap, Deployment, Service, HPA,
PodDisruptionBudget, Job e CronJob — e `k8s/overlays/local` (e, adiante, `aws`) com o que varia:
imagem, endereço do banco, SMTP, tipo do Service e o Secret.

**Por quê.** Kustomize vem embutido no `kubectl`, não exige instalar nada e evita o
*template* de texto do Helm: o que se aplica é YAML válido, que o `kubeconform` consegue
validar. Para um único serviço, a parametrização do Helm seria cerimônia.

O Deployment segue a linha de segurança da imagem: `runAsNonRoot` com UID numérico, sem
escalonamento de privilégio, todas as *capabilities* removidas, sistema de arquivos somente
leitura e perfil `seccomp` padrão. O HPA usa 70% de CPU e 80% de memória, entre 2 e 6 réplicas,
com janela de redução curta para a demonstração caber no vídeo.

**Custo — descoberto no teste.** O HPA calcula sobre os *requests*. Com 192 Mi de memória
pedida, o .NET retinha ~130 Mi por pod depois de um pico (67%) e o HPA nunca voltava às 2
réplicas, porque com 2 pods passaria dos 80%. O request foi para 256 Mi.

O Secret **não** é versionado: `base/secret.modelo.yaml` só documenta as chaves, e o valor
vem do overlay local (descartável) ou do pipeline de CD.

---

### ADR-13 · SMTP genérico, com Mailpit em desenvolvimento

**Contexto.** Era preciso um provedor de e-mail para a demonstração, sem custo e sem depender
de uma conta empresarial.

**Decisão.** Cliente SMTP genérico (MailKit), configurado por `Smtp__*`. Em produção, uma
conta Gmail com **senha de app** guardada num Secret; em desenvolvimento, o Mailpit, que recebe
tudo e não entrega nada.

**Por quê.** Qualquer provedor SMTP serve sem alterar código, e o Mailpit permite ver o e-mail
exato que o cliente receberia, sem risco de enviar mensagem de teste a alguém. As credenciais
nunca vão para o repositório.

**Custo.** O Gmail limita o volume de envio e não é um serviço transacional. Adequado para a
demonstração; um serviço como o Amazon SES seria a troca natural em produção, e como o envio
está atrás de `IEnviadorDeEmail` ela afetaria só o adaptador.

---

### ADR-14 · Aprovação do orçamento pelo cliente em rota simples, sem webhook

**Contexto.** O cliente precisa aprovar ou recusar o orçamento que recebeu por e-mail.

**Decisão.** Duas rotas anônimas, `POST /acompanhamento/orcamento/aprovar` e `/reprovar`,
identificadas pelo número da OS (que vai no e-mail) mais o CPF/CNPJ do titular. Não há *link*
mágico nem webhook.

**Por quê.** Reaproveita o desenho da consulta pública de acompanhamento: o documento é o segredo
e a rota tem limite de taxa próprio. Dados que não conferem devolvem a mesma resposta de uma OS
inexistente, para não permitir percorrer números de OS. Responder duas vezes é `422` e não
altera nada; a recusa devolve as reservas de peça ao estoque.

**Custo.** Quem souber o número e o CPF de alguém pode responder por ele. É o mesmo nível de
confiança da consulta de acompanhamento e é o que o enunciado pede; um token assinado, de uso
único e com validade, seria o próximo passo se o risco crescesse.

---

### ADR-15 · Nuvem: EKS, RDS e ECR, com Terraform em camadas *(decidido; em implementação)*

**Contexto.** O ambiente de entrega é a AWS, com créditos limitados.

**Decisão.** EKS para o cluster, RDS PostgreSQL 17 (`db.t4g.micro`, subnet privada) para o
banco e ECR para as imagens. O Terraform fica em três camadas: `00-bootstrap` (estado remoto no
S3), `10-base` (rede, ECR, RDS, papel OIDC do CI) e `20-cluster` (EKS e nós), esta última
**criada e destruída sob demanda**. A autenticação do CI é por **OIDC**, sem chave de acesso de
longa duração, com um papel restrito à `main` deste repositório.

**Por quê.** O plano de controle do EKS e os nós respondem pela maior parte do custo (~US$ 0,25
por hora no total, ~185 por mês ligado direto), então separar a camada do cluster permite
destruí-la toda madrugada e manter o banco e a rede. Sem NAT Gateway (nós em subnet pública) se
economizam ~US$ 33 por mês. O OIDC elimina o segredo mais perigoso do pipeline: não há chave
da AWS para vazar.

**Custo.** Nós em subnet pública ficam mais expostos, o que se compensa com *security groups*
restritos. O cluster só existe enquanto alguém usa, então a demonstração exige criá-lo (~15
minutos) antes de mostrar qualquer coisa.

---

## Fluxo de uma requisição

```mermaid
sequenceDiagram
    participant C as Cliente HTTP
    participant MW as Middlewares
    participant A as Autenticação/Autorização
    participant F as Filtro de validação
    participant CT as Controlador
    participant S as Serviço de aplicação
    participant D as Agregado
    participant R as Repositório
    participant DB as PostgreSQL

    C->>MW: POST /api/v1/ordens-servico/{id}/pecas
    MW->>MW: cabeçalhos de segurança
    MW->>A: valida JWT e política
    A->>F: valida o contrato de entrada
    F->>CT: ação do controlador
    CT->>S: AdicionarPecaAsync(id, requisição)

    rect rgb(240, 248, 255)
    Note over S,DB: Transação explícita
    S->>R: carrega OS e Peça
    R->>DB: SELECT
    S->>D: peca.Reservar(4, osId)
    Note over D: invariante: qtd ≤ disponível
    S->>D: ordem.AdicionarPeca(...)
    Note over D: invariante: itens alteráveis?
    S->>R: SalvarAlteracoes
    Note over R: despacha eventos → razão de estoque
    R->>DB: INSERT/UPDATE + COMMIT
    end

    S-->>CT: OrdemServicoResponse
    CT-->>C: 200 OK

    Note over MW: Se qualquer etapa lançar,<br/>o middleware converte em problem+json
```

### Tradução de erros

| Exceção | HTTP | Significado |
|---|---|---|
| `ValidacaoException` | **400** | Contrato de entrada malformado |
| `NaoAutorizadoException` | **401** | Credenciais inválidas ou conta bloqueada |
| *(política de autorização)* | **403** | Autenticado, mas sem permissão |
| `RecursoNaoEncontradoException` | **404** | Recurso inexistente |
| `ConflitoException` | **409** | Chave natural duplicada |
| `DbUpdateConcurrencyException` | **409** | Alteração concorrente |
| `DomainException` | **422** | **Requisição correta, operação não permitida no estado atual** |
| *(qualquer outra)* | **500** | Mensagem genérica; detalhe só no log do servidor |

A distinção entre **400** e **422** é deliberada: "seu JSON está errado" e "seu JSON está
certo, mas você não pode entregar um veículo que ainda não foi finalizado" são problemas
diferentes e exigem reações diferentes do cliente da API.

---

## Estrutura de pastas

```
AutoMecanic/
├── src/
│   ├── AutoMecanic.Domain/            💎 sem dependências
│   │   ├── Abstractions/                 Entity, AggregateRoot, ValueObject, DomainEvent
│   │   ├── SharedKernel/                 Dinheiro
│   │   ├── Clientes/  Veiculos/          contexto Clientes e Veículos
│   │   ├── Servicos/                     contexto Catálogo
│   │   ├── Estoque/                      contexto Estoque
│   │   ├── OrdensServico/                contexto Ordem de Serviço (núcleo)
│   │   └── Identidade/                   contexto Autenticação
│   ├── AutoMecanic.Application/       ⚙️ casos de uso e portas
│   │   ├── Abstractions/                 interfaces de repositório, UoW, serviços
│   │   ├── Common/                       paginação, exceções
│   │   ├── Validacao/                    validadores FluentValidation
│   │   ├── OrdensServico/CasosDeUso/     um caso de uso por classe, por etapa do fluxo
│   │   └── <contexto>/                   DTOs + serviço de aplicação
│   ├── AutoMecanic.Infrastructure/    🔌 EF Core, PostgreSQL, BCrypt, JWT
│   │   ├── Persistencia/
│   │   │   ├── Configuracoes/            mapeamentos por agregado
│   │   │   ├── Conversores/              VO ⇄ coluna
│   │   │   ├── Repositorios/
│   │   │   ├── Migracoes/                geradas pelo EF Core
│   │   │   └── Seed/
│   │   ├── Notificacoes/                 caixa de saída, SMTP (MailKit), worker
│   │   ├── Seguranca/                    BCrypt, JWT
│   │   └── Servicos/                     relógio
│   └── AutoMecanic.Api/               📡 controladores, middlewares, Swagger
├── tests/
│   ├── AutoMecanic.UnitTests/            475 verificações, ~3 s, sem I/O
│   ├── AutoMecanic.ArchitectureTests/    22 verificações das regras de dependência
│   └── AutoMecanic.IntegrationTests/     101 verificações, API + PostgreSQL reais
├── k8s/                                  manifestos Kustomize (base e overlays)
├── docs/                                 esta documentação
├── Dockerfile · docker-compose.yml
└── Directory.Build.props · Directory.Packages.props
```

---

## Qualidade

| Prática | Como está implementada |
|---|---|
| Gestão central de pacotes | `Directory.Packages.props` — uma versão por pacote em toda a solução |
| Convenções compartilhadas | `Directory.Build.props` + `.editorconfig` |
| Nulabilidade | `<Nullable>enable</Nullable>` com `WarningsAsErrors` em nulos |
| Documentação de API | XML docs obrigatórios, incorporados ao Swagger |
| Cobertura | 92,2% de linhas · 94,5% de métodos · todas as camadas acima de 80% |
| Testes de integração | PostgreSQL real via Testcontainers |
| Log estruturado | Serilog, correlação por requisição |
| Verificação de saúde | `/health/vivo` (processo) e `/health/pronto` (banco) |

---

**Próximo:** [Relatório de análise de vulnerabilidades](06-relatorio-de-seguranca.md).
