import { Component, computed, input, output, signal } from '@angular/core';

import { UsuarioDoSistema } from '../modelos';

/** O que o formulário devolve ao criar. */
export interface CadastroDeUsuario {
  email: string;
  nome: string;
  senha: string;
  perfil: string;
}

/** Uma troca de senha pedida pelo administrador. */
export interface TrocaDeSenha {
  email: string;
  senha: string;
}

/**
 * Tela de usuários. Só administrador chega aqui - a API recusa o resto com
 * 403, e a tela nem oferece o caminho.
 */
@Component({
  selector: 'app-usuarios',
  templateUrl: './usuarios.html',
  styleUrl: './usuarios.css',
})
export class Usuarios {
  readonly itens = input.required<UsuarioDoSistema[]>();
  readonly euSou = input<string>('');
  /** Falso quando não há banco: o cadastro some no próximo reinício. */
  readonly persistente = input<boolean>(true);
  readonly erro = input<string | null>(null);
  readonly salvando = input<boolean>(false);

  readonly criar = output<CadastroDeUsuario>();
  readonly trocarSenha = output<TrocaDeSenha>();
  readonly excluir = output<string>();

  protected readonly nome = signal('');
  protected readonly email = signal('');
  protected readonly senha = signal('');
  protected readonly perfil = signal('usuario');

  /** Qual linha está com o campo de nova senha aberto. */
  protected readonly trocando = signal<string | null>(null);
  protected readonly senhaNova = signal('');

  protected readonly podeCriar = computed(
    () =>
      this.nome().trim().length > 0 &&
      this.email().trim().includes('@') &&
      this.senha().length >= 8,
  );

  protected escrever(alvo: { set(valor: string): void }, evento: Event): void {
    alvo.set((evento.target as HTMLInputElement | HTMLSelectElement).value);
  }

  protected enviar(evento: Event): void {
    evento.preventDefault();

    if (!this.podeCriar()) {
      return;
    }

    this.criar.emit({
      email: this.email().trim(),
      nome: this.nome().trim(),
      senha: this.senha(),
      perfil: this.perfil(),
    });
  }

  /** Chamado de fora quando o cadastro deu certo. */
  limpar(): void {
    this.nome.set('');
    this.email.set('');
    this.senha.set('');
    this.perfil.set('usuario');
  }

  protected abrirTroca(email: string): void {
    this.trocando.set(this.trocando() === email ? null : email);
    this.senhaNova.set('');
  }

  protected confirmarTroca(email: string): void {
    if (this.senhaNova().length < 8) {
      return;
    }

    this.trocarSenha.emit({ email, senha: this.senhaNova() });
    this.trocando.set(null);
    this.senhaNova.set('');
  }

  protected souEu(email: string): boolean {
    return email.toLowerCase() === this.euSou().toLowerCase();
  }
}
