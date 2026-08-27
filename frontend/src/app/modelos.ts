/** Espelha os DTOs devolvidos pela API em C# (FluxoCaixa.Core.Relatorio). */

/** Uma categoria do relatório, com o nível dela na hierarquia. */
export interface Categoria {
  rotulo: string;
  nivel: number;
  previsto: number | null;
  realizado: number | null;
  diferenca: number | null;
  percentualRealizado: number | null;
}

/** O detalhamento de um mês. */
export interface MesDetalhe {
  mes: string;
  categorias: Categoria[];
}

/** Uma linha do resumo: o mês em números redondos. */
export interface ResumoMes {
  mes: string;
  recebimentosPrevisto: number | null;
  recebimentosRealizado: number | null;
  pagamentosPrevisto: number | null;
  pagamentosRealizado: number | null;
  geracaoPrevista: number | null;
  geracaoRealizada: number | null;
  saldoFinalRealizado: number | null;
}

/** Um total que não fecha com o que veio no arquivo de origem. */
export interface Divergencia {
  categoria: string;
  coluna: string;
  totalArquivo: number;
  totalCalculado: number;
  diferenca: number;
}

/** Resultado da conferência contra a coluna Total da origem. */
export interface Conferencia {
  ok: boolean;
  divergencias: Divergencia[];
}

/** O relatório inteiro. */
export interface Relatorio {
  titulo: string;
  arquivo: string;
  encoding: string;
  separador: string;
  meses: string[];
  resumoPorMes: ResumoMes[];
  detalhePorMes: MesDetalhe[];
  totalDoPeriodo: Categoria[];
  totalGeral: Categoria[];
  conferencia: Conferencia;
  avisos: string[];
}

/** Ajustes de apresentação enviados para a API. */
export interface OpcoesRelatorio {
  incluirZerados?: boolean;
  semRecuo?: boolean;
  separador?: string;
}

/** Um arquivo pronto para o navegador salvar. */
export interface ArquivoBaixado {
  conteudo: Blob;
  nome: string;
}
