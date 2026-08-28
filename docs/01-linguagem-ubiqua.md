# Linguagem Ubíqua

> **O que é.** O vocabulário único e sem ambiguidade compartilhado por quem trabalha na
> oficina e por quem escreve o código. Cada termo abaixo aparece com o mesmo nome na
> conversa do balcão, nos diagramas, nas classes C# e nos endpoints da API — quando o
> mecânico diz "a OS está aguardando aprovação", existe um `StatusOrdemServico.AguardandoAprovacao`
> literalmente com esse nome.

## Por que isso importa neste projeto

O problema descrito no desafio é, em boa parte, um problema de vocabulário. "Orçamento",
"peça reservada" e "serviço finalizado" significam coisas diferentes para o atendente, o
mecânico e o almoxarife — e é dessa divergência que nascem os erros de priorização e as
falhas de controle de estoque citadas no enunciado.

A regra que seguimos: **nenhum conceito de negócio recebe nome técnico no código, e nenhum
termo do código é inventado sem existir no negócio.** Onde a tradução seria inevitável, o
termo permanece em português.

---

## Termos centrais

### Ordem de Serviço (OS)

O documento que representa **um atendimento completo de um veículo**, do momento em que ele
entra na oficina até a entrega ao cliente. É a unidade em torno da qual todo o resto gira:
sem OS, não há orçamento, não há consumo de peça e não há serviço executado.

Identificada externamente por um **Número** curto e legível (`OS-2026-000042`), que é o que
o cliente informa ao ligar.

> No código: `OrdemServico` (raiz de agregado) · `NumeroOrdemServico` (objeto de valor)

---

### Status da Ordem de Serviço

A etapa em que o atendimento se encontra. São exatamente sete, e a OS está sempre em uma
delas:

| Status | O que significa na oficina |
|---|---|
| **Recebida** | O veículo entrou, a OS foi aberta. Ninguém olhou o carro ainda. |
| **Em diagnóstico** | Um mecânico está avaliando e montando a lista de serviços e peças. |
| **Aguardando aprovação** | O orçamento foi enviado. A bola está com o cliente. |
| **Em execução** | O cliente aprovou. O carro está sendo trabalhado. |
| **Finalizada** | Os serviços acabaram. O carro está pronto, aguardando retirada. |
| **Entregue** | O cliente levou o carro. Fim do atendimento. |
| **Cancelada** | O atendimento terminou sem execução — orçamento reprovado, expirado ou desistência. |

**Regra que a palavra carrega:** o status nunca é "definido"; ele **muda como consequência**
de uma ação concreta. Não existe "mudar o status para Em execução" — existe "o cliente
aprovou o orçamento", e disso decorre o status.

> No código: `StatusOrdemServico`

---

### Orçamento

O valor apresentado ao cliente para aprovação, **calculado automaticamente** a partir dos
serviços e peças incluídos na OS. Nunca é digitado.

Tem situação própria, independente da OS: *Em elaboração* (rascunho interno da oficina),
*Aguardando aprovação* (já enviado), *Aprovado*, *Reprovado* ou *Expirado*.

**Regra que a palavra carrega:** depois de **enviado**, o orçamento é um compromisso. Os
itens da OS ficam congelados — o cliente aprova exatamente o que viu. Mudar o escopo exige
devolver a OS ao diagnóstico e gerar um orçamento novo.

> No código: `Orcamento` (entidade filha) · `StatusOrcamento`

---

### Cliente

A pessoa física ou jurídica atendida pela oficina, identificada pelo **CPF ou CNPJ** — a
chave natural usada na recepção.

**Regra que a palavra carrega:** cliente **não se exclui, se inativa**. As Ordens de Serviço
já emitidas precisam continuar apontando para um cliente existente.

> No código: `Cliente` (raiz de agregado) · `Documento` (objeto de valor)

---

### Veículo

O carro atendido, identificado pela **placa**. Pertence a um cliente, mas tem vida própria:
pode ser transferido para outro dono sem deixar de ser o mesmo veículo, com o mesmo histórico.

**Regra que a palavra carrega:** a placa é imutável. Outra placa significa outro veículo — o
caminho correto é inativar este e cadastrar o novo, preservando o histórico.

> No código: `Veiculo` (raiz de agregado) · `Placa` (objeto de valor)

---

### Serviço

Uma linha do **catálogo** da oficina: "troca de óleo", "alinhamento", "revisão de freios".
Traz o **preço de tabela** e o **tempo estimado** de execução.

**Regra que a palavra carrega:** o preço do catálogo é o preço *de hoje*. Quando o serviço
entra em uma OS, o preço é **copiado e congelado** — um reajuste amanhã não altera um
orçamento já apresentado.

> No código: `Servico` (raiz de agregado) · `ItemServico` (a cópia congelada dentro da OS)

---

### Peça e Insumo

Tudo que o almoxarifado controla: peças de reposição (pastilha, filtro, bateria) e insumos
consumíveis (óleo, aditivo, fluido). O sistema trata os dois da mesma forma.

Identificada pelo **Código** (SKU) e controlada em três quantidades distintas — a distinção
mais importante do controle de estoque:

| Quantidade | O que é |
|---|---|
| **Em estoque** | O que está fisicamente na prateleira. |
| **Reservada** | A parte do saldo físico já prometida a orçamentos pendentes de aprovação. |
| **Disponível** | Em estoque − Reservada. **É o que se pode prometer a uma nova OS.** |

**Regra que a palavra carrega:** sem essa separação, duas Ordens de Serviço prometeriam a
mesma última peça ao cliente. A peça só sai da prateleira quando o orçamento é aprovado.

> No código: `Peca` (raiz de agregado) · `ItemPeca` (a cópia congelada dentro da OS)

---

### Reserva

O ato de **separar** uma quantidade de peça para um orçamento em elaboração. A peça continua
fisicamente no estoque, mas deixa de ser prometível a outra OS.

Uma reserva tem três destinos possíveis:

- **Consumida** — o cliente aprovou; a peça sai do estoque e vai para o veículo;
- **Liberada** — o cliente reprovou, o item foi removido ou a OS foi cancelada; volta ao disponível;
- **Expirada** — o cliente nunca respondeu; o prazo do orçamento venceu e a reserva é liberada.

> No código: `Peca.Reservar` · `Peca.ConsumirReserva` · `Peca.LiberarReserva`

---

### Movimento de Estoque

Um lançamento no **razão** (kardex) do almoxarifado. Todo lançamento registra o saldo antes
e depois, o motivo e — quando houver — a OS que o originou.

Tipos: **Entrada** (compra), **Saída** (consumo em OS), **Ajuste** (contagem física),
**Estorno** (devolução ao estoque), **Perda** (avaria, vencimento).

**Regra que a palavra carrega:** o razão é *append-only*. Nenhum lançamento é alterado nem
excluído — é isso que torna o saldo auditável e reconstituível.

> No código: `MovimentoEstoque` (raiz de agregado, imutável)

---

### Estoque Mínimo (ponto de ressuprimento)

A quantidade abaixo da qual a peça precisa ser recomprada. Quando o **disponível** cruza esse
ponto, o sistema emite um alerta de ressuprimento.

> No código: `Peca.EstoqueMinimo` · evento `EstoqueAtingiuNivelMinimo`

---

### Diagnóstico

A avaliação técnica do veículo feita pelo mecânico. Produz o **laudo técnico** e a lista de
serviços e peças que comporão o orçamento.

Distingue-se do **relato do problema**, que é o que o *cliente* disse na recepção
("está fazendo um barulho na frente"). Os dois convivem na OS: um é a queixa, o outro é o
achado técnico.

> No código: `OrdemServico.DescricaoProblema` (relato) · `OrdemServico.DiagnosticoTecnico` (laudo)

---

### Recepção

O ato de receber o veículo no balcão: identificar o cliente pelo CPF/CNPJ, localizar ou
cadastrar o veículo pela placa, registrar a quilometragem de entrada e o relato do problema,
e abrir a OS. Tudo isso é **uma única operação** do ponto de vista do atendente.

> No código: `POST /api/v1/ordens-servico/recepcao`

---

### Tempo Médio de Execução

O indicador de gestão exigido pela oficina: a média do intervalo entre o **início da
execução** (aprovação do orçamento) e a **finalização** dos serviços, considerando apenas as
OS finalizadas no período.

Distingue-se do **tempo total de atendimento**, que vai da abertura da OS até a entrega e
inclui o tempo em que o carro ficou parado esperando a decisão do cliente.

> No código: `OrdemServico.DuracaoDaExecucao` · `OrdemServico.TempoTotalDeAtendimento`

---

## Termos de estrutura (vocabulário de DDD)

Estes não vêm do negócio, mas do modelo. Aparecem aqui para que a leitura dos outros
documentos não dependa de conhecimento prévio.

| Termo | Significado neste projeto |
|---|---|
| **Agregado** | Um conjunto de objetos tratado como uma unidade para efeito de alteração e consistência. Ex.: uma OS com seus itens, orçamento e histórico. |
| **Raiz de Agregado** | O único objeto pelo qual se pode alterar o agregado. Ex.: não se adiciona um item direto na lista — pede-se à `OrdemServico`. |
| **Entidade** | Objeto com identidade própria e estável ao longo do tempo. Dois clientes com o mesmo nome são clientes diferentes. |
| **Objeto de Valor** | Objeto sem identidade, imutável, comparado pelo conteúdo. Dois CPFs com o mesmo número são o mesmo CPF. Concentra as regras de formação. |
| **Invariante** | Regra que precisa valer o tempo todo, não só no momento da gravação. Ex.: "o reservado nunca excede o saldo físico". |
| **Evento de Domínio** | Um fato de negócio que já aconteceu. Nomeado no passado: `OrcamentoAprovadoPeloCliente`. |
| **Contexto Delimitado** | Uma fronteira dentro da qual um termo tem um significado só. Ver [Context Map](03-context-map.md). |

---

## Termos deliberadamente **não** usados

Palavras que aparecem em sistemas de oficina, mas que evitamos porque criam ambiguidade:

| Evitado | Por quê | Usamos |
|---|---|---|
| "Pedido" | Confunde OS com pedido de compra ao fornecedor | Ordem de Serviço |
| "Produto" | Trata peça e serviço como a mesma coisa, e eles têm regras diferentes | Peça / Serviço |
| "Cancelar orçamento" | Ambíguo entre reprovar (decisão do cliente) e cancelar a OS (decisão da oficina) | Reprovar orçamento / Cancelar OS |
| "Baixar peça" | Não distingue reservar de consumir | Reservar / Consumir reserva |
| "Fechar OS" | Não distingue finalizar o serviço de entregar o carro | Finalizar serviço / Entregar veículo |
| "Status do orçamento = OS" | São duas máquinas de estado distintas | Status da OS / Situação do orçamento |

---

## Do vocabulário ao código

A tabela abaixo é o índice de tradução completo. É o que permite ler o código como quem lê
uma descrição do negócio.

| Termo do negócio | Tipo no código | Camada |
|---|---|---|
| Ordem de Serviço | `OrdemServico` | Domínio (raiz de agregado) |
| Número da OS | `NumeroOrdemServico` | Domínio (objeto de valor) |
| Status da OS | `StatusOrdemServico` | Domínio (enumeração) |
| Orçamento | `Orcamento` | Domínio (entidade filha) |
| Situação do orçamento | `StatusOrcamento` | Domínio (enumeração) |
| Serviço contratado na OS | `ItemServico` | Domínio (entidade filha) |
| Peça prevista na OS | `ItemPeca` | Domínio (entidade filha) |
| Linha do tempo da OS | `HistoricoStatus` | Domínio (entidade filha) |
| Cliente | `Cliente` | Domínio (raiz de agregado) |
| CPF / CNPJ | `Documento` | Domínio (objeto de valor) |
| Veículo | `Veiculo` | Domínio (raiz de agregado) |
| Placa | `Placa` | Domínio (objeto de valor) |
| Serviço do catálogo | `Servico` | Domínio (raiz de agregado) |
| Peça / insumo | `Peca` | Domínio (raiz de agregado) |
| Movimento de estoque | `MovimentoEstoque` | Domínio (raiz de agregado) |
| Usuário do sistema | `Usuario` | Domínio (raiz de agregado) |
| Valor em reais | `Dinheiro` | Domínio (objeto de valor compartilhado) |
| Recepção do veículo | `ReceberVeiculoAsync` | Aplicação (caso de uso) |
| Acompanhamento pelo cliente | `AcompanharAsync` | Aplicação (caso de uso) |
| Tempo médio de execução | `ObterTempoMedioDeExecucaoAsync` | Aplicação (caso de uso) |

---

**Próximo:** [Event Storming](02-event-storming.md) — os fluxos completos do negócio.
