import { Component, computed, inject, signal } from '@angular/core';

import { AutenticacaoService } from './autenticacao.service';
import { DetalheMes, TOTAL_DO_PERIODO } from './detalhe-mes/detalhe-mes';
import { FluxoCaixaService } from './fluxo-caixa.service';
import { Login } from './login/login';
import { Relatorio } from './modelos';
import { ResumoMensal } from './resumo-mensal/resumo-mensal';
import { TotalGeral } from './total-geral/total-geral';

@Component({
  selector: 'app-root',
  imports: [Login, ResumoMensal, DetalheMes, TotalGeral],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {
  private readonly servico = inject(FluxoCaixaService);
  private readonly autenticacao = inject(AutenticacaoService);

  protected readonly autenticado = this.autenticacao.autenticado;
  protected readonly usuario = this.autenticacao.usuario;

  protected readonly arquivo = signal<File | null>(null);
  protected readonly relatorio = signal<Relatorio | null>(null);
  protected readonly incluirZerados = signal(false);
  protected readonly analisando = signal(false);
  protected readonly baixando = signal(false);
  protected readonly erro = signal<string | null>(null);
  protected readonly mesSelecionado = signal<string>(TOTAL_DO_PERIODO);

  protected readonly periodo = computed(() => {
    const meses = this.relatorio()?.meses ?? [];
    return meses.length > 0 ? `${meses[0]} a ${meses.at(-1)} (${meses.length} meses)` : '';
  });

  protected sair(): void {
    this.autenticacao.sair();
    this.arquivo.set(null);
    this.relatorio.set(null);
    this.erro.set(null);
  }

  protected selecionarArquivo(evento: Event): void {
    const escolhido = (evento.target as HTMLInputElement).files?.[0] ?? null;
    this.arquivo.set(escolhido);
    this.relatorio.set(null);
    this.erro.set(null);
  }

  protected soltarArquivo(evento: DragEvent): void {
    evento.preventDefault();
    const escolhido = evento.dataTransfer?.files?.[0] ?? null;
    if (escolhido) {
      this.arquivo.set(escolhido);
      this.relatorio.set(null);
      this.erro.set(null);
    }
  }

  protected alternarZerados(evento: Event): void {
    this.incluirZerados.set((evento.target as HTMLInputElement).checked);
    if (this.relatorio()) {
      this.analisar();
    }
  }

  protected analisar(): void {
    const arquivo = this.arquivo();
    if (!arquivo || this.analisando()) {
      return;
    }

    this.analisando.set(true);
    this.erro.set(null);

    this.servico.analisar(arquivo, { incluirZerados: this.incluirZerados() }).subscribe({
      next: (relatorio) => {
        this.relatorio.set(relatorio);
        this.mesSelecionado.set(TOTAL_DO_PERIODO);
        this.analisando.set(false);
      },
      error: (falha: unknown) => {
        this.erro.set(this.mensagemDeErro(falha));
        this.analisando.set(false);
      },
    });
  }

  protected baixar(): void {
    const arquivo = this.arquivo();
    if (!arquivo || this.baixando()) {
      return;
    }

    this.baixando.set(true);

    this.servico.consolidar(arquivo, { incluirZerados: this.incluirZerados() }).subscribe({
      next: ({ conteudo, nome }) => {
        const endereco = URL.createObjectURL(conteudo);
        const link = document.createElement('a');
        link.href = endereco;
        link.download = nome;
        link.click();
        URL.revokeObjectURL(endereco);
        this.baixando.set(false);
      },
      error: (falha: unknown) => {
        this.erro.set(this.mensagemDeErro(falha));
        this.baixando.set(false);
      },
    });
  }

  /** A API manda { mensagem } nos erros de leitura; o resto vira texto genérico. */
  private mensagemDeErro(falha: unknown): string {
    const corpo = (falha as { error?: { mensagem?: string } })?.error;
    if (corpo?.mensagem) {
      return corpo.mensagem;
    }

    const status = (falha as { status?: number })?.status;
    if (status === 0) {
      return 'Não foi possível falar com a API. Verifique se o backend está no ar.';
    }

    if (status === 401) {
      return 'Sua sessão expirou. Entre de novo para continuar.';
    }

    return 'Não foi possível processar a planilha.';
  }
}
