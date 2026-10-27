# Bucket S3 que guarda o estado do Terraform das camadas 10-base e 20-cluster.
#
# É a única camada que não pode usar o próprio bucket (ele ainda não existe): nasce com estado
# local e depois migra para o bucket. Ver README.md desta pasta.

data "aws_caller_identity" "atual" {}

locals {
  nome_do_bucket = coalesce(var.nome_do_bucket, "${var.projeto}-tfstate-${data.aws_caller_identity.atual.account_id}")
}

resource "aws_s3_bucket" "estado" {
  bucket = local.nome_do_bucket

  # Perder este bucket é perder o mapa de tudo o que o Terraform criou. Destruí-lo exige
  # remover esta trava de propósito.
  lifecycle {
    prevent_destroy = true
  }
}

# Versionamento: cada gravação do estado vira uma versão, e um estado corrompido ou apagado
# por engano pode ser recuperado.
resource "aws_s3_bucket_versioning" "estado" {
  bucket = aws_s3_bucket.estado.id

  versioning_configuration {
    status = "Enabled"
  }
}

# O estado pode conter segredos (a senha do RDS, por exemplo); fica criptografado em repouso.
resource "aws_s3_bucket_server_side_encryption_configuration" "estado" {
  bucket = aws_s3_bucket.estado.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
    bucket_key_enabled = true
  }
}

resource "aws_s3_bucket_public_access_block" "estado" {
  bucket = aws_s3_bucket.estado.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

# Desliga as ACLs: só o dono do bucket (e as políticas do IAM) decide quem acessa.
resource "aws_s3_bucket_ownership_controls" "estado" {
  bucket = aws_s3_bucket.estado.id

  rule {
    object_ownership = "BucketOwnerEnforced"
  }
}

resource "aws_s3_bucket_lifecycle_configuration" "estado" {
  bucket = aws_s3_bucket.estado.id

  # O versionamento acumula versões; as antigas não precisam ficar para sempre.
  rule {
    id     = "expirar-versoes-antigas"
    status = "Enabled"

    filter {}

    noncurrent_version_expiration {
      noncurrent_days = var.dias_para_expirar_versoes_antigas
    }

    abort_incomplete_multipart_upload {
      days_after_initiation = 7
    }
  }

  depends_on = [aws_s3_bucket_versioning.estado]
}

# Recusa qualquer acesso que não use TLS.
data "aws_iam_policy_document" "somente_tls" {
  statement {
    sid     = "RecusarAcessoSemTLS"
    effect  = "Deny"
    actions = ["s3:*"]

    resources = [
      aws_s3_bucket.estado.arn,
      "${aws_s3_bucket.estado.arn}/*",
    ]

    principals {
      type        = "*"
      identifiers = ["*"]
    }

    condition {
      test     = "Bool"
      variable = "aws:SecureTransport"
      values   = ["false"]
    }
  }
}

resource "aws_s3_bucket_policy" "estado" {
  bucket = aws_s3_bucket.estado.id
  policy = data.aws_iam_policy_document.somente_tls.json

  # A política e o bloqueio de acesso público são gravados em seguida; a ordem evita conflito.
  depends_on = [aws_s3_bucket_public_access_block.estado]
}
