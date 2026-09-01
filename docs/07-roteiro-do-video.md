# Roteiro do vídeo de demonstração

> Roteiro para o vídeo de até 15 minutos exigido na entrega. Cobre **todos** os pontos do
> requisito, com tempos sugeridos e o que mostrar na tela em cada momento.
>
> Todas as requisições estão prontas em [`AutoMecanic.http`](../AutoMecanic.http), na mesma
> ordem deste roteiro — basta clicar em *Send Request*.

## Antes de gravar

```bash
# Ambiente limpo, para a demonstração começar do zero
docker compose down -v
docker compose up -d --build

# Aguarde a API ficar saudável
docker compose ps
```

Deixe abertos: o Swagger (`http://localhost:8080/swagger`), a coleção Postman importada (ou o
`AutoMecanic.http` no editor) e o terminal.

> **O ambiente já sobe povoado.** O seed cria 8 Ordens de Serviço distribuídas por todas as
> situações, então o painel operacional e o indicador de tempo médio têm dados reais desde o
> primeiro acesso — você não precisa executar o fluxo antes de mostrar os números.

---

## Roteiro

### 0 · Abertura — 30 s

Apresente o grupo e o problema em uma frase:

> "Uma oficina mecânica de médio porte controlava atendimento, diagnóstico e entrega em
> planilhas. Construímos o back-end que organiza esse fluxo, controla o estoque e deixa o
> cliente acompanhar o serviço."

---

### 1 · Subindo o ambiente — 1 min

**Mostre:** o terminal com `docker compose up -d --build` (pode ser gravação acelerada) e
`docker compose ps` com os dois contêineres saudáveis.

**Diga:**
> "Um único comando sobe banco e API. As migrações são aplicadas e a carga inicial é semeada
> automaticamente — não há passo manual."

**Mostre:** `http://localhost:8080/swagger` abrindo.

---

### 2 · Arquitetura e DDD — 2 min 30 s

**Mostre:** a estrutura de pastas no editor, expandindo `src/`.

**Diga:**
> "Monolito em camadas, com a regra de dependência apontando para dentro. O Domínio não tem
> **nenhum** pacote NuGet — abra o `.csproj` e veja."

**Mostre:** `src/AutoMecanic.Domain/AutoMecanic.Domain.csproj`, destacando o comentário.

**Mostre:** [`docs/03-context-map.md`](03-context-map.md) renderizado no GitHub — o diagrama
dos cinco contextos.

**Diga:**
> "Cinco contextos delimitados. Ordem de Serviço é o núcleo; Clientes, Estoque e Catálogo são
> apoio; Autenticação é genérico. O relacionamento mais interessante é o de parceria entre
> Ordem de Serviço e Estoque — nenhum dos dois manda no outro."

**Mostre:** [`docs/02-event-storming.md`](02-event-storming.md), rolando pelos quatro fluxos.

**Diga:**
> "O Event Storming cobre os quatro fluxos pedidos. Cada post-it laranja virou um `record` de
> evento no código; cada azul, um método público de agregado."

**Mostre:** `src/AutoMecanic.Domain/OrdensServico/Events/OrdemServicoEventos.cs` ao lado do
quadro, para mostrar a correspondência direta.

---

### 3 · Linguagem Ubíqua no código — 1 min

**Mostre:** `OrdemServico.cs`, rolando pelos nomes dos métodos.

**Diga:**
> "`IniciarDiagnostico`, `GerarOrcamento`, `AprovarOrcamento`, `EntregarVeiculo`. São as
> mesmas palavras que o atendente usa. E repare no que **não** existe: não há
> `SetStatus`. O status é sempre consequência de uma ação."

---

### 4 · Autenticação e validação — 1 min 30 s

**Execute** (`AutoMecanic.http`, seções 1 e 2):

1. `GET /clientes` sem token → **401**
2. Login com senha errada → **401**, mensagem genérica

   > "A resposta é idêntica à de e-mail inexistente. Diferenciá-las permitiria descobrir quais
   > contas existem."

3. Login correto → token JWT
4. `POST /clientes` com CPF `111.111.111-11` → **400**

   > "Passa no cálculo do módulo 11, mas não é um CPF válido. O domínio rejeita
   > explicitamente sequências repetidas."

5. Requisição com quatro campos inválidos → **400** com todos os erros de uma vez
6. Placa `PLACA-RUIM` → **400**

---

### 5 · Fluxo completo da Ordem de Serviço — 4 min

Esta é a parte central. Execute as seções 4 a 10 do `AutoMecanic.http`.

**4.1 · Recepção** → status **Recebida**
> "Uma chamada só: identifica o cliente pelo CPF, cadastra o veículo pela placa e abre a OS.
> É o que o atendente faz no balcão."

Destaque o número gerado: `OS-2026-000001`.

**5.1 e 5.2 · Diagnóstico** → status **Em diagnóstico**, laudo registrado.

**6.1 · Incluir serviço**
> "O preço veio do catálogo e foi congelado no item. Um reajuste amanhã não muda este
> orçamento."

**6.2 e 6.3 · Incluir peça — o ponto mais importante da demonstração**

Mostre o `GET /pecas/{id}` **antes e depois**:

| Campo | Antes | Depois |
|---|---|---|
| `quantidadeEmEstoque` | N | **N — não muda** |
| `quantidadeReservada` | 0 | **4** |
| `quantidadeDisponivel` | N | **N − 4** |

> "A peça continua na prateleira, mas já não pode ser prometida a outra OS. É isso que impede
> duas Ordens de Serviço de venderem a mesma última peça — o problema de controle de estoque
> descrito no enunciado."

**7.1 · Gerar orçamento**
> "O valor não é digitado em lugar nenhum. É a soma dos itens com o desconto aplicado."

**7.2 · Enviar** → status **Aguardando aprovação**

**7.3 · Tentar incluir outro serviço** → **422**
> "Os itens estão congelados. O cliente aprova exatamente o que viu."

**8.1 · Acompanhamento pelo cliente**, sem token
> "Número da OS mais documento. Os dois juntos funcionam como prova de posse."

**8.2 · Documento de outro cliente** → **404**
> "Mesma resposta de OS inexistente. Sem isso, daria para percorrer números sequenciais e ler
> dados de terceiros."

**9.1 e 9.2 · Aprovar** → status **Em execução**, e agora o saldo físico cai de 150 para 146.

**9.3 · Razão de estoque**
> "Cada lançamento registra o saldo antes e depois. É gravado na mesma transação que alterou
> o saldo — não existe saldo sem lançamento."

**10.1 · Tentar entregar sem finalizar** → **422**

**10.2 e 10.3 · Finalizar e entregar** → **Finalizada**, depois **Entregue**.

**10.4 · Tentar cancelar** → **422** (estado terminal)

**10.5 · Detalhe da OS** — mostre a linha do tempo com as seis transições.

---

### 6 · Fluxo alternativo: reprovação — 1 min

Execute a seção 11.

> "Nova OS, reserva de 10 unidades, orçamento enviado, cliente reprova. A OS é cancelada e o
> saldo volta exatamente ao que era. Nada saiu do estoque."

Mostre o `GET /pecas/{id}` confirmando a devolução.

---

### 7 · Gestão administrativa e indicadores — 1 min 30 s

Execute as seções 12 e 13.

- Listagem de OS com filtros; busca por documento e por placa
- Alertas de ressuprimento com sugestão de compra
- `GET /indicadores/tempo-medio-execucao`

> "O requisito pede o tempo médio de execução. Devolvemos também a mediana — uma única OS
> excepcionalmente longa distorce a média sem que a operação tenha piorado — e a aderência à
> estimativa, que compara o tempo real com o previsto no catálogo."

---

### 8 · Autorização por perfil — 1 min

Execute a seção 14.

- Criar um usuário Mecânico e autenticar como ele
- Mecânico consultando OS → **200**
- Mecânico tentando criar usuário → **403**
- Mecânico trocando a própria senha → **204**

> "As políticas são nomeadas pela capacidade de negócio, não pelo cargo. E há uma história
> aqui: os endpoints de autoatendimento estavam no controlador de usuários, que exige perfil
> de administrador. O ASP.NET Core combina os atributos `[Authorize]` de classe e ação, então
> nenhum outro perfil conseguia trocar a própria senha. Foi um teste de integração que
> encontrou."

---

### 9 · Testes e cobertura — 1 min 30 s

**Execute:**

```bash
dotnet test
```

**Mostre:** os dois resultados — 439 unitários em ~3 s, 47 de integração em ~20 s.

**Diga:**
> "Os testes de integração sobem um PostgreSQL real via Testcontainers. Não usamos provedor
> em memória de propósito: conversores de objeto de valor, restrições CHECK e o controle de
> concorrência por `xmin` simplesmente não existem fora do PostgreSQL."

**Mostre:** o relatório de cobertura HTML.

| Camada | Cobertura |
|---|---:|
| Application | 97,1% |
| Domain | 90,9% |
| Api | 89,6% |
| Infrastructure | 88,4% |
| **Total** | **92,2%** |

> "O requisito pede 80% nos domínios críticos. Domínio está em 90,9% e Aplicação em 97,1%."

---

### 10 · Segurança — 1 min 30 s

**Execute:**

```bash
dotnet list package --vulnerable --include-transitive
```

> "Nenhuma dependência vulnerável, em nenhum dos seis projetos."

**Mostre:** o resultado do Trivy sobre a imagem.

> "Zero vulnerabilidades críticas ou altas. As dez ocorrências estão em um componente do
> Ubuntu base, o OpenSSL, todas de severidade média ou baixa e com correção já disponível a
> montante."

**Mostre:** a tabela de redução da superfície de ataque em
[`docs/06-relatorio-de-seguranca.md`](06-relatorio-de-seguranca.md).

> "Trocamos a imagem padrão pela variante *chiseled*: de cerca de 90 pacotes de sistema
> operacional para 8, e de 25 vulnerabilidades médias para 4. Sem shell e sem gerenciador de
> pacotes, um atacante que conseguisse execução de comandos não teria ferramentas para se
> movimentar."

**Execute** a seção 16 do `.http` e mostre os cabeçalhos de resposta — e a ausência de `Server`.

---

### 11 · Encerramento — 30 s

> "Resumindo: monolito em camadas com DDD aplicado de verdade, 486 testes cobrindo 92% do
> código, zero vulnerabilidades críticas ou altas, e um `docker compose up` para subir tudo.
> A documentação completa, incluindo Event Storming, Context Map e o relatório de segurança,
> está na pasta `docs` do repositório."

---

## Checklist de cobertura do requisito

Use antes de publicar, para confirmar que nada ficou de fora.

- [ ] Ambiente sobe com `docker compose up`
- [ ] Swagger apresentado
- [ ] Arquitetura em camadas explicada
- [ ] Event Storming apresentado
- [ ] Context Map apresentado
- [ ] Modelo de domínio e Linguagem Ubíqua mostrados no código
- [ ] Identificação do cliente por CPF/CNPJ
- [ ] Cadastro de veículo com placa, marca, modelo e ano
- [ ] Inclusão de serviços e de peças
- [ ] Orçamento gerado automaticamente
- [ ] Envio do orçamento ao cliente
- [ ] Os seis status demonstrados em sequência
- [ ] Alteração automática de status pelas ações
- [ ] Consulta do cliente via API
- [ ] CRUD de clientes, veículos, serviços e peças
- [ ] Controle de estoque com reserva e baixa
- [ ] Listagem e detalhamento de OS
- [ ] Tempo médio de execução
- [ ] Autenticação JWT
- [ ] Validação de CPF/CNPJ e placa
- [ ] Testes unitários e de integração executados
- [ ] Cobertura acima de 80% mostrada
- [ ] Análise de vulnerabilidades apresentada

---

**Voltar ao [índice da documentação](00-visao-geral.md).**
