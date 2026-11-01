// Carga para o HPA: sobe o número de usuários virtuais, segura o pico e desce, para
// `kubectl get hpa -w` mostrar as réplicas subindo e depois voltando ao mínimo.
//
// Cada usuário virtual faz login uma vez (o hash BCrypt é caro) e depois consulta a API em
// laço; se o token deixar de valer (401), entra de novo. Parâmetros vindos do Job:
//   ALVO            endereço da API dentro do cluster
//   SENHA_ADMIN     senha do administrador criado pelo seed
//   USUARIOS_PICO   usuários virtuais no pico
//   DURACAO_PICO    quanto tempo segurar o pico (ex.: 3m)
import http from 'k6/http';
import { check, sleep } from 'k6';

const alvo = __ENV.ALVO || 'http://automecanic-api';
const senha = __ENV.SENHA_ADMIN || 'Admin@AutoMecanic2026';
const pico = parseInt(__ENV.USUARIOS_PICO || '40', 10);
const duracaoDoPico = __ENV.DURACAO_PICO || '3m';

export const options = {
  stages: [
    { duration: '30s', target: pico },
    { duration: duracaoDoPico, target: pico },
    { duration: '20s', target: 0 },
  ],
  thresholds: {
    // Não derruba o Job por lentidão durante a escalada; só por erro de verdade.
    http_req_failed: ['rate<0.05'],
  },
};

// Token de cada usuário virtual (cada VU tem o seu próprio escopo de módulo).
let token = null;

function entrar() {
  const resposta = http.post(
    `${alvo}/api/v1/autenticacao/login`,
    JSON.stringify({ email: 'admin@automecanic.com.br', senha }),
    { headers: { 'Content-Type': 'application/json' }, tags: { operacao: 'login' } },
  );

  token = check(resposta, { 'login 200': (r) => r.status === 200 }) ? resposta.json('token') : null;
}

export default function () {
  if (!token) {
    entrar();
    if (!token) {
      sleep(1);
      return;
    }
  }

  const cabecalhos = { headers: { Authorization: `Bearer ${token}` } };

  for (const caminho of ['/api/v1/ordens-servico', '/api/v1/clientes', '/api/v1/pecas']) {
    const resposta = http.get(`${alvo}${caminho}`, { ...cabecalhos, tags: { operacao: 'consulta' } });

    if (resposta.status === 401) {
      token = null;
      break;
    }
    check(resposta, { 'consulta 200': (r) => r.status === 200 });
  }

  sleep(0.1);
}
