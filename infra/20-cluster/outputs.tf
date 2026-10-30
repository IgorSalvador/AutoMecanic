output "nome_do_cluster" {
  value = module.eks.cluster_name
}

output "endpoint" {
  value = module.eks.cluster_endpoint
}

output "versao_do_kubernetes" {
  value = module.eks.cluster_version
}

output "comando_do_kubeconfig" {
  description = "Cola no terminal para o kubectl falar com o cluster."
  value       = "aws eks update-kubeconfig --region ${var.regiao} --name ${module.eks.cluster_name}"
}
