# Fluxo de Caixa — CSV consolidado

Sistema que lê a planilha de fluxo de caixa exportada em CSV (aquela larga, com
`Previsto` e `Realizado` para cada mês) e gera **um único arquivo CSV**, mais
organizado: um bloco por mês, subitens recuados dentro de cada grupo e o
**total geral no final**.

Não gera um arquivo por mês nem por cliente — é sempre um arquivo só.

## Como usar

Precisa apenas de Python 3.10 ou superior. Não há dependências externas.

```bash
# usa dados/fluxo_de_caixa_mensal.csv e escreve em saida/fluxo_de_caixa_consolidado.csv
python gerar_fluxo.py

# escolhendo os arquivos
python gerar_fluxo.py minha_planilha.csv -s saida/relatorio_2026.csv
```

Saída no terminal:

```
Origem lida: dados/fluxo_de_caixa_mensal.csv (cp1252, separador ';')
Categorias: 59 | Meses: 12 (JAN/2026 a DEZ/2026)
Arquivo gerado: saida/fluxo_de_caixa_consolidado.csv
```

### Opções

| Opção | Para que serve |
| --- | --- |
| `-s`, `--saida` | Caminho do arquivo gerado (padrão `saida/fluxo_de_caixa_consolidado.csv`). |
| `--incluir-zerados` | Mantém no relatório as categorias sem movimento no mês (por padrão elas são omitidas para o arquivo ficar limpo). |
| `--sem-recuo` | Não recua os subitens dentro da coluna `Categoria`. |
| `--separador-entrada` | Separador do arquivo de origem. Por padrão é detectado sozinho (`;`, `,` ou tabulação). |
| `--separador-saida` | Separador do arquivo gerado (padrão `;`, que é o que o Excel em português espera). |

## O que sai no arquivo

O arquivo é escrito em UTF-8 com BOM e separado por `;`, então abre direto no
Excel com os acentos certos. Ele tem cinco partes, nesta ordem:

1. **Cabeçalho** — origem, período, data de geração e legenda das colunas.
2. **Resumo por mês** — uma linha por mês com recebimentos, pagamentos, geração
   de caixa (previsto e realizado) e o saldo final, mais a linha de total.
3. **Um bloco por mês** — `MÊS 01 - JAN/2026`, `MÊS 02 - FEV/2026`, … Cada bloco
   traz todas as categorias com `Previsto`, `Realizado`, `Diferença` e
   `% Realizado`.
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

O módulo `fluxo_caixa/hierarquia.py` refaz essa árvore comparando os valores
**mês a mês** (e não só o total do ano, que produziria falsos positivos). Uma
linha só vira filha de outra quando a soma bate em todos os meses, o sinal é o
mesmo e não há uma linha de resumo no caminho. Nada é chutado: o que não fecha
continua no nível de cima. Na planilha de exemplo isso recupera os três níveis
corretamente, por exemplo:

```
Total de Pagamentos
  Despesas Administrativas e Comerciais
    Salários
      Salários - Folha
      Salários - Médicos
```

## Detalhes que o sistema já trata

- **Acentuação**: o arquivo de origem costuma vir em `cp1252`/`latin-1`; a
  leitura tenta UTF-8, depois `cp1252` e `latin-1`.
- **Números em português**: `1.234,56`, `(1.234,56)` e `1.234,56-` viram
  `Decimal` (sem erro de arredondamento de ponto flutuante) e voltam formatados
  no mesmo padrão.
- **Saldos não são somados**: `Saldo do Mês Anterior` e `Saldo Final de Caixa`
  são fotografias de um momento, então no total do período valem o saldo do
  primeiro e do último mês, não a soma dos doze.
- **Linhas com colunas faltando** viram um aviso no fim do relatório em vez de
  quebrar a execução.

## Estrutura do projeto

```
gerar_fluxo.py              atalho para rodar o gerador
fluxo_caixa/
  cli.py                    argumentos de linha de comando
  leitura.py                lê o CSV largo e normaliza os valores
  hierarquia.py             reconstrói grupo -> subgrupo -> item
  relatorio.py              monta e grava o CSV consolidado
  numeros.py                números no formato brasileiro
dados/                      planilha de origem
saida/                      arquivo gerado
testes/                     testes automatizados
```

## Testes

```bash
python -m unittest discover -s testes -t .
```

Os testes cobrem a leitura do CSV, a reconstrução da hierarquia (inclusive
verificando que todo grupo é exatamente a soma dos filhos em cada um dos doze
meses), a formatação dos números e a estrutura do relatório gerado.
