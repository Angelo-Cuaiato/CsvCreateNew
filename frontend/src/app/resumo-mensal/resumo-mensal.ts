import { Component, computed, input, output } from '@angular/core';

import { ResumoMes } from '../modelos';
import { ValorPipe } from '../valor.pipe';

/**
 * O ano inteiro em uma tabela: uma linha por mês e o total do período no fim.
 */
@Component({
  selector: 'app-resumo-mensal',
  imports: [ValorPipe],
  templateUrl: './resumo-mensal.html',
  styleUrl: './resumo-mensal.css',
})
export class ResumoMensal {
  readonly resumo = input.required<ResumoMes[]>();
  readonly mesAtivo = input<string | null>(null);

  /** O usuário clicou em um mês e quer ver o detalhamento dele. */
  readonly mesEscolhido = output<string>();

  protected readonly total = computed(() => {
    const linhas = this.resumo();
    const somar = (campo: keyof ResumoMes): number =>
      linhas.reduce((acumulado, linha) => acumulado + ((linha[campo] as number | null) ?? 0), 0);

    return {
      recebimentosPrevisto: somar('recebimentosPrevisto'),
      recebimentosRealizado: somar('recebimentosRealizado'),
      pagamentosPrevisto: somar('pagamentosPrevisto'),
      pagamentosRealizado: somar('pagamentosRealizado'),
      geracaoPrevista: somar('geracaoPrevista'),
      geracaoRealizada: somar('geracaoRealizada'),
      // Saldo não se soma: vale o último mês do período.
      saldoFinalRealizado: linhas.at(-1)?.saldoFinalRealizado ?? null,
    };
  });

  protected classeValor(valor: number | null): string {
    if (valor === null || valor === 0) {
      return 'neutro';
    }

    return valor > 0 ? 'positivo' : 'negativo';
  }
}
