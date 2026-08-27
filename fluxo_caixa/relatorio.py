"""Montagem do CSV unico e consolidado: um bloco por mes e o total no final."""

from __future__ import annotations

import csv
from dataclasses import dataclass
from datetime import datetime
from decimal import Decimal
from pathlib import Path

from .hierarquia import No, e_saldo, montar_arvore
from .leitura import Linha, Planilha, normalizar
from .numeros import ZERO, formatar_percentual, formatar_valor, percentual

CABECALHO_DETALHE = [
    "Categoria",
    "Previsto (R$)",
    "Realizado (R$)",
    "Diferença (R$)",
    "% Realizado",
]

CABECALHO_RESUMO = [
    "Mês",
    "Recebimentos Previsto (R$)",
    "Recebimentos Realizado (R$)",
    "Pagamentos Previsto (R$)",
    "Pagamentos Realizado (R$)",
    "Geração de Caixa Prevista (R$)",
    "Geração de Caixa Realizada (R$)",
    "Saldo Final Realizado (R$)",
]

# Espaço não separável (U+00A0): ao contrário do espaço comum, ele não é
# removido pelo Excel/LibreOffice na importação, então o recuo da hierarquia
# sobrevive ao abrir o arquivo.
RECUO = "\u00a0" * 4

RESUMO = {
    "saldo_inicial": "saldo do mes anterior",
    "recebimentos": "total de recebimentos",
    "pagamentos": "total de pagamentos",
    "geracao": "geracao de caixa do periodo",
    "transferencias": "total de transferencias",
    "saldo_final": "saldo final de caixa",
}


@dataclass
class Par:
    """Um valor previsto e o realizado correspondente."""

    previsto: Decimal | None = None
    realizado: Decimal | None = None

    @property
    def diferenca(self) -> Decimal | None:
        if self.previsto is None and self.realizado is None:
            return None
        return (self.realizado or ZERO) - (self.previsto or ZERO)

    @property
    def zerado(self) -> bool:
        return not (self.previsto or ZERO) and not (self.realizado or ZERO)


def valores_do_mes(linha: Linha, indice: int) -> Par:
    return Par(linha.previsto[indice], linha.realizado[indice])


def valores_do_periodo(linha: Linha, total_meses: int) -> Par:
    """Total do ano. Saldos nao se somam: valem o inicio e o fim do periodo."""
    if e_saldo(linha):
        if normalizar(linha.rotulo) == RESUMO["saldo_inicial"]:
            return valores_do_mes(linha, 0)
        return valores_do_mes(linha, total_meses - 1)

    previsto = sum((v for v in linha.previsto if v is not None), ZERO)
    realizado = sum((v for v in linha.realizado if v is not None), ZERO)
    return Par(previsto, realizado)


def _subtotal_do_mes(no: No, indice: int) -> Par:
    """Soma o no e todos os descendentes - usado so para saber se o mes esta zerado."""
    previsto = ZERO
    realizado = ZERO
    for atual in no.percorrer():
        par = valores_do_mes(atual.linha, indice)
        previsto += par.previsto or ZERO
        realizado += par.realizado or ZERO
    return Par(previsto, realizado)


def _linha_detalhe(rotulo: str, nivel: int, par: Par, recuar: bool = True) -> list[str]:
    prefixo = RECUO * nivel if recuar else ""
    return [
        f"{prefixo}{rotulo}",
        formatar_valor(par.previsto),
        formatar_valor(par.realizado),
        formatar_valor(par.diferenca),
        formatar_percentual(percentual(par.realizado, par.previsto)),
    ]


class Relatorio:
    """Transforma a planilha larga no relatorio consolidado."""

    def __init__(
        self,
        planilha: Planilha,
        incluir_zerados: bool = False,
        recuar: bool = True,
    ) -> None:
        self.planilha = planilha
        self.incluir_zerados = incluir_zerados
        self.recuar = recuar
        self.arvore = montar_arvore(planilha.linhas)
        self.meses = planilha.meses

    # ------------------------------------------------------------------ blocos

    def cabecalho(self) -> list[list[str]]:
        origem = self.planilha.origem.name if self.planilha.origem else "(não informado)"
        periodo = (
            f"{self.meses[0]} a {self.meses[-1]} ({len(self.meses)} meses)"
            if self.meses
            else "(sem meses)"
        )
        return [
            [f"{self.planilha.titulo} - RELATÓRIO CONSOLIDADO"],
            ["Arquivo de origem", origem],
            ["Período", periodo],
            ["Gerado em", datetime.now().strftime("%d/%m/%Y %H:%M")],
            ["Valores", "R$ - formato brasileiro (1.234,56)"],
            [
                "Legenda",
                "Diferença = Realizado - Previsto | % Realizado = Realizado / Previsto",
            ],
            [],
        ]

    def resumo_por_mes(self) -> list[list[str]]:
        recebimentos = self._linha_resumo("recebimentos")
        pagamentos = self._linha_resumo("pagamentos")
        geracao = self._linha_resumo("geracao")
        saldo_final = self._linha_resumo("saldo_final")
        if not any((recebimentos, pagamentos, geracao)):
            return []

        linhas: list[list[str]] = [["RESUMO POR MÊS"], list(CABECALHO_RESUMO)]
        for indice, mes in enumerate(self.meses):
            linhas.append(
                [
                    mes,
                    *self._celulas(recebimentos, indice),
                    *self._celulas(pagamentos, indice),
                    *self._celulas(geracao, indice),
                    formatar_valor(valores_do_mes(saldo_final, indice).realizado)
                    if saldo_final
                    else "",
                ]
            )

        total = len(self.meses)
        linhas.append(
            [
                "TOTAL DO PERÍODO",
                *self._celulas_periodo(recebimentos, total),
                *self._celulas_periodo(pagamentos, total),
                *self._celulas_periodo(geracao, total),
                formatar_valor(valores_do_periodo(saldo_final, total).realizado)
                if saldo_final
                else "",
            ]
        )
        linhas.append([])
        return linhas

    def detalhe_dos_meses(self) -> list[list[str]]:
        linhas: list[list[str]] = []
        for indice, mes in enumerate(self.meses):
            linhas.append([f"MÊS {indice + 1:02d} - {mes}"])
            linhas.append(list(CABECALHO_DETALHE))
            linhas.extend(self._detalhe(indice))
            linhas.append([])
        return linhas

    def total_do_periodo(self) -> list[list[str]]:
        titulo = (
            f"TOTAL DO PERÍODO - {self.meses[0]} A {self.meses[-1]}"
            if self.meses
            else "TOTAL DO PERÍODO"
        )
        linhas: list[list[str]] = [[titulo], list(CABECALHO_DETALHE)]
        linhas.extend(self._detalhe(None))
        linhas.append([])
        linhas.extend(self._fechamento())
        return linhas

    def conferencia(self) -> list[list[str]]:
        """Compara o total calculado com o total que veio no arquivo de origem."""
        divergencias: list[list[str]] = []
        total_meses = len(self.meses)
        for linha in self.planilha.linhas:
            if e_saldo(linha):
                continue
            calculado = valores_do_periodo(linha, total_meses)
            for rotulo, esperado, obtido in (
                ("Previsto", linha.total_previsto_origem, calculado.previsto),
                ("Realizado", linha.total_realizado_origem, calculado.realizado),
            ):
                if esperado is None or obtido is None:
                    continue
                if abs(esperado - obtido) > Decimal("0.05"):
                    divergencias.append(
                        [
                            linha.rotulo,
                            rotulo,
                            formatar_valor(esperado),
                            formatar_valor(obtido),
                            formatar_valor(obtido - esperado),
                        ]
                    )

        linhas: list[list[str]] = [["CONFERÊNCIA"]]
        if divergencias:
            linhas.append(
                [
                    "Categoria",
                    "Coluna",
                    "Total no arquivo (R$)",
                    "Total somado mês a mês (R$)",
                    "Diferença (R$)",
                ]
            )
            linhas.extend(divergencias)
        else:
            linhas.append(
                [
                    "Todos os totais somados mês a mês conferem com o total do arquivo de origem."
                ]
            )

        for aviso in self.planilha.avisos:
            linhas.append(["Aviso", aviso])
        return linhas

    def montar(self) -> list[list[str]]:
        return [
            *self.cabecalho(),
            *self.resumo_por_mes(),
            *self.detalhe_dos_meses(),
            *self.total_do_periodo(),
            *self.conferencia(),
        ]

    # ----------------------------------------------------------------- apoio

    def _detalhe(self, indice: int | None) -> list[list[str]]:
        """Arvore de categorias de um mes (indice) ou do periodo inteiro (None)."""
        linhas: list[list[str]] = []
        total_meses = len(self.meses)

        def visitar(no: No) -> None:
            if indice is None:
                par = valores_do_periodo(no.linha, total_meses)
                relevante = not par.zerado
            else:
                par = valores_do_mes(no.linha, indice)
                relevante = not _subtotal_do_mes(no, indice).zerado

            if not relevante and not self.incluir_zerados and not e_saldo(no.linha):
                return

            linhas.append(_linha_detalhe(no.rotulo, no.nivel, par, self.recuar))
            for filho in no.filhos:
                visitar(filho)

        for raiz in self.arvore:
            visitar(raiz)
        return linhas

    def _fechamento(self) -> list[list[str]]:
        """O total de tudo, em cinco linhas, no fim do arquivo."""
        total_meses = len(self.meses)
        itens = [
            ("Saldo inicial do período", self._linha_resumo("saldo_inicial")),
            ("Total de recebimentos", self._linha_resumo("recebimentos")),
            ("Total de pagamentos", self._linha_resumo("pagamentos")),
            ("Total de transferências", self._linha_resumo("transferencias")),
            ("Geração de caixa do período", self._linha_resumo("geracao")),
            ("Saldo final do período", self._linha_resumo("saldo_final")),
        ]
        linhas: list[list[str]] = [["TOTAL GERAL"], list(CABECALHO_DETALHE)]
        for rotulo, linha in itens:
            if linha is None:
                continue
            linhas.append(
                _linha_detalhe(rotulo, 0, valores_do_periodo(linha, total_meses), False)
            )
        linhas.append([])
        return linhas

    def _linha_resumo(self, chave: str) -> Linha | None:
        return self.planilha.linha(RESUMO[chave])

    @staticmethod
    def _celulas(linha: Linha | None, indice: int) -> list[str]:
        if linha is None:
            return ["", ""]
        par = valores_do_mes(linha, indice)
        return [formatar_valor(par.previsto), formatar_valor(par.realizado)]

    @staticmethod
    def _celulas_periodo(linha: Linha | None, total_meses: int) -> list[str]:
        if linha is None:
            return ["", ""]
        par = valores_do_periodo(linha, total_meses)
        return [formatar_valor(par.previsto), formatar_valor(par.realizado)]


def escrever_relatorio(
    planilha: Planilha,
    destino: str | Path,
    separador: str = ";",
    incluir_zerados: bool = False,
    recuar: bool = True,
) -> Path:
    """Gera o arquivo unico consolidado e devolve o caminho criado."""
    destino = Path(destino)
    destino.parent.mkdir(parents=True, exist_ok=True)

    relatorio = Relatorio(planilha, incluir_zerados=incluir_zerados, recuar=recuar)
    # utf-8-sig para o Excel abrir os acentos corretamente.
    with destino.open("w", encoding="utf-8-sig", newline="") as saida:
        escritor = csv.writer(saida, delimiter=separador, lineterminator="\r\n")
        escritor.writerows(relatorio.montar())
    return destino
