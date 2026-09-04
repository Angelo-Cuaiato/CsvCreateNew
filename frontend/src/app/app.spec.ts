import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { App } from './app';
import { Sessao } from './autenticacao.service';
import { Historico, Relatorio } from './modelos';
import { tokenInterceptor } from './token.interceptor';

const sessao: Sessao = {
  token: 'token-de-teste',
  expiraEm: new Date(Date.now() + 3_600_000).toISOString(),
  email: 'admin@exemplo.com',
  nome: 'Administrador',
  perfil: 'administrador',
};

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
  conferencia: { ok: true, comparavel: true, divergencias: [] },
  avisos: [],
};

const idDaAnalise = '3f1b9c1e-6a2f-4c0b-9d5e-7a1c2b3d4e5f';

const historico: Historico = {
  persistente: true,
  itens: [
    {
      id: idDaAnalise,
      email: 'admin@exemplo.com',
      autor: 'Administrador',
      nomeArquivo: 'fluxo.csv',
      enviadoEm: '2026-09-04T14:54:00Z',
      primeiroMes: 'JAN/2026',
      ultimoMes: 'FEV/2026',
      quantidadeDeMeses: 2,
      conferenciaOk: true,
      conferenciaComparavel: true,
      podeSomar: true,
    },
  ],
};

describe('App', () => {
  let fixture: ComponentFixture<App>;
  let http: HttpTestingController;

  beforeEach(async () => {
    // Já logado: a sessão guardada é lida quando o serviço é criado.
    localStorage.setItem('fluxo-caixa.sessao', JSON.stringify(sessao));

    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideHttpClient(withInterceptors([tokenInterceptor])),
        provideHttpClientTesting(),
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(App);
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();

    // Com sessão, a tela busca o histórico sozinha.
    http.expectOne('/api/fluxo/historico').flush(historico);
    fixture.detectChanges();
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  function enviarPlanilha(): void {
    const componente = fixture.componentInstance as unknown as {
      arquivo: { set(valor: File): void };
      analisar(): void;
    };

    componente.arquivo.set(new File(['a;b'], 'fluxo.csv', { type: 'text/csv' }));
    componente.analisar();

    http
      .expectOne((r) => r.url === '/api/fluxo/analisar')
      .flush({ id: idDaAnalise, relatorio });
    fixture.detectChanges();

    // Analisar guarda a análise, então a lista é buscada de novo.
    http.expectOne('/api/fluxo/historico').flush(historico);
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

  it('identifica quem está logado no topo', () => {
    const texto = (fixture.nativeElement as HTMLElement).querySelector('.sessao')?.textContent ?? '';

    expect(texto).toContain('Administrador');
    expect(texto).toContain('administrador');
  });

  it('sair volta para a tela de login', () => {
    enviarPlanilha();

    (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('.sessao .sair')?.click();
    fixture.detectChanges();

    const raiz = fixture.nativeElement as HTMLElement;
    expect(raiz.querySelector('app-login')).not.toBeNull();
    expect(raiz.querySelector('app-resumo-mensal')).toBeNull();
  });

  it('lista as análises anteriores', () => {
    const texto = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(texto).toContain('Análises anteriores');
    expect(texto).toContain('fluxo.csv');
    expect(texto).toContain('JAN/2026 a FEV/2026');
    expect(texto).toContain('Administrador');
  });

  it('reabre uma análise do histórico sem reenviar a planilha', () => {
    const raiz = fixture.nativeElement as HTMLElement;
    raiz.querySelector<HTMLButtonElement>('app-historico .abrir')?.click();

    http.expectOne(`/api/fluxo/historico/${idDaAnalise}`).flush({ id: idDaAnalise, relatorio });
    fixture.detectChanges();

    // Nenhum upload aconteceu: o relatório veio pronto do servidor.
    http.expectNone((r) => r.url === '/api/fluxo/analisar');
    expect(raiz.textContent).toContain('Resumo por mês');
    expect(raiz.querySelectorAll('app-resumo-mensal tbody tr').length).toBe(2);
  });

  it('baixa o CSV guardado pelo identificador da análise', () => {
    const raiz = fixture.nativeElement as HTMLElement;
    const baixar = Array.from(raiz.querySelectorAll<HTMLButtonElement>('app-historico .acoes button'))
      .find((botao) => botao.textContent?.includes('Baixar'));

    baixar?.click();

    const pedido = http.expectOne(`/api/fluxo/historico/${idDaAnalise}/csv`);
    expect(pedido.request.method).toBe('GET');
    pedido.flush(new Blob(['a;b'], { type: 'text/csv' }));
  });

  it('apaga uma análise e tira a linha da lista', () => {
    const raiz = fixture.nativeElement as HTMLElement;
    const apagar = raiz.querySelector<HTMLButtonElement>('app-historico .acoes .remover');

    apagar?.click();
    http.expectOne(`/api/fluxo/historico/${idDaAnalise}`).flush(null, { status: 204, statusText: 'No Content' });
    fixture.detectChanges();

    expect(raiz.querySelector('app-historico tbody')).toBeNull();
    expect(raiz.textContent).toContain('Nenhuma análise ainda');
  });

  it('mostra o somatório de todas na tela quando nenhuma está marcada', () => {
    const raiz = fixture.nativeElement as HTMLElement;
    expect(raiz.querySelector('app-historico .somatorio p')?.textContent).toContain(
      'soma todas as 1 guardadas',
    );

    raiz.querySelector<HTMLButtonElement>('app-historico .somatorio button')?.click();

    const pedido = http.expectOne('/api/fluxo/somatorio');
    expect(pedido.request.method).toBe('POST');
    expect(pedido.request.body).toEqual({ ids: [] });

    pedido.flush({ ...relatorio, arquivo: 'poa.csv + cmbs.csv' });
    fixture.detectChanges();

    // O relatório somado ocupa a tela, como qualquer outra análise.
    expect(raiz.textContent).toContain('Somatório de');
    expect(raiz.textContent).toContain('poa.csv + cmbs.csv');
    expect(raiz.textContent).toContain('Total geral');
    expect(raiz.querySelectorAll('app-resumo-mensal tbody tr').length).toBe(2);
  });

  it('soma só as análises marcadas', () => {
    const raiz = fixture.nativeElement as HTMLElement;
    const caixa = raiz.querySelector<HTMLInputElement>('app-historico .marca input');

    caixa!.checked = true;
    caixa!.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    expect(raiz.querySelector('app-historico .somatorio p')?.textContent).toContain(
      '1 análise(s) marcada(s)',
    );

    raiz.querySelector<HTMLButtonElement>('app-historico .somatorio button')?.click();

    const pedido = http.expectOne('/api/fluxo/somatorio');
    expect(pedido.request.body).toEqual({ ids: [idDaAnalise] });
    pedido.flush(relatorio);
  });

  it('baixa o CSV do somatório que está na tela, e não o de uma análise', () => {
    const raiz = fixture.nativeElement as HTMLElement;
    raiz.querySelector<HTMLButtonElement>('app-historico .somatorio button')?.click();
    http.expectOne('/api/fluxo/somatorio').flush(relatorio);
    fixture.detectChanges();

    const baixar = raiz.querySelector<HTMLButtonElement>('.identificacao .principal');
    expect(baixar?.textContent?.trim()).toBe('Baixar CSV do somatório');

    baixar?.click();

    const pedido = http.expectOne('/api/fluxo/somatorio/csv');
    expect(pedido.request.body).toEqual({ ids: [] });
    pedido.flush(new Blob(['a;b'], { type: 'text/csv' }));
  });

  it('não deixa marcar uma análise sem planilha de origem guardada', () => {
    const componente = fixture.componentInstance as unknown as { carregarHistorico(): void };
    componente.carregarHistorico();

    http.expectOne('/api/fluxo/historico').flush({
      persistente: true,
      itens: [{ ...historico.itens[0], podeSomar: false }],
    });
    fixture.detectChanges();

    const caixa = (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLInputElement>('app-historico .marca input');

    expect(caixa?.disabled).toBeTrue();
  });

  it('avisa quando o histórico não sobrevive a um reinício', () => {
    const componente = fixture.componentInstance as unknown as { carregarHistorico(): void };
    componente.carregarHistorico();

    http.expectOne('/api/fluxo/historico').flush({ ...historico, persistente: false });
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('sem banco de dados');
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

describe('App sem sessão', () => {
  let fixture: ComponentFixture<App>;
  let http: HttpTestingController;

  beforeEach(async () => {
    localStorage.clear();

    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideHttpClient(withInterceptors([tokenInterceptor])),
        provideHttpClientTesting(),
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(App);
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  it('mostra o login e esconde o resto da aplicação', () => {
    const raiz = fixture.nativeElement as HTMLElement;

    expect(raiz.querySelector('app-login')).not.toBeNull();
    expect(raiz.querySelector('input[type=file]')).toBeNull();
    expect(raiz.textContent).toContain('Entre para enviar a planilha');
  });

  it('entra com as credenciais certas e libera a aplicação', () => {
    const raiz = fixture.nativeElement as HTMLElement;

    const email = raiz.querySelector<HTMLInputElement>('input[type=email]')!;
    email.value = 'admin@exemplo.com';
    email.dispatchEvent(new Event('input'));

    const senha = raiz.querySelector<HTMLInputElement>('input[type=password]')!;
    senha.value = 'fluxo@2026';
    senha.dispatchEvent(new Event('input'));

    raiz.querySelector('form')!.dispatchEvent(new Event('submit'));

    const requisicao = http.expectOne('/api/auth/login');
    expect(requisicao.request.body).toEqual({ email: 'admin@exemplo.com', senha: 'fluxo@2026' });
    requisicao.flush({
      token: 'token-de-teste',
      expiraEm: new Date(Date.now() + 3_600_000).toISOString(),
      email: 'admin@exemplo.com',
      nome: 'Administrador',
      perfil: 'administrador',
    });
    fixture.detectChanges();

    // Entrar já traz o histórico, sem precisar recarregar a página.
    http.expectOne('/api/fluxo/historico').flush({ persistente: true, itens: [] });
    fixture.detectChanges();

    expect(raiz.querySelector('app-login')).toBeNull();
    expect(raiz.querySelector('input[type=file]')).not.toBeNull();
    localStorage.clear();
  });

  it('mostra o erro quando as credenciais não conferem', () => {
    const raiz = fixture.nativeElement as HTMLElement;

    const email = raiz.querySelector<HTMLInputElement>('input[type=email]')!;
    email.value = 'admin@exemplo.com';
    email.dispatchEvent(new Event('input'));

    const senha = raiz.querySelector<HTMLInputElement>('input[type=password]')!;
    senha.value = 'errada';
    senha.dispatchEvent(new Event('input'));

    raiz.querySelector('form')!.dispatchEvent(new Event('submit'));

    http
      .expectOne('/api/auth/login')
      .flush({ mensagem: 'E-mail ou senha inválidos.' }, { status: 401, statusText: 'Unauthorized' });
    fixture.detectChanges();

    expect(raiz.querySelector('.alerta.erro')?.textContent).toContain('E-mail ou senha inválidos.');
    expect(raiz.querySelector('app-login')).not.toBeNull();
  });
});
