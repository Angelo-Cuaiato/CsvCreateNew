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
- **HTTPS**: Caddy na entrada, com certificado do Let's Encrypt renovado
  sozinho.

```
backend/
  src/FluxoCaixa.Core/     leitura, hierarquia, relatório, números e segurança
  src/FluxoCaixa.Dados/    usuários e histórico no PostgreSQL (esquema e carga inicial)
  src/FluxoCaixa.Api/      API HTTP que o front consome (JWT, CORS, rate limit)
  tests/                   testes de unidade e de integração (xUnit)
  Dockerfile               imagem da API
frontend/                  aplicação Angular (login + telas do relatório)
  Dockerfile               build de produção servido por nginx
  default.conf.template    modelo do nginx: serve o front e encaminha /api
  resolver-do-ambiente.sh  faz o nginx reconsultar o DNS da API a cada deploy
anubis/botPolicies.yaml    regras do anti-bot
dados/                     planilha de exemplo
docker-compose.yml         anubis + front + API + banco
.env.example               modelo das variáveis (copie para .env)
.env.railway.example       o mesmo, para plataformas de deploy
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

Depois abra <https://SEU_DOMINIO> — a aplicação inteira sai por essa porta.

Sobem cinco contêineres, nesta cadeia:

```
navegador → caddy (HTTPS) → anubis (prova de trabalho) → web (front + /api) → api → db
```

- **caddy** é quem atende a internet (80 e 443). Ele pede e renova o
  certificado sozinho, e redireciona HTTP para HTTPS.
- **anubis** apresenta o desafio ao navegador e só encaminha quem resolve.
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

Para depurar direto no banco, na API ou no Anubis, descomente os blocos
`ports` desses serviços no `docker-compose.yml`.

### Antes de ir para produção

Uma lista curta do que **precisa** estar feito:

- [ ] `DOMINIO` apontando para o IP da máquina, com as portas 80 e 443 abertas
      — sem isso o Let's Encrypt não emite o certificado.
- [ ] `EMAIL_TLS` preenchido (obrigatório: vazio, o Caddy não sobe).
- [ ] `JWT_CHAVE` e `ANUBIS_CHAVE` geradas por você
      (`openssl rand -base64 48` e `openssl rand -hex 32`). A API **se recusa a
      subir em produção** com as chaves de exemplo do repositório.
- [ ] `POSTGRES_PASSWORD` e `ADMIN_SENHA` trocadas. Depois do primeiro acesso,
      troque a senha do administrador e remova `ADMIN_*` do `.env`.
- [ ] Backup agendado: `./scripts/backup-banco.sh` no cron, e um restore
      testado pelo menos uma vez.
- [ ] Uma subida de teste completa antes da real — a experiência deste projeto
      é que erro de configuração só aparece rodando.

E o que ficou **conscientemente de fora**, para você decidir:

- O perfil vale para a gestão de usuários — criar, trocar senha e excluir são
  só do `administrador`. Nas rotas de fluxo ele não separa nada: qualquer
  usuário autenticado envia planilhas, vê todas as análises e apaga qualquer
  uma. Isso é intencional: o histórico é do escritório, não de cada um.
- O histórico guarda cada análise (arquivo, período, autor e data) e permite
  baixar de novo o mesmo CSV, mas não é uma trilha de auditoria: qualquer
  usuário autenticado vê e apaga as análises de todos.
- O limite de requisições vive na memória de cada instância. Com mais de uma
  réplica atrás de um balanceador, cada uma conta a sua cota — para valer no
  conjunto, o contador precisa ir para um Redis.

### Subindo em uma plataforma (Railway, Render e afins)

O `docker-compose.yml` é para uma máquina sua. Plataformas de deploy não
executam compose: cada serviço é um deploy separado, construído a partir do seu
próprio Dockerfile. Para isso, o nginx do front lê duas variáveis:

| Variável | Padrão | Para que serve |
| --- | --- | --- |
| `PORT` | `80` | porta que o nginx escuta. A plataforma sorteia uma e injeta aqui. |
| `API_UPSTREAM` | `api:8080` | host:porta da API. Fora do compose o nome do serviço muda (no Railway, `api.railway.internal:8080`). |

O arquivo `frontend/default.conf.template` é processado na subida do
contêiner pelo próprio `envsubst` da imagem oficial do nginx. Um filtro
(`NGINX_ENVSUBST_FILTER` no Dockerfile) limita a substituição a essas duas
variáveis, para que `$uri`, `$host` e as outras do nginx passem intactas.

Como os padrões reproduzem o que o compose sempre usou, **localmente nada
muda**: `docker compose up -d` continua igual.

A lista pronta para copiar está em **`.env.railway.example`**, já separada
por serviço. E a API precisa das suas variáveis — ela lê a configuração com **dois
sublinhados** no lugar do `:`, e se recusa a subir sem a chave do token:

| Variável | Valor |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `Jwt__ChaveSecreta` | gere com `openssl rand -base64 48` (mínimo 32 bytes) |
| `Jwt__MinutosDeValidade` | `60` |
| `AtrasDeProxy` | `true` |
| `ConnectionStrings__Postgres` | a URI do banco (`postgresql://usuario:senha@host:porta/banco`) ou as palavras-chave (`Host=...;Port=5432;Database=...`) |
| `UsuarioInicial__Email` / `__Senha` / `__Nome` | primeiro usuário, criado só com a tabela vazia |

Dois tropeços comuns:

- A chave e a senha do administrador **não podem conter `troque` nem
  `exemplo`**: em produção a API rejeita esses valores de propósito, para
  ninguém subir com o que está no `.env.example`.
- Aponte a conexão para a **variável única** que a plataforma já oferece pronta
  (no Railway, `${{Postgres.DATABASE_PRIVATE_URL}}` — a privada não cobra
  tráfego de saída). A API aceita a URI e a converte sozinha. Montar a string
  pedaço por pedaço a partir de `PGHOST`, `PGPORT` e afins também funciona, mas
  basta uma referência não resolver — o nome do serviço estar diferente, por
  exemplo — para o valor virar `Host=;Port=;Database=` e a API não subir.

Deixe a API **sem domínio público**: quem fala com ela é o nginx do front, pela
rede privada. Se a API subir e o front devolver 502, a rede privada do Railway é
IPv6 — defina `ASPNETCORE_URLS=http://[::]:8080` para o Kestrel escutar nela
também. E, se você mesmo assim publicar a API, ela precisa escutar na porta da
plataforma: `ASPNETCORE_HTTP_PORTS=${{PORT}}`.

Para chegar na aplicação, gere o domínio público **no serviço do front**
(no Railway: Settings > Networking > Generate Domain). Se ele perguntar a porta,
fixe `PORT=8080` nas variáveis desse serviço e responda 8080 — o nginx escuta na
`PORT`. A API fica sem domínio: quem fala com ela é o nginx, pela rede interna.

O que fica de fora nessas plataformas:

- **Caddy** não vai: a própria plataforma termina o HTTPS. Deploye só `web`,
  `api` e o banco.
- **Anubis** também não: ele exige ser o primeiro da fila para enxergar o IP
  real do visitante, e ali sempre há um proxy na frente.
- Mantenha `AtrasDeProxy=true` na API — o IP que chega é o do proxy da
  plataforma, e sem isso o limite de login contaria todo mundo no mesmo balde.
- O banco pode ser o Postgres gerenciado da plataforma; a API só precisa da
  string de conexão em `ConnectionStrings__Padrao`.

### Cópia de segurança

```bash
./scripts/backup-banco.sh              # grava em ./backups, mantém os 14 últimos
./scripts/backup-banco.sh /mnt/backup  # ou onde você mandar
```

O próprio script traz, no cabeçalho, o comando de restauração.

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
| `GET` | `/api/usuarios` | **admin** | Lista quem pode entrar — sem o hash da senha. |
| `POST` | `/api/usuarios` | **admin** | Cadastra `{ email, nome, senha, perfil }`. |
| `PUT` | `/api/usuarios/{email}/senha` | **admin** | Troca a senha de alguém. |
| `DELETE` | `/api/usuarios/{email}` | **admin** | Exclui um usuário. |
| `POST` | `/api/fluxo/analisar` | sim | Recebe o CSV (`multipart/form-data`, campo `arquivo`) e devolve o relatório em JSON. |
| `POST` | `/api/fluxo/consolidar` | sim | Recebe o mesmo CSV e devolve o arquivo consolidado (`text/csv`) para download. |
| `GET` | `/api/fluxo/historico` | sim | Lista as análises guardadas, das mais recentes para as mais antigas. |
| `GET` | `/api/fluxo/historico/{id}` | sim | Devolve o relatório daquela análise, sem reenviar a planilha. |
| `GET` | `/api/fluxo/historico/{id}/csv` | sim | Baixa o CSV guardado junto com a análise. |
| `DELETE` | `/api/fluxo/historico/{id}` | sim | Apaga a análise do histórico. |
| `GET` | `/api/fluxo/totais` | sim | O fechamento de todas as análises somadas — sem os meses detalhados. É o que alimenta o cartão fixo da tela. |
| `POST` | `/api/fluxo/somatorio` | sim | Soma as análises pedidas (`{ "ids": [...] }`, vazio = todas) e devolve o relatório somado. |
| `POST` | `/api/fluxo/somatorio/csv` | sim | O mesmo somatório, como arquivo para download. |

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

### Usuários e perfis

Há dois perfis, e um deles agora tranca coisas de verdade:

| Perfil | Pode |
| --- | --- |
| `administrador` | tudo, mais a tela de **Usuários**: cadastrar, trocar a senha de qualquer um e excluir |
| `usuario` | enviar planilhas, ver e baixar **todas** as análises, somar e apagar análises |

O botão **Usuários** aparece no topo só para administrador, e as rotas
`/api/usuarios` respondem `403` para o resto — a tela esconder o caminho é
conveniência, não segurança; quem manda é a API.

Duas travas existem para não haver como se trancar do lado de fora: ninguém
exclui o próprio usuário, e não dá para excluir o **último administrador**.

A senha tem mínimo de 8 caracteres e é gravada como hash PBKDF2 — a API nunca
devolve o hash, nem na listagem. **Um usuário comum não troca a própria
senha**: quem troca é o administrador. Se isso incomodar, o passo seguinte é
uma tela de "minha senha" que exige a senha atual.

Sem banco de dados, os usuários criados vivem só na memória do processo e a
resposta traz `persistente: false` — a tela avisa, em vez de prometer um
cadastro que some no próximo deploy.

### Histórico das análises

Toda planilha enviada por `/api/fluxo/analisar` fica guardada: o relatório
inteiro e o arquivo consolidado, com quem enviou e quando. Na tela isso vira a
lista **Análises anteriores**, onde cada linha reabre o relatório ou baixa o CSV
de novo — **sem a planilha de origem em mãos**.

O CSV fica gravado em vez de ser gerado na hora do download. Custa alguns
kilobytes por análise e paga com uma garantia: o arquivo baixado meses depois é
byte a byte o que a pessoa viu no dia, mesmo que o formato do relatório mude no
meio do caminho.

Duas coisas que valem saber:

- **Sem banco, não há histórico de verdade.** A API guarda na memória do
  processo e a resposta de `/api/fluxo/historico` traz `persistente: false`, que
  a tela mostra como um aviso. Some no próximo reinício.
- **Não é trilha de auditoria.** Qualquer usuário autenticado vê e apaga as
  análises de todos. O campo `autor` diz quem enviou, mas nada impede outra
  pessoa de apagar.

A limpeza é manual — não há expiração automática. Em uso intenso, a tabela
`analises` cresce; `DELETE FROM analises WHERE enviado_em < now() - interval '1 year'`
resolve, e cabe num cron ao lado do backup.

### Total de todas as análises

A tela mostra, **sem ninguém pedir**, um cartão `Total de todas as análises`
logo acima da lista do histórico: saldo inicial, recebimentos, pagamentos,
transferências, geração de caixa e saldo final de tudo que já foi enviado. Ele
se refaz a cada envio e a cada exclusão — enviar uma planilha nova já muda o
número na tela.

Esse cartão vem de `/api/fluxo/totais`, que devolve só o fechamento e não os
doze meses detalhados, porque é buscado toda vez que a lista carrega. A soma em
si é a mesma do somatório completo, e há teste garantindo que os dois batem.

Como cada chamada relê e soma as planilhas guardadas, o custo cresce com o
tamanho do histórico. Com dezenas de análises isso é imperceptível; com
milhares, o caminho é guardar o total já somado e atualizá-lo a cada envio.

### Somatório de várias análises

O botão **Baixar CSV do somatório**, no cartão `Total de todas as análises`,
entrega um arquivo com **todas** as análises guardadas somadas: mesma estrutura
de sempre — resumo por mês, detalhamento e **total geral no final** — com os
valores de todas as planilhas juntos.

A escolha de quais somar saiu da tela quando o total virou automático: marcar
linhas e pedir a soma era um passo a mais para chegar ao mesmo número que agora
já está ali. `POST /api/fluxo/somatorio` continua aceitando uma lista de `ids`
para quem quiser somar um subconjunto pela API.

A soma acontece nas **planilhas de origem**, não nos relatórios prontos. Isso
importa: a hierarquia é reconstruída por casamento de somas, e somar relatórios
já montados daria pais e filhos incoerentes. Por isso o histórico guarda também
o arquivo enviado (coluna `origem`), e por isso o relatório somado continua
passando pela conferência contra os totais da origem.

Categorias com o mesmo nome se juntam (ignorando acento e caixa: `Energia
Elétrica` e `ENERGIA ELETRICA` viram uma só). Categoria que existe em uma
planilha e não na outra entra com os valores que tem. Meses que só uma planilha
cobre entram no período.

Análises gravadas **antes** desta funcionalidade não têm a coluna `origem` e
ficam de fora — a caixa de seleção delas aparece desabilitada. Basta reenviar a
planilha para elas voltarem a contar.

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

**No navegador** a sessão fica no `localStorage`: sobrevive a recarregar a
página e a fechar a aba, e vale para todas as abas do mesmo navegador. Quem
volta dentro do prazo do token entra direto. Um interceptor põe o
`Authorization` em toda chamada e, ao receber `401`, encerra a sessão e volta
para o login.

Era `sessionStorage`, que morria junto com a aba — mais discreto e, na prática,
irritante: pedia senha a cada aba nova. O que segura o risco da troca é o prazo
do próprio token: passado ele, o que está guardado não serve mais. **Se o
sistema fica aberto em máquina compartilhada, aumente esse prazo com cuidado** —
`Jwt__MinutosDeValidade=480` cobre um dia de trabalho, e é também o tempo que
alguém sentando na máquina teria de acesso. Se o requisito for resistir a XSS, o
próximo passo é o backend mandar o token num cookie `HttpOnly` +
`SameSite=Strict` e o front parar de tocar nele.

**Limites de uso**: `/api/auth/login` aceita 10 tentativas por minuto **por
IP** (contra força bruta). As rotas de fluxo aceitam 30 chamadas por minuto
**por usuário**, com uma fila curta de 5 para absorver rajadas — cada chamada
lê uma planilha inteira em memória, e sem limite essa é a forma mais barata de
derrubar a API. Acima disso, `429`.

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
- **Arquivo que não é fluxo de caixa é recusado**: a leitura exige que os
  rótulos das colunas se pareçam com meses (`JAN/2026`, `01/2026`, `Janeiro`).
  Um CSV de outro assunto volta com `400` e uma mensagem dizendo o que foi
  encontrado, em vez de virar um relatório vazio.
- **A conferência não mente**: quando o arquivo de origem não traz coluna
  `Total`, o relatório diz "não houve o que conferir" em vez de "os totais
  conferem".
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
