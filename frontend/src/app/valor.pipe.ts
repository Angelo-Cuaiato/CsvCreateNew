import { Pipe, PipeTransform } from '@angular/core';

/**
 * Valor em reais no padrão brasileiro: 43.623.381,07. Célula vazia vira "—".
 */
@Pipe({ name: 'valor' })
export class ValorPipe implements PipeTransform {
  private static readonly formato = new Intl.NumberFormat('pt-BR', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  });

  transform(valor: number | null | undefined, vazio = '—'): string {
    return valor === null || valor === undefined ? vazio : ValorPipe.formato.format(valor);
  }
}

/**
 * Percentual já calculado pela API: 98,7%.
 */
@Pipe({ name: 'percentual' })
export class PercentualPipe implements PipeTransform {
  private static readonly formato = new Intl.NumberFormat('pt-BR', {
    minimumFractionDigits: 1,
    maximumFractionDigits: 1,
  });

  transform(valor: number | null | undefined, vazio = '—'): string {
    return valor === null || valor === undefined
      ? vazio
      : `${PercentualPipe.formato.format(valor)}%`;
  }
}
