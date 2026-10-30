# Alarmes de gasto. Avisam por e-mail; não impedem gastos.
#
# Se você já criou estes orçamentos à mão (guia AWS do zero, passo 5), o Terraform recusará
# criá-los de novo. Veja no README desta pasta como importá-los ou como desligar esta parte.

resource "aws_budgets_budget" "alerta" {
  for_each = var.criar_orcamentos ? toset([for v in var.limites_dos_orcamentos_usd : tostring(v)]) : toset([])

  name         = "${var.projeto}-alerta-${each.key}"
  budget_type  = "COST"
  limit_amount = each.key
  limit_unit   = "USD"
  time_unit    = "MONTHLY"

  # Mede o consumo bruto. Com os créditos descontados, o custo líquido ficaria em zero
  # enquanto houvesse crédito, e o alarme nunca dispararia.
  cost_types {
    include_credit = false
    include_refund = false
  }

  notification {
    comparison_operator        = "GREATER_THAN"
    threshold                  = 100
    threshold_type             = "PERCENTAGE"
    notification_type          = "ACTUAL"
    subscriber_email_addresses = var.emails_de_alerta
  }

  notification {
    comparison_operator        = "GREATER_THAN"
    threshold                  = 100
    threshold_type             = "PERCENTAGE"
    notification_type          = "FORECASTED"
    subscriber_email_addresses = var.emails_de_alerta
  }

  lifecycle {
    precondition {
      condition     = length(var.emails_de_alerta) > 0
      error_message = "Informe ao menos um e-mail em emails_de_alerta (ou desligue com criar_orcamentos = false)."
    }
  }
}
