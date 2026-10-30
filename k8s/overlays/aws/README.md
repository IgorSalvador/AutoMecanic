# Overlay AWS — a aplicação no EKS

O mesmo `k8s/base` do cluster local, com o que é da nuvem: imagem do **ECR**, banco no **RDS**,
e-mail pelo **Gmail** e entrada por um **NLB** (balanceador de rede).

| O que muda em relação ao `base` | Por quê |
|---|---|
| Imagem do ECR (conta e tag definidas no deploy) | Os nós puxam a imagem do repositório da sua conta |
| `Service` do tipo `LoadBalancer` (NLB) com `externalTrafficPolicy: Local` | Entrada pública; a API vê o IP real do cliente |
| `Hospedagem__UsarCabecalhosEncaminhados: "false"` | O NLB é de camada 4 e não define `X-Forwarded-For`; confiar no cabeçalho deixaria qualquer um forjar o IP e escapar do limite de taxa |
| `Seed__IncluirDadosDeDemonstracao: "true"` | Clientes, veículos, peças e OS de exemplo para a apresentação |
| Secret criado pelo script, **fora do git** | Cadeia do RDS, chave do JWT, senha do admin e credenciais do Gmail |

> ⚠️ **Limitações conhecidas desta demonstração**
> - **Só HTTP.** Sem domínio nem certificado, o NLB atende na porta 80 sem TLS. O login e o token
>   JWT trafegam sem criptografia: serve para a demonstração, **não** para dados reais.
> - O balanceador recebe um endereço automático da AWS (`xxxx.elb.us-east-1.amazonaws.com`) que
>   **muda a cada vez que o cluster é recriado**.

---

## Antes de começar

1. Camadas `10-base` e `20-cluster` aplicadas e o `kubectl` conectado
   (`kubectl get nodes` mostra 2 nós `Ready`). Ver [`infra/20-cluster`](../../../infra/20-cluster/README.md).
2. **A imagem precisa estar no ECR.** O pipeline de CD faz isso sozinho. À mão:

```bash
# na raiz do repositório (Git Bash), com o perfil da AWS ativo
REPO=$(terraform -chdir=infra/10-base output -raw ecr_repositorio_url)
aws ecr get-login-password --region us-east-1 | docker login --username AWS --password-stdin "${REPO%%/*}"
docker build -t "$REPO:manual" .
docker push "$REPO:manual"
```

3. Uma **senha de app do Gmail** (Conta Google → Segurança → Verificação em duas etapas → Senhas
   de app). Sem ela os e-mails não saem, mas o resto funciona (deixe `SMTP_USUARIO` vazio e
   configure `Notificacoes__Habilitadas=false` no ConfigMap, ou aceite os erros de envio nos logs).

---

## Aplicar

No **Git Bash**, na raiz do repositório:

```bash
export IMAGEM="$(terraform -chdir=infra/10-base output -raw ecr_repositorio_url):manual"
export CADEIA_DE_CONEXAO="$(terraform -chdir=infra/10-base output -raw banco_cadeia_de_conexao)"
export JWT_CHAVE="$(openssl rand -base64 48)"
export SEED_SENHA_ADMIN='Escolha@UmaSenhaForte1'
export SMTP_USUARIO='sua.conta@gmail.com'
export SMTP_SENHA='senha de app do gmail'

DRY_RUN=1 ./k8s/overlays/aws/aplicar.sh     # confere tudo sem alterar o cluster
./k8s/overlays/aws/aplicar.sh               # aplica de verdade
```

O script: cria o namespace e o Secret, recria o Job de migração, aplica os manifestos, espera a
migração terminar, reinicia a API e espera o endereço do balanceador (alguns minutos). Termina
mostrando o endereço.

> Guarde o `JWT_CHAVE` e a `SEED_SENHA_ADMIN` que escolheu: o script não as mostra de novo.
> Quem as guarda de forma definitiva, no pipeline, são os segredos do GitHub.

### Conferir

```bash
kubectl -n automecanic get pods              # 2+ pods da API Running; migração Completed
kubectl -n automecanic get hpa               # CPU e memória com percentuais
kubectl -n automecanic get service automecanic-api
ENDERECO=$(kubectl -n automecanic get service automecanic-api -o jsonpath='{.status.loadBalancer.ingress[0].hostname}')
curl "http://$ENDERECO/health/pronto"        # 200 (o DNS leva 1 a 3 min para propagar)
```

Abra `http://<endereço>/swagger`. Para rodar a coleção Postman:

```bash
npx --yes newman run postman/AutoMecanic.postman_collection.json \
  --folder "00 · Fase 2 — abertura completa, resposta do cliente e fila" \
  --env-var "baseUrl=http://$ENDERECO" --env-var "senhaAdmin=$SEED_SENHA_ADMIN"
```

### Fazer os e-mails citarem o endereço do balanceador

O endereço só existe depois do primeiro deploy. Rode o script de novo com ele:

```bash
URL_PUBLICA="http://$ENDERECO" ./k8s/overlays/aws/aplicar.sh
```

---

## Teste de carga (HPA)

O mesmo Job do cluster local serve:

```bash
kubectl apply -k k8s/overlays/local/carga
kubectl -n automecanic get hpa -w
```

O Job parte de um único IP, então os limites de taxa de produção devolveriam `429`. **Só para a
demonstração**, eleve-os e reinicie (os valores voltam ao padrão no próximo `aplicar.sh`):

```bash
kubectl -n automecanic patch configmap automecanic-api --type merge \
  -p '{"data":{"LimiteDeTaxa__LoginPorMinuto":"100000","LimiteDeTaxa__GlobalPorMinuto":"100000"}}'
kubectl -n automecanic rollout restart deployment/automecanic-api
```

Ao terminar: `kubectl delete -k k8s/overlays/local/carga`.

---

## Desligar e parar de pagar

O balanceador é criado pelo **Kubernetes**, não pelo Terraform: **remova-o antes de destruir o
cluster**, senão ele fica órfão, continua cobrando (~US$ 0,025/h) e trava a exclusão da VPC.

**1. Remover a aplicação e o balanceador**

```bash
kubectl delete -k k8s/overlays/aws --ignore-not-found
```

Isso apaga a aplicação, o `Service` (e portanto o NLB), o namespace e o Secret.

**2. Confirmar que o balanceador sumiu** (1 a 3 minutos):

```bash
aws elbv2 describe-load-balancers --query "LoadBalancers[].LoadBalancerName"
aws elb describe-load-balancers --query "LoadBalancerDescriptions[].LoadBalancerName"
```

Os dois devem devolver `[]`. Se aparecer algum, espere ou apague pelo console (EC2 → Load
Balancers).

**3. Destruir o cluster** — e, se não for usar por dias, também a camada `10-base` (o RDS cobra
~US$ 14/mês mesmo parado):

```powershell
cd infra\20-cluster ; terraform destroy
cd ..\10-base       ; terraform destroy     # só se não vai usar por mais de alguns dias
```

Passo a passo completo, com a conferência do que sobrou cobrando, em
[`infra/20-cluster/README.md`](../../../infra/20-cluster/README.md#desligar-e-parar-de-pagar).

**Só quero parar a API, mantendo o cluster?** Isso **não** economiza: o plano de controle e os
nós cobram do mesmo jeito. Só destruir o cluster para o custo.

---

## Problemas comuns

| Sintoma | Causa e solução |
|---|---|
| `ImagePullBackOff` | A imagem/tag não existe no ECR, ou a conta/região do repositório está errada. `aws ecr list-images --repository-name automecanic-api`. |
| Job de migração em `Error` | Veja `kubectl -n automecanic logs job/automecanic-migracao`. Causa comum: cadeia de conexão errada ou o RDS ainda subindo. Rode `aplicar.sh` de novo. |
| `EXTERNAL-IP` do Service fica `<pending>` | O balanceador está sendo criado (até 5 min). Se passar disso: `kubectl -n automecanic describe service automecanic-api` e leia os eventos. |
| `curl` ao balanceador não responde logo após aparecer o endereço | O DNS e o *health check* do NLB levam 1 a 3 minutos. |
| API em `CrashLoopBackOff` com erro de e-mail/SMTP | Com `Notificacoes__Habilitadas=true`, a API valida o SMTP na subida e exige remetente. Informe `SMTP_USUARIO`/`SMTP_SENHA` ou desligue as notificações no ConfigMap. |
| Respostas `429` fora do teste de carga | Limite de taxa por IP. Com `externalTrafficPolicy: Local` cada cliente tem o seu IP; atrás de VPN/NAT corporativo vários compartilham o mesmo. |
