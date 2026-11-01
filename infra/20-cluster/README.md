# 20-cluster — o cluster Kubernetes (EKS)

A camada **cara**. Foi desenhada para ser **criada quando alguém for usar e destruída logo
depois**. Depende da camada [`10-base`](../10-base/README.md) (rede, banco, ECR e papel do CI).

| Recurso | Para quê | Custo aproximado |
|---|---|---|
| Plano de controle do EKS (Kubernetes 1.36) | O "cérebro" do cluster | **US$ 0,10/h** |
| 2 nós `t3.medium` (grupo gerenciado) | Onde os pods rodam | **~US$ 0,083/h** (os dois) |
| IPs públicos dos nós (2) | Os nós, em subnets públicas, falam com a internet | ~US$ 0,01/h |
| Add-ons: `vpc-cni`, `kube-proxy`, `coredns`, `metrics-server` | Rede, DNS e métricas para o HPA | US$ 0 |
| *Access entries* | Quem pode usar o `kubectl` (você, a equipe e o papel do CI) | US$ 0 |
| **Load balancer** (criado depois, pelo Kubernetes, quando o `Service` é aplicado) | A porta de entrada da API | **~US$ 0,025/h** |

> ⚠️ **Ligado, o conjunto custa cerca de US$ 0,25 por hora (~US$ 6 por dia, ~US$ 185 por mês).**
> Com US$ 200 de crédito, **esquecer o cluster ligado consome tudo em cerca de um mês.** Destrua
> ao terminar (seção [Desligar](#desligar-e-parar-de-pagar)).

**Por que a versão 1.36.** Versões fora do *suporte padrão* do EKS cobram uma taxa extra por hora
de cluster. Em outubro de 2026 estão em suporte padrão as versões 1.34 a 1.37; a 1.36 vale até
agosto de 2027. Se atualizar a versão, confira o
[calendário](https://docs.aws.amazon.com/eks/latest/userguide/kubernetes-versions.html).

---

## Antes de começar

1. **A camada `10-base` precisa estar aplicada** (`terraform output` nela deve funcionar).
2. **O plano gratuito da AWS pode recusar o EKS.** O plano do projeto assume que funciona, mas
   isso só se confirma ao criar. Se o `apply` falhar com uma mensagem sobre o *plano da conta*
   (*free plan* / *paid plan*), veja [Problemas comuns](#problemas-comuns): a saída é migrar para o
   **plano pago** (os créditos continuam valendo, mas passa a cobrar no cartão depois deles).
   Decida com a equipe **antes** de migrar.
3. **Saiba o seu ARN de usuário IAM**, porque só entra no cluster quem estiver na lista:

```powershell
aws sts get-caller-identity --query Arn --output text
# arn:aws:iam::123456789012:user/matheus
```

---

## Passo 1 · Informar quem administra o cluster

```powershell
cd infra\20-cluster
Copy-Item terraform.tfvars.exemplo terraform.tfvars
notepad terraform.tfvars
```

Coloque o ARN de **cada integrante** (incluindo o seu). **Quem não estiver na lista não consegue
usar o `kubectl`**, mesmo sendo administrador da conta. O papel do CI entra sozinho.

O `terraform.tfvars` **não vai para o git**.

## Passo 2 · Planejar e aplicar

```powershell
terraform init
terraform plan
```

O `plan` deve terminar em algo como **`Plan: ~35 to add, 0 to change, 0 to destroy`** (o número
exato depende do módulo). Leia a lista e procure:

- o cluster com `kubernetes_version = "1.36"`;
- 4 *add-ons* (`vpc-cni`, `kube-proxy`, `coredns`, `metrics-server`);
- um grupo de nós `t3.medium` com 2 nós;
- *access entries* para o papel do CI e para cada integrante.

**Nada** deve aparecer para destruir. Se estiver certo:

```powershell
terraform apply
```

Responda `yes`. **Leva cerca de 15 a 20 minutos** (o plano de controle demora ~10). É normal.

## Passo 3 · Conectar o `kubectl`

```powershell
terraform output -raw comando_do_kubeconfig | Invoke-Expression
kubectl get nodes
```

Esperado: **2 nós `Ready`**. Confira também:

```powershell
kubectl get pods -n kube-system          # coredns, kube-proxy, aws-node e metrics-server Running
kubectl top nodes                        # mostra CPU e memória (metrics-server funcionando)
```

Se o `kubectl` responder `You must be logged in to the server (Unauthorized)`, o seu ARN não
está em `administradores`. Adicione, rode `terraform apply` de novo e repita o comando do
kubeconfig.

## Passo 4 · Aplicar a aplicação

Com o cluster de pé, o deploy da aplicação é feito pelo overlay `k8s/overlays/aws` (tarefa C3) e,
no dia a dia, pelo pipeline de CD (E2). Veja [`k8s/overlays/aws/README.md`](../../k8s/overlays/aws/README.md).

---

## Desligar e parar de pagar

> **Regra de ouro:** destrua o que o *Kubernetes* criou **antes** de destruir o que o
> *Terraform* criou. O Terraform não conhece o load balancer, porque quem o criou foi o
> Kubernetes ao aplicar o `Service`. Se você destruir o cluster com o balanceador ainda existindo,
> o balanceador **fica órfão, continua cobrando** e **trava a exclusão da VPC**.

### Passo A · Remover a aplicação (e o load balancer)

```powershell
kubectl delete -k k8s/overlays/aws --ignore-not-found
```

Se o overlay AWS ainda não existir ou já foi removido, apague o `Service` direto:

```powershell
kubectl -n automecanic delete service automecanic-api --ignore-not-found
```

**Espere o balanceador sumir** (1 a 3 minutos) e confirme:

```powershell
aws elb describe-load-balancers --query "LoadBalancerDescriptions[].LoadBalancerName"
aws elbv2 describe-load-balancers --query "LoadBalancers[].LoadBalancerName"
```

Os dois comandos devem devolver `[]`. Se aparecer algum, **ainda não destrua o cluster**:
espere mais um pouco ou apague o balanceador pelo console (EC2 → Load Balancers).

### Passo B · Destruir o cluster

```powershell
cd infra\20-cluster
terraform destroy
```

Leia a lista (cluster, nós, *add-ons*, *access entries*, grupos de segurança, papéis) e responda
`yes`. Leva cerca de **10 a 15 minutos**.

### Passo C · Conferir que não sobrou nada cobrando

No console da AWS (região **us-east-1**), confirme que estão **vazios**:

| Onde | O que verificar |
|---|---|
| **EKS → Clusters** | nenhum cluster |
| **EC2 → Instances** | nenhuma instância (os nós somem com o grupo) |
| **EC2 → Load Balancers** | nenhum balanceador |
| **EC2 → Volumes** | nenhum volume (sobras dos nós ou de volumes de dados) |
| **VPC → Elastic IPs** | nenhum endereço alocado |
| **VPC → Network Interfaces** | nenhuma interface `available` ligada ao cluster |
| **CloudWatch → Log groups** | nenhum `/aws/eks/automecanic/...` (o cluster não cria, mas confira) |

Por linha de comando:

```powershell
aws eks list-clusters
aws ec2 describe-instances --filters "Name=instance-state-name,Values=running" --query "Reservations[].Instances[].InstanceId"
aws ec2 describe-addresses --query "Addresses[].PublicIp"
```

Todos devem devolver listas vazias. Depois olhe **Billing → Bills** (ou **Cost Explorer**) no dia
seguinte: o custo por hora deve ter parado.

### Se o `destroy` travar

| Sintoma | Causa e solução |
|---|---|
| `DependencyViolation` ao apagar subnets, grupos de segurança ou a VPC | Sobrou um balanceador ou uma interface de rede órfã. Apague o balanceador (EC2 → Load Balancers), espere ~5 minutos e repita o `terraform destroy`. |
| `terraform destroy` demora mais de 30 minutos | Quase sempre é um balanceador ou volume ainda preso. Veja o Passo C. |
| `ResourceInUseException` em *access entries* | Rode `terraform destroy` de novo; a ordem se resolve sozinha. |

### Pausar sem destruir?

**Não vale a pena.** O plano de controle do EKS (US$ 0,10/h) **não pode ser pausado**: ele cobra
mesmo com zero nós. Zerar os nós só economiza o custo deles e deixa o cluster inútil. O caminho
barato é **destruir e recriar** (15 a 20 minutos).

---

## Problemas comuns

| Sintoma | Causa e solução |
|---|---|
| `Error: reading remote state ... NoSuchKey` | A camada `10-base` ainda não foi aplicada. Aplique-a primeiro. |
| Recusa do EKS por causa do **plano da conta** (*free plan*) | O plano gratuito pode bloquear o serviço. Migre para o plano pago em *Billing → Free Tier → Upgrade plan* **com a equipe** (os créditos são mantidos; o que passar deles é cobrado). |
| `You must be logged in to the server (Unauthorized)` | O ARN não está em `administradores`. Adicione, aplique e refaça o kubeconfig. |
| `kubectl top nodes` retorna erro de métricas | O *add-on* `metrics-server` ainda subindo; espere 1 a 2 minutos. |
| Nós ficam `NotReady` | O `vpc-cni` não ficou pronto. `kubectl -n kube-system get pods`; se `aws-node` estiver em erro, veja `kubectl logs`. |
| `UnsupportedAvailabilityZoneException` | A AWS não tem capacidade para o EKS numa das zonas. Rode o `apply` de novo, ou ajuste as zonas na camada `10-base`. |
| `InsufficientInstanceCapacity` com Spot | Sem capacidade Spot no momento. Volte a `usar_spot = false` (sob demanda). |
| Custo maior que o esperado | O cluster esquecido ligado. Siga a seção **Desligar** e olhe o Cost Explorer por *serviço*. |
