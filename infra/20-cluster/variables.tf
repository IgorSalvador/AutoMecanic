variable "regiao" {
  description = "Região da AWS."
  type        = string
  default     = "us-east-1"
}

variable "projeto" {
  description = "Nome do projeto; entra nas tags."
  type        = string
  default     = "automecanic"
}

variable "bucket_do_estado" {
  description = "Bucket do estado do Terraform (camada 00-bootstrap), de onde se lê a camada 10-base."
  type        = string
  default     = "automecanic-tfstate-976193265037"
}

variable "versao_do_kubernetes" {
  description = "Versão do Kubernetes. Use sempre uma versão em SUPORTE PADRÃO do EKS: as em suporte estendido cobram uma taxa extra por hora de cluster. Calendário: https://docs.aws.amazon.com/eks/latest/userguide/kubernetes-versions.html"
  type        = string
  default     = "1.36"
}

variable "tipo_de_instancia" {
  description = "Tipo de instância dos nós."
  type        = string
  default     = "t3.medium"
}

variable "quantidade_de_nos" {
  description = "Quantidade fixa de nós. O autoescalonamento é de pods (HPA), não de nós."
  type        = number
  default     = 2
}

variable "usar_spot" {
  description = "Nós Spot custam ~70% menos, mas a AWS pode retirá-los com 2 minutos de aviso. Serve para estudo; não use em produção."
  type        = bool
  default     = false
}

variable "administradores" {
  description = "ARNs dos usuários IAM que administram o cluster com kubectl: os integrantes da equipe, INCLUINDO quem roda o apply. Sem estar aqui, ninguém entra no cluster além do papel do CI."
  type        = list(string)

  validation {
    condition     = length(var.administradores) > 0 && alltrue([for a in var.administradores : can(regex("^arn:aws:iam::[0-9]{12}:(user|role)/.+", a))])
    error_message = "Informe ao menos um ARN de usuário ou papel IAM, no formato arn:aws:iam::<conta>:user/<nome>."
  }
}
