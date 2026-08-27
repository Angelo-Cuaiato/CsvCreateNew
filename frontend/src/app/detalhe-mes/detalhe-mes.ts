import { Component, computed, input, model } from '@angular/core';

import { Categoria, MesDetalhe } from '../modelos';
import { PercentualPipe, ValorPipe } from '../valor.pipe';

/** Opção extra na barra de meses: o período inteiro somado. */
export const TOTAL_DO_PERIODO = 'TOTAL DO PERÍODO';

/**
 * As categorias de um mês, com a hierarquia recuada, ou o total do período.
 */
@Component({
  selector: 'app-detalhe-mes',
  imports: [ValorPipe, PercentualPipe],
  templateUrl: './detalhe-mes.html',
  styleUrl: './detalhe-mes.css',
})
export class DetalheMes {
  readonly meses = input.required<MesDetalhe[]>();
  readonly totalDoPeriodo = input.required<Categoria[]>();

  /** Mês em exibição; o componente pai acompanha a escolha. */
  readonly selecionado = model<string>(TOTAL_DO_PERIODO);

  protected readonly totalDoPeriodoRotulo = TOTAL_DO_PERIODO;

  protected readonly abas = computed(() => [
    TOTAL_DO_PERIODO,
    ...this.meses().map((mes) => mes.mes),
  ]);

  protected readonly categorias = computed<Categoria[]>(() => {
    const escolhido = this.selecionado();
    if (escolhido === TOTAL_DO_PERIODO) {
      return this.totalDoPeriodo();
    }

    return this.meses().find((mes) => mes.mes === escolhido)?.categorias ?? [];
  });

  protected escolher(aba: string): void {
    this.selecionado.set(aba);
  }

  protected classeValor(valor: number | null): string {
    if (valor === null || valor === 0) {
      return 'neutro';
    }

    return valor > 0 ? 'positivo' : 'negativo';
  }
}
