terraform {
  # use_lockfile (trava nativa do backend S3, sem DynamoDB) exige o Terraform 1.10 ou superior.
  required_version = ">= 1.10.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }
  }
}

provider "aws" {
  region = var.regiao

  default_tags {
    tags = {
      Projeto       = var.projeto
      Camada        = "00-bootstrap"
      GerenciadoPor = "terraform"
    }
  }
}
