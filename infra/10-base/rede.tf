# Rede: VPC em duas zonas de disponibilidade.
#
#   subnets públicas   nós do Kubernetes e load balancer (têm rota para a internet)
#   subnets privadas   só o RDS (sem rota para a internet)
#
# Não há NAT Gateway, de propósito: custaria ~US$ 33/mês. Os nós ficam em subnets públicas, com
# IP público, para falar com a internet (baixar imagens, enviar e-mail) pelo Internet Gateway;
# o banco, esse sim, nunca é alcançável de fora.

data "aws_availability_zones" "disponiveis" {
  state = "available"

  # Zonas "locais" e "wavelength" não servem para o EKS.
  filter {
    name   = "opt-in-status"
    values = ["opt-in-not-required"]
  }
}

locals {
  zonas = slice(data.aws_availability_zones.disponiveis.names, 0, 2)
}

resource "aws_vpc" "principal" {
  cidr_block           = var.cidr_da_vpc
  enable_dns_support   = true
  enable_dns_hostnames = true

  tags = {
    Name = "${var.projeto}-vpc"
  }
}

resource "aws_internet_gateway" "principal" {
  vpc_id = aws_vpc.principal.id

  tags = {
    Name = "${var.projeto}-igw"
  }
}

resource "aws_subnet" "publica" {
  count = length(local.zonas)

  vpc_id                  = aws_vpc.principal.id
  availability_zone       = local.zonas[count.index]
  cidr_block              = cidrsubnet(var.cidr_da_vpc, 8, count.index)
  map_public_ip_on_launch = true

  tags = {
    Name = "${var.projeto}-publica-${local.zonas[count.index]}"

    # O EKS e o controlador de load balancer descobrem as subnets por estas tags.
    "kubernetes.io/role/elb"                       = "1"
    "kubernetes.io/cluster/${var.nome_do_cluster}" = "shared"
  }
}

resource "aws_subnet" "privada" {
  count = length(local.zonas)

  vpc_id            = aws_vpc.principal.id
  availability_zone = local.zonas[count.index]
  cidr_block        = cidrsubnet(var.cidr_da_vpc, 8, count.index + 10)

  tags = {
    Name = "${var.projeto}-privada-${local.zonas[count.index]}"
  }
}

resource "aws_route_table" "publica" {
  vpc_id = aws_vpc.principal.id

  route {
    cidr_block = "0.0.0.0/0"
    gateway_id = aws_internet_gateway.principal.id
  }

  tags = {
    Name = "${var.projeto}-rotas-publicas"
  }
}

resource "aws_route_table_association" "publica" {
  count = length(aws_subnet.publica)

  subnet_id      = aws_subnet.publica[count.index].id
  route_table_id = aws_route_table.publica.id
}

# A tabela das subnets privadas só tem a rota local da VPC: nada entra nem sai pela internet.
resource "aws_route_table" "privada" {
  vpc_id = aws_vpc.principal.id

  tags = {
    Name = "${var.projeto}-rotas-privadas"
  }
}

resource "aws_route_table_association" "privada" {
  count = length(aws_subnet.privada)

  subnet_id      = aws_subnet.privada[count.index].id
  route_table_id = aws_route_table.privada.id
}
