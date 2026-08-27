import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { App } from './app';
import { Relatorio } from './modelos';

const relatorio: Relatorio = {
  titulo: 'FLUXO DE CAIXA',
  arquivo: 'fluxo.csv',
  encoding: 'windows-1252',
  separador: ';',
  meses: ['JAN/2026', 'FEV/2026'],
  resumoPorMes: [
    {
      mes: 'JAN/2026',
      recebimentosPrevisto: 100,
      recebimentosRealizado: 90,
      pagamentosPrevisto: -40,
      pagamentosRealizado: -35,
      geracaoPrevista: 60,
      geracaoRealizada: 55,
      saldoFinalRealizado: 1055,
    },
    {
      mes: 'FEV/2026',
      recebimentosPrevisto: 200,
      recebimentosRealizado: 210,
      pagamentosPrevisto: -50,
      pagamentosRealizado: -55,
      geracaoPrevista: 150,
      geracaoRealizada: 155,
      saldoFinalRealizado: 1210,
    },
  ],
  detalhePorMes: [
    {
      mes: 'JAN/2026',
      categorias: [
        {
          rotulo: 'Total de Recebimentos',
          nivel: 0,
          previsto: 100,
          realizado: 90,
          diferenca: -10,
          percentualRealizado: 90,
        },
        {
          rotulo: 'Vendas',
          nivel: 1,
          previsto: 100,
          realizado: 90,
          diferenca: -10,
          percentualRealizado: 90,
        },
      ],
    },
    { mes: 'FEV/2026', categorias: [] },
  ],
  totalDoPeriodo: [
    {
      rotulo: 'Total de Recebimentos',
      nivel: 0,
      previsto: 300,
      realizado: 300,
      diferenca: 0,
      percentualRealizado: 100,
    },
  ],
  totalGeral: [
    {
      rotulo: 'Total de recebimentos',
      nivel: 0,
      previsto: 300,
      realizado: 300,
      diferenca: 0,
      percentualRealizado: 100,
    },
  ],
  conferencia: { ok: true, divergencias: [] },
  avisos: [],
};

describe('App', () => {
  let fixture: ComponentFixture<App>;
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(App);
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  function enviarPlanilha(): void {
    const componente = fixture.componentInstance as unknown as {
      arquivo: { set(valor: File): void };
      analisar(): void;
    };

    componente.arquivo.set(new File(['a;b'], 'fluxo.csv', { type: 'text/csv' }));
    componente.analisar();

    http.expectOne((r) => r.url === '/api/fluxo/analisar').flush(relatorio);
    fixture.detectChanges();
  }

  it('mostra o título e o convite para enviar a planilha', () => {
    const texto = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(texto).toContain('Fluxo de Caixa');
    expect(texto).toContain('Escolher planilha CSV');
  });

  it('não pede análise enquanto não há arquivo', () => {
    const botao = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>(
      'button.principal',
    );

    expect(botao?.disabled).toBeTrue();
    http.expectNone((r) => r.url.includes('/api/fluxo'));
  });

  it('mostra o resumo, o total geral e o detalhamento depois de analisar', () => {
    enviarPlanilha();

    const raiz = fixture.nativeElement as HTMLElement;
    const texto = raiz.textContent ?? '';

    expect(texto).toContain('Resumo por mês');
    expect(texto).toContain('Total geral');
    expect(texto).toContain('JAN/2026');
    expect(texto).toContain('Os totais conferem com a origem');
    expect(raiz.querySelectorAll('app-resumo-mensal tbody tr').length).toBe(2);
  });

  it('formata os valores no padrão brasileiro', () => {
    enviarPlanilha();

    const total = (fixture.nativeElement as HTMLElement).querySelector('app-total-geral .realizado');
    expect(total?.textContent?.trim()).toBe('300,00');
  });

  it('troca o detalhamento ao clicar em um mês do resumo', () => {
    enviarPlanilha();

    const raiz = fixture.nativeElement as HTMLElement;
    const janeiro = raiz.querySelector<HTMLElement>('app-resumo-mensal tbody tr');
    janeiro?.click();
    fixture.detectChanges();

    const abaAtiva = raiz.querySelector('app-detalhe-mes .aba.ativa');
    expect(abaAtiva?.textContent?.trim()).toBe('JAN/2026');
    expect(raiz.querySelector('app-detalhe-mes tbody')?.textContent).toContain('Vendas');
  });

  it('mostra a mensagem de erro devolvida pela API', () => {
    const componente = fixture.componentInstance as unknown as {
      arquivo: { set(valor: File): void };
      analisar(): void;
    };

    componente.arquivo.set(new File(['a'], 'errado.csv'));
    componente.analisar();

    http
      .expectOne((r) => r.url === '/api/fluxo/analisar')
      .flush({ mensagem: 'Arquivo fora do formato esperado.' }, { status: 400, statusText: 'Bad Request' });
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelector('.alerta.erro')?.textContent).toContain(
      'Arquivo fora do formato esperado.',
    );
  });
});
