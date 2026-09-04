import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { AnaliseFeita, ArquivoBaixado, Historico, OpcoesRelatorio } from './modelos';

/**
 * Conversa com a API em C#. Em desenvolvimento as chamadas passam pelo proxy
 * do `ng serve` (proxy.conf.json) e chegam no backend em localhost:5217.
 */
@Injectable({ providedIn: 'root' })
export class FluxoCaixaService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/fluxo';

  /**
   * Envia a planilha e recebe o relatório pronto para a tela. A API guarda a
   * análise e devolve o identificador dela, que é por onde o CSV é baixado.
   */
  analisar(arquivo: File, opcoes: OpcoesRelatorio = {}): Observable<AnaliseFeita> {
    return this.http.post<AnaliseFeita>(`${this.base}/analisar`, this.formulario(arquivo), {
      params: this.parametros(opcoes),
    });
  }

  /** As análises já feitas, das mais recentes para as mais antigas. */
  historico(): Observable<Historico> {
    return this.http.get<Historico>(`${this.base}/historico`);
  }

  /** Reabre uma análise guardada, sem reenviar a planilha. */
  analiseGuardada(id: string): Observable<AnaliseFeita> {
    return this.http.get<AnaliseFeita>(`${this.base}/historico/${id}`);
  }

  /** Apaga uma análise do histórico. */
  apagarDoHistorico(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/historico/${id}`);
  }

  /**
   * Baixa o CSV guardado junto com a análise. É o mesmo arquivo gerado no dia,
   * e não uma nova geração - por isso não precisa da planilha de origem.
   */
  baixarDoHistorico(id: string, nomeOrigem: string): Observable<ArquivoBaixado> {
    return this.http
      .get(`${this.base}/historico/${id}/csv`, { responseType: 'blob', observe: 'response' })
      .pipe(
        map((resposta) => ({
          conteudo: resposta.body ?? new Blob(),
          nome: this.nomeDoCabecalho(resposta.headers.get('Content-Disposition'), nomeOrigem),
        })),
      );
  }

  /** Baixa o arquivo único consolidado. */
  consolidar(arquivo: File, opcoes: OpcoesRelatorio = {}): Observable<ArquivoBaixado> {
    return this.http
      .post(`${this.base}/consolidar`, this.formulario(arquivo), {
        params: this.parametros(opcoes),
        responseType: 'blob',
        observe: 'response',
      })
      .pipe(
        map((resposta) => ({
          conteudo: resposta.body ?? new Blob(),
          nome: this.nomeDoCabecalho(resposta.headers.get('Content-Disposition'), arquivo.name),
        })),
      );
  }

  private formulario(arquivo: File): FormData {
    const dados = new FormData();
    dados.append('arquivo', arquivo, arquivo.name);
    return dados;
  }

  private parametros(opcoes: OpcoesRelatorio): HttpParams {
    let parametros = new HttpParams();

    if (opcoes.incluirZerados) {
      parametros = parametros.set('incluirZerados', true);
    }

    if (opcoes.semRecuo) {
      parametros = parametros.set('semRecuo', true);
    }

    if (opcoes.separador) {
      parametros = parametros.set('separador', opcoes.separador);
    }

    return parametros;
  }

  /** Usa o nome sugerido pela API; se ele não vier, deriva do arquivo enviado. */
  private nomeDoCabecalho(cabecalho: string | null, nomeOrigem: string): string {
    const sugerido = cabecalho?.match(/filename\*?=(?:UTF-8'')?"?([^";]+)"?/i)?.[1];
    if (sugerido) {
      return decodeURIComponent(sugerido);
    }

    return `${nomeOrigem.replace(/\.csv$/i, '')}_consolidado.csv`;
  }
}
