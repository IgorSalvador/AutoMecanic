output "regiao" {
  value = var.regiao
}

output "vpc_id" {
  value = aws_vpc.principal.id
}

output "cidr_da_vpc" {
  value = aws_vpc.principal.cidr_block
}

output "subnets_publicas" {
  description = "Onde ficam os nós do cluster e o load balancer."
  value       = aws_subnet.publica[*].id
}

output "subnets_privadas" {
  description = "Onde fica o banco."
  value       = aws_subnet.privada[*].id
}

output "nome_do_cluster" {
  description = "Nome combinado para o cluster EKS (as tags das subnets já o usam)."
  value       = var.nome_do_cluster
}

output "ecr_repositorio_url" {
  description = "Endereço do repositório de imagens, sem a tag."
  value       = aws_ecr_repository.api.repository_url
}

output "ecr_repositorio_nome" {
  value = aws_ecr_repository.api.name
}

output "banco_endereco" {
  description = "Endereço (DNS) do RDS; só resolve de dentro da VPC."
  value       = aws_db_instance.principal.address
}

output "banco_porta" {
  value = aws_db_instance.principal.port
}

output "banco_nome" {
  value = aws_db_instance.principal.db_name
}

output "banco_usuario" {
  value = aws_db_instance.principal.username
}

output "banco_senha" {
  description = "Senha gerada do banco. Leia com: terraform output -raw banco_senha"
  value       = random_password.banco.result
  sensitive   = true
}

# SSL é obrigatório no RDS PostgreSQL 15+ (rds.force_ssl). "Trust Server Certificate" evita
# distribuir o bundle de CAs da AWS: o tráfego vai criptografado, dentro da VPC.
output "banco_cadeia_de_conexao" {
  description = "Valor de ConnectionStrings__PostgreSQL para o Secret do Kubernetes."
  value       = "Host=${aws_db_instance.principal.address};Port=${aws_db_instance.principal.port};Database=${aws_db_instance.principal.db_name};Username=${aws_db_instance.principal.username};Password=${random_password.banco.result};SSL Mode=Require;Trust Server Certificate=true;Maximum Pool Size=50"
  sensitive   = true
}

output "ci_papel_arn" {
  description = "Valor do segredo AWS_ROLE_ARN no GitHub."
  value       = aws_iam_role.ci.arn
}
