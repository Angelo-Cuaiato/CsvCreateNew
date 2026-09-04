import { Component, computed, input, output } from '@angular/core';

import { ResumoDeAnalise } from '../modelos';

/**
 * As análises já feitas. Cada linha reabre o relatório sem reenviar a planilha,
 * e baixa o mesmo CSV que foi gerado no dia.
 */
@Component({
  selector: 'app-historico',
  templateUrl: './historico.html',
  styleUrl: './historico.css',
})
export class Historico {
  readonly itens = input.required<ResumoDeAnalise[]>();
  readonly selecionada = input<string | null>(null);
  /** Falso quando não há banco: o histórico vive só até o próximo reinício. */
  readonly persistente = input<boolean>(true);
  readonly carregando = input<boolean>(false);

  readonly abrir = output<string>();
  readonly baixar = output<string>();
  readonly apagar = output<string>();
  protected readonly vazio = computed(() => this.itens().length === 0);

  /** "04/09/2026 11:54" no fuso de quem está olhando. */
  protected quando(iso: string): string {
    const data = new Date(iso);
    return Number.isNaN(data.getTime())
      ? iso
      : data.toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' });
  }

  protected periodo(item: ResumoDeAnalise): string {
    if (!item.primeiroMes || !item.ultimoMes) {
      return '—';
    }

    return item.primeiroMes === item.ultimoMes
      ? item.primeiroMes
      : `${item.primeiroMes} a ${item.ultimoMes}`;
  }
}
