output "nome_do_bucket" {
  description = "Bucket do estado; vai no bloco backend de cada camada."
  value       = aws_s3_bucket.estado.id
}

output "regiao" {
  description = "Região do bucket."
  value       = var.regiao
}

output "configuracao_do_backend" {
  description = "Bloco backend para colar no backend.tf de uma camada (troque a chave pelo caminho da camada)."
  value       = <<-EOT
    terraform {
      backend "s3" {
        bucket       = "${aws_s3_bucket.estado.id}"
        key          = "<camada>/terraform.tfstate"
        region       = "${var.regiao}"
        encrypt      = true
        use_lockfile = true
      }
    }
  EOT
}
