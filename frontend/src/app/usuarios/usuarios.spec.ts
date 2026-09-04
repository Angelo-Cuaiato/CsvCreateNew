import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { App } from '../app';
import { Sessao } from '../autenticacao.service';

function entrar(perfil: string): void {
  const sessao: Sessao = {
    token: 'token-de-teste',
    expiraEm: new Date(Date.now() + 3_600_000).toISOString(),
    email: 'chefe@exemplo.com',
    nome: 'Chefe',
    perfil,
  };

  localStorage.setItem('fluxo-caixa.sessao', JSON.stringify(sessao));
}

const historico = { persistente: true, itens: [] };
const totais = { analises: 0, periodo: '', totalGeral: [] };

const usuarios = {
  persistente: true,
  itens: [
    { email: 'chefe@exemplo.com', nome: 'Chefe', perfil: 'administrador' },
    { email: 'maria@exemplo.com', nome: 'Maria', perfil: 'usuario' },
  ],
};

describe('Tela de usuários', () => {
  let fixture: ComponentFixture<App>;
  let http: HttpTestingController;

  async function montar(perfil: string): Promise<void> {
    entrar(perfil);

    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(App);
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();

    http.expectOne('/api/fluxo/historico').flush(historico);
    http.expectOne('/api/fluxo/totais').flush(totais);
    fixture.detectChanges();
  }

  function abrir(): void {
    const botao = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('.sessao button'),
    ).find((b) => b.textContent?.trim() === 'Usuários');

    botao?.click();
    http.expectOne('/api/usuarios').flush(usuarios);
    fixture.detectChanges();
  }

  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  it('não oferece a tela para quem é usuário comum', async () => {
    await montar('usuario');

    const topo = (fixture.nativeElement as HTMLElement).querySelector('.sessao')?.textContent ?? '';
    expect(topo).not.toContain('Usuários');
    http.expectNone('/api/usuarios');
  });

  it('administrador abre a tela e vê quem está cadastrado', async () => {
    await montar('administrador');
    abrir();

    const raiz = fixture.nativeElement as HTMLElement;
    expect(raiz.querySelector('app-usuarios')).not.toBeNull();
    expect(raiz.textContent).toContain('maria@exemplo.com');
    expect(raiz.textContent).toContain('Maria');

    // A tela do fluxo sai de cena: são duas telas, não duas seções.
    expect(raiz.querySelector('app-historico')).toBeNull();
  });

  it('cadastra um usuário e recarrega a lista', async () => {
    await montar('administrador');
    abrir();

    const raiz = fixture.nativeElement as HTMLElement;
    const preencher = (seletor: string, valor: string) => {
      const campo = raiz.querySelector<HTMLInputElement>(seletor)!;
      campo.value = valor;
      campo.dispatchEvent(new Event('input'));
    };

    preencher('.novo input[type=text]', 'João');
    preencher('.novo input[type=email]', 'joao@exemplo.com');
    preencher('.novo input[type=password]', 'senha-com-oito');
    fixture.detectChanges();

    raiz.querySelector('form.novo')!.dispatchEvent(new Event('submit'));

    const pedido = http.expectOne('/api/usuarios');
    expect(pedido.request.method).toBe('POST');
    expect(pedido.request.body).toEqual({
      email: 'joao@exemplo.com',
      nome: 'João',
      senha: 'senha-com-oito',
      perfil: 'usuario',
    });

    pedido.flush({});
    http.expectOne('/api/usuarios').flush(usuarios);
  });

  it('não deixa cadastrar com senha curta', async () => {
    await montar('administrador');
    abrir();

    const raiz = fixture.nativeElement as HTMLElement;
    const preencher = (seletor: string, valor: string) => {
      const campo = raiz.querySelector<HTMLInputElement>(seletor)!;
      campo.value = valor;
      campo.dispatchEvent(new Event('input'));
    };

    preencher('.novo input[type=text]', 'João');
    preencher('.novo input[type=email]', 'joao@exemplo.com');
    preencher('.novo input[type=password]', 'curta');
    fixture.detectChanges();

    expect(raiz.querySelector<HTMLButtonElement>('.novo .principal')?.disabled).toBeTrue();
  });

  it('troca a senha de outro usuário', async () => {
    await montar('administrador');
    abrir();

    const raiz = fixture.nativeElement as HTMLElement;
    const linhaDaMaria = raiz.querySelectorAll('tbody tr')[1];
    const trocar = linhaDaMaria.querySelector<HTMLButtonElement>('.acoes button')!;

    trocar.click();
    fixture.detectChanges();

    const campo = raiz.querySelector<HTMLInputElement>('.troca input')!;
    campo.value = 'senha-nova-dela';
    campo.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    raiz.querySelector<HTMLButtonElement>('.troca .principal')!.click();

    const pedido = http.expectOne('/api/usuarios/maria%40exemplo.com/senha');
    expect(pedido.request.method).toBe('PUT');
    expect(pedido.request.body).toEqual({ senha: 'senha-nova-dela' });
    pedido.flush(null);
  });

  it('não deixa o administrador excluir a si mesmo', async () => {
    await montar('administrador');
    abrir();

    const raiz = fixture.nativeElement as HTMLElement;
    const linhas = raiz.querySelectorAll('tbody tr');

    const excluirEu = linhas[0].querySelectorAll<HTMLButtonElement>('.acoes button')[1];
    const excluirMaria = linhas[1].querySelectorAll<HTMLButtonElement>('.acoes button')[1];

    expect(excluirEu.disabled).toBeTrue();
    expect(excluirMaria.disabled).toBeFalse();

    excluirMaria.click();
    http.expectOne('/api/usuarios/maria%40exemplo.com').flush(null, { status: 204, statusText: 'No Content' });
    fixture.detectChanges();

    expect(raiz.textContent).not.toContain('maria@exemplo.com');
  });

  it('mostra a mensagem que a API devolve ao recusar', async () => {
    await montar('administrador');
    abrir();

    const raiz = fixture.nativeElement as HTMLElement;
    const excluirMaria = raiz.querySelectorAll('tbody tr')[1]
      .querySelectorAll<HTMLButtonElement>('.acoes button')[1];

    excluirMaria.click();
    http
      .expectOne('/api/usuarios/maria%40exemplo.com')
      .flush({ mensagem: 'Este é o único administrador.' }, { status: 400, statusText: 'Bad Request' });
    fixture.detectChanges();

    expect(raiz.querySelector('.alerta.erro')?.textContent).toContain('único administrador');
  });
});
