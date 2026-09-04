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
 * A sessão fica no `localStorage`: sobrevive a fechar a aba e vale para todas
 * as abas do mesmo navegador. Era `sessionStorage`, que morria junto com a
 * aba - correto do ponto de vista de exposição, e irritante na prática, porque
 * obrigava a entrar de novo a cada aba aberta.
 *
 * A troca aumenta a janela em que um XSS acharia o token guardado. O que
 * segura o risco é o prazo do próprio token (`Jwt__MinutosDeValidade`, uma
 * hora por padrão): passado ele, o que está guardado não serve mais. Se o
 * requisito for resistir a XSS de verdade, o caminho é o backend mandar o
 * token em cookie `HttpOnly` + `SameSite=Strict` e o front parar de tocar nele.
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
      localStorage.setItem(CHAVE, JSON.stringify(sessao));
    } catch {
      // Navegador sem armazenamento (permissão negada, modo restrito): a
      // sessão continua valendo em memória até recarregar a página.
    }
  }

  private recuperar(): Sessao | null {
    try {
      const guardado = localStorage.getItem(CHAVE);
      if (!guardado) {
        return null;
      }

      const sessao = JSON.parse(guardado) as Sessao;

      if (this.expirou(sessao)) {
        // Não adianta guardar o que já venceu: sai daqui para a próxima
        // leitura não repetir o trabalho.
        this.limparArmazenamento();
        return null;
      }

      return sessao;
    } catch {
      return null;
    }
  }

  private limparArmazenamento(): void {
    try {
      localStorage.removeItem(CHAVE);
    } catch {
      // Nada a fazer: sem armazenamento não há o que limpar.
    }
  }

  private expirou(sessao: Sessao): boolean {
    const limite = Date.parse(sessao.expiraEm);
    return Number.isNaN(limite) || limite <= Date.now();
  }
}
