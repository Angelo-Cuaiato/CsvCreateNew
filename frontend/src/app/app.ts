import { Component, computed, effect, inject, signal } from '@angular/core';
import { Observable } from 'rxjs';

import { AutenticacaoService } from './autenticacao.service';
import { DetalheMes, TOTAL_DO_PERIODO } from './detalhe-mes/detalhe-mes';
import { FluxoCaixaService } from './fluxo-caixa.service';
import { Historico } from './historico/historico';
import { Login } from './login/login';
import { UsuariosService } from './usuarios.service';
import { CadastroDeUsuario, TrocaDeSenha, Usuarios } from './usuarios/usuarios';
import {
  ArquivoBaixado,
  Relatorio,
  ResumoDeAnalise,
  TotaisDeTudo,
  UsuarioDoSistema,
} from './modelos';
import { ResumoMensal } from './resumo-mensal/resumo-mensal';
import { TotalGeral } from './total-geral/total-geral';

@Component({
  selector: 'app-root',
  imports: [Login, ResumoMensal, DetalheMes, TotalGeral, Historico, Usuarios],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {
  private readonly servico = inject(FluxoCaixaService);
  private readonly autenticacao = inject(AutenticacaoService);
  private readonly gestaoDeUsuarios = inject(UsuariosService);

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
  /** Qual tela está aberta. Sem roteador: são duas, e o topo alterna. */
  protected readonly tela = signal<'fluxo' | 'usuarios'>('fluxo');

  protected readonly ehAdministrador = computed(
    () => this.usuario()?.perfil === 'administrador',
  );

  protected readonly usuarios = signal<UsuarioDoSistema[]>([]);
  protected readonly usuariosPersistentes = signal(true);
  protected readonly erroDeUsuarios = signal<string | null>(null);
  protected readonly salvandoUsuario = signal(false);

  /** O total de tudo que já foi enviado, recalculado a cada envio. */
  protected readonly totais = signal<TotaisDeTudo | null>(null);

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
    this.historico.set([]);
    this.totais.set(null);
    this.usuarios.set([]);
    this.tela.set('fluxo');
    this.erro.set(null);
  }

  protected abrirUsuarios(): void {
    this.tela.set('usuarios');
    this.carregarUsuarios();
  }

  protected carregarUsuarios(): void {
    this.gestaoDeUsuarios.listar().subscribe({
      next: (lista) => {
        this.usuarios.set(lista.itens);
        this.usuariosPersistentes.set(lista.persistente);
        this.erroDeUsuarios.set(null);
      },
      error: (falha: unknown) => this.erroDeUsuarios.set(this.mensagemDeErro(falha)),
    });
  }

  protected criarUsuario(dados: CadastroDeUsuario): void {
    this.salvandoUsuario.set(true);
    this.erroDeUsuarios.set(null);

    this.gestaoDeUsuarios.criar(dados).subscribe({
      next: () => {
        this.salvandoUsuario.set(false);
        this.carregarUsuarios();
      },
      error: (falha: unknown) => {
        this.erroDeUsuarios.set(this.mensagemDeErro(falha));
        this.salvandoUsuario.set(false);
      },
    });
  }

  protected trocarSenhaDeUsuario({ email, senha }: TrocaDeSenha): void {
    this.erroDeUsuarios.set(null);

    this.gestaoDeUsuarios.trocarSenha(email, senha).subscribe({
      error: (falha: unknown) => this.erroDeUsuarios.set(this.mensagemDeErro(falha)),
    });
  }

  protected excluirUsuario(email: string): void {
    this.erroDeUsuarios.set(null);

    this.gestaoDeUsuarios.excluir(email).subscribe({
      next: () => this.usuarios.update((atuais) => atuais.filter((u) => u.email !== email)),
      error: (falha: unknown) => this.erroDeUsuarios.set(this.mensagemDeErro(falha)),
    });
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
            this.mesSelecionado.set(TOTAL_DO_PERIODO);
        // A planilha de origem não voltou junto - e não precisa: o CSV baixa
        // pelo identificador da análise.
        this.arquivo.set(null);
      },
      error: (falha: unknown) => this.erro.set(this.mensagemDeErro(falha)),
    });
  }

  /** Baixa o CSV com todas as análises somadas, o mesmo total do cartão. */
  protected baixarSomatorio(): void {
    this.entregar(this.servico.somatorioCsv([], { incluirZerados: this.incluirZerados() }));
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
    const id = this.analiseId();
    if (id && !this.baixando()) {
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
