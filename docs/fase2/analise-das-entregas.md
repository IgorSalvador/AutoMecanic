# Análise das entregas — Fase 2

Confronto entre o que o enunciado da Fase 2 pede, o que o
[plano de implementação](plano-de-implementacao.md) previu e o que está de fato na `main`
(posição de 03/11/2026, commit `033fb7d`, depois do merge do README da Fase 2).

**Resumo:** todo o código, os manifestos, o Terraform, os pipelines e o README da Fase 2 estão
na `main`, o CD já rodou com sucesso na AWS e a versão foi marcada com a tag `v2.0.0`. Falta o
restante da **trilha F**: relatório de segurança, documento de entrega e vídeo.

---

## 1. Requisitos do enunciado

| Requisito | Situação | Onde está |
|---|:-:|---|
| Refatoração com Clean Code e Clean Architecture | ✅ | Um caso de uso por classe em `Application/OrdensServico/CasosDeUso/` (A3); relógio fora do domínio com `BannedApiAnalyzers` (A2); dívidas técnicas da Fase 1 corrigidas (A1) |
| Testes automatizados dos fluxos críticos | ✅ | Unitários, integração (PostgreSQL real via Testcontainers) e **testes de arquitetura** com NetArchTest (A4) |
| Abertura de OS completa (cliente, veículo, serviços e peças) | ✅ | `POST /ordens-servico/recepcao`, tudo na mesma transação, com reserva de estoque (A5) |
| Consulta de status da OS | ✅ | `GET /ordens-servico/{id}/status` (A6) |
| Aprovação e recusa do orçamento pelo cliente | ✅ | `POST /acompanhamento/orcamento/aprovar` e `/reprovar`, com número da OS + CPF/CNPJ (A7) |
| Listagem priorizada, sem finalizadas e entregues | ✅ | `GET /ordens-servico`: Em execução > Aguardando aprovação > Em diagnóstico > Recebida; exclui também as canceladas (A8) |
| Atualização de status por e-mail | ✅ | Outbox transacional + *worker* com `FOR UPDATE SKIP LOCKED` e SMTP (Gmail na nuvem, Mailpit local) (A9) |
| Dockerfile e docker-compose | ✅ | Labels OCI; Mailpit no compose (B1, B2) |
| Kubernetes em `/k8s`: Deployment, Service, ConfigMap, Secret, HPA | ✅ | Kustomize com `base/` e overlays `local/` (kind) e `aws/` (EKS + NLB); Job de migração e CronJob de expiração (C1–C3) |
| Demonstração do HPA | ✅ | Job k6 em `k8s/overlays/local/carga`, HPA de 2 a 6 réplicas (C4) |
| Terraform em `/infra`: cluster e banco | ✅ | Três camadas: `00-bootstrap` (estado em S3), `10-base` (VPC, ECR, RDS, OIDC, Budgets), `20-cluster` (EKS) (D1–D4) |
| CI/CD: build, testes, imagem, deploy do cluster, do banco e dos manifestos | ✅ | `ci.yml` (testes, cobertura, Trivy, kubeconform, Terraform, Newman); `cd.yml` (OIDC, ECR, Terraform, migração, rollout, teste de fumaça), já executado com sucesso na AWS; `destruir-cluster.yml` (madrugada) (E1–E3) |
| README com objetivos, arquitetura e instruções | ✅ | Reescrito para a Fase 2 (F1); falta só o link do vídeo |
| Desenho da arquitetura | ✅ | Três diagramas Mermaid no README: componentes, infraestrutura AWS e fluxo de deploy |
| Collection de APIs | ✅ | Pasta *00 · Fase 2* na collection Postman, executada no CI e no CD (F3) |
| Vídeo de até 15 minutos | ⬜ | A gravar; ver os riscos da seção 3 |

---

## 2. Pendências antes da entrega

Em ordem de prioridade.

| # | Pendência | Tarefa | O que fazer |
|---|---|:-:|---|
| 1 | **Link do vídeo no README** | F1 | Substituir o marcador "A PREENCHER" no topo e marcar o vídeo como ✅ em "Requisitos atendidos" |
| 2 | **Guardar a evidência do deploy** | E2 | O CD já rodou com sucesso: guardar o link da execução e capturas para o vídeo e o `ENTREGA.md` |
| 3 | **Relatório de segurança sem a Fase 2** | F4 | `docs/06-relatorio-de-seguranca.md` foi alterado pela última vez em 21/10. Acrescentar: rota pública de aprovação (limite próprio, resposta idêntica), segredo SMTP, OIDC sem chave de acesso, Secret fora do git, RDS em subnet privada, NLB com `externalTrafficPolicy: Local`, e os resultados da seção 4 abaixo |
| 4 | **Cobertura desatualizada no README** | F1 | Os 598 testes conferem, mas a tabela de cobertura ainda é a da Fase 1 (92,2%). Os valores atuais estão na seção 4 |
| 5 | **`ENTREGA.md` é o da Fase 1** | F6 | Reescrever para a Fase 2 e exportar o PDF do portal |
| 6 | **ADR-15 marcado "em implementação"** | F2 | Retirar a marcação em `docs/05-arquitetura.md` |
| 7 | **Plano com situação desatualizada** | — | C3, D1–D3, E2 e E3 estão 🟨 em `plano-de-implementacao.md`, mas já estão na `main` |
| 8 | **Vídeo** | F5 | Gravar e incluir o link no README e no `ENTREGA.md` |

---

## 3. Riscos para a gravação

| Risco | Consequência | Como evitar |
|---|---|---|
| O cluster leva ~15 min para subir | Não cabe no vídeo | Rodar o CD **antes** de gravar e mostrar a execução já concluída |
| O teste de fumaça do CD usa `marina.souza@exemplo.com.br` | O Gmail tenta entregar em um domínio que não existe; a cliente Marina já existe no banco com esse e-mail | Na demonstração, abrir a OS com **outro CPF** e um **e-mail real** do grupo |
| Limite de taxa de produção (300 req/min por IP) | O k6 parte de um único IP, recebe `429` e o HPA não escala | Elevar os limites no ConfigMap antes da gravação, como descrito em `k8s/overlays/aws/README.md` |
| O destroy agendado roda às 03:00 | O cluster some se a gravação atravessar a madrugada | Gravar no mesmo dia do deploy |
| E-mail do Gmail cair em spam | O e-mail "não chega" na hora | Testar antes; deixar a caixa de spam aberta em outra aba |
| Custo | ~US$ 0,25/h ligado | Destruir o cluster logo após gravar (workflow *Destruir cluster*) |

---

## 4. Testes e varredura de vulnerabilidades (03/11/2026, commit `033fb7d`)

### Testes e cobertura

| Suíte | Verificações | Resultado |
|---|---:|:-:|
| Arquitetura | 22 | ✅ |
| Unitários | 475 | ✅ |
| Integração (PostgreSQL real) | 101 | ✅ |
| **Total** | **598** | **0 falhas** |

| Camada | Cobertura de linhas |
|---|---:|
| Application | 98,6% |
| Domain | 92,6% |
| Api | 86,7% |
| Infrastructure | 81,6% |
| **Total** | **91,0%** |

### Dependências e imagem

| Frente | Ferramenta | Crítica | Alta | Média | Baixa |
|---|---|:-:|:-:|:-:|:-:|
| Pacotes NuGet, diretos e transitivos (7 projetos) | `dotnet list package --vulnerable`, contra o nuget.org | 0 | 0 | 0 | 0 |
| Runtime .NET 10.0.14 na imagem | Trivy 0.69.3 | 0 | 0 | 0 | 0 |
| Pacotes do SO na imagem (Ubuntu 24.04 *chiseled*) | Trivy 0.69.3 | 0 | 0 | **2** | 0 |
| Segredos no repositório e na imagem | Trivy 0.69.3 | 0 | 0 | 0 | 0 |

As duas médias são da `libc6` (CVE-2026-18374 e CVE-2026-89092), **ainda sem versão corrigida**.
Basta reconstruir a imagem com `--pull` quando a correção sair. O portão do CI (críticas e altas)
passa.

> A varredura de pacotes precisa da opção `--source https://api.nuget.org/v3/index.json` nesta
> máquina: sem ela, o `dotnet` consulta só a pasta local de pacotes do SDK, que não tem dados de
> vulnerabilidade, e o resultado "nenhum pacote vulnerável" não significa nada.

### Configuração (Trivy `fs --scanners misconfig` em `infra/` e `k8s/`)

São 3 críticas, 6 altas, 16 médias e 21 baixas. Nenhuma bloqueia a entrega, mas o relatório de
segurança deve registrá-las como **riscos aceitos** ou **falsos positivos**:

| Achado | Severidade | Avaliação |
|---|:-:|---|
| Endpoint do EKS público e aberto a `0.0.0.0/0` (AWS-0040, AWS-0041) | Crítica | Risco aceito: o CD roda em runners do GitHub, sem IP fixo. O acesso exige IAM (*access entries*) |
| Nós com saída liberada para qualquer IP (AWS-0104) | Crítica | Risco aceito: sem NAT Gateway, os nós precisam sair para o ECR e para o Gmail |
| Subnets públicas atribuem IP público (AWS-0164) | Alta | Consequência da decisão de não usar NAT (economia de ~US$ 33/mês) |
| Secrets do Kubernetes sem criptografia KMS no EKS (AWS-0039) | Alta | Melhoria possível: `encryption_config` no módulo do EKS |
| Tags do ECR mutáveis (AWS-0031) | Alta | Melhoria simples: `image_tag_mutability = "IMMUTABLE"`; o CD já publica uma tag por commit |
| Bucket do estado sem chave KMS própria (AWS-0132) | Alta | Risco aceito: usa a criptografia gerenciada pela AWS (SSE-S3) |
| PostgreSQL local roda como root e com disco gravável (KSV-0012, KSV-0014) | Alta · Média | Só no overlay `local` (kind); não vai para a AWS |
| Imagem roda como root (DS-0002) | Alta | **Falso positivo:** a imagem *chiseled* já roda com o UID 1654 sem precisar de `USER` |
| ConfigMap com conteúdo sensível (KSV-01010) | Média | **Falso positivo:** são nomes de chaves `Jwt__*` (emissor, audiência, validade), não segredos |
| Tag `:latest` no `base` (KSV-0013) | Média | O overlay `aws` troca pela tag do commit no deploy |
| RDS: retenção de backup de 1 dia, sem proteção contra exclusão, sem autenticação IAM (AWS-0077, 0176, 0177) | Média | Risco aceito para um ambiente de demonstração destruído com frequência |
| Logs do plano de controle do EKS e *flow logs* da VPC desligados (AWS-0038, AWS-0178) | Média | Risco aceito por custo (CloudWatch) |
