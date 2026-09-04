import { Component, input, output } from '@angular/core';

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
  /** Texto do botão do cabeçalho. Vazio: o cartão não tem botão. */
  readonly acao = input<string>('');
  readonly acionar = output<void>();

  protected classeValor(valor: number | null): string {
    if (valor === null || valor === 0) {
      return 'neutro';
    }

    return valor > 0 ? 'positivo' : 'negativo';
  }
}
