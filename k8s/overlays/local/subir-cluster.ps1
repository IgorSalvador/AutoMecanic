# Sobe o AutoMecanic num cluster kind local (equivalente ao subir-cluster.sh).
#
#   .\k8s\overlays\local\subir-cluster.ps1             sobe (ou atualiza)
#   .\k8s\overlays\local\subir-cluster.ps1 -Destruir   remove o cluster
#
# Requisitos: docker, kind e kubectl.
param([switch]$Destruir)

$ErrorActionPreference = 'Stop'

$cluster = 'automecanic'
$imagem = 'automecanic-api:local'
$ns = 'automecanic'
$raiz = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$local = Join-Path $raiz 'k8s\overlays\local'

# Comandos nativos não lançam exceção ao falhar, e vários (kind, docker) escrevem o progresso
# em stderr, que no Windows PowerShell 5.1 viraria erro. Aqui só o código de saída decide.
function Executar {
    param([string]$Comando, [string[]]$Argumentos)
    $anterior = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & $Comando @Argumentos 2>&1 | ForEach-Object { Write-Host "$_" }
        $codigo = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $anterior
    }
    if ($codigo -ne 0) { throw "Falhou ($codigo): $Comando $($Argumentos -join ' ')" }
}

if ($Destruir) {
    Executar kind @('delete', 'cluster', '--name', $cluster)
    return
}

foreach ($ferramenta in 'docker', 'kind', 'kubectl') {
    if (-not (Get-Command $ferramenta -ErrorAction SilentlyContinue)) { throw "Falta instalar: $ferramenta" }
}

# kind e git escrevem avisos em stderr (ex.: "No kind clusters found."), que no Windows
# PowerShell 5.1 viram erro terminante; aqui o stderr é esperado e descartado.
$ErrorActionPreference = 'Continue'
$existentes = & kind get clusters 2>$null
$revisao = & git -C $raiz rev-parse --short HEAD 2>$null
$ErrorActionPreference = 'Stop'

if ($existentes -contains $cluster) {
    Write-Host "==> Cluster '$cluster' já existe; reaproveitando."
} else {
    Write-Host "==> Criando o cluster '$cluster'."
    Executar kind @('create', 'cluster', '--config', (Join-Path $local 'kind-config.yaml'))
}
Executar kubectl @('config', 'use-context', "kind-$cluster")

Write-Host "==> Construindo a imagem $imagem."
if (-not $revisao) { $revisao = 'desconhecida' }
Executar docker @('build', '-t', $imagem, '--build-arg', "REVISAO=$revisao", $raiz)
Executar kind @('load', 'docker-image', $imagem, '--name', $cluster)

Write-Host '==> Instalando o metrics-server.'
Executar kubectl @('apply', '-k', (Join-Path $local 'metrics-server'))
Executar kubectl @('-n', 'kube-system', 'rollout', 'status', 'deployment/metrics-server', '--timeout=180s')

Write-Host '==> Aplicando os manifestos.'
# O template de um Job é imutável: apagar o anterior permite recriá-lo com a imagem nova.
Executar kubectl @('-n', $ns, 'delete', 'job', 'automecanic-migracao', '--ignore-not-found')
Executar kubectl @('apply', '-k', $local)

Write-Host '==> Aguardando banco, migração e API.'
Executar kubectl @('-n', $ns, 'rollout', 'status', 'statefulset/postgres', '--timeout=180s')
Executar kubectl @('-n', $ns, 'wait', '--for=condition=complete', 'job/automecanic-migracao', '--timeout=300s')
# O Deployment pode já existir com a mesma tag "local": força os pods a usarem a imagem recém-carregada.
Executar kubectl @('-n', $ns, 'rollout', 'restart', 'deployment/automecanic-api')
Executar kubectl @('-n', $ns, 'rollout', 'status', 'deployment/automecanic-api', '--timeout=180s')

Write-Host @"

Pronto.
  API / Swagger   http://localhost:8080
  Mailpit         http://localhost:8025
  Administrador   admin@automecanic.com.br  /  Admin@AutoMecanic2026

Acompanhe o autoescalonamento:  kubectl -n $ns get hpa -w
Remover tudo:                   .\k8s\overlays\local\subir-cluster.ps1 -Destruir
"@
