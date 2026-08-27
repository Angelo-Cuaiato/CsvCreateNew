import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { ArquivoBaixado, OpcoesRelatorio, Relatorio } from './modelos';

/**
 * Conversa com a API em C#. Em desenvolvimento as chamadas passam pelo proxy
 * do `ng serve` (proxy.conf.json) e chegam no backend em localhost:5217.
 */
@Injectable({ providedIn: 'root' })
export class FluxoCaixaService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/fluxo';

  /** Envia a planilha e recebe o relatório pronto para a tela. */
  analisar(arquivo: File, opcoes: OpcoesRelatorio = {}): Observable<Relatorio> {
    return this.http.post<Relatorio>(`${this.base}/analisar`, this.formulario(arquivo), {
      params: this.parametros(opcoes),
    });
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
