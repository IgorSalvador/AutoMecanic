#!/usr/bin/env bash
# Publica o AutoMecanic num cluster EKS: cria o Secret, roda a migração, atualiza a API e espera
# o load balancer. É o mesmo roteiro que o pipeline de CD executa, e pode ser rodado à mão.
#
# Variáveis OBRIGATÓRIAS (nada disto vai para o git):
#   IMAGEM               imagem completa, com a tag     ex.: 123456789012.dkr.ecr.us-east-1.amazonaws.com/automecanic-api:abc1234
#   CADEIA_DE_CONEXAO    ConnectionStrings__PostgreSQL  (output banco_cadeia_de_conexao do Terraform)
#   JWT_CHAVE            segredo do JWT, 32+ caracteres
#   SEED_SENHA_ADMIN     senha do administrador inicial
# Variáveis OPCIONAIS:
#   SMTP_USUARIO         conta Gmail que envia os e-mails (também usada como remetente)
#   SMTP_SENHA           senha de app do Gmail
#   DRY_RUN=1            só valida e mostra o que faria, sem alterar o cluster
#
# Uso:  IMAGEM=... CADEIA_DE_CONEXAO=... JWT_CHAVE=... SEED_SENHA_ADMIN=... ./aplicar.sh
set -euo pipefail

NS=automecanic
RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
DRY_RUN="${DRY_RUN:-0}"

# Evita que o Git Bash no Windows converta caminhos.
export MSYS_NO_PATHCONV=1

faltando=()
for variavel in IMAGEM CADEIA_DE_CONEXAO JWT_CHAVE SEED_SENHA_ADMIN; do
  [[ -n "${!variavel:-}" ]] || faltando+=("$variavel")
done
if ((${#faltando[@]})); then
  echo "Faltam variáveis obrigatórias: ${faltando[*]}" >&2
  exit 1
fi

[[ "$IMAGEM" == *:* ]] || { echo "IMAGEM precisa ter tag (repositorio:tag)." >&2; exit 1; }
(( ${#JWT_CHAVE} >= 32 )) || { echo "JWT_CHAVE precisa ter 32+ caracteres." >&2; exit 1; }

NOME_DA_IMAGEM="${IMAGEM%:*}"
TAG_DA_IMAGEM="${IMAGEM##*:}"

# Trabalha numa cópia dos manifestos: a tag e o repositório reais entram só aqui, e o arquivo
# versionado continua com os valores de exemplo.
TMP="$(mktemp -d)"
ARQUIVO_DO_SECRET="$(mktemp)"
trap 'rm -rf "$TMP" "$ARQUIVO_DO_SECRET"' EXIT
chmod 600 "$ARQUIVO_DO_SECRET"

cp -r "$RAIZ/k8s" "$TMP/k8s"

# No Git Bash do Windows, o kubectl.exe não entende caminhos como /tmp/...; o cygpath os converte.
# No Linux (CI) o comando não existe e o caminho segue como está.
OVERLAY="$TMP/k8s/overlays/aws"
if command -v cygpath >/dev/null 2>&1; then OVERLAY="$(cygpath -m "$OVERLAY")"; fi
sed -i \
  -e "s|^\( *newName:\) .*|\1 ${NOME_DA_IMAGEM}|" \
  -e "s|^\( *newTag:\) .*|\1 ${TAG_DA_IMAGEM}|" \
  "$TMP/k8s/overlays/aws/kustomization.yaml"

# O Secret vai por arquivo (e não por argumentos de linha de comando) para as senhas não
# aparecerem na lista de processos. Chaves do Secret ganham das do ConfigMap (vêm depois no envFrom).
{
  echo "ConnectionStrings__PostgreSQL=${CADEIA_DE_CONEXAO}"
  echo "Jwt__ChaveDeAssinatura=${JWT_CHAVE}"
  echo "Seed__SenhaDoAdministrador=${SEED_SENHA_ADMIN}"
  echo "Smtp__Usuario=${SMTP_USUARIO:-}"
  echo "Smtp__Senha=${SMTP_SENHA:-}"
  # O Gmail só aceita enviar com o remetente igual à conta autenticada.
  if [[ -n "${SMTP_USUARIO:-}" ]]; then echo "Smtp__Remetente=${SMTP_USUARIO}"; fi
  if [[ -n "${URL_PUBLICA:-}" ]]; then echo "Notificacoes__UrlPublicaDaApi=${URL_PUBLICA}"; fi
} > "$ARQUIVO_DO_SECRET"

if [[ -z "${SMTP_USUARIO:-}" ]]; then
  echo "AVISO: SMTP_USUARIO vazio; com Notificacoes__Habilitadas=true a API recusará subir sem remetente válido." >&2
fi

if [[ "$DRY_RUN" == "1" ]]; then
  echo "==> DRY_RUN: renderizando os manifestos (imagem $IMAGEM)."
  kubectl kustomize "$OVERLAY" | grep -E "^kind:|image:|type: LoadBalancer|externalTrafficPolicy" | sort | uniq -c
  echo "==> DRY_RUN: chaves do Secret que seriam gravadas:"
  cut -d= -f1 "$ARQUIVO_DO_SECRET" | sed 's/^/    /'
  exit 0
fi

echo "==> Contexto do kubectl: $(kubectl config current-context)"

echo "==> Garantindo o namespace e gravando o Secret."
kubectl create namespace "$NS" --dry-run=client -o yaml | kubectl apply -f -
kubectl -n "$NS" create secret generic automecanic-api \
  --from-env-file="$ARQUIVO_DO_SECRET" --dry-run=client -o yaml | kubectl apply -f -

echo "==> Aplicando os manifestos."
# O template de um Job é imutável: o anterior é apagado para ser recriado com a imagem nova.
kubectl -n "$NS" delete job automecanic-migracao --ignore-not-found
kubectl apply -k "$OVERLAY"

echo "==> Aguardando a migração do banco."
kubectl -n "$NS" wait --for=condition=complete job/automecanic-migracao --timeout=600s

echo "==> Aguardando a API."
kubectl -n "$NS" rollout restart deployment/automecanic-api
kubectl -n "$NS" rollout status deployment/automecanic-api --timeout=300s

echo "==> Aguardando o endereço do load balancer (pode levar alguns minutos)."
ENDERECO=""
for _ in $(seq 1 60); do
  ENDERECO="$(kubectl -n "$NS" get service automecanic-api -o jsonpath='{.status.loadBalancer.ingress[0].hostname}' 2>/dev/null || true)"
  [[ -n "$ENDERECO" ]] && break
  sleep 10
done
[[ -n "$ENDERECO" ]] || { echo "O load balancer não recebeu endereço a tempo." >&2; exit 1; }

echo
echo "Pronto."
echo "  API / Swagger   http://${ENDERECO}/swagger"
echo "  Saúde           http://${ENDERECO}/health/pronto"
echo
echo "O DNS do balanceador pode levar 1 a 3 minutos para responder."
echo "Para os e-mails citarem este endereço, rode de novo com URL_PUBLICA=http://${ENDERECO}"
echo
echo "DESLIGAR (para não pagar): kubectl delete -k k8s/overlays/aws  e depois destruir infra/20-cluster."
