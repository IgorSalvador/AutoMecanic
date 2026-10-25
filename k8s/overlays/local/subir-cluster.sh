#!/usr/bin/env bash
# Sobe o AutoMecanic num cluster kind local. Pode ser repetido: reaproveita o cluster
# existente e reaplica a imagem e os manifestos.
#
#   ./k8s/overlays/local/subir-cluster.sh            sobe (ou atualiza)
#   ./k8s/overlays/local/subir-cluster.sh --destruir remove o cluster
#
# Requisitos: docker, kind e kubectl.
set -euo pipefail

CLUSTER=automecanic
IMAGEM=automecanic-api:local
NS=automecanic
RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
LOCAL="$RAIZ/k8s/overlays/local"

# Evita que o Git Bash no Windows converta caminhos como /tmp em caminhos do Windows.
export MSYS_NO_PATHCONV=1

if [[ "${1:-}" == "--destruir" ]]; then
  kind delete cluster --name "$CLUSTER"
  exit 0
fi

for ferramenta in docker kind kubectl; do
  command -v "$ferramenta" >/dev/null || { echo "Falta instalar: $ferramenta" >&2; exit 1; }
done

if kind get clusters 2>/dev/null | grep -qx "$CLUSTER"; then
  echo "==> Cluster '$CLUSTER' já existe; reaproveitando."
else
  echo "==> Criando o cluster '$CLUSTER'."
  kind create cluster --config "$LOCAL/kind-config.yaml"
fi
kubectl config use-context "kind-$CLUSTER" >/dev/null

echo "==> Construindo a imagem $IMAGEM."
docker build -t "$IMAGEM" --build-arg REVISAO="$(git -C "$RAIZ" rev-parse --short HEAD 2>/dev/null || echo desconhecida)" "$RAIZ"
kind load docker-image "$IMAGEM" --name "$CLUSTER"

echo "==> Instalando o metrics-server."
kubectl apply -k "$LOCAL/metrics-server"
kubectl -n kube-system rollout status deployment/metrics-server --timeout=180s

echo "==> Aplicando os manifestos."
# O template de um Job é imutável: apagar o anterior permite recriá-lo com a imagem nova.
kubectl -n "$NS" delete job automecanic-migracao --ignore-not-found
kubectl apply -k "$LOCAL"

echo "==> Aguardando banco, migração e API."
kubectl -n "$NS" rollout status statefulset/postgres --timeout=180s
kubectl -n "$NS" wait --for=condition=complete job/automecanic-migracao --timeout=300s
# O Deployment pode já existir com a mesma tag "local": força os pods a usarem a imagem recém-carregada.
kubectl -n "$NS" rollout restart deployment/automecanic-api
kubectl -n "$NS" rollout status deployment/automecanic-api --timeout=180s

cat <<MSG

Pronto.
  API / Swagger   http://localhost:8080
  Mailpit         http://localhost:8025
  Administrador   admin@automecanic.com.br  /  Admin@AutoMecanic2026

Acompanhe o autoescalonamento:  kubectl -n $NS get hpa -w
Remover tudo:                   $0 --destruir
MSG
