# Modelo de Domínio

> **O que é.** Os agregados, entidades, objetos de valor e regras que compõem o modelo, e —
> mais importante — **por que cada fronteira está onde está**. Um agregado não é um grupo de
> tabelas: é uma fronteira de consistência, e a pergunta que a define é *"o que precisa mudar
> junto, na mesma transação?"*.

## Visão geral

```mermaid
classDiagram
    direction LR

    class Cliente {
        <<Raiz de Agregado>>
        +Guid Id
        +string Nome
        +Documento Documento
        +Email Email
        +Telefone Telefone
        +Endereco? Endereco
        +bool Ativo
        +Cadastrar()$
        +AtualizarCadastro()
        +Inativar()
        +GarantirClienteAtivo()
    }

    class Veiculo {
        <<Raiz de Agregado>>
        +Guid Id
        +Guid ClienteId
        +Placa Placa
        +string Marca
        +string Modelo
        +int Quilometragem
        +Cadastrar()$
        +RegistrarQuilometragem()
        +TransferirPara()
    }

    class Servico {
        <<Raiz de Agregado>>
        +Guid Id
        +string Nome
        +CategoriaServico Categoria
        +Dinheiro Preco
        +int TempoEstimadoEmMinutos
        +Cadastrar()$
        +ReajustarPreco()
    }

    class Peca {
        <<Raiz de Agregado>>
        +Guid Id
        +string Codigo
        +Dinheiro PrecoUnitario
        +int QuantidadeEmEstoque
        +int QuantidadeReservada
        +int QuantidadeDisponivel
        +int EstoqueMinimo
        +Reservar()
        +ConsumirReserva()
        +LiberarReserva()
        +AjustarSaldo()
    }

    class MovimentoEstoque {
        <<Raiz de Agregado>>
        +Guid Id
        +Guid PecaId
        +TipoMovimentoEstoque Tipo
        +int SaldoAnterior
        +int SaldoAtual
        +Registrar()$
    }

    class OrdemServico {
        <<Raiz de Agregado>>
        +Guid Id
        +NumeroOrdemServico Numero
        +Guid ClienteId
        +Guid VeiculoId
        +StatusOrdemServico Status
        +Abrir()$
        +IniciarDiagnostico()
        +AdicionarServico()
        +AdicionarPeca()
        +GerarOrcamento()
        +AprovarOrcamento()
        +FinalizarServico()
        +EntregarVeiculo()
    }

    class ItemServico {
        <<Entidade>>
        +Guid ServicoId
        +string Descricao
        +Dinheiro PrecoUnitario
        +int Quantidade
        +Dinheiro Subtotal
    }

    class ItemPeca {
        <<Entidade>>
        +Guid PecaId
        +string CodigoPeca
        +Dinheiro PrecoUnitario
        +int Quantidade
        +bool Reservada
        +bool Consumida
    }

    class Orcamento {
        <<Entidade>>
        +Dinheiro ValorServicos
        +Dinheiro ValorPecas
        +decimal PercentualDesconto
        +Dinheiro ValorTotal
        +StatusOrcamento Status
        +DateTimeOffset? ValidoAte
    }

    class HistoricoStatus {
        <<Entidade>>
        +StatusOrdemServico? StatusAnterior
        +StatusOrdemServico StatusAtual
        +DateTimeOffset OcorridoEm
    }

    class Usuario {
        <<Raiz de Agregado>>
        +Guid Id
        +Email Email
        +string SenhaHash
        +PerfilUsuario Perfil
        +int TentativasFalhas
        +TentarAutenticar()
        +AlterarSenha()
    }

    OrdemServico "1" *-- "0..*" ItemServico : contém
    OrdemServico "1" *-- "0..*" ItemPeca : contém
    OrdemServico "1" *-- "0..1" Orcamento : contém
    OrdemServico "1" *-- "1..*" HistoricoStatus : contém

    Veiculo ..> Cliente : ClienteId
    OrdemServico ..> Cliente : ClienteId
    OrdemServico ..> Veiculo : VeiculoId
    ItemServico ..> Servico : ServicoId (cópia)
    ItemPeca ..> Peca : PecaId (cópia)
    MovimentoEstoque ..> Peca : PecaId
```

> **Leitura das setas:** losango preenchido (`*--`) = **composição**, a entidade filha não
> existe fora do agregado. Linha tracejada (`..>`) = **referência por identidade**, atravessa
> fronteira de agregado e carrega apenas o `Guid`.

---

## Agregado: Ordem de Serviço 🎯

O agregado central. A fronteira responde à pergunta: *o que precisa ser verdadeiro no mesmo
instante?*

```mermaid
flowchart TB
    subgraph AGG["Fronteira do agregado Ordem de Serviço"]
        OS["<b>OrdemServico</b><br/>raiz de agregado"]
        IS["ItemServico<br/><i>0..n</i>"]
        IP["ItemPeca<br/><i>0..n</i>"]
        OR["Orcamento<br/><i>0..1</i>"]
        HS["HistoricoStatus<br/><i>1..n</i>"]
        OS --> IS
        OS --> IP
        OS --> OR
        OS --> HS
    end

    CLI(["Cliente<br/><i>outro agregado</i>"])
    VEI(["Veículo<br/><i>outro agregado</i>"])
    SER(["Serviço<br/><i>outro agregado</i>"])
    PEC(["Peça<br/><i>outro agregado</i>"])

    OS -. "ClienteId" .-> CLI
    OS -. "VeiculoId" .-> VEI
    IS -. "ServicoId + cópia do preço" .-> SER
    IP -. "PecaId + cópia do preço" .-> PEC

    classDef raiz fill:#FFD700,stroke:#B39700,stroke-width:3px,color:#111
    classDef filha fill:#FFF3C4,stroke:#B39700,color:#111
    classDef externo fill:#E0E0E0,stroke:#757575,color:#111
    class OS raiz
    class IS,IP,OR,HS filha
    class CLI,VEI,SER,PEC externo
```

### Por que estas quatro entidades estão dentro

| Entidade filha | Por que faz parte do agregado |
|---|---|
| `ItemServico` | O valor do orçamento é a soma dos itens. Alterar um item **tem que** recalcular o total no mesmo instante, ou o cliente vê um valor que não corresponde ao que vai pagar. |
| `ItemPeca` | Mesma razão, mais a situação de reserva/consumo, que precisa acompanhar as transições da OS. |
| `Orcamento` | Sua situação (*enviado*, *aprovado*) é o que determina se os itens podem ser alterados. Fora do agregado, essa regra viraria uma consulta que alguém esqueceria de fazer. |
| `HistoricoStatus` | Toda transição precisa deixar rastro. Registrar dentro do agregado torna impossível mudar o status sem gravar a linha do tempo. |

### O que ficou de fora, e por quê

| Conceito | Por que é agregado separado |
|---|---|
| `Cliente`, `Veículo` | Ciclo de vida independente. O veículo é transferido, consultado e alterado sem que exista uma OS. |
| `Serviço`, `Peça` | São catálogos globais. Carregá-los junto obrigaria a bloquear o catálogo inteiro para alterar uma OS. |
| `MovimentoEstoque` | A coleção cresce indefinidamente. Mantê-la dentro obrigaria a carregar todo o histórico para movimentar uma única peça. |

### Máquina de estados

```mermaid
stateDiagram-v2
    [*] --> Recebida : Abrir()<br/><i>recepção do veículo</i>

    Recebida --> EmDiagnostico : IniciarDiagnostico()
    Recebida --> Cancelada : Cancelar(motivo)

    EmDiagnostico --> AguardandoAprovacao : EnviarOrcamentoParaAprovacao()
    EmDiagnostico --> Cancelada : Cancelar(motivo)

    AguardandoAprovacao --> EmExecucao : AprovarOrcamento()
    AguardandoAprovacao --> Cancelada : ReprovarOrcamento(motivo)
    AguardandoAprovacao --> Cancelada : ExpirarOrcamento()
    AguardandoAprovacao --> EmDiagnostico : RetornarParaDiagnostico()
    AguardandoAprovacao --> Cancelada : Cancelar(motivo)

    EmExecucao --> Finalizada : FinalizarServico()

    Finalizada --> Entregue : EntregarVeiculo()

    Entregue --> [*]
    Cancelada --> [*]

    note right of AguardandoAprovacao
        A partir daqui os itens
        estão CONGELADOS
    end note

    note right of EmExecucao
        Peças baixadas do estoque.
        Cancelamento não é mais possível.
    end note
```

**A propriedade que a máquina garante:** não existe caminho para `Entregue` que não passe por
`Finalizada`, nem para `EmExecucao` que não passe por uma aprovação de orçamento. Não é
convenção — é o que o código recusa fazer:

```csharp
// Domain/OrdensServico/OrdemServico.cs
private void ExigirStatus(StatusOrdemServico destino, StatusOrdemServico[] origensPermitidas)
{
    GarantirNaoTerminal();

    if (!origensPermitidas.Contains(Status))
    {
        throw new DomainException("TRANSICAO_INVALIDA",
            $"Não é possível mover a Ordem de Serviço para '{destino.Descricao()}' " +
            $"a partir de '{Status.Descricao()}'.");
    }
}
```

### Invariantes

| # | Invariante | Onde é garantida |
|---|---|---|
| 1 | Toda OS tem cliente, veículo e relato do problema | `Abrir()` |
| 2 | O status só muda por transição válida | `ExigirStatus()` |
| 3 | Itens só mudam antes do envio do orçamento | `GarantirItensAlteraveis()` |
| 4 | O orçamento é sempre a **soma calculada** dos itens | `GerarOrcamento()` · `RecalcularOrcamentoSeExistir()` |
| 5 | A execução só começa com orçamento aprovado | `AprovarOrcamento()` |
| 6 | A entrega só ocorre após a finalização | `EntregarVeiculo()` |
| 7 | Estados terminais não admitem transições | `GarantirNaoTerminal()` |
| 8 | Peça consumida não pode ser removida nem ter quantidade alterada | `ItemPeca.AlterarQuantidade()` |
| 9 | Toda transição gera registro na linha do tempo | `AlterarStatus()` |
| 10 | O cancelamento só é permitido antes da execução | `Status.PermiteCancelamento()` |

---

## Agregado: Peça 📦

A fronteira de consistência do **saldo**.

```mermaid
flowchart LR
    subgraph SALDO["Decomposição do saldo"]
        direction TB
        FIS["<b>Quantidade em Estoque</b><br/>o que está na prateleira<br/><i>ex.: 20</i>"]
        RES["<b>Quantidade Reservada</b><br/>já prometido a orçamentos<br/><i>ex.: 4</i>"]
        DIS["<b>Quantidade Disponível</b><br/>= físico − reservado<br/><b>o que se pode prometer</b><br/><i>ex.: 16</i>"]
        FIS --> DIS
        RES --> DIS
    end

    MIN["<b>Estoque Mínimo</b><br/><i>ponto de ressuprimento</i>"]
    ALE["🔔 Alerta de compra"]
    DIS -->|"disponível ≤ mínimo"| MIN --> ALE

    classDef fisico fill:#BBDEFB,stroke:#1565C0,color:#111
    classDef reservado fill:#FFE0B2,stroke:#E65100,color:#111
    classDef disponivel fill:#C8E6C9,stroke:#2E7D32,stroke-width:3px,color:#111
    classDef alerta fill:#FFCDD2,stroke:#C62828,color:#111
    class FIS fisico
    class RES reservado
    class DIS disponivel
    class MIN,ALE alerta
```

### O ciclo de vida de uma reserva

```mermaid
stateDiagram-v2
    [*] --> Livre : peça no estoque

    Livre --> Reservada : Reservar(qtd, osId)<br/><i>ao incluir na OS</i>

    Reservada --> Consumida : ConsumirReserva()<br/><i>orçamento aprovado</i>
    Reservada --> Livre : LiberarReserva()<br/><i>reprovado, cancelado,<br/>expirado ou item removido</i>

    Consumida --> [*] : peça aplicada no veículo
```

### Invariantes

| # | Invariante | Consequência de violá-la |
|---|---|---|
| 1 | `QuantidadeEmEstoque ≥ 0` | Saldo negativo: estoque fictício |
| 2 | `QuantidadeReservada ≥ 0` | Reserva negativa: promessa inexistente |
| 3 | `QuantidadeReservada ≤ QuantidadeEmEstoque` | Prometer mais do que existe |
| 4 | Reserva ≤ disponível | Duas OS vendem a mesma peça |
| 5 | Consumo ≤ reservado | Baixar peça que ninguém separou |
| 6 | Ajuste não pode ficar abaixo do reservado | Promessas sem lastro físico |
| 7 | Perda não consome o reservado | Peça prometida some sem aviso |
| 8 | Peça com reserva pendente não é inativada | Orçamento apontando para item fora de linha |
| 9 | Código único; preço > 0 | Ambiguidade de SKU; item de graça no orçamento |

> As invariantes 1, 2, 3 e o preço positivo são declaradas **também no banco**, como
> restrições `CHECK`. Se um dia alguém alterar dados por SQL direto, a garantia continua valendo:
>
> ```sql
> ALTER TABLE automecanic.peca
>   ADD CONSTRAINT ck_peca_reserva_menor_que_saldo
>   CHECK (quantidade_reservada <= quantidade_em_estoque);
> ```

---

## Agregado: Movimento de Estoque 📒

Um agregado à parte, **imutável**, formando o razão (kardex) do almoxarifado.

**Por que não é filho de `Peca`:** a coleção cresce indefinidamente. Mantê-la dentro do
agregado obrigaria a carregar milhares de lançamentos para movimentar uma unidade.

**Como saldo e razão não divergem:** o lançamento não é criado por quem movimenta o estoque —
é criado por um manipulador do evento `EstoqueMovimentado` que roda **dentro da mesma
transação**:

```csharp
// Application/Estoque/Handlers/RegistrarMovimentoNoRazaoHandler.cs
public async Task TratarAsync(EstoqueMovimentado evento, CancellationToken ct)
{
    var movimento = MovimentoEstoque.Registrar(
        evento.PecaId, evento.Tipo, evento.Quantidade,
        evento.SaldoAnterior, evento.SaldoAtual,
        evento.Motivo, evento.OrdemServicoId, evento.OcorridoEm);

    await repositorio.AdicionarAsync(movimento, ct);
}
```

Se a gravação do razão falhar, a alteração de saldo é desfeita junto. **Não existe saldo sem
lançamento correspondente.**

---

## Agregados: Cliente e Veículo 👤🚗

Dois agregados no mesmo contexto, referenciando-se por identidade.

**Por que o veículo não é entidade filha do cliente:**

1. Tem ciclo de vida próprio — pode ser **transferido** e continuar o mesmo veículo;
2. É consultado e alterado sem carregar o cliente (recepção busca por placa);
3. É a ele que a Ordem de Serviço se refere;
4. Um cliente com muitos veículos forçaria carregar todos para alterar um.

### Invariantes

| Agregado | Invariante |
|---|---|
| Cliente | Documento válido (dígito verificador) e único; nome de 3 a 150 caracteres; **documento imutável** |
| Cliente | Cliente inativo não recebe novas OS nem atualização cadastral |
| Veículo | Placa válida nos dois padrões e única; **placa imutável** |
| Veículo | Ano de fabricação plausível; ano-modelo = fabricação ou fabricação + 1 |
| Veículo | **Quilometragem monotônica** — nunca retrocede |
| Veículo | Veículo inativo não recebe novas OS |

---

## Agregado: Usuário 🔐

Concentra as regras de segurança que o requisito exige.

```mermaid
stateDiagram-v2
    [*] --> Ativo : Criar()

    Ativo --> Ativo : login bem-sucedido<br/><i>zera o contador</i>
    Ativo --> ComTentativas : login malsucedido<br/><i>contador++</i>
    ComTentativas --> Ativo : login bem-sucedido
    ComTentativas --> Bloqueado : 5ª tentativa malsucedida<br/><i>bloqueia por 15 min</i>
    Bloqueado --> Ativo : prazo expira<br/><b>ou</b> Desbloquear()

    Ativo --> Inativo : Inativar()
    Inativo --> Ativo : Reativar()

    note right of Bloqueado
        Enquanto bloqueado, nem a
        senha correta é aceita
    end note
```

**A senha em claro nunca entra no domínio.** O agregado recebe uma *função* de hash injetada
pela infraestrutura, o que mantém o domínio livre de dependência de biblioteca criptográfica:

```csharp
public static Usuario Criar(string? nome, string? email, string? senha,
                            PerfilUsuario perfil, Func<string, string> gerarHash)
{
    return new Usuario(NovoId(), ValidarNome(nome), Email.Criar(email),
                       gerarHash(ValidarPolitcaDeSenha(senha)), perfil);
}
```

---

## Objetos de Valor

Cada objeto de valor existe para **tornar um estado inválido impossível de representar**.

### `Documento` — CPF ou CNPJ

Valida os dígitos verificadores, não só o formato. Rejeita explicitamente sequências repetidas
(`111.111.111-11`), que passam no módulo 11 mas não são documentos válidos.

Aceita o **CNPJ alfanumérico** adotado pela Receita Federal: 12 posições alfanuméricas
seguidas de 2 dígitos verificadores. O algoritmo usa `ASCII − 48` para o valor de cada
posição, o que faz letras valerem 17 a 42 e mantém o cálculo **idêntico** para CNPJ numérico.

```csharp
private static char CalcularDigitoModulo11(string baseCalculo, int[] pesos)
{
    var soma = 0;
    for (var i = 0; i < baseCalculo.Length; i++)
        soma += (baseCalculo[i] - '0') * pesos[i];   // '0'→0 … 'A'→17 … 'Z'→42

    var resto = soma % 11;
    return (char)('0' + (resto < 2 ? 0 : 11 - resto));
}
```

### `Placa` — brasileira e Mercosul

Aceita `ABC1234` e `ABC1D23`, normalizando entrada (`abc-1234` → `ABC1234`) para que a mesma
placa digitada de formas diferentes seja reconhecida como o mesmo veículo.

### `Dinheiro` — valor em reais

Arredonda a 2 casas com `MidpointRounding.ToEven` (arredondamento bancário), evitando o viés
sistemático que apareceria ao somar muitos itens de orçamento. Proíbe valores negativos.

### `Email`, `Telefone`, `Endereco`

- **Email** — formato validado e normalizado para minúsculas; limite de 254 caracteres (RFC 5321).
- **Telefone** — 10 ou 11 dígitos com DDD validado contra a faixa oficial; celular exige o nono dígito.
- **Endereco** — opcional, mas **completo quando informado**; UF validada, CEP com 8 dígitos.

### `NumeroOrdemServico` — o número que o cliente vê

Formato `OS-AAAA-NNNNNN`, reiniciado a cada ano. A alocação é atômica no banco:

```sql
INSERT INTO automecanic.sequencia_ordem_servico (ano, ultimo_valor)
VALUES (@ano, 1)
ON CONFLICT (ano) DO UPDATE SET ultimo_valor = sequencia_ordem_servico.ultimo_valor + 1
RETURNING ultimo_valor;
```

Duas requisições simultâneas são serializadas pelo bloqueio de linha do PostgreSQL e recebem
números diferentes. Ler-e-incrementar em duas etapas produziria duplicatas sob concorrência.

---

## Blocos de construção

```mermaid
classDiagram
    class Entity {
        <<abstract>>
        +Guid Id
        +NovoId()$ Guid
        +Equals() bool
    }
    class AggregateRoot {
        <<abstract>>
        +uint Versao
        +IReadOnlyCollection~IDomainEvent~ EventosDeDominio
        #RegistrarEvento()
        +LimparEventos()
    }
    class ValueObject {
        <<abstract>>
        #ObterComponentesDeIgualdade()* IEnumerable
        +Equals() bool
    }
    class IDomainEvent {
        <<interface>>
        +Guid EventoId
        +DateTimeOffset OcorridoEm
    }
    class DomainException {
        +string? Codigo
    }

    Entity <|-- AggregateRoot
    AggregateRoot ..> IDomainEvent : acumula
```

| Bloco | Papel | Detalhe de implementação |
|---|---|---|
| `Entity` | Identidade estável | `Guid.CreateVersion7()` — ordenável por tempo, reduz fragmentação de índice no PostgreSQL |
| `AggregateRoot` | Fronteira de consistência | `Versao` mapeada para `xmin` do PostgreSQL: concorrência otimista |
| `ValueObject` | Igualdade estrutural | Compara os componentes declarados por cada tipo |
| `DomainEvent` | Fato consumado | `record` imutável, nome no passado |
| `DomainException` | Violação de invariante | Traduzida para **HTTP 422**, distinta de erro de formato (400) e falha técnica (500) |

### O ciclo de vida de um evento de domínio

```mermaid
sequenceDiagram
    participant App as Serviço de Aplicação
    participant AG as Agregado
    participant UoW as Unidade de Trabalho
    participant H as Manipulador
    participant DB as PostgreSQL

    App->>AG: AprovarOrcamento()
    AG->>AG: RegistrarEvento(OrcamentoAprovado)
    App->>UoW: SalvarAlteracoesAsync()

    rect rgb(240, 248, 255)
    Note over UoW,DB: Uma única transação
    UoW->>AG: coleta e limpa os eventos
    UoW->>H: DespacharAsync(eventos)
    H->>UoW: adiciona novas entidades<br/><i>(ex.: MovimentoEstoque)</i>
    UoW->>DB: SaveChanges — tudo junto
    end
```

**Despachar antes do `SaveChanges` é decisão de projeto:** as entidades que os manipuladores
criarem entram na mesma gravação e, portanto, na mesma transação.

---

## Do modelo ao banco

O mapeamento preserva o modelo em vez de achatá-lo:

| Conceito do modelo | Estratégia de persistência | Por quê |
|---|---|---|
| Objeto de valor de campo único | `ValueConverter` para coluna de texto | Esquema simples; indexável; reconstruído pela fábrica do domínio, então **nenhum valor inválido entra na memória, nem vindo do banco** |
| `Endereco` (multi-campo) | Tipo *owned*, colunas no próprio registro | Não tem sentido fora do cliente |
| Entidades filhas da OS | *Owned collections* em tabelas próprias, sempre carregadas | O agregado nunca é carregado pela metade |
| Enumerações | Gravadas **por nome** | `SELECT status FROM ordem_servico` é autoexplicativo |
| `Dinheiro` | `numeric(14,2)` | Decimal exato, sem erro de ponto flutuante |
| Propriedades derivadas | `Ignore` — calculadas em memória | Não podem divergir da fonte da verdade |
| `Versao` | `xmin` (`xid`) do PostgreSQL | Concorrência otimista sem coluna extra |
| Nomes | `snake_case` por convenção automática | Idiomático em PostgreSQL; dispensa aspas em SQL manual |

---

**Próximo:** [Arquitetura e decisões técnicas](05-arquitetura.md).
