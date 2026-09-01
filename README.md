# Fluxo de Caixa — CSV consolidado

Sistema que lê a planilha de fluxo de caixa exportada em CSV (aquela larga, com
`Previsto` e `Realizado` para cada mês) e devolve **um único arquivo CSV** mais
organizado: um bloco por mês, subitens recuados dentro de cada grupo e o
**total geral no final**.

Não gera um arquivo por mês nem por cliente — é sempre um arquivo só.

- **Backend**: .NET 8 (ASP.NET Core Minimal API) com [CsvHelper](https://joshclose.github.io/CsvHelper/) para ler e gravar CSV.
- **Front**: Angular 20 (standalone components + signals).
- **Acesso**: login com JWT; senhas guardadas como hash PBKDF2-HMAC-SHA256.
- **Banco**: PostgreSQL (opcional — sem ele, os usuários vêm da configuração).
- **Docker**: `docker compose up` sobe front, API, banco e o Anubis na entrada.
- **Anti-bot**: [Anubis](https://anubis.techaro.lol) exige prova de trabalho do
  navegador antes de deixar chegar na aplicação.

```
backend/
  src/FluxoCaixa.Core/     leitura, hierarquia, relatório, números e segurança
  src/FluxoCaixa.Dados/    usuários no PostgreSQL (esquema e carga inicial)
  src/FluxoCaixa.Api/      API HTTP que o front consome (JWT, CORS, rate limit)
  tests/                   testes de unidade e de integração (xUnit)
  Dockerfile               imagem da API
frontend/                  aplicação Angular (login + telas do relatório)
  Dockerfile               build de produção servido por nginx
  nginx.conf               serve o front e encaminha /api para a API
anubis/botPolicies.yaml    regras do anti-bot
dados/                     planilha de exemplo
docker-compose.yml         anubis + front + API + banco
.env.example               modelo das variáveis (copie para .env)
```

## Pegando o código

O trabalho está na branch `claude/csv-system-monthly-totals-ntcdke`:

```bash
git clone https://github.com/Angelo-Cuaiato/CsvCreateNew.git
cd CsvCreateNew
git checkout claude/csv-system-monthly-totals-ntcdke
```

## Como rodar com Docker

A forma mais curta de subir a API com banco:

```bash
cp .env.example .env      # troque as senhas e as duas chaves
docker compose up -d
```

Depois abra <http://localhost:8080> — a aplicação inteira sai por essa porta.

Sobem quatro contêineres, nesta cadeia:

```
navegador → anubis (prova de trabalho) → web (front + /api) → api → db
```

- **anubis** é a única porta publicada. Ele apresenta o desafio ao navegador e
  só encaminha quem resolve.
- **web** é o nginx: serve o Angular já compilado e encaminha `/api` para a API.
  Como front e API saem da mesma origem, o navegador nem precisa de CORS.
- **api** e **db** não expõem porta nenhuma para fora.

A API só inicia depois que o banco responde, cria a tabela de usuários sozinha
e, **se ela estiver vazia**, cadastra o usuário de `ADMIN_EMAIL`/`ADMIN_SENHA`.
Numa segunda subida esse cadastro não se repete e não sobrescreve nada.

```bash
docker compose logs -f api    # acompanhar
docker compose down           # parar (o volume do banco fica)
docker compose down -v        # parar e apagar os dados
```

Para depurar direto no banco ou na API, descomente os blocos `ports` dos
serviços `db` e `api` no `docker-compose.yml`.

### O anti-bot (Anubis)

O Anubis fica na frente de tudo e exige do navegador uma prova de trabalho —
um cálculo curto em JavaScript — antes de liberar o acesso. Isso encarece a
vida de scraper e de robô de IA, que costumam desistir, sem pedir CAPTCHA a
ninguém.

- As regras estão em `anubis/botPolicies.yaml`: robôs patológicos e de IA são
  recusados, `/api/saude`, `/robots.txt` e o favicon passam direto (para o
  monitoramento funcionar), e todo navegador resolve o desafio na primeira
  visita. O cookie emitido vale para as chamadas seguintes, inclusive as que o
  front faz para `/api`.
- `ANUBIS_DIFICULDADE` controla o custo do desafio (zeros exigidos no hash). O
  padrão 4 é o recomendado; acima de 5 começa a incomodar em celular.
- `ANUBIS_CHAVE` assina o cookie do desafio — gere com `openssl rand -hex 32`.
- `USE_REMOTE_ADDRESS=true` é obrigatório aqui: o Anubis é o primeiro da fila,
  sem outro proxy na frente. Sem isso ele espera um `X-Real-Ip` que ninguém
  põe e responde **500 em todas as rotas**.
- A imagem está **fixada em uma versão** (`v1.25.0`) em vez de `:latest`: os
  arquivos importados pela política vêm de dentro da imagem e mudam de nome
  entre versões. Ao atualizar, confira se os imports do `botPolicies.yaml`
  ainda existem — o Anubis se recusa a subir se algum sumir, o que é bom, mas
  derruba a aplicação se você descobrir só em produção.
- Navegadores **headless** (Playwright, Puppeteer e afins) são recusados por
  regra do próprio Anubis. Se você tem automação de teste que passa pela porta
  pública, ela vai precisar de exceção — ou aponte-a direto para o serviço
  `web`, sem passar pelo Anubis.

**O Anubis não substitui a autenticação.** Ele filtra tráfego automatizado de
navegador; a API continua exigindo o token JWT, e clientes de linha de comando
(um `curl` de integração, por exemplo) seguem passando pela política padrão e
esbarrando no login normalmente.

Uma consequência de ter proxy na frente: o IP que chega na API passa a ser o do
proxy. Por isso o compose define `AtrasDeProxy=true`, que faz a API ler o
`X-Forwarded-For` — senão o limite de 10 tentativas de login por minuto
contaria todos os usuários no mesmo balde. Fora do compose esse ajuste fica
desligado de propósito: confiar nesse cabeçalho com a API exposta direto
deixaria qualquer cliente forjar o próprio IP.

## Como rodar sem Docker

Precisa do **.NET SDK 8** e do **Node 20+**. São dois terminais.

```bash
# 1) backend  → http://localhost:5217
cd backend
dotnet run --project src/FluxoCaixa.Api

# 2) front    → http://localhost:4200  (outro terminal)
cd frontend
npm install
npm start
```

Depois abra <http://localhost:4200>. A API não tem página inicial: acessar
`localhost:5217` direto no navegador devolve 404, o que é esperado — quem fala
com ela é o front.

Sem banco configurado, a API lê os usuários do `appsettings.Development.json`,
e é de lá que sai o login de desenvolvimento abaixo.

O `ng serve` já vem com um proxy (`proxy.conf.json`) que manda tudo que começa
com `/api` para o backend em `localhost:5217` — não precisa configurar CORS em
desenvolvimento. A porta 5217 é a mesma do perfil de execução da API
(`Properties/launchSettings.json`); mudando uma, mude a outra.

Na tela aparece primeiro o login. Em desenvolvimento já vem um usuário pronto:

| E-mail | Senha |
| --- | --- |
| `admin@exemplo.com` | `fluxo@2026` |

Depois de entrar: escolha (ou arraste) o CSV, clique em **Analisar** e o
relatório aparece; **Baixar CSV consolidado** salva o arquivo único.

## A API

| Método | Rota | Token | O que faz |
| --- | --- | --- | --- |
| `GET` | `/api/saude` | não | Responde `{"status":"ok"}`. |
| `POST` | `/api/auth/login` | não | Recebe `{ "email", "senha" }` e devolve o token, quando expira e os dados do usuário. |
| `GET` | `/api/auth/eu` | sim | Devolve quem está logado, segundo o token enviado. |
| `POST` | `/api/fluxo/analisar` | sim | Recebe o CSV (`multipart/form-data`, campo `arquivo`) e devolve o relatório em JSON. |
| `POST` | `/api/fluxo/consolidar` | sim | Recebe o mesmo CSV e devolve o arquivo consolidado (`text/csv`) para download. |

As rotas marcadas com token exigem o cabeçalho `Authorization: Bearer <token>`;
sem ele a resposta é `401`.

Parâmetros de query aceitos pelas duas rotas de fluxo:

| Parâmetro | Padrão | Para que serve |
| --- | --- | --- |
| `incluirZerados` | `false` | Mantém as categorias sem movimento no mês (por padrão são omitidas). |
| `semRecuo` | `false` | Não recua os subitens na coluna `Categoria` (só em `/consolidar`). |
| `separador` | `;` | Separador do arquivo gerado (só em `/consolidar`). |

Exemplo com `curl`:

```bash
TOKEN=$(curl -s -X POST http://localhost:5217/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@exemplo.com","senha":"fluxo@2026"}' | jq -r .token)

curl -H "Authorization: Bearer $TOKEN" \
     -F "arquivo=@dados/fluxo_de_caixa_mensal.csv" \
     http://localhost:5217/api/fluxo/consolidar -OJ
```

Erros de leitura voltam como `400` com `{"mensagem":"..."}`, que o front mostra
direto na tela.

## O que sai no arquivo

O arquivo é escrito em UTF-8 com BOM e separado por `;`, então abre direto no
Excel com os acentos certos. Ele tem cinco partes, nesta ordem:

1. **Cabeçalho** — origem, período, data de geração e legenda das colunas.
2. **Resumo por mês** — uma linha por mês com recebimentos, pagamentos, geração
   de caixa (previsto e realizado) e o saldo final, mais a linha de total.
3. **Um bloco por mês** — `MÊS 01 - JAN/2026`, `MÊS 02 - FEV/2026`, … Cada bloco
   traz as categorias com `Previsto`, `Realizado`, `Diferença` e `% Realizado`.
4. **Total do período** — as mesmas categorias somadas de janeiro a dezembro,
   fechando com o bloco `TOTAL GERAL` (saldo inicial, recebimentos, pagamentos,
   transferências, geração de caixa e saldo final).
5. **Conferência** — compara o total somado mês a mês com a coluna `Total` que
   veio no arquivo de origem e lista qualquer divergência.

Trecho do resultado:

```
MÊS 01 - JAN/2026
Categoria;Previsto (R$);Realizado (R$);Diferença (R$);% Realizado
Saldo do Mês Anterior;43.623.381,07;43.056.714,77;-566.666,30;98,7%
Total de Recebimentos;937.423,13;887.137,81;-50.285,32;94,6%
    Encerramento de Contrato;11.767,83;582,16;-11.185,67;4,9%
    Receitas de Vendas;925.655,30;886.555,65;-39.099,65;95,8%
        Faturamento;907.127,02;868.027,37;-39.099,65;95,7%
```

## Como a hierarquia é reconstruída

O CSV de origem vem sem indentação: grupo, subgrupo e item aparecem todos no
mesmo nível. A relação entre eles, porém, continua nos números — o valor de um
grupo é a soma dos filhos que vêm logo abaixo dele.

`ConstrutorHierarquia` refaz essa árvore comparando os valores **mês a mês** (e
não só o total do ano, que produziria falsos positivos). Uma linha só vira filha
de outra quando a soma bate em todos os meses, o sinal é o mesmo e não há uma
linha de resumo no caminho. Nada é chutado: o que não fecha continua no nível de
cima. Na planilha de exemplo isso recupera os três níveis corretamente:

```
Total de Pagamentos
  Despesas Administrativas e Comerciais
    Salários
      Salários - Folha
      Salários - Médicos
```

## Login, token e senhas

**Senhas** nunca são gravadas. O que fica armazenado é o resultado de
PBKDF2-HMAC-SHA256 com 210 mil iterações (recomendação do OWASP) e um salt
aleatório de 16 bytes por usuário, no formato
`pbkdf2-sha256$iterações$salt$hash`. Na conferência a comparação é feita em
tempo fixo (`CryptographicOperations.FixedTimeEquals`), e quando o e-mail não
existe o serviço ainda assim calcula um hash descartável — as duas situações
levam o mesmo tempo e devolvem a mesma mensagem, então a API não conta quais
e-mails estão cadastrados.

Para cadastrar uma senha:

```bash
cd backend
dotnet run --project src/FluxoCaixa.Api -- hash-senha "a senha do usuário"
# pbkdf2-sha256$210000$T3dq...$9fK2...
```

O resultado vai para a lista `Usuarios` da configuração.

**Token**: o login devolve um JWT assinado em HMAC-SHA256, válido por 60
minutos (`Jwt:MinutosDeValidade`). Emissor, audiência, assinatura e validade
são conferidos a cada chamada, sem a tolerância padrão de 5 minutos no
vencimento. O token carrega e-mail, nome e perfil — nada de senha.

**No navegador** a sessão fica no `sessionStorage`: some quando a aba fecha e
não é compartilhada entre abas. Um interceptor põe o `Authorization` em toda
chamada e, ao receber `401`, encerra a sessão e volta para o login. Se o
requisito for resistir a XSS, o próximo passo é o backend mandar o token num
cookie `HttpOnly` + `SameSite=Strict` e o front parar de tocar nele.

**Força bruta**: `/api/auth/login` aceita 10 tentativas por minuto por IP;
acima disso responde `429`.

### Configuração em produção

O `appsettings.json` versionado **não tem segredo nenhum** — `Jwt:ChaveSecreta`
vem vazia e a aplicação se recusa a subir sem uma chave de pelo menos 32 bytes.
A chave de desenvolvimento e o usuário de demonstração estão só em
`appsettings.Development.json`, que serve para rodar na sua máquina e não deve
ser usado em produção. Lá, passe tudo por variável de ambiente ou cofre:

```bash
export Jwt__ChaveSecreta="$(openssl rand -base64 48)"
export Usuarios__0__Email="voce@empresa.com"
export Usuarios__0__Nome="Seu Nome"
export Usuarios__0__Perfil="administrador"
export Usuarios__0__SenhaHash="pbkdf2-sha256$210000$..."
```

Como o token viaja no cabeçalho, sirva a API por HTTPS em produção.

### Onde ficam os usuários

Depende de haver banco configurado:

| `ConnectionStrings:Postgres` | De onde vêm os usuários |
| --- | --- |
| definida | Tabela `usuarios` do PostgreSQL. |
| ausente | Lista `Usuarios` da configuração (é o caminho do `dotnet run` local e dos testes). |

A tabela é criada na subida por um script idempotente (`CREATE TABLE IF NOT
EXISTS`), então subir várias instâncias da API contra o mesmo banco não dá
conflito. `UsuarioInicial__Email` e `UsuarioInicial__Senha` só têm efeito
enquanto a tabela está vazia — depois do primeiro acesso, troque a senha e
remova essas variáveis.

Para cadastrar mais gente, gere o hash com o comando `hash-senha` acima e
insira direto:

```sql
INSERT INTO usuarios (email, nome, senha_hash, perfil)
VALUES ('fulano@empresa.com', 'Fulano', 'pbkdf2-sha256$210000$...', 'usuario');
```

## Detalhes que o sistema já trata

- **Acentuação**: o arquivo de origem costuma vir em `windows-1252`; a leitura
  tenta UTF-8 primeiro e cai para `windows-1252` quando os bytes não batem.
- **Números em português**: `1.234,56`, `(1.234,56)` e `1.234,56-` viram
  `decimal` (sem erro de ponto flutuante) e voltam formatados no mesmo padrão.
  O formato é montado à mão, sem depender do ICU instalado no servidor.
- **Saldos não são somados**: `Saldo do Mês Anterior` e `Saldo Final de Caixa`
  são fotografias de um momento, então no total do período valem o saldo do
  primeiro e do último mês, não a soma dos doze.
- **Linhas com colunas faltando** viram um aviso no relatório em vez de quebrar
  a execução.
- **Separador**: `;`, `,` ou tabulação são detectados sozinhos.
- **A tela não anuncia a tecnologia**: nada de créditos de framework na
  interface, e a API responde sem o cabeçalho `Server`. O atributo
  `ng-version` no elemento raiz é carimbado pelo próprio Angular em tempo de
  execução e não tem como ser removido pela aplicação.

## Testes

```bash
# backend — 98 testes (80 de unidade + 18 de integração da API)
cd backend && dotnet test

# front — 23 testes
cd frontend && npm test          # abre o Chrome
cd frontend && npm run test:ci   # headless, sem sandbox (contêiner/CI)
```

Os testes do backend cobrem a leitura do CSV, a reconstrução da hierarquia
(inclusive verificando que todo grupo é exatamente a soma dos filhos em cada um
dos doze meses), a formatação dos números, a estrutura do relatório, o hash das
senhas e a autenticação. Os de integração sobem a API em memória e conferem que
as rotas de fluxo devolvem `401` sem token, que com token o relatório e o
download funcionam, e que o token é recusado quando está vencido (inclusive por
poucos segundos, já que `ClockSkew` está zerado), adulterado, assinado com outra
chave ou emitido para outro emissor/audiência.

Os do front cobrem o serviço HTTP, o interceptor e as telas: login com
credenciais certas e erradas, envio da planilha, exibição do resumo, troca de
mês, mensagem de erro e o logout.
