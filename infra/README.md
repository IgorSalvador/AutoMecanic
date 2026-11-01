# Infraestrutura (Terraform)

Três camadas independentes, aplicadas **nesta ordem** e destruídas na **ordem inversa**:

```
infra/
├── 00-bootstrap/   bucket S3 do estado do Terraform          (cria uma vez; fica para sempre)
├── 10-base/        rede, ECR, banco RDS, papel OIDC do CI    (fica de pé entre demonstrações)
└── 20-cluster/     cluster EKS e nós                         (CRIA E DESTRÓI sob demanda)
```

Cada camada tem um README com o passo a passo, as verificações e **como desligar**:

| Camada | O que cria | Custo ligada | README |
|---|---|---|---|
| `00-bootstrap` | Bucket S3 versionado, criptografado, sem acesso público | centavos/mês | [abrir](00-bootstrap/README.md) |
| `10-base` | VPC (2 zonas, sem NAT), ECR, RDS PostgreSQL 17, papel OIDC, alarmes de gasto | **~US$ 0,02/h** (~US$ 14/mês, o RDS) | [abrir](10-base/README.md) |
| `20-cluster` | EKS 1.36, 2 nós `t3.medium`, add-ons, acessos | **~US$ 0,20/h** (+ ~US$ 0,025/h do balanceador criado pelo Kubernetes) | [abrir](20-cluster/README.md) |

> ⚠️ **Tudo ligado custa ~US$ 0,25 por hora (~US$ 185 por mês).** Com US$ 200 de crédito, um
> ambiente esquecido consome o crédito em cerca de um mês — e, no plano gratuito, a conta é
> encerrada quando o crédito acaba.

---

## Ordem para aplicar

1. `00-bootstrap` (uma vez) → [guia AWS do zero](../docs/08-guia-aws-do-zero.md)
2. `10-base`
3. `20-cluster`
4. A aplicação, pelo [pipeline](../docs/09-guia-cd-aws.md) ou à mão ([`k8s/overlays/aws`](../k8s/overlays/aws/README.md))

## Ordem para destruir (desligar e parar de pagar)

1. **A aplicação e o load balancer** — `kubectl delete -k k8s/overlays/aws`, e **espere o
   balanceador sumir**. Ele foi criado pelo Kubernetes, não pelo Terraform; se sobrar, fica
   órfão, cobra e trava a exclusão da VPC.
2. `20-cluster` — `terraform destroy` (a parte mais cara; **faça sempre**).
3. `10-base` — `terraform destroy` (**só se for ficar dias sem usar**; apaga o banco e os dados).
4. `00-bootstrap` — **quase nunca.** O bucket guarda o histórico de tudo o que o Terraform criou.

Atalho: o workflow **Destruir cluster** (GitHub Actions) faz os passos 1 e 2 sozinho, toda
madrugada e quando disparado à mão. Passo a passo e conferência do que sobrou cobrando:
[`docs/09-guia-cd-aws.md`](../docs/09-guia-cd-aws.md#passo-6--desligar).

## Como a pasta é validada

O CI roda, em cada camada e sem credenciais da AWS: `terraform fmt -check`,
`terraform init -backend=false -lockfile=readonly` e `terraform validate`. Rode o mesmo
localmente antes de abrir um PR:

```bash
cd infra/<camada>
terraform fmt -recursive
terraform init -backend=false
terraform validate
```

Os arquivos `.terraform.lock.hcl` **são versionados** (travam a versão dos provedores para todos
e para o CI). O estado (`*.tfstate`), a pasta `.terraform/` e o `terraform.tfvars` **nunca** vão
para o git.
