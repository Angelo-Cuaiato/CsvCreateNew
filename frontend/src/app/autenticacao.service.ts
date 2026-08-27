import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';

/** O que a API devolve no login. */
export interface Sessao {
  token: string;
  expiraEm: string;
  email: string;
  nome: string;
  perfil: string;
}

const CHAVE = 'fluxo-caixa.sessao';

/**
 * Guarda a sessão do usuário e fala com /api/auth.
 *
 * A sessão fica no `sessionStorage`: some quando a aba fecha e não é
 * compartilhada com outras abas do mesmo navegador. É o suficiente para um
 * token de vida curta; se o requisito for resistir a XSS, o caminho é o
 * backend mandar o token em cookie `HttpOnly` + `SameSite=Strict` e o front
 * parar de tocar nele.
 */
@Injectable({ providedIn: 'root' })
export class AutenticacaoService {
  private readonly http = inject(HttpClient);
  private readonly sessao = signal<Sessao | null>(this.recuperar());

  /** Quem está logado agora, ou null. */
  readonly usuario = computed(() => this.sessao());

  /** True enquanto houver sessão válida (token não expirado). */
  readonly autenticado = computed(() => {
    const atual = this.sessao();
    return atual !== null && !this.expirou(atual);
  });

  entrar(email: string, senha: string): Observable<Sessao> {
    return this.http
      .post<Sessao>('/api/auth/login', { email, senha })
      .pipe(tap((sessao) => this.guardar(sessao)));
  }

  sair(): void {
    this.sessao.set(null);
    this.limparArmazenamento();
  }

  /** Token para o interceptor colocar no cabeçalho Authorization. */
  token(): string | null {
    const atual = this.sessao();
    if (atual === null || this.expirou(atual)) {
      return null;
    }

    return atual.token;
  }

  private guardar(sessao: Sessao): void {
    this.sessao.set(sessao);
    try {
      sessionStorage.setItem(CHAVE, JSON.stringify(sessao));
    } catch {
      // Navegador sem armazenamento (janela anônima, permissão negada): a
      // sessão continua valendo em memória até recarregar a página.
    }
  }

  private recuperar(): Sessao | null {
    try {
      const guardado = sessionStorage.getItem(CHAVE);
      if (!guardado) {
        return null;
      }

      const sessao = JSON.parse(guardado) as Sessao;
      return this.expirou(sessao) ? null : sessao;
    } catch {
      return null;
    }
  }

  private limparArmazenamento(): void {
    try {
      sessionStorage.removeItem(CHAVE);
    } catch {
      // Nada a fazer: sem armazenamento não há o que limpar.
    }
  }

  private expirou(sessao: Sessao): boolean {
    const limite = Date.parse(sessao.expiraEm);
    return Number.isNaN(limite) || limite <= Date.now();
  }
}
