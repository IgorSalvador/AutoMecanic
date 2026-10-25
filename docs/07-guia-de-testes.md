# Guia de testes

> **O que é.** Como verificar, passo a passo, tudo o que foi construído: testes automatizados,
> `docker compose`, cluster Kubernetes local, e-mails, autoescalonamento e coleção Postman.
> A [Parte 2](#parte-2--aws) trata da nuvem e diz com franqueza o que ainda não existe.
>
> Os comandos da Parte 1 foram executados e conferidos. Os da Parte 2 são um roteiro e estão
> marcados como tal.

## Sumário

- [Parte 1 · Local](#parte-1--local)
  - [1. Pré-requisitos](#1-pré-requisitos)
  - [2. Testes automatizados](#2-testes-automatizados)
  - [3. Docker Compose](#3-docker-compose)
  - [4. Coleção Postman](#4-coleção-postman)
  - [5. E-mails no Mailpit](#5-e-mails-no-mailpit)
  - [6. Kubernetes local (kind)](#6-kubernetes-local-kind)
  - [7. Autoescalonamento (HPA)](#7-autoescalonamento-hpa)
  - [8. Validar os manifestos sem cluster](#8-validar-os-manifestos-sem-cluster)
- [Parte 2 · AWS](#parte-2--aws)
- [Problemas conhecidos](#problemas-conhecidos)

---

## Parte 1 · Local

### 1. Pré-requisitos

| Ferramenta | Para quê | Obrigatória em |
|---|---|---|
| Docker Desktop (com ~8 GB de memória) | Contêineres, Testcontainers, kind | tudo |
| .NET SDK 10 | `dotnet test` | seção 2 |
| Node.js 20+ | Newman e gerador da coleção | seção 4 |
| `kubectl` (já vem com o Docker Desktop) | Kubernetes | seções 6 a 8 |
| [`kind`](https://kind.sigs.k8s.io/docs/user/quick-start/#installation) | Cluster local | seções 6 e 7 |

Confira:

```bash
docker version --format '{{.Server.Version}}'
dotnet --version
kubectl version --client
kind version
```

> No Windows, prefira o **PowerShell** para os scripts do cluster e o **Git Bash** para os
> demais comandos. Onde houver diferença, ela está indicada.

---

### 2. Testes automatizados

O Docker precisa estar **em execução**: os testes de integração sobem um PostgreSQL real.

```bash
dotnet test
```

Resultado esperado — **598 verificações, 0 falhas**:

| Projeto | Verificações | Cobre |
|---|---:|---|
| `AutoMecanic.UnitTests` | 475 | Domínio e casos de uso, sem I/O (~3 s) |
| `AutoMecanic.ArchitectureTests` | 22 | Regras de dependência entre camadas |
| `AutoMecanic.IntegrationTests` | 101 | API completa com PostgreSQL (~1 min) |

Para rodar um projeto só:

```bash
dotnet test tests/AutoMecanic.UnitTests
dotnet test tests/AutoMecanic.ArchitectureTests
dotnet test tests/AutoMecanic.IntegrationTests
```

**Se os testes de integração falharem com** `Docker is either not running or misconfigured`, o
Docker Desktop não está ligado.

---

### 3. Docker Compose

Sobe o banco, a API e o Mailpit.

```bash
cp .env.example .env
```

Edite o `.env` e troque os três valores obrigatórios (`POSTGRES_PASSWORD`, `JWT_CHAVE` e
`SEED_SENHA_ADMIN`). Depois:

```bash
docker compose up -d --build
docker compose ps
```

Os três serviços devem aparecer como `healthy`: `automecanic-api`, `automecanic-banco` e
`automecanic-mailpit`.

**Verificações**

```bash
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:8080/health/pronto   # 200
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:8080/swagger/v1/swagger.json   # 200
```

| O que abrir | Endereço |
|---|---|
| Swagger | http://localhost:8080/swagger |
| Mailpit | http://localhost:8025 |

**Login e uso do Swagger**

```bash
curl -X POST http://localhost:8080/api/v1/autenticacao/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@automecanic.com.br","senha":"<SEED_SENHA_ADMIN do .env>"}'
```

Copie o `token` e informe-o no botão **Authorize** do Swagger.

**Labels da imagem (B1)**

```bash
docker image inspect automecanic-api:2.0.0 --format '{{json .Config.Labels}}'
```

Devem constar `org.opencontainers.image.version` e `org.opencontainers.image.revision`.
Para preencher a revisão com o commit atual: `GIT_REVISAO=$(git rev-parse --short HEAD) docker compose up -d --build`.

**Encerrar**

```bash
docker compose down        # mantém os dados
docker compose down -v     # remove também o volume do banco
```

---

### 4. Coleção Postman

Com a API no ar (compose ou kind):

```bash
npx --yes newman run postman/AutoMecanic.postman_collection.json \
  --folder "00 · Fluxo completo da Ordem de Serviço" \
  --env-var "senhaAdmin=<SEED_SENHA_ADMIN>"

npx --yes newman run postman/AutoMecanic.postman_collection.json \
  --folder "00 · Fase 2 — abertura completa, resposta do cliente e fila" \
  --env-var "senhaAdmin=<SEED_SENHA_ADMIN>"
```

| Pasta | Requisições | Asserções | Falhas esperadas |
|---|---:|---:|---:|
| Fluxo completo da Ordem de Serviço | 25 | 51 | 0 |
| Fase 2 | 23 | 45 | 0 |

As duas pastas podem ser repetidas. A da **Fase 2** exercita: recepção completa com serviços e
peças, consulta de status, aprovação e recusa **pelo próprio cliente** (rota pública), fila
priorizada e histórico.

**No Postman (interface):** *Import* → os dois arquivos de `postman/` → ambiente
*AutoMecanic — Local* → ajuste `senhaAdmin` → execute a pasta desejada no *Collection Runner*.

**Sem Postman:** [`AutoMecanic.http`](../AutoMecanic.http) com a extensão *REST Client* do VS Code.
A seção 17 cobre a Fase 2.

**Regerar a coleção** (depois de mudar a API):

```bash
node tools/gerar-collection.mjs                # a API precisa estar no ar
```

---

### 5. E-mails no Mailpit

A cada mudança de situação da OS, o cliente recebe um e-mail: OS aberta, orçamento enviado,
em execução, finalizada, entregue e cancelada.

1. Execute a pasta da Fase 2 (seção 4) ou o fluxo manual no Swagger.
2. Abra http://localhost:8025.
3. Confira que há mensagens para `marina.souza@exemplo.com.br`, e que a do **orçamento
   enviado** traz o número da OS e as instruções de aprovação.

Pela linha de comando:

```bash
curl -s http://localhost:8025/api/v1/messages | grep -o '"total":[0-9]*'
```

O envio só acontece com `Notificacoes__Habilitadas=true` (já é o padrão do compose e do kind).
As mensagens ficam na tabela `mensagem_saida` e são enviadas em segundo plano, a cada ~5 s, por
isso podem levar alguns segundos para aparecer.

**Para enviar de verdade pelo Gmail:** ative a verificação em duas etapas, gere uma **senha de
app** e ajuste no `.env`:

```bash
SMTP_HOST=smtp.gmail.com
SMTP_PORTA=587
SMTP_SEGURANCA=StartTls
SMTP_USUARIO=seu@gmail.com
SMTP_SENHA=<senha de app>
SMTP_REMETENTE=seu@gmail.com
```

---

### 6. Kubernetes local (kind)

O mesmo conjunto de manifestos da nuvem, em um cluster de um nó. **Pare o compose antes**
(`docker compose down`): ambos usam as portas 8080 e 8025.

**Subir**

```powershell
# Windows
.\k8s\overlays\local\subir-cluster.ps1
```

```bash
# Linux / macOS / Git Bash
./k8s/overlays/local/subir-cluster.sh
```

O script cria o cluster `automecanic`, constrói a imagem, instala o metrics-server, aplica os
manifestos, espera a migração e reinicia a API. Leva alguns minutos na primeira vez e termina
com a mensagem **"Pronto."**. Ele pode ser repetido: reaproveita o cluster.

**Verificar**

```bash
kubectl -n automecanic get pods
```

| Recurso | Estado esperado |
|---|---|
| `automecanic-api-…` (2 pods) | `1/1 Running` |
| `postgres-0` | `1/1 Running` |
| `mailpit-…` | `1/1 Running` |
| `automecanic-migracao-…` | `Completed` |

```bash
kubectl -n automecanic get hpa                        # CPU e memória com percentuais, não <unknown>
curl http://localhost:8080/health/pronto              # 200
kubectl -n automecanic logs job/automecanic-migracao  # "Banco de dados pronto."
```

Depois, rode as pastas do Postman (seção 4) contra `http://localhost:8080`: o cluster local
responde no mesmo endereço do compose.

**CronJob de expiração de orçamentos**

```bash
kubectl -n automecanic get cronjob
kubectl -n automecanic create job --from=cronjob/automecanic-expiracao teste-expiracao
kubectl -n automecanic wait --for=condition=complete job/teste-expiracao --timeout=120s
kubectl -n automecanic logs job/teste-expiracao      # "Rotina de expiração concluída: 0 orçamento(s) expirado(s)."
kubectl -n automecanic delete job teste-expiracao
```

> A linha `Cannot load library libgssapi_krb5.so.2` nos logs é inofensiva: a imagem enxuta não
> traz a biblioteca de autenticação Kerberos, que a aplicação não usa.

**Atualizar depois de mudar o código:** rode o script de novo. Ele reconstrói a imagem, recria o
Job de migração (o template de um Job é imutável) e reinicia a API.

**Remover tudo**

```powershell
.\k8s\overlays\local\subir-cluster.ps1 -Destruir     # ./subir-cluster.sh --destruir no Linux
```

---

### 7. Autoescalonamento (HPA)

Com o cluster da seção 6 de pé, em **dois terminais**.

**Terminal 1 — acompanhar**

```bash
kubectl -n automecanic get hpa -w
```

**Terminal 2 — gerar carga**

```bash
kubectl apply -k k8s/overlays/local/carga
kubectl -n automecanic logs -f job/carga-k6
```

O teste sobe a 40 usuários virtuais em 30 s, segura o pico por 3 min e desce.

**O que deve acontecer**

| Momento | Réplicas | CPU no HPA |
|---|---:|---|
| Antes | 2 | ~10% |
| ~30 s depois do início | sobe até **6** | passa de 400% do alvo de 70% |
| Durante o pico | 6 | muito acima do alvo |
| ~3 min depois do fim | volta a **2** | cai para ~10% |

O Job termina com `http_req_failed ... rate<0.05` aprovado (esperado: cerca de 0,05% de falhas,
todas de login na rajada inicial).

**Acompanhar os pods**

```bash
kubectl -n automecanic get pods -w
```

**Limpar e repetir**

```bash
kubectl delete -k k8s/overlays/local/carga
```

> **Atenção:** a descida leva alguns minutos de propósito. Se o HPA ficar preso acima de 2
> réplicas, confira o uso de memória em `kubectl -n automecanic get hpa`: com requests de memória
> pequenos demais ele nunca volta ao mínimo (foi o defeito encontrado e corrigido no C4).

---

### 8. Validar os manifestos sem cluster

Não exige cluster. Renderiza os manifestos e valida contra o esquema do Kubernetes.

```bash
kubectl kustomize k8s/base                 # 9 recursos
kubectl kustomize k8s/overlays/local       # 15 recursos
kubectl kustomize k8s/base | docker run --rm -i ghcr.io/yannh/kubeconform:latest -strict -summary -
```

Esperado: `Valid: 9, Invalid: 0` para o `base`, e `Valid: 15, Invalid: 0` para o overlay local.

---

## Parte 2 · AWS

> ### Situação: ainda não é possível testar na AWS
>
> A infraestrutura em nuvem **ainda não foi construída**. O que existe hoje é a preparação:
> a imagem, os manifestos do `k8s/base` e o desenho em
> [ADR-15](05-arquitetura.md#adr-15--nuvem-eks-rds-e-ecr-com-terraform-em-camadas-decidido-em-implementação).
> Os itens abaixo são o **roteiro de teste** para quando cada tarefa do
> [plano da Fase 2](fase2/plano-de-implementacao.md) for concluída. Os comandos exatos serão
> confirmados e preenchidos em cada tarefa.

### O que falta para testar na AWS

| Tarefa | O que entrega | Situação |
|---|---|:-:|
| D1 · `infra/00-bootstrap` | Bucket S3 do estado do Terraform | ⬜ |
| D2 · `infra/10-base` | Rede, ECR, RDS, papel OIDC, Budgets | ⬜ |
| D3 · `infra/20-cluster` | EKS e nós | ⬜ |
| C3 · `k8s/overlays/aws` | Service LoadBalancer, imagem do ECR, RDS, SMTP do Gmail | ⬜ |
| E1 a E3 · GitHub Actions | CI ampliado, `cd.yml`, `destruir-cluster.yml` | ⬜ |

### Antes de qualquer teste

- **Custo.** O ambiente completo custa cerca de **US$ 0,25 por hora** ligado (≈ US$ 185 por mês
  se esquecido). Só suba para testar e **destrua ao terminar**. Os alarmes do AWS Budgets em
  US$ 20, 50 e 100 já estão configurados (tarefa P3).
- **Credenciais.** Cada integrante com acesso IAM e MFA, região `us-east-1`.
- **Ferramentas:** AWS CLI, Terraform e `kubectl`, além das da Parte 1.
- **Segredos do GitHub:** `AWS_ROLE_ARN`, `JWT_CHAVE`, `SEED_SENHA_ADMIN`, `SMTP_USUARIO` e
  `SMTP_SENHA`. Nenhum deve ir para o repositório.

### Roteiro de teste (a executar quando as tarefas estiverem prontas)

**1. Infraestrutura — após D1 a D3**

- [ ] `terraform init` e `plan` em cada camada, sem erro.
- [ ] `apply` do `00-bootstrap`, depois `10-base`, depois `20-cluster`, nessa ordem.
- [ ] `aws eks update-kubeconfig` e `kubectl get nodes` mostrando 2 nós `Ready`.
- [ ] `terraform destroy` do `20-cluster` sem deixar recursos (conferir no console da AWS).
- [ ] O banco e a rede permanecem de pé depois de destruir o cluster.

**2. Aplicação no cluster — após C3**

- [ ] A imagem está no ECR com a tag do commit.
- [ ] Job de migração `Completed` contra o RDS.
- [ ] Pods da API `Running` e `Ready`; `kubectl get hpa` com métricas.
- [ ] `kubectl get svc automecanic-api` com o endereço do load balancer.
- [ ] `curl http://<endereço>/health/pronto` responde 200.

**3. Funcionalidade — com o endereço do load balancer**

- [ ] Newman contra o endereço da AWS: as duas pastas da seção 4, 0 falhas
  (`--env-var "baseUrl=http://<endereço>"`).
- [ ] E-mail real chegando na caixa do cliente pelo Gmail.
- [ ] Teste de carga (k6) mostrando o HPA de 2 a 6 réplicas, repetindo a seção 7.

**4. Pipeline — após E1 a E3**

- [ ] Pull request: `ci.yml` valida os manifestos (`kubeconform`) e o Terraform (`fmt`, `validate`).
- [ ] Push na `main` ou disparo manual: `cd.yml` constrói, testa, publica no ECR, aplica o
  Terraform, migra e atualiza o cluster, e roda o teste de fumaça com o Newman.
- [ ] O *environment* `aws` exige aprovação antes do deploy.
- [ ] `destruir-cluster.yml` (manual e agendado) remove só a camada `20-cluster`.

**5. Encerramento**

- [ ] Cluster destruído e **nenhum** recurso cobrando (EKS, nós, load balancer, IPs públicos).

---

## Problemas conhecidos

| Sintoma | Causa e solução |
|---|---|
| `Docker is either not running or misconfigured` nos testes de integração | O Docker Desktop não está ligado. |
| `subir-cluster` falha com `connection refused` no meio da execução | O nó do kind foi encerrado, em geral por falta de memória no Docker Desktop. Dê mais memória (≥ 8 GB), rode `docker start automecanic-control-plane` e repita o script. |
| Porta 8080 ou 8025 ocupada | O compose e o kind usam as mesmas portas. Pare um antes de subir o outro. |
| `kubectl get hpa` mostra `<unknown>` | O metrics-server ainda não coletou. Aguarde ~1 minuto; confirme com `kubectl -n kube-system get pods`. |
| Pod do Mailpit em `CrashLoopBackOff` | Precisa de `/tmp` gravável (já resolvido no manifesto). Se reaparecer, veja `kubectl logs`. |
| Newman falha no login | Confira `--env-var "senhaAdmin=…"`: deve ser o `SEED_SENHA_ADMIN` do `.env` (no kind, `Admin@AutoMecanic2026`). |
| Respostas `429` durante o teste de carga | Os limites de taxa de produção foram aplicados. O overlay local os eleva; use-o. |
| Caracteres acentuados quebrados no PowerShell | O `.ps1` precisa estar salvo como UTF-8 **com BOM** (já está). |

---

**Voltar:** [Visão geral](00-visao-geral.md).
