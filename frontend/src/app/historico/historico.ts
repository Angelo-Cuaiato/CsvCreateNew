import { Component, computed, input, output, signal } from '@angular/core';

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
  readonly somando = input<boolean>(false);

  readonly abrir = output<string>();
  readonly baixar = output<string>();
  readonly apagar = output<string>();
  /** Os identificadores a somar. Vazio significa "todas as que podem". */
  readonly somar = output<string[]>();

  protected readonly vazio = computed(() => this.itens().length === 0);

  /** Marcadas à mão. Sem nenhuma marcada, o botão soma todas. */
  protected readonly marcadas = signal<ReadonlySet<string>>(new Set());

  protected readonly somaveis = computed(() => this.itens().filter((item) => item.podeSomar));

  protected readonly escolhidas = computed(() => {
    const marcadas = this.marcadas();
    return this.somaveis().filter((item) => marcadas.has(item.id));
  });

  /** Quantas entram na soma: as marcadas, ou todas quando não há marcação. */
  protected readonly quantasNaSoma = computed(() =>
    this.escolhidas().length > 0 ? this.escolhidas().length : this.somaveis().length,
  );

  protected marcada(id: string): boolean {
    return this.marcadas().has(id);
  }

  protected alternar(id: string, evento: Event): void {
    const marcado = (evento.target as HTMLInputElement).checked;

    this.marcadas.update((atuais) => {
      const proximas = new Set(atuais);
      if (marcado) {
        proximas.add(id);
      } else {
        proximas.delete(id);
      }
      return proximas;
    });
  }

  protected pedirSoma(): void {
    this.somar.emit(this.escolhidas().map((item) => item.id));
  }

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
