#!/usr/bin/env node
/**
 * Gera a coleção Postman do AutoMecanic a partir do documento OpenAPI da própria API.
 *
 * A geração a partir do OpenAPI é deliberada: a coleção passa a ser um reflexo do
 * contrato publicado, e não uma segunda fonte de verdade que envelhece em silêncio.
 * Um endpoint novo aparece na coleção ao regerar; um endpoint removido some.
 *
 * O que o gerador acrescenta por cima do OpenAPI:
 *   · uma pasta "Fluxo completo", escrita à mão, que percorre o ciclo de vida da Ordem
 *     de Serviço na ordem certa e encadeia token e identificadores entre as requisições;
 *   · corpos de exemplo preenchidos com valores plausíveis (CPF e placa válidos), e não
 *     com o "string" genérico que os conversores costumam produzir;
 *   · scripts de teste que verificam o status esperado e guardam variáveis.
 *
 * Uso:
 *   node tools/gerar-collection.mjs [--url http://localhost:8080] [--saida postman/]
 *
 * Requer a API no ar (ela serve o /swagger/v1/swagger.json).
 */

import fs from 'node:fs';
import path from 'node:path';

// ─────────────────────────────────────────────────────────────────────────────
// Parâmetros
// ─────────────────────────────────────────────────────────────────────────────

const args = process.argv.slice(2);
const opcao = (nome, padrao) => {
  const i = args.indexOf(`--${nome}`);
  return i >= 0 && args[i + 1] ? args[i + 1] : padrao;
};

const URL_BASE = opcao('url', 'http://localhost:8080');
const SAIDA = opcao('saida', 'postman');

// ─────────────────────────────────────────────────────────────────────────────
// Valores de exemplo
//
// Preenchem os corpos gerados a partir do schema. Os documentos e placas são
// fictícios, porém válidos: o domínio recusa dígito verificador incorreto, então
// um exemplo com "string" seria rejeitado antes de chegar ao caso de uso — e a
// coleção nasceria inútil.
// ─────────────────────────────────────────────────────────────────────────────

const EXEMPLOS = {
  documento: '111.444.777-35',
  documentoCliente: '111.444.777-35',
  placa: 'JKL5M67',
  nome: 'Carlos Andrade',
  nomeCliente: 'Carlos Andrade',
  email: 'carlos.andrade@exemplo.com.br',
  emailCliente: 'carlos.andrade@exemplo.com.br',
  telefone: '(11) 91234-5678',
  telefoneCliente: '(11) 91234-5678',
  marca: 'Toyota',
  modelo: 'Corolla XEi 2.0',
  cor: 'Prata',
  anoFabricacao: 2021,
  anoModelo: 2022,
  quilometragem: 42000,
  quilometragemEntrada: 42000,
  descricaoProblema: 'Ruído metálico ao frear em baixa velocidade.',
  diagnostico: 'Pastilhas dianteiras no limite de desgaste. Fluido de freio escurecido.',
  observacao: 'Serviço concluído e testado.',
  motivo: 'Justificativa da operação.',
  descricao: 'Descrição detalhada.',
  codigo: 'PECA-EXEMPLO-001',
  senha: 'Senha@Forte1',
  senhaAtual: 'Senha@Forte1',
  novaSenha: 'NovaSenha@2026',
  quantidade: 1,
  quantidadeInicial: 10,
  quantidadeApurada: 10,
  estoqueMinimo: 3,
  preco: 199.9,
  precoUnitario: 89.9,
  novoPreco: 249.9,
  percentualDesconto: 10,
  validadeEmDias: 7,
  tempoEstimadoEmMinutos: 60,
  logradouro: 'Rua das Oficinas',
  numero: '1500',
  complemento: 'Sala 2',
  bairro: 'Centro',
  cidade: 'São Paulo',
  uf: 'SP',
  cep: '01310-100',
};

/**
 * Variáveis cujo significado independe do recurso.
 */
const VARIAVEIS_POR_PARAMETRO = {
  clienteId: '{{clienteId}}',
  responsavelId: '{{usuarioId}}',
  documento: '{{documentoCliente}}',
  placa: '{{placaVeiculo}}',
  codigo: '{{codigoPeca}}',
  numero: '{{osNumero}}',
};

/**
 * Variável do identificador principal, por recurso.
 *
 * `{id}` significa coisas diferentes em cada rota: em `/pecas/{id}` é uma peça, em
 * `/clientes/{id}` é um cliente. Resolver só pelo nome do parâmetro apontaria todas
 * as rotas para a mesma variável, e a coleção de referência viria com identificadores
 * trocados — funcionando por acaso, quando funcionasse.
 */
const VARIAVEL_DO_RECURSO = {
  'ordens-servico': '{{osId}}',
  clientes: '{{clienteId}}',
  veiculos: '{{veiculoId}}',
  servicos: '{{servicoId}}',
  pecas: '{{pecaId}}',
  usuarios: '{{usuarioId}}',
};

/**
 * Variável de um identificador de item dentro da Ordem de Serviço, que também depende
 * do trecho da rota: `/{id}/servicos/{itemId}` e `/{id}/pecas/{itemId}` são itens de
 * coleções distintas.
 */
function variavelDoParametro(caminho, nomeDoParametro) {
  if (nomeDoParametro in VARIAVEIS_POR_PARAMETRO) {
    return VARIAVEIS_POR_PARAMETRO[nomeDoParametro];
  }

  const segmentos = caminho.split('/').filter(Boolean);

  if (nomeDoParametro === 'itemId') {
    if (caminho.includes('/servicos/')) return '{{itemServicoId}}';
    if (caminho.includes('/pecas/')) return '{{itemPecaId}}';
    return '{{itemId}}';
  }

  if (nomeDoParametro === 'id') {
    // O recurso é o segmento imediatamente anterior ao primeiro parâmetro de caminho.
    const recurso = segmentos.find(s => VARIAVEL_DO_RECURSO[s]);
    if (recurso) return VARIAVEL_DO_RECURSO[recurso];
  }

  return `{{${nomeDoParametro}}}`;
}

// ─────────────────────────────────────────────────────────────────────────────
// Resolução de schema → corpo de exemplo
// ─────────────────────────────────────────────────────────────────────────────

function resolver(schema, doc, profundidade = 0) {
  if (!schema || profundidade > 6) return {};

  if (schema.$ref) {
    const nome = schema.$ref.replace('#/components/schemas/', '');
    return resolver(doc.components?.schemas?.[nome], doc, profundidade + 1);
  }

  if (schema.allOf) {
    return schema.allOf.reduce(
      (acc, parte) => Object.assign(acc, resolver(parte, doc, profundidade + 1)),
      {},
    );
  }

  return schema;
}

function exemploDoSchema(schema, doc, nomeDoCampo = '', profundidade = 0) {
  const s = resolver(schema, doc, profundidade);

  if (s.enum?.length) return s.enum[0];
  if (nomeDoCampo && nomeDoCampo in EXEMPLOS) return EXEMPLOS[nomeDoCampo];

  switch (s.type) {
    case 'object': {
      const corpo = {};
      for (const [campo, sub] of Object.entries(s.properties ?? {})) {
        corpo[campo] = exemploDoSchema(sub, doc, campo, profundidade + 1);
      }
      return corpo;
    }
    case 'array':
      return [exemploDoSchema(s.items, doc, nomeDoCampo, profundidade + 1)];
    case 'integer':
      return 0;
    case 'number':
      return 0.0;
    case 'boolean':
      return true;
    case 'string':
      if (s.format === 'uuid') return '00000000-0000-0000-0000-000000000000';
      if (s.format === 'date-time') return new Date().toISOString();
      return '';
    default:
      return s.properties ? exemploDoSchema({ ...s, type: 'object' }, doc, nomeDoCampo, profundidade) : null;
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// Conversão OpenAPI → itens Postman
// ─────────────────────────────────────────────────────────────────────────────

const TITULOS_DE_PASTA = {
  Autenticacao: '01 · Autenticação',
  Acompanhamento: '02 · Acompanhamento (público)',
  OrdensServico: '03 · Ordens de Serviço',
  Clientes: '04 · Clientes',
  Veiculos: '05 · Veículos',
  Servicos: '06 · Catálogo de Serviços',
  Pecas: '07 · Peças e Estoque',
  Usuarios: '08 · Usuários',
  MeuPerfil: '09 · Meu Perfil',
  Indicadores: '10 · Indicadores',
};

function urlPostman(caminho, parametros) {
  let bruto = `{{baseUrl}}${caminho}`;
  const variaveisDeCaminho = [];

  for (const p of parametros.filter(x => x.in === 'path')) {
    const valor = variavelDoParametro(caminho, p.name);
    bruto = bruto.replace(`{${p.name}}`, valor);
    variaveisDeCaminho.push({ key: p.name, value: valor, description: p.description ?? '' });
  }

  const query = parametros
    .filter(x => x.in === 'query')
    .map(p => ({
      key: p.name,
      value: '',
      description: p.description ?? '',
      // Parâmetros opcionais entram desabilitados: a requisição funciona ao ser
      // enviada como está, e quem quiser filtrar apenas marca a caixa.
      disabled: !p.required,
    }));

  if (query.some(q => !q.disabled)) {
    bruto += '?' + query.filter(q => !q.disabled).map(q => `${q.key}=`).join('&');
  }

  const [semQuery] = bruto.split('?');

  return {
    raw: bruto,
    host: ['{{baseUrl}}'],
    path: semQuery.replace('{{baseUrl}}', '').split('/').filter(Boolean),
    query,
    variable: variaveisDeCaminho,
  };
}

function requisicaoDeOperacao(caminho, metodo, op, doc) {
  const parametros = [...(op.parameters ?? [])];
  const anonima = op.security !== undefined && op.security.length === 0
    ? true
    : caminho.includes('/autenticacao/') || caminho.includes('/acompanhamento');

  const cabecalhos = [];
  const corpoSchema = op.requestBody?.content?.['application/json']?.schema;

  if (corpoSchema) {
    cabecalhos.push({ key: 'Content-Type', value: 'application/json' });
  }

  const item = {
    name: `${metodo.toUpperCase()} ${caminho}`,
    request: {
      method: metodo.toUpperCase(),
      header: cabecalhos,
      url: urlPostman(caminho, parametros),
      description: [op.summary, op.description, op.remarks].filter(Boolean).join('\n\n'),
    },
    response: [],
  };

  if (anonima) {
    item.request.auth = { type: 'noauth' };
  }

  if (corpoSchema) {
    item.request.body = {
      mode: 'raw',
      raw: JSON.stringify(exemploDoSchema(corpoSchema, doc), null, 2),
      options: { raw: { language: 'json' } },
    };
  }

  return item;
}

function pastasDeReferencia(doc) {
  const porTag = new Map();

  for (const [caminho, item] of Object.entries(doc.paths)) {
    for (const [metodo, op] of Object.entries(item)) {
      if (!['get', 'post', 'put', 'patch', 'delete'].includes(metodo)) continue;

      const tag = op.tags?.[0] ?? 'Outros';
      if (!porTag.has(tag)) porTag.set(tag, []);
      porTag.get(tag).push(requisicaoDeOperacao(caminho, metodo, op, doc));
    }
  }

  return [...porTag.entries()]
    .sort(([a], [b]) => (TITULOS_DE_PASTA[a] ?? a).localeCompare(TITULOS_DE_PASTA[b] ?? b, 'pt-BR'))
    .map(([tag, itens]) => ({
      name: TITULOS_DE_PASTA[tag] ?? tag,
      description: `Todas as operações de ${tag}, geradas a partir do OpenAPI.`,
      item: itens.sort((a, b) => a.name.localeCompare(b.name, 'pt-BR')),
    }));
}

// ─────────────────────────────────────────────────────────────────────────────
// Pasta "Fluxo completo" — escrita à mão, na ordem em que a oficina trabalha
// ─────────────────────────────────────────────────────────────────────────────

const teste = (...linhas) => ({
  listen: 'test',
  script: { type: 'text/javascript', exec: linhas },
});

const esperaStatus = (codigo, rotulo) =>
  `pm.test("${rotulo}", () => pm.response.to.have.status(${codigo}));`;

const guarda = (variavel, caminho) =>
  `pm.collectionVariables.set("${variavel}", pm.response.json()${caminho});`;

function requisicao({ nome, metodo, caminho, corpo, descricao, anonima, testes }) {
  const item = {
    name: nome,
    request: {
      method: metodo,
      header: corpo ? [{ key: 'Content-Type', value: 'application/json' }] : [],
      url: {
        raw: `{{baseUrl}}${caminho}`,
        host: ['{{baseUrl}}'],
        path: caminho.split('?')[0].split('/').filter(Boolean),
        query: caminho.includes('?')
          ? caminho.split('?')[1].split('&').map(p => {
              const [key, ...resto] = p.split('=');
              return { key, value: resto.join('=') };
            })
          : [],
      },
      description: descricao,
    },
    response: [],
  };

  if (anonima) item.request.auth = { type: 'noauth' };
  if (corpo) {
    item.request.body = { mode: 'raw', raw: JSON.stringify(corpo, null, 2), options: { raw: { language: 'json' } } };
  }
  if (testes?.length) item.event = [teste(...testes)];

  return item;
}

function pastaDeFluxo() {
  return {
    name: '00 · Fluxo completo da Ordem de Serviço',
    description:
      'Percorre o ciclo de vida inteiro na ordem em que a oficina trabalha. Cada requisição '
      + 'guarda o que a próxima precisa, então basta executar de cima para baixo — ou usar o '
      + 'Collection Runner para rodar tudo de uma vez.\n\n'
      + 'Inclui os casos negativos que demonstram as regras: itens congelados após o envio do '
      + 'orçamento, acompanhamento com documento de terceiro e transição inválida em estado terminal.',
    item: [
      requisicao({
        nome: '01 · Prontidão da API',
        metodo: 'GET',
        caminho: '/health/pronto',
        anonima: true,
        descricao: 'Só responde saudável com o banco de dados acessível.',
        testes: [esperaStatus(200, 'API pronta')],
      }),

      requisicao({
        nome: '02 · Login — guarda o token',
        metodo: 'POST',
        caminho: '/api/v1/autenticacao/login',
        anonima: true,
        corpo: { email: '{{emailAdmin}}', senha: '{{senhaAdmin}}' },
        descricao:
          'Autentica e guarda o JWT na variável `token`, usada por todas as requisições seguintes.\n\n'
          + 'Ajuste `senhaAdmin` nas variáveis da coleção para o valor de `SEED_SENHA_ADMIN` do seu `.env`.',
        testes: [
          esperaStatus(200, 'Login bem-sucedido'),
          guarda('token', '.token'),
          'pm.test("Resposta não expõe hash de senha", () =>',
          '    pm.expect(pm.response.text().toLowerCase()).to.not.include("senhahash"));',
        ],
      }),

      requisicao({
        nome: '03 · Catálogo — guarda um serviço',
        metodo: 'GET',
        caminho: '/api/v1/servicos?apenasAtivos=true&tamanhoPagina=5',
        descricao: 'O seed cria 10 serviços com preço de tabela e tempo estimado.',
        testes: [
          esperaStatus(200, 'Catálogo disponível'),
          'pm.test("Catálogo semeado", () => pm.expect(pm.response.json().itens).to.not.be.empty);',
          guarda('servicoId', '.itens[0].id'),
        ],
      }),

      requisicao({
        nome: '04 · Estoque — guarda uma peça',
        metodo: 'GET',
        caminho: '/api/v1/pecas?apenasAtivas=true&tamanhoPagina=5',
        descricao: 'Repare nos três saldos: em estoque, reservada e disponível.',
        testes: [
          esperaStatus(200, 'Estoque disponível'),
          guarda('pecaId', '.itens[0].id'),
          'pm.collectionVariables.set("saldoAntes", pm.response.json().itens[0].quantidadeEmEstoque);',
        ],
      }),

      requisicao({
        nome: '05 · CPF inválido → 400',
        metodo: 'POST',
        caminho: '/api/v1/clientes',
        corpo: {
          nome: 'Cliente Inválido',
          documento: '111.111.111-11',
          email: 'invalido@exemplo.com',
          telefone: '11987654321',
        },
        descricao:
          'Sequências repetidas passam no cálculo do módulo 11, mas não são CPFs válidos. '
          + 'O domínio as rejeita explicitamente.',
        testes: [esperaStatus(400, 'CPF inválido recusado')],
      }),

      requisicao({
        nome: '06 · Recepção do veículo → Recebida',
        metodo: 'POST',
        caminho: '/api/v1/ordens-servico/recepcao',
        corpo: {
          documentoCliente: '{{documentoCliente}}',
          nomeCliente: 'Carlos Andrade',
          emailCliente: 'carlos.andrade@exemplo.com.br',
          telefoneCliente: '(11) 91234-5678',
          placa: '{{placaVeiculo}}',
          marca: 'Toyota',
          modelo: 'Corolla XEi 2.0',
          anoFabricacao: 2021,
          anoModelo: 2022,
          cor: 'Prata',
          descricaoProblema: 'Ruído metálico ao frear em baixa velocidade.',
          quilometragemEntrada: 42000,
        },
        descricao:
          'Identifica o cliente pelo CPF/CNPJ, localiza ou cadastra o veículo pela placa e abre '
          + 'a OS — tudo em uma transação. É o balcão da oficina em uma chamada.',
        testes: [
          esperaStatus(201, 'OS aberta'),
          'pm.test("Nasce no status Recebida", () => pm.expect(pm.response.json().status).to.eql("Recebida"));',
          'pm.test("Número no formato OS-AAAA-NNNNNN", () =>',
          '    pm.expect(pm.response.json().numero).to.match(/^OS-\\d{4}-\\d{6}$/));',
          guarda('osId', '.id'),
          guarda('osNumero', '.numero'),
        ],
      }),

      requisicao({
        nome: '07 · Iniciar diagnóstico → Em diagnóstico',
        metodo: 'POST',
        caminho: '/api/v1/ordens-servico/{{osId}}/diagnostico/iniciar',
        testes: [
          esperaStatus(200, 'Diagnóstico iniciado'),
          'pm.test("Status Em diagnóstico", () => pm.expect(pm.response.json().status).to.eql("EmDiagnostico"));',
        ],
      }),

      requisicao({
        nome: '08 · Registrar laudo técnico',
        metodo: 'POST',
        caminho: '/api/v1/ordens-servico/{{osId}}/diagnostico',
        corpo: { diagnostico: 'Pastilhas dianteiras no limite de desgaste. Fluido de freio escurecido.' },
        testes: [esperaStatus(200, 'Laudo registrado')],
      }),

      requisicao({
        nome: '09 · Incluir serviço — preço congelado',
        metodo: 'POST',
        caminho: '/api/v1/ordens-servico/{{osId}}/servicos',
        corpo: { servicoId: '{{servicoId}}', quantidade: 1 },
        descricao:
          'O preço vem do catálogo e é copiado para o item. Um reajuste posterior na tabela '
          + 'não altera este orçamento.',
        testes: [
          esperaStatus(200, 'Serviço incluído'),
          'pm.test("Preço foi congelado no item", () =>',
          '    pm.expect(pm.response.json().servicos[0].precoUnitario).to.be.above(0));',
        ],
      }),

      requisicao({
        nome: '10 · Incluir peça — reserva no estoque',
        metodo: 'POST',
        caminho: '/api/v1/ordens-servico/{{osId}}/pecas',
        corpo: { pecaId: '{{pecaId}}', quantidade: 4 },
        descricao:
          'A inclusão reserva a quantidade no estoque na mesma transação. Sem saldo disponível, '
          + 'a operação inteira falha e a OS não muda.',
        testes: [
          esperaStatus(200, 'Peça incluída'),
          'pm.test("Peça marcada como reservada", () =>',
          '    pm.expect(pm.response.json().pecas[0].reservada).to.be.true);',
          guarda('itemPecaId', '.pecas[0].id'),
        ],
      }),

      requisicao({
        nome: '11 · Reserva não mexe no saldo físico',
        metodo: 'GET',
        caminho: '/api/v1/pecas/{{pecaId}}',
        descricao:
          'O ponto mais importante do controle de estoque: a peça continua na prateleira, mas '
          + 'deixa de ser prometível a outra Ordem de Serviço.',
        testes: [
          esperaStatus(200, 'Peça consultada'),
          'const p = pm.response.json();',
          'pm.test("Saldo físico intacto", () =>',
          '    pm.expect(p.quantidadeEmEstoque).to.eql(Number(pm.collectionVariables.get("saldoAntes"))));',
          'pm.test("Reservado = 4", () => pm.expect(p.quantidadeReservada).to.eql(4));',
          'pm.test("Disponível caiu 4", () =>',
          '    pm.expect(p.quantidadeDisponivel).to.eql(p.quantidadeEmEstoque - 4));',
        ],
      }),

      requisicao({
        nome: '12 · Gerar orçamento — valor calculado',
        metodo: 'POST',
        caminho: '/api/v1/ordens-servico/{{osId}}/orcamento',
        corpo: { percentualDesconto: 10 },
        descricao: 'O valor nunca é informado: é a soma dos itens com o desconto aplicado.',
        testes: [
          esperaStatus(200, 'Orçamento gerado'),
          'const o = pm.response.json().orcamento;',
          'pm.test("Total = bruto com desconto", () =>',
          '    pm.expect(o.valorTotal).to.be.closeTo(o.valorBruto * 0.9, 0.01));',
          'pm.test("Situação Em elaboração", () => pm.expect(o.status).to.eql("EmElaboracao"));',
        ],
      }),

      requisicao({
        nome: '13 · Enviar ao cliente → Aguardando aprovação',
        metodo: 'POST',
        caminho: '/api/v1/ordens-servico/{{osId}}/orcamento/enviar',
        corpo: { validadeEmDias: 7 },
        testes: [
          esperaStatus(200, 'Orçamento enviado'),
          'pm.test("Status Aguardando aprovação", () =>',
          '    pm.expect(pm.response.json().status).to.eql("AguardandoAprovacao"));',
        ],
      }),

      requisicao({
        nome: '14 · Alterar item após envio → 422',
        metodo: 'POST',
        caminho: '/api/v1/ordens-servico/{{osId}}/servicos',
        corpo: { servicoId: '{{servicoId}}', quantidade: 1 },
        descricao:
          'A regra que garante que o cliente aprova exatamente o valor que viu. Mudar o escopo '
          + 'exige devolver a OS ao diagnóstico e gerar um orçamento novo.',
        testes: [
          esperaStatus(422, 'Itens congelados'),
          'pm.test("Código da regra violada", () =>',
          '    pm.expect(pm.response.json().codigo).to.eql("ITENS_CONGELADOS"));',
        ],
      }),

      requisicao({
        nome: '15 · Acompanhamento pelo cliente (sem token)',
        metodo: 'GET',
        caminho: '/api/v1/acompanhamento?numero={{osNumero}}&documento=11144477735',
        anonima: true,
        descricao:
          'Número da OS e documento juntos funcionam como prova de posse. A visão é reduzida: '
          + 'sem responsável técnico, sem custo por peça, sem identificadores internos.',
        testes: [
          esperaStatus(200, 'Acompanhamento acessível'),
          'const a = pm.response.json();',
          'pm.test("Não expõe dados internos", () => {',
          '    pm.expect(a.responsavelId).to.be.undefined;',
          '    pm.expect(a.clienteId).to.be.undefined;',
          '});',
        ],
      }),

      requisicao({
        nome: '16 · Acompanhamento com documento de terceiro → 404',
        metodo: 'GET',
        caminho: '/api/v1/acompanhamento?numero={{osNumero}}&documento=52998224725',
        anonima: true,
        descricao:
          'Resposta idêntica à de OS inexistente. Distingui-las permitiria percorrer números '
          + 'sequenciais e ler dados de terceiros.',
        testes: [esperaStatus(404, 'Documento de terceiro recusado')],
      }),

      requisicao({
        nome: '17 · Aprovar → Em execução e baixa do estoque',
        metodo: 'POST',
        caminho: '/api/v1/ordens-servico/{{osId}}/orcamento/aprovar',
        descricao: 'A aprovação é o momento em que a peça sai fisicamente da prateleira.',
        testes: [
          esperaStatus(200, 'Orçamento aprovado'),
          'const os = pm.response.json();',
          'pm.test("Status Em execução", () => pm.expect(os.status).to.eql("EmExecucao"));',
          'pm.test("Peça consumida", () => pm.expect(os.pecas[0].consumida).to.be.true);',
        ],
      }),

      requisicao({
        nome: '18 · Saldo físico caiu',
        metodo: 'GET',
        caminho: '/api/v1/pecas/{{pecaId}}',
        testes: [
          esperaStatus(200, 'Peça consultada'),
          'const p = pm.response.json();',
          'pm.test("Saldo reduzido em 4", () =>',
          '    pm.expect(p.quantidadeEmEstoque).to.eql(Number(pm.collectionVariables.get("saldoAntes")) - 4));',
          'pm.test("Reserva zerada", () => pm.expect(p.quantidadeReservada).to.eql(0));',
        ],
      }),

      requisicao({
        nome: '19 · Razão de estoque registrou a saída',
        metodo: 'GET',
        caminho: '/api/v1/pecas/movimentos?ordemServicoId={{osId}}',
        descricao:
          'O lançamento é gravado na mesma transação que alterou o saldo: não existe saldo sem '
          + 'lançamento correspondente.',
        testes: [
          esperaStatus(200, 'Extrato disponível'),
          'pm.test("Há uma saída de 4 unidades", () =>',
          '    pm.expect(pm.response.json().itens.some(m => m.tipo === "Saida" && m.quantidade === 4)).to.be.true);',
        ],
      }),

      requisicao({
        nome: '20 · Entregar sem finalizar → 422',
        metodo: 'POST',
        caminho: '/api/v1/ordens-servico/{{osId}}/entregar',
        corpo: {},
        descricao: 'Não existe caminho para Entregue que não passe por Finalizada.',
        testes: [esperaStatus(422, 'Transição inválida recusada')],
      }),

      requisicao({
        nome: '21 · Finalizar → Finalizada',
        metodo: 'POST',
        caminho: '/api/v1/ordens-servico/{{osId}}/finalizar',
        corpo: { observacao: 'Pastilhas substituídas e fluido trocado. Teste em pista aprovado.' },
        testes: [
          esperaStatus(200, 'Serviço finalizado'),
          'const os = pm.response.json();',
          'pm.test("Status Finalizada", () => pm.expect(os.status).to.eql("Finalizada"));',
          'pm.test("Duração real calculada", () =>',
          '    pm.expect(os.duracaoDaExecucaoEmMinutos).to.not.be.null);',
        ],
      }),

      requisicao({
        nome: '22 · Entregar → Entregue',
        metodo: 'POST',
        caminho: '/api/v1/ordens-servico/{{osId}}/entregar',
        corpo: { observacao: 'Cliente retirou o veículo e conferiu o serviço.' },
        testes: [
          esperaStatus(200, 'Veículo entregue'),
          'const os = pm.response.json();',
          'pm.test("Status Entregue", () => pm.expect(os.status).to.eql("Entregue"));',
          'pm.test("Linha do tempo com 6 transições", () =>',
          '    pm.expect(os.historico.length).to.eql(6));',
        ],
      }),

      requisicao({
        nome: '23 · Cancelar OS entregue → 422',
        metodo: 'POST',
        caminho: '/api/v1/ordens-servico/{{osId}}/cancelar',
        corpo: { motivo: 'tentativa fora de hora' },
        descricao: 'Estado terminal não admite novas transições.',
        testes: [esperaStatus(422, 'Estado terminal recusa transição')],
      }),

      requisicao({
        nome: '24 · Tempo médio de execução',
        metodo: 'GET',
        caminho: '/api/v1/indicadores/tempo-medio-execucao',
        descricao:
          'A mediana acompanha a média porque uma única OS excepcionalmente longa distorce a '
          + 'média sem que a operação tenha piorado.',
        testes: [
          esperaStatus(200, 'Indicador disponível'),
          'pm.test("Há ordens finalizadas no período", () =>',
          '    pm.expect(pm.response.json().ordensFinalizadas).to.be.above(0));',
        ],
      }),

      requisicao({
        nome: '25 · Painel operacional',
        metodo: 'GET',
        caminho: '/api/v1/indicadores/painel',
        testes: [
          esperaStatus(200, 'Painel disponível'),
          'pm.test("Contagem por situação preenchida", () =>',
          '    pm.expect(Object.keys(pm.response.json().ordensPorStatus)).to.not.be.empty);',
        ],
      }),
    ],
  };
}

// ─────────────────────────────────────────────────────────────────────────────
// Montagem
// ─────────────────────────────────────────────────────────────────────────────

async function main() {
  const urlDoDocumento = `${URL_BASE}/swagger/v1/swagger.json`;

  console.log(`Lendo o OpenAPI de ${urlDoDocumento} …`);

  const resposta = await fetch(urlDoDocumento);

  if (!resposta.ok) {
    console.error(`Falha ao ler o OpenAPI (${resposta.status}). A API está no ar?`);
    console.error(`Suba o ambiente com: docker compose up -d`);
    process.exit(1);
  }

  const doc = await resposta.json();

  const colecao = {
    info: {
      _postman_id: 'a17e0c5e-0000-4000-8000-automecanic01',
      name: 'AutoMecanic — Oficina Mecânica',
      description:
        '# AutoMecanic\n\n'
        + 'Sistema Integrado de Atendimento e Execução de Serviços — Tech Challenge Fase 1, '
        + 'Pós-Tech FIAP 15SOAT.\n\n'
        + '## Como usar\n\n'
        + '1. Suba o ambiente: `docker compose up -d --build`\n'
        + '2. Ajuste a variável `senhaAdmin` para o valor de `SEED_SENHA_ADMIN` do seu `.env`\n'
        + '3. Execute a pasta **00 · Fluxo completo** de cima para baixo — ou pelo Collection Runner\n\n'
        + 'A autenticação é da coleção inteira: a requisição de login guarda o token e as demais '
        + 'o herdam. Os endpoints públicos (login e acompanhamento) estão marcados como sem autenticação.\n\n'
        + '## Organização\n\n'
        + '- **00 · Fluxo completo** — o ciclo de vida da Ordem de Serviço na ordem em que a oficina '
        + 'trabalha, com verificações automáticas. Inclui os casos negativos que demonstram as regras.\n'
        + '- **01 a 10** — referência de todos os endpoints, agrupados por recurso e gerados a partir '
        + 'do OpenAPI da própria API.\n\n'
        + `_Gerada a partir do OpenAPI em ${new Date().toISOString().slice(0, 10)}. `
        + 'Para regerar: `node tools/gerar-collection.mjs`._',
      schema: 'https://schema.getpostman.com/json/collection/v2.1.0/collection.json',
    },
    auth: {
      type: 'bearer',
      bearer: [{ key: 'token', value: '{{token}}', type: 'string' }],
    },
    event: [
      {
        listen: 'prerequest',
        script: {
          type: 'text/javascript',
          exec: [
            '// Avisa cedo, e com clareza, quando a coleção é executada fora de ordem.',
            'const precisaDeToken = !pm.request.url.toString().includes("/autenticacao/login")',
            '    && !pm.request.url.toString().includes("/acompanhamento")',
            '    && !pm.request.url.toString().includes("/health/");',
            '',
            'if (precisaDeToken && !pm.collectionVariables.get("token")) {',
            '    console.warn("Sem token: execute primeiro \'02 · Login\' na pasta do fluxo completo.");',
            '}',
          ],
        },
      },
    ],
    variable: [
      { key: 'baseUrl', value: URL_BASE, type: 'string' },
      { key: 'emailAdmin', value: 'admin@automecanic.com.br', type: 'string' },
      { key: 'senhaAdmin', value: 'Admin@AutoMecanic2026', type: 'string' },
      { key: 'documentoCliente', value: '111.444.777-35', type: 'string' },
      { key: 'placaVeiculo', value: 'JKL5M67', type: 'string' },
      { key: 'token', value: '', type: 'string' },
      { key: 'osId', value: '', type: 'string' },
      { key: 'osNumero', value: '', type: 'string' },
      { key: 'servicoId', value: '', type: 'string' },
      { key: 'pecaId', value: '', type: 'string' },
      { key: 'itemPecaId', value: '', type: 'string' },
      { key: 'itemServicoId', value: '', type: 'string' },
      { key: 'veiculoId', value: '', type: 'string' },
      { key: 'usuarioId', value: '', type: 'string' },
      { key: 'clienteId', value: '', type: 'string' },
      { key: 'itemId', value: '', type: 'string' },
      { key: 'codigoPeca', value: 'OL-5W30-1L', type: 'string' },
      { key: 'saldoAntes', value: '', type: 'string' },
    ],
    item: [pastaDeFluxo(), ...pastasDeReferencia(doc)],
  };

  const ambiente = {
    id: 'b17e0c5e-0000-4000-8000-automecanic02',
    name: 'AutoMecanic — Local',
    values: [
      { key: 'baseUrl', value: URL_BASE, type: 'default', enabled: true },
      { key: 'emailAdmin', value: 'admin@automecanic.com.br', type: 'default', enabled: true },
      { key: 'senhaAdmin', value: 'Admin@AutoMecanic2026', type: 'secret', enabled: true },
    ],
    _postman_variable_scope: 'environment',
  };

  fs.mkdirSync(SAIDA, { recursive: true });

  const arquivoColecao = path.join(SAIDA, 'AutoMecanic.postman_collection.json');
  const arquivoAmbiente = path.join(SAIDA, 'AutoMecanic-Local.postman_environment.json');

  fs.writeFileSync(arquivoColecao, JSON.stringify(colecao, null, 2) + '\n');
  fs.writeFileSync(arquivoAmbiente, JSON.stringify(ambiente, null, 2) + '\n');

  const totalDeRequisicoes = colecao.item.reduce((soma, pasta) => soma + pasta.item.length, 0);

  console.log(`\nColeção gerada com ${colecao.item.length} pastas e ${totalDeRequisicoes} requisições:`);
  colecao.item.forEach(p => console.log(`  ${String(p.item.length).padStart(3)}  ${p.name}`));
  console.log(`\n  ${arquivoColecao}`);
  console.log(`  ${arquivoAmbiente}`);
}

await main();
