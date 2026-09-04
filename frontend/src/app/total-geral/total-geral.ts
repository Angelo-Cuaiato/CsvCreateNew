import { Component, input } from '@angular/core';

import { Categoria } from '../modelos';
import { PercentualPipe, ValorPipe } from '../valor.pipe';

/**
 * O fechamento do período: saldo inicial, recebimentos, pagamentos, geração de
 * caixa e saldo final - os mesmos números do bloco TOTAL GERAL do CSV.
 */
@Component({
  selector: 'app-total-geral',
  imports: [ValorPipe, PercentualPipe],
  templateUrl: './total-geral.html',
  styleUrl: './total-geral.css',
})
export class TotalGeral {
  readonly itens = input.required<Categoria[]>();
  readonly periodo = input<string>('');
  readonly titulo = input<string>('Total geral');

  protected classeValor(valor: number | null): string {
    if (valor === null || valor === 0) {
      return 'neutro';
    }

    return valor > 0 ? 'positivo' : 'negativo';
  }
}
