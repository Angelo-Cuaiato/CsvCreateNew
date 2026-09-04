import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { AutenticacaoService, Sessao } from './autenticacao.service';
import { FluxoCaixaService } from './fluxo-caixa.service';
import { tokenInterceptor } from './token.interceptor';

function sessao(minutos = 60): Sessao {
  return {
    token: 'token-de-teste',
    expiraEm: new Date(Date.now() + minutos * 60_000).toISOString(),
    email: 'admin@exemplo.com',
    nome: 'Administrador',
    perfil: 'administrador',
  };
}

describe('AutenticacaoService', () => {
  let autenticacao: AutenticacaoService;
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([tokenInterceptor])),
        provideHttpClientTesting(),
      ],
    });

    autenticacao = TestBed.inject(AutenticacaoService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  it('começa deslogado', () => {
    expect(autenticacao.autenticado()).toBeFalse();
    expect(autenticacao.token()).toBeNull();
  });

  it('guarda a sessão depois do login', () => {
    autenticacao.entrar('admin@exemplo.com', 'fluxo@2026').subscribe();

    const requisicao = http.expectOne('/api/auth/login');
    expect(requisicao.request.method).toBe('POST');
    expect(requisicao.request.body).toEqual({ email: 'admin@exemplo.com', senha: 'fluxo@2026' });

    requisicao.flush(sessao());

    expect(autenticacao.autenticado()).toBeTrue();
    expect(autenticacao.usuario()?.nome).toBe('Administrador');
    expect(autenticacao.token()).toBe('token-de-teste');
  });

  it('não guarda nada quando o login falha', () => {
    autenticacao.entrar('admin@exemplo.com', 'errada').subscribe({ error: () => undefined });

    http
      .expectOne('/api/auth/login')
      .flush({ mensagem: 'E-mail ou senha inválidos.' }, { status: 401, statusText: 'Unauthorized' });

    expect(autenticacao.autenticado()).toBeFalse();
    expect(localStorage.getItem('fluxo-caixa.sessao')).toBeNull();
  });

  it('trata token vencido como sessão encerrada', () => {
    autenticacao.entrar('admin@exemplo.com', 'fluxo@2026').subscribe();
    http.expectOne('/api/auth/login').flush(sessao(-1));

    expect(autenticacao.autenticado()).toBeFalse();
    expect(autenticacao.token()).toBeNull();
  });

  it('sair limpa a sessão e o armazenamento', () => {
    autenticacao.entrar('admin@exemplo.com', 'fluxo@2026').subscribe();
    http.expectOne('/api/auth/login').flush(sessao());

    autenticacao.sair();

    expect(autenticacao.autenticado()).toBeFalse();
    expect(localStorage.getItem('fluxo-caixa.sessao')).toBeNull();
  });
});

describe('tokenInterceptor', () => {
  let autenticacao: AutenticacaoService;
  let fluxo: FluxoCaixaService;
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([tokenInterceptor])),
        provideHttpClientTesting(),
      ],
    });

    autenticacao = TestBed.inject(AutenticacaoService);
    fluxo = TestBed.inject(FluxoCaixaService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  function entrar(): void {
    autenticacao.entrar('admin@exemplo.com', 'fluxo@2026').subscribe();
    http.expectOne('/api/auth/login').flush(sessao());
  }

  it('põe o token nas chamadas da API', () => {
    entrar();

    fluxo.analisar(new File(['a'], 'fluxo.csv')).subscribe();
    const requisicao = http.expectOne((r) => r.url === '/api/fluxo/analisar');

    expect(requisicao.request.headers.get('Authorization')).toBe('Bearer token-de-teste');
    requisicao.flush({});
  });

  it('não manda token no próprio login', () => {
    entrar();

    autenticacao.entrar('outro@exemplo.com', 'senha').subscribe();
    const requisicao = http.expectOne('/api/auth/login');

    expect(requisicao.request.headers.has('Authorization')).toBeFalse();
    requisicao.flush(sessao());
  });

  it('derruba a sessão quando a API responde 401', () => {
    entrar();
    expect(autenticacao.autenticado()).toBeTrue();

    fluxo.analisar(new File(['a'], 'fluxo.csv')).subscribe({ error: () => undefined });
    http
      .expectOne((r) => r.url === '/api/fluxo/analisar')
      .flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(autenticacao.autenticado()).toBeFalse();
  });
});

describe('AutenticacaoService — a sessão dura entre visitas', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
  });

  afterEach(() => localStorage.clear());

  it('guarda no localStorage, que sobrevive a fechar a aba', () => {
    // sessionStorage morre com a aba: quem fechasse o navegador tinha que
    // entrar de novo, e abrir uma segunda aba já pedia senha.
    const servico = TestBed.inject(AutenticacaoService);

    servico.entrar('admin@exemplo.com', 'segredo').subscribe();
    TestBed.inject(HttpTestingController).expectOne('/api/auth/login').flush({
      token: 'abc',
      expiraEm: new Date(Date.now() + 3_600_000).toISOString(),
      email: 'admin@exemplo.com',
      nome: 'Administrador',
      perfil: 'administrador',
    });

    expect(localStorage.getItem('fluxo-caixa.sessao')).toContain('abc');
    expect(sessionStorage.getItem('fluxo-caixa.sessao')).toBeNull();
  });

  it('descarta e limpa o que já venceu', () => {
    localStorage.setItem(
      'fluxo-caixa.sessao',
      JSON.stringify({
        token: 'velho',
        expiraEm: new Date(Date.now() - 1000).toISOString(),
        email: 'admin@exemplo.com',
        nome: 'Administrador',
        perfil: 'administrador',
      }),
    );

    const servico = TestBed.inject(AutenticacaoService);

    expect(servico.autenticado()).toBeFalse();
    expect(servico.token()).toBeNull();
    // Não fica lixo guardado para a próxima leitura tropeçar de novo.
    expect(localStorage.getItem('fluxo-caixa.sessao')).toBeNull();
  });
});
