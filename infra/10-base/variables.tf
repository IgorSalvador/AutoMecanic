variable "regiao" {
  description = "Região da AWS."
  type        = string
  default     = "us-east-1"
}

variable "projeto" {
  description = "Nome do projeto; compõe o nome dos recursos e as tags."
  type        = string
  default     = "automecanic"
}

variable "nome_do_cluster" {
  description = "Nome do cluster EKS (camada 20-cluster). Entra nas tags das subnets, que o EKS usa para achar onde criar load balancers."
  type        = string
  default     = "automecanic"
}

variable "cidr_da_vpc" {
  description = "Faixa de endereços da VPC."
  type        = string
  default     = "10.0.0.0/16"
}

variable "github_repositorio" {
  description = "Repositório (dono/nome) autorizado a assumir o papel do CI por OIDC."
  type        = string
  default     = "IgorSalvador/AutoMecanic"
}

variable "github_ambiente" {
  description = "Nome do environment do GitHub Actions que protege o deploy (exige aprovação)."
  type        = string
  default     = "aws"
}

variable "classe_do_banco" {
  description = "Classe da instância RDS."
  type        = string
  default     = "db.t4g.micro"
}

variable "armazenamento_do_banco_gb" {
  description = "Armazenamento do RDS, em GB (gp3)."
  type        = number
  default     = 20
}

variable "criar_orcamentos" {
  description = "Cria os alarmes do AWS Budgets. Desligue se eles já existem e você não quer importá-los."
  type        = bool
  default     = true
}

variable "limites_dos_orcamentos_usd" {
  description = "Valores mensais, em dólares, que disparam alarme por e-mail."
  type        = list(number)
  default     = [20, 50, 100]
}

variable "emails_de_alerta" {
  description = "E-mails que recebem os alarmes de gasto. Obrigatório quando criar_orcamentos for true."
  type        = list(string)
  default     = []

  validation {
    condition     = alltrue([for e in var.emails_de_alerta : can(regex("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", e))])
    error_message = "Informe endereços de e-mail válidos."
  }
}
