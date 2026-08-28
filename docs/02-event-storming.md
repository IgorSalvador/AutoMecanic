# Event Storming

> **O que é.** O resultado das sessões de Event Storming com a oficina: o negócio descrito
> como uma sequência de **fatos que acontecem**, e não como telas ou tabelas. Cada post-it do
> quadro virou um tipo no código — os eventos laranja são `record`s em `Events/`, os comandos
> azuis são métodos públicos dos agregados, e as políticas lilás são manipuladores de evento
> ou coordenação na camada de aplicação.

## Convenção de cores

| Cor | Post-it | O que representa | Onde vive no código |
|---|---|---|---|
| 🟠 Laranja | **Evento de Domínio** | Um fato que já aconteceu. Nome no passado. | `Domain/*/Events/*.cs` |
| 🔵 Azul | **Comando** | Uma intenção. Nome no imperativo. | Método público do agregado |
| 🟡 Amarelo | **Agregado** | Onde a decisão é tomada e a invariante é garantida. | Raiz de agregado |
| 🟣 Lilás | **Política** | "Sempre que X acontece, faça Y." | `IDomainEventHandler<T>` ou serviço de aplicação |
| 🟢 Verde | **Modelo de Leitura** | O que alguém precisa ver para decidir. | DTO de resposta / endpoint de consulta |
| 🟨 Bege | **Ator** | Quem dispara o comando. | Perfil de usuário / cliente final |
| 🔴 Vermelho | **Hotspot** | Dúvida, risco ou decisão em aberto. | Documentado ao fim de cada fluxo |

---

## Visão geral — a jornada completa

```mermaid
flowchart LR
    subgraph F1["Fluxo 1 — Criação e acompanhamento"]
        A1[Veículo recebido] --> A2[Diagnóstico realizado]
    end
    subgraph F2["Fluxo 2 — Orçamento"]
        B1[Orçamento gerado] --> B2[Enviado ao cliente] --> B3{Decisão}
    end
    subgraph F3["Fluxo 3 — Execução e entrega"]
        C1[Execução iniciada] --> C2[Serviço finalizado] --> C3[Veículo entregue]
    end
    subgraph F4["Fluxo 4 — Peças e insumos"]
        D1[Peça reservada] --> D2[Peça consumida]
        D3[Estoque reposto]
    end

    A2 --> B1
    B3 -->|Aprovado| C1
    B3 -->|Reprovado ou expirado| X[OS cancelada]
    B1 -.->|dispara| D1
    C1 -.->|dispara| D2
    X -.->|dispara| D4[Reserva liberada]

    classDef fluxo fill:#F5F5F5,stroke:#999,color:#111
    class F1,F2,F3,F4 fluxo
```

---

## Fluxo 1 — Criação e acompanhamento da Ordem de Serviço

### Narrativa

O cliente chega à oficina com o carro. O atendente pergunta o **CPF ou CNPJ** — se o cliente
já foi atendido antes, o cadastro aparece; se não, é criado na hora. O mesmo vale para o
veículo, buscado pela **placa**. O atendente registra a leitura do odômetro e o que o cliente
relatou ("está fazendo um barulho na frente"), e **a Ordem de Serviço nasce no status Recebida**.

O carro entra na fila. Quando um mecânico o assume, a OS passa a **Em diagnóstico**. O mecânico
avalia, registra o **laudo técnico** e monta a lista de serviços e peças.

Em paralelo, o cliente pode acompanhar tudo pelo número da OS mais o próprio documento.

### Quadro

```mermaid
flowchart TB
    ATOR1(["🟨 Atendente"]):::ator
    ATOR2(["🟨 Mecânico"]):::ator
    ATOR3(["🟨 Cliente"]):::ator

    C1["🔵 Receber veículo<br/><i>documento, placa, relato</i>"]:::comando
    C2["🔵 Iniciar diagnóstico"]:::comando
    C3["🔵 Registrar diagnóstico<br/><i>laudo técnico</i>"]:::comando
    C4["🔵 Consultar acompanhamento<br/><i>número + documento</i>"]:::comando

    AG1{{"🟡 Cliente"}}:::agregado
    AG2{{"🟡 Veículo"}}:::agregado
    AG3{{"🟡 Ordem de Serviço"}}:::agregado

    E1["🟠 ClienteCadastrado"]:::evento
    E2["🟠 VeiculoCadastrado"]:::evento
    E3["🟠 QuilometragemAtualizada"]:::evento
    E4["🟠 OrdemDeServicoAberta<br/><i>status → Recebida</i>"]:::evento
    E5["🟠 DiagnosticoIniciado<br/><i>status → Em diagnóstico</i>"]:::evento
    E6["🟠 DiagnosticoRegistrado"]:::evento
    E7["🟠 StatusDaOrdemAlterado"]:::evento

    P1["🟣 Política<br/>Toda transição de status<br/>vai para a linha do tempo"]:::politica

    L1["🟢 Painel operacional<br/><i>OS por situação</i>"]:::leitura
    L2["🟢 Acompanhamento do cliente<br/><i>situação + linha do tempo</i>"]:::leitura

    ATOR1 --> C1
    C1 --> AG1 --> E1
    C1 --> AG2 --> E2
    AG2 --> E3
    C1 --> AG3 --> E4

    ATOR2 --> C2 --> AG3 --> E5
    ATOR2 --> C3 --> AG3 --> E6

    E4 --> E7
    E5 --> E7
    E7 --> P1 --> L1
    E7 --> L2

    ATOR3 --> C4 --> L2

    classDef evento fill:#FF9900,stroke:#B36B00,color:#111
    classDef comando fill:#1E90FF,stroke:#0B5FA5,color:#fff
    classDef agregado fill:#FFD700,stroke:#B39700,color:#111
    classDef politica fill:#C77DFF,stroke:#8E44AD,color:#111
    classDef leitura fill:#90EE90,stroke:#4CAF50,color:#111
    classDef ator fill:#FFFACD,stroke:#C9B458,color:#111
```

### Tabela de rastreabilidade

| Comando (🔵) | Agregado (🟡) | Evento (🟠) | Regra que o agregado garante |
|---|---|---|---|
| Receber veículo | Cliente, Veículo, Ordem de Serviço | `ClienteCadastrado`, `VeiculoCadastrado`, `OrdemDeServicoAberta` | Documento e placa válidos; cliente e veículo ativos; **o veículo pertence ao cliente informado** |
| Iniciar diagnóstico | Ordem de Serviço | `DiagnosticoIniciado`, `StatusDaOrdemAlterado` | Só a partir de *Recebida* |
| Registrar diagnóstico | Ordem de Serviço | `DiagnosticoRegistrado` | Só em *Em diagnóstico* ou *Em execução*; laudo não vazio |
| Consultar acompanhamento | — (leitura) | — | **Número e documento juntos**; resposta idêntica quando não confere |

### Hotspots

> 🔴 **O veículo pode ter trocado de dono desde o último atendimento.**
> Decisão: a recepção detecta a divergência e **transfere o veículo** para o cliente que o
> trouxe, registrando `VeiculoTransferido`. A alternativa — recusar o atendimento — seria
> inaceitável para o balcão.

> 🔴 **O cliente digita o odômetro errado e informa um valor menor que o anterior.**
> Decisão: a quilometragem é **monotônica**. Um valor menor é recusado
> (`QUILOMETRAGEM_RETROATIVA`), porque na prática indica erro de digitação ou adulteração.

> 🔴 **Como o cliente consulta sem ter login?**
> Decisão: número da OS **mais** documento funcionam como prova de posse. Sem essa
> combinação, alguém poderia percorrer números sequenciais e ler dados de terceiros.

---

## Fluxo 2 — Elaboração, aprovação e reprovação do orçamento

### Narrativa

Com o laudo pronto, o mecânico inclui na OS os **serviços** do catálogo e as **peças**
necessárias. Cada inclusão de peça **reserva** a quantidade no estoque na mesma transação —
se não há saldo disponível, a inclusão inteira falha.

O sistema **calcula o orçamento** somando os itens e aplicando o desconto comercial. Nada é
digitado. O atendente envia ao cliente, e a OS passa a **Aguardando aprovação** — a partir
daqui os itens ficam **congelados**.

O cliente aprova ou reprova. Aprovando, a OS vai direto para **Em execução** e as peças
reservadas são **baixadas do estoque**. Reprovando, a OS é **cancelada** e as reservas voltam
ao disponível. Se o prazo vencer sem resposta, uma rotina de manutenção **expira** o orçamento
com o mesmo efeito da reprovação.

### Quadro

```mermaid
flowchart TB
    ATOR1(["🟨 Mecânico"]):::ator
    ATOR2(["🟨 Atendente"]):::ator
    ATOR3(["🟨 Cliente"]):::ator
    ATOR4(["🟨 Rotina de manutenção"]):::ator

    C1["🔵 Adicionar serviço"]:::comando
    C2["🔵 Adicionar peça"]:::comando
    C3["🔵 Gerar orçamento<br/><i>desconto %</i>"]:::comando
    C4["🔵 Enviar orçamento<br/><i>validade em dias</i>"]:::comando
    C5["🔵 Aprovar orçamento"]:::comando
    C6["🔵 Reprovar orçamento<br/><i>motivo</i>"]:::comando
    C7["🔵 Expirar orçamentos vencidos"]:::comando
    C8["🔵 Devolver para revisão"]:::comando

    AG1{{"🟡 Ordem de Serviço"}}:::agregado
    AG2{{"🟡 Peça"}}:::agregado

    E1["🟠 ServicoIncluidoNaOrdem"]:::evento
    E2["🟠 PecaIncluidaNaOrdem"]:::evento
    E3["🟠 QuantidadeReservada"]:::evento
    E4["🟠 OrcamentoGerado"]:::evento
    E5["🟠 OrcamentoEnviadoAoCliente<br/><i>status → Aguardando aprovação</i>"]:::evento
    E6["🟠 OrcamentoAprovadoPeloCliente<br/><i>status → Em execução</i>"]:::evento
    E7["🟠 OrcamentoReprovadoPeloCliente"]:::evento
    E8["🟠 OrcamentoExpirado"]:::evento
    E9["🟠 OrdemDeServicoCancelada"]:::evento
    E10["🟠 ReservaLiberada"]:::evento
    E11["🟠 EstoqueMovimentado<br/><i>saída</i>"]:::evento

    P1["🟣 Política<br/>Ao incluir peça,<br/>reservar no estoque"]:::politica
    P2["🟣 Política<br/>Ao aprovar,<br/>consumir as reservas"]:::politica
    P3["🟣 Política<br/>Ao reprovar, cancelar<br/>ou expirar, liberar reservas"]:::politica
    P4["🟣 Política<br/>Item incluído recalcula<br/>o orçamento em elaboração"]:::politica

    HOT1["🔴 Itens congelados<br/>após o envio"]:::hotspot

    L1["🟢 Orçamento do cliente<br/><i>serviços, peças, total</i>"]:::leitura
    L2["🟢 OS aguardando aprovação"]:::leitura

    ATOR1 --> C1 --> AG1 --> E1
    ATOR1 --> C2 --> P1 --> AG2 --> E3
    P1 --> AG1 --> E2
    E1 --> P4
    E2 --> P4 --> E4

    ATOR1 --> C3 --> AG1 --> E4 --> L1
    ATOR2 --> C4 --> AG1 --> E5 --> L2
    E5 --> HOT1

    ATOR3 --> C5 --> AG1 --> E6 --> P2 --> AG2 --> E11
    ATOR3 --> C6 --> AG1 --> E7 --> E9
    ATOR4 --> C7 --> AG1 --> E8 --> E9
    E9 --> P3 --> AG2 --> E10

    ATOR2 --> C8 --> AG1

    classDef evento fill:#FF9900,stroke:#B36B00,color:#111
    classDef comando fill:#1E90FF,stroke:#0B5FA5,color:#fff
    classDef agregado fill:#FFD700,stroke:#B39700,color:#111
    classDef politica fill:#C77DFF,stroke:#8E44AD,color:#111
    classDef leitura fill:#90EE90,stroke:#4CAF50,color:#111
    classDef ator fill:#FFFACD,stroke:#C9B458,color:#111
    classDef hotspot fill:#FF4D4D,stroke:#B30000,color:#fff
```

### Tabela de rastreabilidade

| Comando (🔵) | Agregados envolvidos (🟡) | Evento (🟠) | Regra garantida |
|---|---|---|---|
| Adicionar serviço | Ordem de Serviço, Serviço | `ServicoIncluidoNaOrdem` | Serviço ativo; **preço e tempo copiados e congelados**; itens ainda alteráveis |
| Adicionar peça | Ordem de Serviço, **Peça** | `PecaIncluidaNaOrdem` + `QuantidadeReservada` | **Reserva primeiro**: sem saldo disponível, a OS não muda |
| Gerar orçamento | Ordem de Serviço | `OrcamentoGerado` | Ao menos um item; **valor é sempre a soma dos itens**; desconto de 0 a 100 |
| Enviar orçamento | Ordem de Serviço | `OrcamentoEnviadoAoCliente` | Orçamento existente e não vazio; validade de 1 a 90 dias |
| Aprovar orçamento | Ordem de Serviço, **Peça** | `OrcamentoAprovadoPeloCliente`, `ExecucaoIniciada`, `EstoqueMovimentado` | Só a partir de *Aguardando aprovação*; **consumo e transição na mesma transação** |
| Reprovar orçamento | Ordem de Serviço, **Peça** | `OrcamentoReprovadoPeloCliente`, `OrdemDeServicoCancelada`, `ReservaLiberada` | Só a partir de *Aguardando aprovação* |
| Expirar orçamentos | Ordem de Serviço, **Peça** | `OrcamentoExpirado`, `OrdemDeServicoCancelada`, `ReservaLiberada` | Apenas os já vencidos |
| Devolver para revisão | Ordem de Serviço | `StatusDaOrdemAlterado` | Reabre o orçamento; **impossível se já aprovado** |

### Hotspots

> 🔴 **O cliente aprova um valor diferente do que viu.**
> Decisão: o envio do orçamento **congela os itens**. Qualquer alteração exige devolver a OS
> ao diagnóstico e gerar um orçamento novo, que o cliente verá de novo.

> 🔴 **Duas OS prometem a mesma última peça.**
> Decisão: o saldo é separado em *físico* e *reservado*. O que se pode prometer é o
> **disponível**. Um teste de integração cobre exatamente esse cenário.

> 🔴 **O cliente some e a peça fica presa indefinidamente.**
> Decisão: o orçamento tem **validade** (padrão 7 dias). Uma rotina de manutenção expira os
> vencidos e devolve as reservas. Exposta como
> `POST /ordens-servico/manutencao/expirar-orcamentos` para o agendador chamar.

> 🔴 **A oficina quer dar desconto acima do permitido.**
> Decisão em aberto para a Fase 2: hoje o desconto vai de 0 a 100% sem alçada. Uma regra de
> aprovação por faixa de desconto exigiria o conceito de *alçada*, que a oficina ainda não tem.

---

## Fluxo 3 — Execução e finalização do serviço

### Narrativa

Com o orçamento aprovado, a OS já está **Em execução** e o cronômetro do indicador de tempo
médio começou a contar. As peças reservadas foram baixadas do estoque e aplicadas no veículo.

Se, ao desmontar, o mecânico encontra algo novo, ele **complementa o laudo** — mas não pode
incluir itens: isso mudaria o valor que o cliente aprovou. O caminho correto é uma nova OS.

Concluídos os serviços, o mecânico **finaliza**: a OS passa a **Finalizada**, o tempo real de
execução passa a existir e o carro fica pronto para retirada. Quando o cliente busca o
veículo, o atendente registra a **entrega** — estado terminal.

### Quadro

```mermaid
flowchart TB
    ATOR1(["🟨 Mecânico"]):::ator
    ATOR2(["🟨 Atendente"]):::ator

    C1["🔵 Registrar diagnóstico<br/><i>complemento do laudo</i>"]:::comando
    C2["🔵 Confirmar consumo de peça"]:::comando
    C3["🔵 Finalizar serviço<br/><i>observação</i>"]:::comando
    C4["🔵 Entregar veículo<br/><i>observação</i>"]:::comando
    C5["🔵 Atribuir responsável"]:::comando

    AG1{{"🟡 Ordem de Serviço"}}:::agregado

    E1["🟠 ExecucaoIniciada<br/><i>marco inicial do tempo</i>"]:::evento
    E2["🟠 DiagnosticoRegistrado"]:::evento
    E3["🟠 ServicoFinalizado<br/><i>status → Finalizada</i><br/><i>duração em minutos</i>"]:::evento
    E4["🟠 VeiculoEntregueAoCliente<br/><i>status → Entregue</i>"]:::evento
    E5["🟠 StatusDaOrdemAlterado"]:::evento

    P1["🟣 Política<br/>Finalização alimenta o<br/>tempo médio de execução"]:::politica
    P2["🟣 Política<br/>Estado terminal recusa<br/>qualquer nova transição"]:::politica

    HOT1["🔴 Escopo novo descoberto<br/>ao desmontar"]:::hotspot

    L1["🟢 Tempo médio de execução<br/><i>média, mediana, aderência</i>"]:::leitura
    L2["🟢 Veículos prontos para retirada"]:::leitura
    L3["🟢 Histórico do veículo"]:::leitura

    E1 --> AG1
    ATOR1 --> C1 --> AG1 --> E2
    ATOR1 --> C2 --> AG1
    ATOR1 --> C3 --> AG1 --> E3 --> E5
    E3 --> P1 --> L1
    E3 --> L2
    ATOR2 --> C4 --> AG1 --> E4 --> E5
    E4 --> P2
    E4 --> L3
    ATOR2 --> C5 --> AG1
    C1 -.-> HOT1

    classDef evento fill:#FF9900,stroke:#B36B00,color:#111
    classDef comando fill:#1E90FF,stroke:#0B5FA5,color:#fff
    classDef agregado fill:#FFD700,stroke:#B39700,color:#111
    classDef politica fill:#C77DFF,stroke:#8E44AD,color:#111
    classDef leitura fill:#90EE90,stroke:#4CAF50,color:#111
    classDef ator fill:#FFFACD,stroke:#C9B458,color:#111
    classDef hotspot fill:#FF4D4D,stroke:#B30000,color:#fff
```

### Tabela de rastreabilidade

| Comando (🔵) | Agregado (🟡) | Evento (🟠) | Regra garantida |
|---|---|---|---|
| Confirmar consumo de peça | Ordem de Serviço | — | Só com a OS *Em execução* |
| Registrar diagnóstico | Ordem de Serviço | `DiagnosticoRegistrado` | Permitido em execução; **não permite incluir itens** |
| Finalizar serviço | Ordem de Serviço | `ServicoFinalizado`, `StatusDaOrdemAlterado` | Só a partir de *Em execução*; calcula a duração real |
| Entregar veículo | Ordem de Serviço | `VeiculoEntregueAoCliente` | **Só a partir de *Finalizada*** — nunca pulando a finalização |
| Qualquer comando após a entrega | Ordem de Serviço | — | Recusado: estado terminal |

### Hotspots

> 🔴 **O mecânico descobre um problema novo ao desmontar.**
> Decisão: o laudo pode ser complementado, mas **os itens permanecem congelados**. Serviço
> adicional exige nova OS, com novo orçamento aprovado pelo cliente. É o que preserva a
> confiança do valor aprovado.

> 🔴 **A OS pode ser cancelada durante a execução?**
> Decisão: **não**. A partir da aprovação, peças saíram do estoque e horas foram trabalhadas.
> O cancelamento é permitido apenas em *Recebida*, *Em diagnóstico* e *Aguardando aprovação*.

> 🔴 **O tempo médio deve contar o tempo esperando o cliente decidir?**
> Decisão: **não**, e por isso existem dois indicadores. *Tempo de execução* mede a oficina
> (aprovação → finalização); *tempo total de atendimento* mede a experiência do cliente
> (abertura → entrega). Misturá-los faria a oficina parecer lenta por culpa da demora do cliente.

---

## Fluxo 4 — Gestão de peças e insumos

### Narrativa

O almoxarife cadastra a peça com **saldo inicial** e **ponto de ressuprimento**. A partir daí,
cada movimentação gera um lançamento no razão: **entrada** ao receber do fornecedor,
**saída** ao consumir em uma OS, **perda** por avaria, **ajuste** após contagem física e
**estorno** quando uma peça volta para a prateleira.

O saldo é sempre decomposto em três: o que está na prateleira, o que já foi prometido a
orçamentos pendentes, e o que resta para prometer. Quando o **disponível** cruza o ponto de
ressuprimento, o sistema alerta a compra.

### Quadro

```mermaid
flowchart TB
    ATOR1(["🟨 Estoquista"]):::ator
    ATOR2(["🟨 Ordem de Serviço<br/>(sistema)"]):::ator
    ATOR3(["🟨 Comprador"]):::ator

    C1["🔵 Cadastrar peça<br/><i>saldo inicial, mínimo</i>"]:::comando
    C2["🔵 Registrar entrada<br/><i>quantidade, NF</i>"]:::comando
    C3["🔵 Registrar perda<br/><i>quantidade, motivo</i>"]:::comando
    C4["🔵 Ajustar saldo<br/><i>quantidade apurada</i>"]:::comando
    C5["🔵 Reservar quantidade"]:::comando
    C6["🔵 Consumir reserva"]:::comando
    C7["🔵 Liberar reserva"]:::comando
    C8["🔵 Reajustar preço"]:::comando

    AG1{{"🟡 Peça"}}:::agregado
    AG2{{"🟡 Movimento de Estoque<br/><i>razão append-only</i>"}}:::agregado

    E1["🟠 PecaCadastrada"]:::evento
    E2["🟠 EstoqueMovimentado<br/><i>entrada</i>"]:::evento
    E3["🟠 EstoqueMovimentado<br/><i>perda</i>"]:::evento
    E4["🟠 EstoqueMovimentado<br/><i>ajuste</i>"]:::evento
    E5["🟠 QuantidadeReservada"]:::evento
    E6["🟠 EstoqueMovimentado<br/><i>saída</i>"]:::evento
    E7["🟠 ReservaLiberada"]:::evento
    E8["🟠 EstoqueAtingiuNivelMinimo"]:::evento
    E9["🟠 PrecoDaPecaReajustado"]:::evento

    P1["🟣 Política<br/>Todo EstoqueMovimentado vira<br/>lançamento no razão<br/><b>na mesma transação</b>"]:::politica
    P2["🟣 Política<br/>Disponível ≤ mínimo<br/>dispara alerta de compra"]:::politica

    HOT1["🔴 Ajuste não pode deixar<br/>o saldo abaixo do reservado"]:::hotspot

    L1["🟢 Extrato do razão<br/><i>por peça, por OS, por período</i>"]:::leitura
    L2["🟢 Alertas de ressuprimento<br/><i>com sugestão de compra</i>"]:::leitura
    L3["🟢 Posição de estoque<br/><i>físico / reservado / disponível</i>"]:::leitura

    ATOR1 --> C1 --> AG1 --> E1
    ATOR1 --> C2 --> AG1 --> E2
    ATOR1 --> C3 --> AG1 --> E3
    ATOR1 --> C4 --> AG1 --> E4
    ATOR1 --> C8 --> AG1 --> E9

    ATOR2 --> C5 --> AG1 --> E5
    ATOR2 --> C6 --> AG1 --> E6
    ATOR2 --> C7 --> AG1 --> E7

    E2 --> P1
    E3 --> P1
    E4 --> P1
    E6 --> P1
    P1 --> AG2 --> L1

    E5 --> P2
    E6 --> P2
    E3 --> P2
    P2 --> E8 --> L2
    ATOR3 --> L2

    AG1 --> L3
    C4 -.-> HOT1

    classDef evento fill:#FF9900,stroke:#B36B00,color:#111
    classDef comando fill:#1E90FF,stroke:#0B5FA5,color:#fff
    classDef agregado fill:#FFD700,stroke:#B39700,color:#111
    classDef politica fill:#C77DFF,stroke:#8E44AD,color:#111
    classDef leitura fill:#90EE90,stroke:#4CAF50,color:#111
    classDef ator fill:#FFFACD,stroke:#C9B458,color:#111
    classDef hotspot fill:#FF4D4D,stroke:#B30000,color:#fff
```

### Tabela de rastreabilidade

| Comando (🔵) | Agregado (🟡) | Evento (🟠) | Regra garantida |
|---|---|---|---|
| Cadastrar peça | Peça | `PecaCadastrada`, `EstoqueMovimentado` (entrada) | Código único; preço > 0; saldo inicial registra lançamento |
| Registrar entrada | Peça | `EstoqueMovimentado` (entrada ou estorno) | Motivo obrigatório; classificado como estorno se vier de uma OS |
| Registrar perda | Peça | `EstoqueMovimentado` (perda) | **Não pode consumir o que já está reservado** |
| Ajustar saldo | Peça | `EstoqueMovimentado` (ajuste) | **Saldo apurado ≥ reservado**; registra a diferença |
| Reservar quantidade | Peça | `QuantidadeReservada` | Quantidade ≤ disponível; peça ativa |
| Consumir reserva | Peça | `EstoqueMovimentado` (saída) | Quantidade ≤ reservado; reduz físico e reserva juntos |
| Liberar reserva | Peça | `ReservaLiberada` | Quantidade ≤ reservado |
| Inativar peça | Peça | `PecaInativada` | **Impossível com reserva pendente** |

### Hotspots

> 🔴 **A contagem física acusa menos peças do que o sistema, mas há reservas.**
> Decisão: o ajuste é **recusado** se o saldo apurado for menor que o reservado
> (`AJUSTE_INVALIDO`). Aceitá-lo deixaria promessas já feitas ao cliente sem lastro físico.
> O caminho é liberar as reservas afetadas primeiro — decisão humana, com contato ao cliente.

> 🔴 **O saldo pode divergir do razão?**
> Decisão: **não pode**, e a garantia é estrutural. O lançamento é criado por um manipulador
> do evento `EstoqueMovimentado` que roda **dentro da mesma transação** que alterou o saldo.
> Se um falhar, o outro é desfeito.

> 🔴 **Quanto comprar quando o alerta dispara?**
> Decisão para o MVP: sugerir a reposição até o **dobro do ponto de ressuprimento**, criando
> uma folga de um ciclo. Um cálculo baseado em consumo histórico e prazo de entrega do
> fornecedor fica para a Fase 2.

---

## Consolidação — todos os eventos de domínio

| Evento | Contexto | Disparado por | Consumido por |
|---|---|---|---|
| `ClienteCadastrado` | Clientes | Cadastro / recepção | Auditoria |
| `DadosDeContatoDoClienteAtualizados` | Clientes | Atualização cadastral | Auditoria |
| `ClienteInativado` / `ClienteReativado` | Clientes | Gestão administrativa | Auditoria |
| `VeiculoCadastrado` | Veículos | Cadastro / recepção | Auditoria |
| `QuilometragemAtualizada` | Veículos | Recepção do veículo | Histórico de manutenção |
| `VeiculoTransferido` | Veículos | Troca de proprietário | Auditoria |
| `VeiculoInativado` | Veículos | Gestão administrativa | Auditoria |
| `ServicoCadastrado` | Catálogo | Gestão do catálogo | Auditoria |
| `PrecoDoServicoReajustado` | Catálogo | Reajuste de tabela | Auditoria de formação de preço |
| `ServicoInativado` | Catálogo | Gestão do catálogo | Auditoria |
| `PecaCadastrada` | Estoque | Cadastro no almoxarifado | Auditoria |
| `EstoqueMovimentado` | Estoque | Toda alteração de saldo | **Razão de estoque** (mesma transação) |
| `EstoqueAtingiuNivelMinimo` | Estoque | Saldo cruza o ponto de ressuprimento | Alerta de compra |
| `QuantidadeReservada` / `ReservaLiberada` | Estoque | Inclusão/remoção de peça na OS | Posição de estoque |
| `PrecoDaPecaReajustado` | Estoque | Reajuste de preço | Auditoria |
| `PecaInativada` | Estoque | Gestão do almoxarifado | Auditoria |
| `OrdemDeServicoAberta` | Ordem de Serviço | Recepção | Painel operacional |
| `DiagnosticoIniciado` / `DiagnosticoRegistrado` | Ordem de Serviço | Mecânico | Acompanhamento |
| `ServicoIncluidoNaOrdem` / `PecaIncluidaNaOrdem` | Ordem de Serviço | Composição do orçamento | Recálculo do orçamento |
| `ItemRemovidoDaOrdem` | Ordem de Serviço | Remoção de item | Liberação de reserva |
| `OrcamentoGerado` | Ordem de Serviço | Cálculo automático | Orçamento do cliente |
| `OrcamentoEnviadoAoCliente` | Ordem de Serviço | Envio ao cliente | Notificação; congelamento de itens |
| `OrcamentoAprovadoPeloCliente` | Ordem de Serviço | Decisão do cliente | **Consumo das reservas** |
| `OrcamentoReprovadoPeloCliente` | Ordem de Serviço | Decisão do cliente | **Liberação das reservas** |
| `OrcamentoExpirado` | Ordem de Serviço | Rotina de manutenção | **Liberação das reservas** |
| `ExecucaoIniciada` | Ordem de Serviço | Aprovação do orçamento | Marco inicial do tempo de execução |
| `ServicoFinalizado` | Ordem de Serviço | Mecânico | **Tempo médio de execução** |
| `VeiculoEntregueAoCliente` | Ordem de Serviço | Atendente | Histórico do veículo |
| `OrdemDeServicoCancelada` | Ordem de Serviço | Reprovação, expiração ou desistência | Liberação de reservas |
| `StatusDaOrdemAlterado` | Ordem de Serviço | Toda transição | Linha do tempo; acompanhamento |
| `UsuarioCriado` / `UsuarioInativado` | Autenticação | Gestão de acesso | Auditoria |
| `UsuarioAutenticado` | Autenticação | Login bem-sucedido | Trilha de acesso |
| `UsuarioBloqueado` | Autenticação | 5 tentativas malsucedidas | Alerta de segurança |
| `SenhaAlterada` | Autenticação | Troca ou redefinição | Auditoria |

---

**Próximo:** [Context Map](03-context-map.md) — como os contextos se relacionam.
