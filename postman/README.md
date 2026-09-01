# Coleção Postman

Coleção completa da API do AutoMecanic: **98 requisições** cobrindo todos os 73 endpoints,
mais um fluxo guiado com verificações automáticas.

| Arquivo | O que é |
|---|---|
| `AutoMecanic.postman_collection.json` | A coleção |
| `AutoMecanic-Local.postman_environment.json` | Ambiente local (`http://localhost:8080`) |

---

## Como usar

**1. Suba o ambiente**

```bash
docker compose up -d --build
```

**2. Importe no Postman**

*Import* → arraste os dois arquivos → selecione o ambiente **AutoMecanic — Local** no canto
superior direito.

> Também funciona em **Insomnia** e **Bruno**, que importam o formato Postman v2.1.

**3. Ajuste a senha do administrador**

Na variável `senhaAdmin` (da coleção ou do ambiente), coloque o valor de `SEED_SENHA_ADMIN`
do seu arquivo `.env`.

**4. Execute a pasta `00 · Fluxo completo`**

De cima para baixo, ou de uma vez pelo **Collection Runner**. A requisição de login guarda o
token; as demais o herdam automaticamente.

---

## Organização

### `00 · Fluxo completo da Ordem de Serviço` — 25 requisições

Percorre o ciclo de vida inteiro na ordem em que a oficina trabalha, encadeando token e
identificadores. Cada requisição traz verificações automáticas — **51 asserções no total**.

Inclui os casos negativos que demonstram as regras do domínio:

| Requisição | Demonstra |
|---|---|
| `05 · CPF inválido → 400` | Validação por dígito verificador, com rejeição de sequências repetidas |
| `11 · Reserva não mexe no saldo físico` | A separação entre saldo em estoque, reservado e disponível |
| `14 · Alterar item após envio → 422` | Itens congelados: o cliente aprova o valor que viu |
| `16 · Acompanhamento com documento de terceiro → 404` | Resposta idêntica à de OS inexistente |
| `20 · Entregar sem finalizar → 422` | Não há caminho para *Entregue* sem passar por *Finalizada* |
| `23 · Cancelar OS entregue → 422` | Estado terminal não admite transições |

### `01` a `10` — referência por recurso

Todos os endpoints, agrupados por recurso, **gerados a partir do OpenAPI da própria API**.
Corpos de exemplo já preenchidos com valores válidos — o CPF e a placa passam pela validação
do domínio, então as requisições funcionam como estão.

---

## Executando pela linha de comando

Com o [Newman](https://github.com/postmanlabs/newman):

```bash
npx newman run postman/AutoMecanic.postman_collection.json \
  --folder "00 · Fluxo completo da Ordem de Serviço" \
  --env-var "senhaAdmin=SUA_SENHA_DO_ENV"
```

Resultado esperado: **25 requisições, 51 asserções, 0 falhas.**

É exatamente isso que o job `ambiente-completo` do CI executa a cada push, o que impede a
coleção de envelhecer em silêncio.

---

## Regerando a coleção

A parte de referência é derivada do documento OpenAPI, então acompanha a API sozinha:

```bash
docker compose up -d              # a API precisa estar no ar
node tools/gerar-collection.mjs
```

Opções: `--url http://localhost:8080` e `--saida postman`.

O gerador está em [`tools/gerar-collection.mjs`](../tools/gerar-collection.mjs). A pasta do
fluxo completo é escrita à mão dentro dele — é a parte que exige ordem e conhecimento do
negócio, e que uma conversão automática não teria como produzir.

---

## Variáveis

| Variável | Preenchida por | Uso |
|---|---|---|
| `baseUrl` | ambiente | Endereço da API |
| `emailAdmin` · `senhaAdmin` | ambiente | Credenciais do administrador semeado |
| `token` | requisição `02 · Login` | JWT das requisições autenticadas |
| `osId` · `osNumero` | requisição `06 · Recepção` | Ordem de Serviço criada no fluxo |
| `servicoId` · `pecaId` | requisições `03` e `04` | Itens do catálogo e do estoque |
| `itemPecaId` · `itemServicoId` | requisições do fluxo | Itens dentro da OS |
| `clienteId` · `veiculoId` · `usuarioId` | preencher à mão | Usadas na pasta de referência |

---

## Alternativa: arquivo `.http`

Quem prefere não sair do editor pode usar [`AutoMecanic.http`](../AutoMecanic.http), na raiz
do repositório, com a extensão **REST Client** do VS Code. Cobre o mesmo fluxo, sem
verificações automáticas.
