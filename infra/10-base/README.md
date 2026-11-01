# 10-base — rede, repositório de imagens, banco e acesso do CI

A camada que **fica de pé** entre uma demonstração e outra. Depende só do bucket do estado
([`00-bootstrap`](../00-bootstrap/README.md)).

| Recurso | Arquivo | Para quê | Custo aproximado |
|---|---|---|---|
| VPC em 2 zonas, subnets públicas e privadas, Internet Gateway | `rede.tf` | A rede onde tudo roda. **Sem NAT Gateway** | US$ 0 (os IPs públicos dos nós, na camada seguinte, custam) |
| Repositório ECR `automecanic-api` | `ecr.tf` | Guarda as imagens; mantém as 10 mais recentes | centavos |
| RDS PostgreSQL 17, `db.t4g.micro`, 20 GB gp3, zona única | `banco.tf` | O banco, em subnet privada, sem acesso público | **~US$ 0,019/h** (~US$ 14/mês + armazenamento) |
| Provedor OIDC do GitHub e papel `automecanic-ci` | `ci-oidc.tf` | O GitHub Actions entra na AWS sem chave guardada | US$ 0 |
| Alarmes no AWS Budgets (US$ 20, 50 e 100) | `orcamentos.tf` | Avisam por e-mail quando o gasto passa do valor | US$ 0 (os dois primeiros orçamentos da conta são grátis) |

> ⚠️ **O RDS cobra enquanto existir**, mesmo sem uso: cerca de US$ 14 a US$ 17 por mês. Se for
> ficar dias sem trabalhar no projeto, **destrua esta camada** (seção
> [Desligar](#desligar-e-parar-de-pagar)). Quem recria é o `apply`, e o Job de migração
> repovoa o banco.

---

## Pré-requisitos

- O D1 concluído (bucket do estado criado) e `aws sts get-caller-identity` funcionando, como no
  [guia AWS do zero](../../docs/08-guia-aws-do-zero.md).
- Terraform 1.10 ou superior.
- O arquivo `versions.tf` desta pasta já aponta para o bucket `automecanic-tfstate-<conta>`. Se o
  seu bucket tem outro nome, ajuste o bloco `backend "s3"`.

---

## Passo 1 · Informar os e-mails dos alarmes

```powershell
cd infra\10-base
Copy-Item terraform.tfvars.exemplo terraform.tfvars
notepad terraform.tfvars        # coloque os e-mails de quem deve receber os alarmes
```

O `terraform.tfvars` **não vai para o git** (está no `.gitignore`).

### Já criou os alarmes à mão?

O guia AWS do zero (passo 5) manda criar os três orçamentos pelo console. Se você os criou, há
duas saídas. **Escolha uma:**

**A) Deixar os manuais e desligar esta parte** (mais simples). No `terraform.tfvars`:

```hcl
criar_orcamentos = false
```

**B) Passar a gerenciá-los pelo Terraform.** Depois do `terraform init` (passo 2), importe cada um
(troque `123456789012` pelo número da sua conta e confira o nome exato no console):

```powershell
terraform import 'aws_budgets_budget.alerta["20"]'  123456789012:automecanic-alerta-20
terraform import 'aws_budgets_budget.alerta["50"]'  123456789012:automecanic-alerta-50
terraform import 'aws_budgets_budget.alerta["100"]' 123456789012:automecanic-alerta-100
```

Os nomes precisam bater com os criados no console (`automecanic-alerta-20` etc.). Se não
baterem, apague os manuais e deixe o Terraform criar.

> Mais de dois orçamentos por conta passam a ser cobrados (centavos por dia cada). Não deixe
> cópias duplicadas.

---

## Passo 2 · Planejar e aplicar

```powershell
terraform init
terraform plan
```

O `init` lê o bucket e prepara o estado `10-base/terraform.tfstate`. O `plan` deve terminar com
algo próximo de **`Plan: 25 to add, 0 to change, 0 to destroy`** (22 sem os alarmes): a quantidade exata varia
com os alarmes. Leia a lista; **nada deve aparecer para destruir ou alterar**. Procure:

- `aws_db_instance.principal` com `publicly_accessible = false` e `storage_encrypted = true`;
- `aws_iam_role.ci` com a confiança restrita ao seu repositório.

Se estiver certo:

```powershell
terraform apply
```

Responda `yes`. **O RDS leva de 5 a 10 minutos** para ficar pronto; é normal.

### Conferir

```powershell
terraform output
terraform output -raw ci_papel_arn
```

| Output | Para quê |
|---|---|
| `ci_papel_arn` | Vira o segredo `AWS_ROLE_ARN` do GitHub |
| `ecr_repositorio_url` | Para onde o CD publica a imagem |
| `banco_cadeia_de_conexao` (sensível) | Vira `ConnectionStrings__PostgreSQL` no Secret do cluster |
| `subnets_publicas` / `vpc_id` | Usados pela camada `20-cluster` |

Os valores sensíveis não aparecem no `terraform output` sem `-raw` e nome. **Nunca** cole a
cadeia de conexão em mensagens, no git ou em prints.

No console da AWS (região **us-east-1**), confirme: **VPC** (`automecanic-vpc`), **ECR**
(`automecanic-api`), **RDS** (`automecanic-banco`, status *Available*) e **IAM → Roles**
(`automecanic-ci`).

---

## O que o banco tem de especial

- **Sem acesso público.** Está em subnets privadas e o grupo de segurança só aceita a porta 5432
  vinda da VPC. Do seu computador **não dá** para conectar direto; é por isso que a migração roda
  como um Job dentro do cluster.
- **SSL obrigatório** (padrão do PostgreSQL 15+ no RDS). A cadeia de conexão já inclui
  `SSL Mode=Require`.
- **Senha gerada** pelo Terraform e guardada no estado (que fica criptografado no S3). Para vê-la:
  `terraform output -raw banco_senha`.
- **Sem snapshot final** ao destruir: o banco é descartável. **Ao destruir esta camada os dados
  se perdem.** Aceitável aqui, porque o seed e a migração o recriam.

---

## Segurança do papel do CI

O papel `automecanic-ci` só pode ser assumido por:

1. workflows do repositório `IgorSalvador/AutoMecanic` **na branch `main`**; ou
2. jobs desse repositório que usam o *environment* **`aws`** do GitHub (que deve exigir
   aprovação manual).

Nenhum outro repositório, branch ou *fork* consegue. **Limitação consciente:** o papel tem
`AdministratorAccess`, porque o CD aplica Terraform que cria rede, banco e cluster. A proteção
está em quem pode assumi-lo, não no que ele faz.

---

## Desligar e parar de pagar

### Opção 1 · Pausar o banco por alguns dias (não destrói nada)

O RDS pode ser **parado por até 7 dias** (depois a AWS o religa sozinha). Pelo console: **RDS →
Databases → automecanic-banco → Actions → Stop temporarily**. Ou:

```powershell
aws rds stop-db-instance --db-instance-identifier automecanic-banco
aws rds start-db-instance --db-instance-identifier automecanic-banco   # para religar
```

Parado, **o computador do banco não cobra, mas o armazenamento (20 GB) continua cobrando**
(~US$ 2,30/mês), e a AWS o liga de novo depois de 7 dias. Serve para pausas curtas.

> Se o Terraform criou o banco, pare/ligue **só por esses comandos**: não altere o banco
> clicando em configurações, para o estado não divergir.

### Opção 2 · Destruir tudo desta camada (zera o custo)

**Antes**, destrua a camada `20-cluster` (ela depende desta). Só depois:

```powershell
cd infra\10-base
terraform destroy
```

Leia a lista (deve conter os mesmos recursos do `apply`) e responda `yes`. Leva alguns minutos.
Isso apaga o banco **e os dados**, o repositório ECR **e as imagens**, a VPC e o papel do CI.

Confira no console que **não sobrou nada**:

| Onde | O que deve estar vazio |
|---|---|
| RDS → Databases | nenhuma instância |
| RDS → Snapshots (manual) | nenhum snapshot manual |
| ECR → Repositories | nenhum `automecanic-api` |
| VPC → Your VPCs | só a VPC padrão da AWS |
| VPC → Elastic IPs | nenhum endereço alocado |
| EC2 → Volumes | nenhum volume disponível |

> **Os alarmes do Budgets** (se o Terraform os criou) também são removidos no `destroy`. Se preferir
> mantê-los sempre, deixe-os criados à mão e use `criar_orcamentos = false`.

### Opção 3 · Sempre que terminar de trabalhar (rotina)

1. Destrua o cluster (`20-cluster`), a parte mais cara.
2. Se não vai usar por mais de uma semana, destrua também esta camada.
3. Olhe o **Billing → Bills** para confirmar que o custo parou.

---

## Problemas comuns

| Sintoma | Causa e solução |
|---|---|
| `Error: Failed to get existing workspaces` / `NoSuchBucket` | O nome do bucket em `versions.tf` não é o do seu. Confira `terraform output nome_do_bucket` na pasta `00-bootstrap`. |
| `BudgetAlreadyExists` | Os alarmes já existem. Importe-os ou use `criar_orcamentos = false` (ver Passo 1). |
| `EntityAlreadyExists` no provedor OIDC | O provedor do GitHub já existe na conta. Importe: `terraform import aws_iam_openid_connect_provider.github arn:aws:iam::<conta>:oidc-provider/token.actions.githubusercontent.com`. |
| `InvalidParameterCombination` no RDS | A região não oferece a classe/versão. Confirme `us-east-1` e `db.t4g.micro`. |
| `AccessDenied` | O usuário IAM não é administrador, ou o perfil errado está ativo (`$env:AWS_PROFILE`). |
| `Error acquiring the state lock` | Outra execução em andamento, ou uma que foi interrompida. Se tiver certeza de que não há outra: `terraform force-unlock <ID>`. |
