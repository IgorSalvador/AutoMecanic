# 00-bootstrap — bucket do estado do Terraform

Cria **uma única vez** o bucket S3 onde as camadas `10-base` e `20-cluster` guardam o estado.
Sem ele, cada pessoa teria um estado local diferente e o Terraform não saberia o que já existe.

| Recurso | Para quê |
|---|---|
| `aws_s3_bucket` | Guarda o estado. Nome: `automecanic-tfstate-<id da conta>` (único no mundo, sem combinar nada com ninguém) |
| Versionamento | Cada gravação é uma versão: dá para voltar atrás depois de um erro |
| Criptografia (AES-256) | O estado pode conter segredos, como a senha do RDS |
| Bloqueio de acesso público + `BucketOwnerEnforced` | Nada é público e não há ACL |
| Política só-TLS | Recusa acesso sem HTTPS |
| Ciclo de vida | Versões antigas expiram em 90 dias |
| `prevent_destroy` | Impede `terraform destroy` acidental do bucket |

**Trava de concorrência:** o backend S3 usa `use_lockfile = true` (um arquivo `.tflock` no próprio
bucket). Não há tabela DynamoDB, o que exige o **Terraform 1.10 ou superior**.

**Custo:** centavos por mês (alguns KB de estado).

---

## Pré-requisitos

- [Terraform](https://developer.hashicorp.com/terraform/install) 1.10+ e [AWS CLI](https://docs.aws.amazon.com/cli/latest/userguide/getting-started-install.html).
- Credenciais da sua conta IAM com permissão para criar buckets S3 (a conta tem MFA; use o
  perfil que você configurou). Teste com:

```bash
aws sts get-caller-identity
```

A resposta deve mostrar a conta e o usuário esperados, e **a conta do projeto**. O bucket é criado
na conta das credenciais que estiverem ativas, e o nome carrega o ID dela.

---

## Passo 1 · Criar o bucket

Só **uma pessoa** da equipe faz isto, uma vez.

```bash
cd infra/00-bootstrap
terraform init
terraform plan
terraform apply
```

O `plan` deve mostrar **7 recursos** a criar (bucket, versionamento, criptografia, bloqueio
público, controle de propriedade, ciclo de vida e política). Anote o output:

```bash
terraform output nome_do_bucket
```

## Passo 2 · Mover o estado desta camada para o próprio bucket

Até aqui o estado do bootstrap está em `terraform.tfstate`, **no seu computador**. Para não
depender dele:

```bash
cp backend.tf.exemplo backend.tf
# edite backend.tf: troque <NOME-DO-BUCKET> pelo valor do passo 1
terraform init -migrate-state     # responda "yes" para copiar o estado
```

Confirme que está remoto e apague o local:

```bash
aws s3 ls s3://<NOME-DO-BUCKET>/00-bootstrap/
rm terraform.tfstate terraform.tfstate.backup
```

O `backend.tf` **é versionado** (não tem segredo: só o nome do bucket). O `terraform.tfstate`
local nunca deve ir para o git.

## Passo 3 · Usar nas outras camadas

Cada camada declara o mesmo backend, mudando só a chave:

```hcl
terraform {
  backend "s3" {
    bucket       = "<NOME-DO-BUCKET>"
    key          = "10-base/terraform.tfstate"   # 20-cluster/terraform.tfstate na outra
    region       = "us-east-1"
    encrypt      = true
    use_lockfile = true
  }
}
```

O output `configuracao_do_backend` já imprime o bloco pronto:

```bash
terraform output -raw configuracao_do_backend
```

---

## Verificações

```bash
# versionamento ligado
aws s3api get-bucket-versioning --bucket <NOME-DO-BUCKET>

# nada público
aws s3api get-public-access-block --bucket <NOME-DO-BUCKET>

# criptografia
aws s3api get-bucket-encryption --bucket <NOME-DO-BUCKET>

# acesso sem TLS é recusado (deve falhar com AccessDenied)
aws s3api head-bucket --bucket <NOME-DO-BUCKET> --endpoint-url http://s3.us-east-1.amazonaws.com
```

Para testar a trava de concorrência, rode `terraform plan` em dois terminais ao mesmo tempo
numa camada que já use o backend: o segundo deve esperar ou falhar com `Error acquiring the state lock`.

## Se algo der errado

| Situação | O que fazer |
|---|---|
| `BucketAlreadyExists` | Alguém já criou o bucket (ou o nome colide). Use `-var nome_do_bucket=<outro>`. |
| `Unsupported argument: use_lockfile` | Terraform anterior à 1.10. Atualize. |
| Perdi o `terraform.tfstate` antes do passo 2 | `terraform import aws_s3_bucket.estado <NOME-DO-BUCKET>` e importe os demais recursos, ou apague o bucket (vazio) e recrie. |
| Estado corrompido depois do passo 2 | Restaure uma versão anterior do objeto `00-bootstrap/terraform.tfstate` pelo console do S3 (o versionamento guarda todas). |

## Destruir

O bucket tem `prevent_destroy`. Só destrua **depois** de destruir `10-base` e `20-cluster`, e de
esvaziar o bucket (inclusive as versões). Remova a trava `lifecycle` do `main.tf`, rode
`terraform destroy` e restaure a trava. É uma operação que apaga o histórico de tudo o que o
Terraform fez: não a faça por impulso.
