# Fluxo de Caixa — CSV consolidado

Sistema que lê a planilha de fluxo de caixa exportada em CSV (aquela larga, com
`Previsto` e `Realizado` para cada mês) e devolve **um único arquivo CSV** mais
organizado: um bloco por mês, subitens recuados dentro de cada grupo e o
**total geral no final**.

Não gera um arquivo por mês nem por cliente — é sempre um arquivo só.

- **Backend**: .NET 8 (ASP.NET Core Minimal API) com [CsvHelper](https://joshclose.github.io/CsvHelper/) para ler e gravar CSV.
- **Front**: Angular 20 (standalone components + signals).

```
backend/
  src/FluxoCaixa.Core/     leitura, hierarquia, relatório e números (biblioteca)
  src/FluxoCaixa.Api/      API HTTP que o front consome
  tests/                   testes de unidade (xUnit)
frontend/                  aplicação Angular
dados/                     planilha de exemplo
```

## Como rodar

Precisa do **.NET SDK 8** e do **Node 20+**. São dois terminais.

```bash
# 1) backend  → http://localhost:5217
cd backend
dotnet run --project src/FluxoCaixa.Api

# 2) front    → http://localhost:4200
cd frontend
npm install
npm start
```

O `ng serve` já vem com um proxy (`proxy.conf.json`) que manda tudo que começa
com `/api` para o backend em `localhost:5217` — não precisa configurar CORS em
desenvolvimento. Se o backend subir em outra porta, é só ajustar esse arquivo.

Na tela: escolha (ou arraste) o CSV, clique em **Analisar** e o relatório
aparece; **Baixar CSV consolidado** salva o arquivo único.

## A API

| Método | Rota | O que faz |
| --- | --- | --- |
| `GET` | `/api/saude` | Responde `{"status":"ok"}`. |
| `POST` | `/api/fluxo/analisar` | Recebe o CSV (`multipart/form-data`, campo `arquivo`) e devolve o relatório em JSON. |
| `POST` | `/api/fluxo/consolidar` | Recebe o mesmo CSV e devolve o arquivo consolidado (`text/csv`) para download. |

Parâmetros de query aceitos pelas duas rotas de fluxo:

| Parâmetro | Padrão | Para que serve |
| --- | --- | --- |
| `incluirZerados` | `false` | Mantém as categorias sem movimento no mês (por padrão são omitidas). |
| `semRecuo` | `false` | Não recua os subitens na coluna `Categoria` (só em `/consolidar`). |
| `separador` | `;` | Separador do arquivo gerado (só em `/consolidar`). |

Exemplo com `curl`:

```bash
curl -F "arquivo=@dados/fluxo_de_caixa_mensal.csv" \
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

## Testes

```bash
# backend — 54 testes
cd backend && dotnet test

# front — 10 testes
cd frontend && npm test          # abre o Chrome
cd frontend && npm run test:ci   # headless, sem sandbox (contêiner/CI)
```

Os testes do backend cobrem a leitura do CSV, a reconstrução da hierarquia
(inclusive verificando que todo grupo é exatamente a soma dos filhos em cada um
dos doze meses), a formatação dos números e a estrutura do relatório gerado. Os
do front cobrem o serviço HTTP e a tela: envio da planilha, exibição do resumo,
troca de mês e mensagem de erro.
