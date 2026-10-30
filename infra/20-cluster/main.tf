# Cluster EKS. É a parte cara do projeto e foi desenhada para ser CRIADA E DESTRUÍDA sob demanda:
# o plano de controle custa US$ 0,10/h e os nós, ~US$ 0,083/h cada.

# Rede, ECR, RDS e papel do CI vêm da camada 10-base.
data "terraform_remote_state" "base" {
  backend = "s3"

  config = {
    bucket = var.bucket_do_estado
    key    = "10-base/terraform.tfstate"
    region = var.regiao
  }
}

locals {
  base = data.terraform_remote_state.base.outputs
}

module "eks" {
  source  = "terraform-aws-modules/eks/aws"
  version = "~> 21.0"

  name               = local.base.nome_do_cluster
  kubernetes_version = var.versao_do_kubernetes

  vpc_id     = local.base.vpc_id
  subnet_ids = local.base.subnets_publicas

  # Endpoint público (o kubectl e o GitHub Actions precisam alcançá-lo) e privado (os nós falam
  # com o plano de controle por dentro da VPC). Quem entra é decidido pelo IAM.
  endpoint_public_access  = true
  endpoint_private_access = true

  # Todo acesso é explícito, por access_entries (o papel do CI e a lista "administradores").
  # O atalho "quem criou vira administrador" fica desligado: o cluster é criado ora por uma
  # pessoa, ora pelo papel do CI, e o atalho recriaria uma entrada que já consta abaixo, o que
  # o EKS recusa por duplicidade.
  enable_cluster_creator_admin_permissions = false

  access_entries = merge(
    {
      # O papel do GitHub Actions administra o cluster para aplicar os manifestos.
      ci = {
        principal_arn = local.base.ci_papel_arn

        policy_associations = {
          administrador = {
            policy_arn   = "arn:aws:eks::aws:cluster-access-policy/AmazonEKSClusterAdminPolicy"
            access_scope = { type = "cluster" }
          }
        }
      }
    },
    {
      for indice, arn in var.administradores : "pessoa-${indice}" => {
        principal_arn = arn

        policy_associations = {
          administrador = {
            policy_arn   = "arn:aws:eks::aws:cluster-access-policy/AmazonEKSClusterAdminPolicy"
            access_scope = { type = "cluster" }
          }
        }
      }
    },
  )

  # Cada recurso abaixo cobra por conta própria; ficam desligados porque o projeto não precisa.
  #   · logs do plano de controle: o CloudWatch cobra por GB ingerido;
  #   · chave KMS para os Secrets: US$ 1/mês e uma chamada por leitura.
  enabled_log_types           = []
  create_cloudwatch_log_group = false
  create_kms_key              = false
  encryption_config           = null

  addons = {
    # A rede dos pods precisa existir antes de os nós subirem.
    vpc-cni = {
      most_recent    = true
      before_compute = true
    }
    kube-proxy = {
      most_recent = true
    }
    coredns = {
      most_recent = true
    }
    # Sem ele o HPA não enxerga CPU nem memória.
    metrics-server = {
      most_recent = true
    }
  }

  eks_managed_node_groups = {
    principal = {
      ami_type       = "AL2023_x86_64_STANDARD"
      instance_types = [var.tipo_de_instancia]
      capacity_type  = var.usar_spot ? "SPOT" : "ON_DEMAND"

      min_size     = var.quantidade_de_nos
      max_size     = var.quantidade_de_nos
      desired_size = var.quantidade_de_nos
    }
  }
}
