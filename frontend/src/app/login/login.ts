import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { AutenticacaoService } from '../autenticacao.service';

/**
 * Tela de entrada. Só ela aparece enquanto ninguém está autenticado.
 */
@Component({
  selector: 'app-login',
  imports: [FormsModule],
  templateUrl: './login.html',
  styleUrl: './login.css',
})
export class Login {
  private readonly autenticacao = inject(AutenticacaoService);

  protected readonly email = signal('');
  protected readonly senha = signal('');
  protected readonly entrando = signal(false);
  protected readonly erro = signal<string | null>(null);

  protected entrar(evento: Event): void {
    evento.preventDefault();

    if (this.entrando() || !this.email().trim() || !this.senha()) {
      return;
    }

    this.entrando.set(true);
    this.erro.set(null);

    this.autenticacao.entrar(this.email().trim(), this.senha()).subscribe({
      next: () => {
        // A senha não fica em memória depois que o token chega.
        this.senha.set('');
        this.entrando.set(false);
      },
      error: (falha: unknown) => {
        this.senha.set('');
        this.erro.set(this.mensagemDeErro(falha));
        this.entrando.set(false);
      },
    });
  }

  private mensagemDeErro(falha: unknown): string {
    const status = (falha as { status?: number })?.status;

    if (status === 0) {
      return 'Não foi possível falar com a API. Verifique se o backend está no ar.';
    }

    if (status === 429) {
      return 'Muitas tentativas seguidas. Espere um minuto e tente de novo.';
    }

    const mensagem = (falha as { error?: { mensagem?: string } })?.error?.mensagem;
    return mensagem ?? 'Não foi possível entrar.';
  }
}
