# Repositório das imagens do contêiner.

resource "aws_ecr_repository" "api" {
  name = "${var.projeto}-api"

  # O CD publica a tag do commit e, em releases, a tag vX.Y.Z. Mutável para que reexecutar um
  # workflow no mesmo commit não falhe ao republicar a mesma tag.
  image_tag_mutability = "MUTABLE"

  # Permite que o terraform destroy apague o repositório mesmo com imagens dentro. Sem isto
  # o destroy falha e o repositório (e as imagens, que cobram armazenamento) ficam para trás.
  force_delete = true

  image_scanning_configuration {
    scan_on_push = true
  }

  encryption_configuration {
    encryption_type = "AES256"
  }
}

# Mantém só as 10 imagens mais recentes: cada uma ocupa armazenamento, que é cobrado.
resource "aws_ecr_lifecycle_policy" "api" {
  repository = aws_ecr_repository.api.name

  policy = jsonencode({
    rules = [{
      rulePriority = 1
      description  = "Manter as 10 imagens mais recentes"
      selection = {
        tagStatus   = "any"
        countType   = "imageCountMoreThan"
        countNumber = 10
      }
      action = { type = "expire" }
    }]
  })
}
