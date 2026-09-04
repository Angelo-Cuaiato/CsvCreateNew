import { Component, computed, effect, inject, signal } from '@angular/core';
import { Observable } from 'rxjs';

import { AutenticacaoService } from './autenticacao.service';
import { DetalheMes, TOTAL_DO_PERIODO } from './detalhe-mes/detalhe-mes';
import { FluxoCaixaService } from './fluxo-caixa.service';
import { Historico } from './historico/historico';
import { Login } from './login/login';
import { ArquivoBaixado, Relatorio, ResumoDeAnalise, TotaisDeTudo } from './modelos';
import { ResumoMensal } from './resumo-mensal/resumo-mensal';
import { TotalGeral } from './total-geral/total-geral';

@Component({
  selector: 'app-root',
  imports: [Login, ResumoMensal, DetalheMes, TotalGeral, Historico],
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

  /** A análise aberta agora - é por ela que o CSV é baixado. */
  protected readonly analiseId = signal<string | null>(null);
  protected readonly historico = signal<ResumoDeAnalise[]>([]);
  protected readonly historicoPersistente = signal(true);
  protected readonly carregandoHistorico = signal(false);
  protected readonly somando = signal(false);

  /** O total de tudo que já foi enviado, recalculado a cada envio. */
  protected readonly totais = signal<TotaisDeTudo | null>(null);

  /**
   * Quais análises o relatório na tela está somando. Nulo quando a tela mostra
   * uma análise só - e é o que decide de onde vem o CSV ao clicar em baixar.
   */
  protected readonly idsSomados = signal<string[] | null>(null);

  constructor() {
    // O componente de login não avisa ninguém: ele grava a sessão no serviço.
    // Observar o sinal cobre os dois caminhos - quem chega já logado e quem
    // acabou de entrar.
    effect(() => {
      if (this.autenticado()) {
        this.carregarHistorico();
      }
    });
  }

  protected readonly periodo = computed(() => {
    const meses = this.relatorio()?.meses ?? [];
    return meses.length > 0 ? `${meses[0]} a ${meses.at(-1)} (${meses.length} meses)` : '';
  });

  protected sair(): void {
    this.autenticacao.sair();
    this.arquivo.set(null);
    this.relatorio.set(null);
    this.analiseId.set(null);
    this.idsSomados.set(null);
    this.historico.set([]);
    this.totais.set(null);
    this.erro.set(null);
  }

  protected carregarHistorico(): void {
    this.carregandoHistorico.set(true);

    this.servico.historico().subscribe({
      next: (resposta) => {
        this.historico.set(resposta.itens);
        this.historicoPersistente.set(resposta.persistente);
        this.carregandoHistorico.set(false);
      },
      error: () => {
        // Falhar aqui não pode atrapalhar quem só quer enviar uma planilha.
        this.carregandoHistorico.set(false);
      },
    });

    // O total de tudo anda junto com a lista: assim ele se refaz sozinho a cada
    // envio, sem ninguém precisar pedir.
    this.servico.totais().subscribe({
      next: (totais) => this.totais.set(totais),
      error: () => this.totais.set(null),
    });
  }

  protected abrirDoHistorico(id: string): void {
    this.erro.set(null);

    this.servico.analiseGuardada(id).subscribe({
      next: ({ id: aberta, relatorio }) => {
        this.relatorio.set(relatorio);
        this.analiseId.set(aberta);
        this.idsSomados.set(null);
        this.mesSelecionado.set(TOTAL_DO_PERIODO);
        // A planilha de origem não voltou junto - e não precisa: o CSV baixa
        // pelo identificador da análise.
        this.arquivo.set(null);
      },
      error: (falha: unknown) => this.erro.set(this.mensagemDeErro(falha)),
    });
  }

  /**
   * Mostra na tela um relatório com os valores de várias análises somados.
   * Lista vazia soma todas as guardadas. O download sai depois, pelo mesmo
   * botão de sempre.
   */
  protected verSomatorio(ids: string[]): void {
    if (this.somando()) {
      return;
    }

    this.somando.set(true);
    this.erro.set(null);

    this.servico.somatorio(ids, { incluirZerados: this.incluirZerados() }).subscribe({
      next: (relatorio) => {
        this.relatorio.set(relatorio);
        this.idsSomados.set(ids);
        this.analiseId.set(null);
        this.arquivo.set(null);
        this.mesSelecionado.set(TOTAL_DO_PERIODO);
        this.somando.set(false);
      },
      error: (falha: unknown) => {
        this.erro.set(this.mensagemDeErro(falha));
        this.somando.set(false);
      },
    });
  }

  protected apagarDoHistorico(id: string): void {
    this.servico.apagarDoHistorico(id).subscribe({
      next: () => {
        this.historico.update((itens) => itens.filter((item) => item.id !== id));

        if (this.analiseId() === id) {
          this.relatorio.set(null);
          this.analiseId.set(null);
        }

        // Uma análise a menos muda o total de tudo.
        this.carregarHistorico();
      },
      error: (falha: unknown) => this.erro.set(this.mensagemDeErro(falha)),
    });
  }

  protected selecionarArquivo(evento: Event): void {
    const escolhido = (evento.target as HTMLInputElement).files?.[0] ?? null;
    this.arquivo.set(escolhido);
    this.relatorio.set(null);
    this.analiseId.set(null);
    this.idsSomados.set(null);
    this.erro.set(null);
  }

  protected soltarArquivo(evento: DragEvent): void {
    evento.preventDefault();
    const escolhido = evento.dataTransfer?.files?.[0] ?? null;
    if (escolhido) {
      this.arquivo.set(escolhido);
      this.relatorio.set(null);
      this.analiseId.set(null);
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
      next: ({ id, relatorio }) => {
        this.relatorio.set(relatorio);
        this.analiseId.set(id);
        this.idsSomados.set(null);
        this.mesSelecionado.set(TOTAL_DO_PERIODO);
        this.analisando.set(false);
        this.carregarHistorico();
      },
      error: (falha: unknown) => {
        this.erro.set(this.mensagemDeErro(falha));
        this.analisando.set(false);
      },
    });
  }

  protected baixar(): void {
    if (this.baixando()) {
      return;
    }

    const somados = this.idsSomados();
    if (somados) {
      this.entregar(this.servico.somatorioCsv(somados, { incluirZerados: this.incluirZerados() }));
      return;
    }

    const id = this.analiseId();
    if (id) {
      this.baixarDoHistorico(id, this.relatorio()?.arquivo ?? 'fluxo.csv');
    }
  }

  /**
   * Baixa o CSV guardado com a análise. Quem chega pelo histórico não tem mais
   * a planilha de origem em mãos, então o arquivo vem do servidor.
   */
  protected baixarDoHistorico(id: string, nomeOrigem: string): void {
    this.entregar(this.servico.baixarDoHistorico(id, nomeOrigem));
  }

  /** Entrega o arquivo ao navegador. */
  private entregar(pedido: Observable<ArquivoBaixado>): void {
    this.baixando.set(true);
    this.erro.set(null);

    pedido.subscribe({
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
