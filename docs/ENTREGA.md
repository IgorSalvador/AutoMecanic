# Documento de Entrega — Tech Challenge Fase 1

> **Modelo para exportação em PDF.** Preencha os campos marcados com `<<…>>` e exporte
> (VS Code: *Markdown PDF*; ou cole no Google Docs e baixe como PDF).

---

## Identificação

| | |
|---|---|
| **Curso** | Pós-Tech FIAP — Arquitetura de Sistemas .NET com Azure |
| **Turma** | 15SOAT |
| **Fase** | 1 — Tech Challenge |
| **Nome do grupo** | `<<nome do grupo>>` |
| **Data de entrega** | `<<dd/mm/aaaa>>` |

### Participantes

| Nome completo | Username no Discord | RM |
|---|---|---|
| `<<nome>>` | `<<usuario#0000>>` | `<<RM000000>>` |
| `<<nome>>` | `<<usuario#0000>>` | `<<RM000000>>` |
| `<<nome>>` | `<<usuario#0000>>` | `<<RM000000>>` |

---

## Links da entrega

| Item | Link |
|---|---|
| **Repositório (privado)** | `<<https://github.com/usuario/AutoMecanic>>` |
| **Documentação DDD** | `<<link do repositório>>/tree/main/docs` |
| **Vídeo de demonstração** | `<<link do vídeo>>` |
| **Relatório de vulnerabilidades** | `<<link do repositório>>/blob/main/docs/06-relatorio-de-seguranca.md` |

> **Acesso ao avaliador.** O repositório é privado. O usuário indicado pela FIAP foi
> adicionado como colaborador em `<<dd/mm/aaaa>>`.

---

## O projeto

**AutoMecanic — Sistema Integrado de Atendimento e Execução de Serviços.** Back-end de gestão
para uma oficina mecânica de médio porte, cobrindo o ciclo completo do atendimento: recepção
do veículo, diagnóstico, orçamento, execução, entrega e controle de estoque.

### Como executar

```bash
git clone <<url do repositório>> && cd AutoMecanic
cp .env.example .env      # ajuste POSTGRES_PASSWORD, JWT_CHAVE e SEED_SENHA_ADMIN
docker compose up -d --build
```

Swagger em **http://localhost:8080/swagger**. Nenhuma outra ferramenta precisa estar instalada.

### Tecnologias

| Camada | Escolha |
|---|---|
| Linguagem e plataforma | C# 14 · .NET 10 |
| Banco de dados | PostgreSQL 17 (justificativa em `docs/05-arquitetura.md`, ADR-01) |
| Acesso a dados | EF Core 10 · Npgsql |
| Autenticação | JWT HMAC-SHA256 · BCrypt (custo 12) |
| Documentação de API | Swagger / OpenAPI |
| Testes | xUnit · Shouldly · NSubstitute · Testcontainers |
| Contêiner | Docker multiestágio · imagem *chiseled* |

---

## Documentação DDD

Toda a documentação está versionada no repositório, em Markdown com diagramas Mermaid — que o
GitHub renderiza diretamente, sem depender de ferramenta externa.

| Documento | Conteúdo |
|---|---|
| `docs/00-visao-geral.md` | Índice e as cinco ideias que sustentam o modelo |
| `docs/01-linguagem-ubiqua.md` | Vocabulário do negócio e tradução para o código |
| `docs/02-event-storming.md` | Os quatro fluxos, com comandos, eventos, políticas e hotspots |
| `docs/03-context-map.md` | Contextos delimitados e seus relacionamentos |
| `docs/04-modelo-de-dominio.md` | Agregados, entidades, objetos de valor e invariantes |
| `docs/05-arquitetura.md` | Camadas e nove decisões de arquitetura, com seus custos |
| `docs/06-relatorio-de-seguranca.md` | Análise de vulnerabilidades |
| `docs/07-roteiro-do-video.md` | Roteiro da demonstração |

---

## Resultados

### Testes

| Suíte | Verificações | Duração |
|---|---:|---:|
| Unitários | 439 | ~3 s |
| Integração (PostgreSQL real via Testcontainers) | 47 | ~20 s |
| **Total** | **486** | — |

### Cobertura de código

| Camada | Cobertura de linhas |
|---|---:|
| **Application** | **97,1%** |
| **Domain** | **90,9%** |
| Api | 89,6% |
| Infrastructure | 88,4% |
| **Total** | **92,2%** |

> O requisito exige 80% nos domínios críticos. Domínio e Aplicação — onde vivem as regras de
> negócio — estão em 90,9% e 97,1%.

---

## Análise de vulnerabilidades

*Relatório completo em `docs/06-relatorio-de-seguranca.md`.*

### Ferramentas

| Ferramenta | Escopo |
|---|---|
| `dotnet list package --vulnerable --include-transitive` | Dependências .NET, diretas e transitivas |
| Trivy 0.74 (`--scanners vuln,secret,misconfig`) | Imagem de contêiner, segredos e configuração |
| Revisão manual | OWASP API Security Top 10 (2023) |

### Resultado

| Frente | Crítica | Alta | Média | Baixa |
|---|:---:|:---:|:---:|:---:|
| Dependências .NET | 0 | 0 | 0 | 0 |
| Runtime .NET na imagem | 0 | 0 | 0 | 0 |
| Pacotes do sistema operacional | 0 | 0 | **4** | **6** |
| Segredos no repositório e na imagem | 0 | 0 | 0 | 0 |
| Configuração da imagem | 0 | 0 | 0 | 0 |
| **Total** | **0** | **0** | **4** | **6** |

**As 10 ocorrências estão concentradas em um único componente** — o OpenSSL da imagem base
Ubuntu 24.04 (`3.0.13-0ubuntu3.12`) — e são resolvidas pela versão `3.0.13-0ubuntu3.15`, já
disponível a montante. Nenhuma alteração de código é necessária: basta reconstruir a imagem
com `docker compose build --pull`.

### Redução deliberada da superfície de ataque

A imagem final foi migrada da variante padrão do Ubuntu para a variante *chiseled*:

| Métrica | Antes | Depois | Variação |
|---|---:|---:|---:|
| Pacotes de sistema operacional | ~90 | 8 | **−91%** |
| Vulnerabilidades MÉDIA | 25 | 4 | **−84%** |
| Vulnerabilidades ALTA / CRÍTICA | 0 | 0 | — |
| Shell disponível | `/bin/sh`, `/bin/bash` | nenhum | — |
| Gerenciador de pacotes | `apt` | nenhum | — |

### Controles implementados

| Categoria OWASP | Controle |
|---|---|
| API1 — Object Level Authorization | Acompanhamento público exige número da OS **e** documento; resposta idêntica quando não confere |
| API2 — Authentication | BCrypt custo 12; bloqueio após 5 tentativas; sem enumeração de contas; JWT sem tolerância de relógio |
| API3 — Property Level Authorization | Nenhum agregado serializado; todo dado passa por DTO; campos imutáveis fora dos contratos |
| API4 — Resource Consumption | Limite de taxa global e no login; página máxima de 100 itens imposta no tipo |
| API5 — Function Level Authorization | Políticas por capacidade de negócio |
| API6 — Sensitive Business Flows | Regras protegidas no domínio, válidas por qualquer caminho de execução |
| API8 — Security Misconfiguration | Cabeçalhos de segurança; impressão digital do servidor removida; CORS restritivo |

### Defeitos encontrados e corrigidos

Três defeitos foram encontrados pelos **próprios testes automatizados** durante o desenvolvimento:

| # | Severidade | Defeito | Como foi encontrado |
|---|---|---|---|
| 1 | **Alta** | Endpoints de autoatendimento inacessíveis a perfis não administrativos: o ASP.NET Core combina os atributos `[Authorize]` de classe e ação, e a política restritiva da classe barrava o usuário antes da política permissiva da ação | Teste de integração autenticando como atendente |
| 2 | Baixa | Cabeçalho `Server: Kestrel` continuava sendo enviado: o Kestrel o escreve no *flush*, depois de qualquer middleware | Teste ponta a ponta contra o contêiner |
| 3 | **Alta** | Falha total das operações monetárias na imagem endurecida: `Dinheiro` dependia de ICU para `pt-BR`, ausente em imagens mínimas | Teste ponta a ponta após migrar para a imagem *chiseled* |

O primeiro é o mais significativo: **nenhuma ferramenta de varredura o detectaria**, e uma
revisão de código provavelmente também não.

---

## Requisitos atendidos

### Fluxos principais
- ✅ Identificação do cliente por CPF/CNPJ
- ✅ Cadastro de veículo (placa, marca, modelo, ano)
- ✅ Inclusão dos serviços solicitados
- ✅ Inclusão de peças e insumos
- ✅ Orçamento gerado automaticamente
- ✅ Envio do orçamento ao cliente para aprovação

### Acompanhamento
- ✅ Os seis status exigidos, mais *Cancelada*
- ✅ Alteração automática do status conforme as ações
- ✅ Consulta pelo cliente via API

### Gestão administrativa
- ✅ CRUD de clientes, veículos, serviços e peças
- ✅ Controle de estoque
- ✅ Listagem e detalhamento de Ordens de Serviço
- ✅ Monitoramento do tempo médio de execução

### Segurança e qualidade
- ✅ Autenticação JWT para APIs administrativas
- ✅ Validação de dados sensíveis (CPF/CNPJ, placa)
- ✅ Testes unitários e de integração dos principais fluxos

### Requisitos técnicos
- ✅ Back-end monolítico em arquitetura de camadas
- ✅ Banco de dados escolhido e justificado
- ✅ APIs RESTful documentadas via Swagger
- ✅ `Dockerfile` para build da aplicação
- ✅ `docker-compose.yml` orquestrando o ambiente completo
- ✅ Cobertura acima de 80% nos domínios críticos
- ✅ Execução local simples, com README explicativo
- ✅ Repositório privado com acesso ao avaliador

### Entregáveis
- ✅ Vídeo de demonstração — `<<link>>`
- ✅ Documentação DDD com Event Storming, Context Map, modelo de domínio e Linguagem Ubíqua
- ✅ Código-fonte com Dockerfile, docker-compose e README
- ✅ Relatório com análise de vulnerabilidades
- ✅ Este documento de entrega
