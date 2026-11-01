variable "regiao" {
  description = "Região da AWS onde o bucket do estado é criado."
  type        = string
  default     = "us-east-1"
}

variable "projeto" {
  description = "Nome do projeto; compõe o nome do bucket e as tags."
  type        = string
  default     = "automecanic"
}

variable "nome_do_bucket" {
  description = "Nome do bucket do estado. Vazio, usa <projeto>-tfstate-<id da conta>, que é único no mundo sem coordenação entre a equipe."
  type        = string
  default     = null
}

variable "dias_para_expirar_versoes_antigas" {
  description = "Por quantos dias as versões anteriores do estado são mantidas (permitem voltar atrás depois de um erro)."
  type        = number
  default     = 90

  validation {
    condition     = var.dias_para_expirar_versoes_antigas >= 7
    error_message = "Mantenha as versões antigas por pelo menos 7 dias."
  }
}
