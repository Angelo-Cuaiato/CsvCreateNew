import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { FluxoCaixaService } from './fluxo-caixa.service';
import { AnaliseFeita, Relatorio } from './modelos';

describe('FluxoCaixaService', () => {
  let servico: FluxoCaixaService;
  let http: HttpTestingController;

  const planilha = () => new File(['CATEGORIAS;JAN/2026'], 'fluxo.csv', { type: 'text/csv' });

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    servico = TestBed.inject(FluxoCaixaService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('envia a planilha como multipart para /api/fluxo/analisar', () => {
    let recebido: AnaliseFeita | undefined;
    servico.analisar(planilha()).subscribe((analise) => (recebido = analise));

    const requisicao = http.expectOne((r) => r.url === '/api/fluxo/analisar');

    expect(requisicao.request.method).toBe('POST');
    expect(requisicao.request.body instanceof FormData).toBeTrue();
    expect((requisicao.request.body as FormData).get('arquivo')).toBeTruthy();
    expect(requisicao.request.params.has('incluirZerados')).toBeFalse();

    requisicao.flush({ id: 'abc', relatorio: { meses: ['JAN/2026'] } as Partial<Relatorio> });
    expect(recebido?.id).toBe('abc');
    expect(recebido?.relatorio.meses).toEqual(['JAN/2026']);
  });

  it('baixa o CSV guardado pelo identificador, sem enviar a planilha', () => {
    let baixado: { nome: string } | undefined;
    servico.baixarDoHistorico('abc', 'fluxo.csv').subscribe((arquivo) => (baixado = arquivo));

    const requisicao = http.expectOne('/api/fluxo/historico/abc/csv');
    expect(requisicao.request.method).toBe('GET');
    expect(requisicao.request.body).toBeNull();

    requisicao.flush(new Blob(['a;b'], { type: 'text/csv' }), {
      headers: { 'Content-Disposition': 'attachment; filename=fluxo_consolidado.csv' },
    });

    expect(baixado?.nome).toBe('fluxo_consolidado.csv');
  });

  it('lista o histórico', () => {
    servico.historico().subscribe();

    const requisicao = http.expectOne('/api/fluxo/historico');
    expect(requisicao.request.method).toBe('GET');
    requisicao.flush({ persistente: true, itens: [] });
  });

  it('repassa a opção de incluir zerados', () => {
    servico.analisar(planilha(), { incluirZerados: true }).subscribe();

    const requisicao = http.expectOne((r) => r.url === '/api/fluxo/analisar');
    expect(requisicao.request.params.get('incluirZerados')).toBe('true');
    requisicao.flush({});
  });

  it('usa o nome sugerido pela API ao baixar o consolidado', () => {
    let baixado: { nome: string } | undefined;
    servico.consolidar(planilha()).subscribe((arquivo) => (baixado = arquivo));

    const requisicao = http.expectOne((r) => r.url === '/api/fluxo/consolidar');
    expect(requisicao.request.responseType).toBe('blob');

    requisicao.flush(new Blob(['a;b'], { type: 'text/csv' }), {
      headers: { 'Content-Disposition': 'attachment; filename=fluxo_consolidado.csv' },
    });

    expect(baixado?.nome).toBe('fluxo_consolidado.csv');
  });

  it('deriva o nome do arquivo quando a API não manda o cabeçalho', () => {
    let baixado: { nome: string } | undefined;
    servico.consolidar(planilha()).subscribe((arquivo) => (baixado = arquivo));

    http.expectOne((r) => r.url === '/api/fluxo/consolidar').flush(new Blob(['a;b']));

    expect(baixado?.nome).toBe('fluxo_consolidado.csv');
  });
});
