# Acesso do GitHub Actions à AWS por OIDC: o workflow troca um token de curta duração, emitido
# pelo GitHub, por credenciais temporárias. Não existe chave de acesso da AWS guardada no GitHub.

resource "aws_iam_openid_connect_provider" "github" {
  url            = "https://token.actions.githubusercontent.com"
  client_id_list = ["sts.amazonaws.com"]
}

data "aws_iam_policy_document" "ci_confianca" {
  statement {
    effect  = "Allow"
    actions = ["sts:AssumeRoleWithWebIdentity"]

    principals {
      type        = "Federated"
      identifiers = [aws_iam_openid_connect_provider.github.arn]
    }

    condition {
      test     = "StringEquals"
      variable = "token.actions.githubusercontent.com:aud"
      values   = ["sts.amazonaws.com"]
    }

    # Só este repositório, e só em dois casos:
    #   · workflows na branch main;
    #   · jobs do environment protegido (que exige aprovação). Quando um job usa um environment,
    #     o GitHub troca o "sub" do token e ele deixa de conter a branch.
    condition {
      test     = "StringEquals"
      variable = "token.actions.githubusercontent.com:sub"
      values = [
        "repo:${var.github_repositorio}:ref:refs/heads/main",
        "repo:${var.github_repositorio}:environment:${var.github_ambiente}",
      ]
    }
  }
}

resource "aws_iam_role" "ci" {
  name                 = "${var.projeto}-ci"
  description          = "Assumido pelo GitHub Actions (OIDC) para publicar a imagem e aplicar o Terraform"
  assume_role_policy   = data.aws_iam_policy_document.ci_confianca.json
  max_session_duration = 3600
}

# LIMITAÇÃO CONSCIENTE: o CD aplica Terraform que cria rede, banco, cluster e papéis, o que na
# prática exige poderes de administrador. A proteção está em QUEM pode assumir o papel (a trava
# de confiança acima), não no que ele pode fazer. Em produção real, isso se reduziria a uma
# política escrita para os recursos exatos, ou a papéis separados para publicar e para aplicar.
resource "aws_iam_role_policy_attachment" "ci_administrador" {
  role       = aws_iam_role.ci.name
  policy_arn = "arn:aws:iam::aws:policy/AdministratorAccess"
}
