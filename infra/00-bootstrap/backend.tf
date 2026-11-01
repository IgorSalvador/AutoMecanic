# Passo 2 do README: depois de criar o bucket, renomeie este arquivo para backend.tf, troque
# <NOME-DO-BUCKET> pelo output `nome_do_bucket` e rode `terraform init -migrate-state`.
# A partir daí o estado desta própria camada também vive no bucket.
terraform {
  backend "s3" {
    bucket       = "automecanic-tfstate-976193265037"
    key          = "00-bootstrap/terraform.tfstate"
    region       = "us-east-1"
    encrypt      = true
    use_lockfile = true
  }
}
