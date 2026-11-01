# Banco de dados: RDS PostgreSQL 17 em subnets privadas, sem acesso público.

resource "aws_db_subnet_group" "principal" {
  name       = "${var.projeto}-banco"
  subnet_ids = aws_subnet.privada[*].id

  tags = {
    Name = "${var.projeto}-banco"
  }
}

# Só quem está dentro da VPC (os nós do Kubernetes) alcança o banco, e só na porta do PostgreSQL.
resource "aws_security_group" "banco" {
  name        = "${var.projeto}-banco"
  description = "PostgreSQL acessivel somente de dentro da VPC"
  vpc_id      = aws_vpc.principal.id

  tags = {
    Name = "${var.projeto}-banco"
  }
}

resource "aws_vpc_security_group_ingress_rule" "banco_da_vpc" {
  security_group_id = aws_security_group.banco.id
  description       = "PostgreSQL vindo da VPC"
  cidr_ipv4         = var.cidr_da_vpc
  ip_protocol       = "tcp"
  from_port         = 5432
  to_port           = 5432
}

# Alfanumérica de propósito: a senha vai dentro de uma cadeia de conexão do Npgsql, onde ";" e
# "=" teriam significado e exigiriam escape.
resource "random_password" "banco" {
  length  = 32
  special = false
}

resource "aws_db_instance" "principal" {
  identifier = "${var.projeto}-banco"

  engine         = "postgres"
  engine_version = "17"
  instance_class = var.classe_do_banco

  allocated_storage = var.armazenamento_do_banco_gb
  storage_type      = "gp3"
  storage_encrypted = true

  db_name  = "automecanic"
  username = "automecanic"
  password = random_password.banco.result

  db_subnet_group_name   = aws_db_subnet_group.principal.name
  vpc_security_group_ids = [aws_security_group.banco.id]
  publicly_accessible    = false
  multi_az               = false

  backup_retention_period    = 1
  auto_minor_version_upgrade = true
  apply_immediately          = true

  # Projeto de estudo: o banco é recriado do zero pelo Job de migração, então não vale pagar
  # por um snapshot final nem travar o destroy. Em produção real, ambos mudariam.
  skip_final_snapshot = true
  deletion_protection = false

  # A AWS atualiza a versão menor sozinha; sem isto o Terraform tentaria "desfazer" a atualização.
  lifecycle {
    ignore_changes = [engine_version]
  }
}
