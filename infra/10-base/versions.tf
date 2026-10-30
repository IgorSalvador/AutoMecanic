terraform {
  required_version = ">= 1.10.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }

  # O bucket vem da camada 00-bootstrap.
  backend "s3" {
    bucket       = "automecanic-tfstate-976193265037"
    key          = "10-base/terraform.tfstate"
    region       = "us-east-1"
    encrypt      = true
    use_lockfile = true
  }
}

provider "aws" {
  region = var.regiao

  default_tags {
    tags = {
      Projeto       = var.projeto
      Camada        = "10-base"
      GerenciadoPor = "terraform"
    }
  }
}
