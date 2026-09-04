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
  /** O arquivo de origem trazia coluna Total. Se false, não houve o que comparar. */
  comparavel: boolean;
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

/** O que a API devolve ao analisar: o relatório e o identificador guardado. */
export interface AnaliseFeita {
  id: string;
  relatorio: Relatorio;
}

/** Uma linha da lista do histórico. */
export interface ResumoDeAnalise {
  id: string;
  email: string;
  autor: string;
  nomeArquivo: string;
  enviadoEm: string;
  primeiroMes: string | null;
  ultimoMes: string | null;
  quantidadeDeMeses: number;
  conferenciaOk: boolean;
  conferenciaComparavel: boolean;
  /** Se a planilha de origem ficou guardada — sem ela a análise não entra na soma. */
  podeSomar: boolean;
}

/** A lista, mais o aviso de que ela pode não sobreviver a um reinício. */
export interface Historico {
  /** Falso quando a aplicação está sem banco: o histórico some no próximo deploy. */
  persistente: boolean;
  itens: ResumoDeAnalise[];
}

/** O fechamento de tudo que já foi enviado, somado. */
export interface TotaisDeTudo {
  analises: number;
  arquivos?: string[];
  periodo: string;
  totalGeral: Categoria[];
}

/** Um usuário como a tela de administração o vê — nunca com o hash da senha. */
export interface UsuarioDoSistema {
  email: string;
  nome: string;
  perfil: string;
}

/** A lista de usuários, com o aviso de que pode não sobreviver a um reinício. */
export interface ListaDeUsuarios {
  persistente: boolean;
  itens: UsuarioDoSistema[];
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
