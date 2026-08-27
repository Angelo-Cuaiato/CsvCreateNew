"""Leitura da planilha de fluxo de caixa exportada em CSV (formato largo).

O arquivo de origem tem duas linhas de cabecalho:

    FLUXO DE CAIXA;Previsto (R$);Realizado (R$);Previsto (R$);...
    CATEGORIAS;JAN/2026;JAN/2026;FEV/2026;FEV/2026;...;Total;Total

e uma linha por categoria, sem indentacao, com 2 colunas por mes
(previsto e realizado) e um par final com o total do ano.
"""

from __future__ import annotations

import csv
from dataclasses import dataclass, field
from decimal import Decimal
from pathlib import Path

from .numeros import ler_valor

ENCODINGS = ("utf-8-sig", "cp1252", "latin-1")
SEPARADORES = (";", ",", "\t")


@dataclass
class Linha:
    """Uma categoria da planilha, com os valores de cada mes."""

    rotulo: str
    previsto: list[Decimal | None]
    realizado: list[Decimal | None]
    total_previsto_origem: Decimal | None = None
    total_realizado_origem: Decimal | None = None

    def valores(self) -> list[Decimal]:
        """Vetor unico (previsto + realizado de cada mes) usado nas comparacoes."""
        return [v or Decimal("0.00") for v in (*self.previsto, *self.realizado)]


@dataclass
class Planilha:
    """Planilha inteira ja normalizada."""

    titulo: str
    meses: list[str]
    linhas: list[Linha]
    origem: Path | None = None
    encoding: str = "utf-8"
    separador: str = ";"
    avisos: list[str] = field(default_factory=list)

    def linha(self, rotulo: str) -> Linha | None:
        """Busca uma linha pelo rotulo, ignorando acentos e caixa."""
        alvo = normalizar(rotulo)
        for linha in self.linhas:
            if normalizar(linha.rotulo) == alvo:
                return linha
        return None


ACENTOS = str.maketrans(
    "áàâãäéèêëíìîïóòôõöúùûüçÁÀÂÃÄÉÈÊËÍÌÎÏÓÒÔÕÖÚÙÛÜÇ",
    "aaaaaeeeeiiiiooooouuuucAAAAAEEEEIIIIOOOOOUUUUC",
)


def normalizar(texto: str) -> str:
    """Minusculas, sem acento e sem espacos duplicados - para comparar rotulos."""
    return " ".join(texto.translate(ACENTOS).lower().split())


def _decodificar(caminho: Path) -> tuple[str, str]:
    """Le o arquivo tentando os encodings mais comuns em exportacoes brasileiras."""
    bruto = caminho.read_bytes()
    for encoding in ENCODINGS:
        try:
            return bruto.decode(encoding), encoding
        except UnicodeDecodeError:
            continue
    return bruto.decode("latin-1", errors="replace"), "latin-1"


def _detectar_separador(texto: str) -> str:
    primeira = texto.splitlines()[0] if texto.splitlines() else ""
    return max(SEPARADORES, key=primeira.count)


def ler_planilha(caminho: str | Path, separador: str | None = None) -> Planilha:
    """Le o CSV de origem e devolve a planilha normalizada."""
    caminho = Path(caminho)
    texto, encoding = _decodificar(caminho)
    separador = separador or _detectar_separador(texto)

    tabela = [
        linha
        for linha in csv.reader(texto.splitlines(), delimiter=separador)
        if any(celula.strip() for celula in linha)
    ]
    if len(tabela) < 3:
        raise ValueError(
            f"{caminho}: esperava pelo menos 2 linhas de cabecalho e 1 de dados."
        )

    cabecalho_tipos, cabecalho_meses, *corpo = tabela
    titulo = (cabecalho_tipos[0] or "FLUXO DE CAIXA").strip()

    meses, colunas_mes, colunas_total = _mapear_colunas(cabecalho_meses, cabecalho_tipos)
    if not meses:
        raise ValueError(
            f"{caminho}: nenhuma coluna de mes encontrada na segunda linha do cabecalho."
        )

    avisos: list[str] = []
    linhas: list[Linha] = []
    for bruta in corpo:
        rotulo = bruta[0].strip()
        if not rotulo:
            continue

        def celula(indice: int | None) -> Decimal | None:
            if indice is None or indice >= len(bruta):
                return None
            return ler_valor(bruta[indice])

        previsto = [celula(p) for p, _ in colunas_mes]
        realizado = [celula(r) for _, r in colunas_mes]
        total_p = celula(colunas_total[0]) if colunas_total else None
        total_r = celula(colunas_total[1]) if colunas_total else None

        if len(bruta) != len(cabecalho_meses):
            avisos.append(
                f'A linha "{rotulo}" tem {len(bruta)} colunas e o cabecalho tem '
                f"{len(cabecalho_meses)}; as colunas faltantes foram tratadas como vazias."
            )

        linhas.append(
            Linha(
                rotulo=rotulo,
                previsto=previsto,
                realizado=realizado,
                total_previsto_origem=total_p,
                total_realizado_origem=total_r,
            )
        )

    return Planilha(
        titulo=titulo,
        meses=meses,
        linhas=linhas,
        origem=caminho,
        encoding=encoding,
        separador=separador,
        avisos=avisos,
    )


def _mapear_colunas(
    cabecalho_meses: list[str], cabecalho_tipos: list[str]
) -> tuple[list[str], list[tuple[int, int]], tuple[int, int] | None]:
    """Descobre quais colunas sao previsto/realizado de cada mes e quais sao o total."""
    meses: list[str] = []
    colunas_mes: list[tuple[int, int]] = []
    colunas_total: tuple[int, int] | None = None

    indice = 1
    while indice < len(cabecalho_meses):
        rotulo = cabecalho_meses[indice].strip()
        if not rotulo:
            indice += 1
            continue

        seguinte = (
            cabecalho_meses[indice + 1].strip()
            if indice + 1 < len(cabecalho_meses)
            else ""
        )
        par = (indice, indice + 1) if normalizar(seguinte) == normalizar(rotulo) else (indice, None)

        # O cabecalho de cima diz qual coluna e previsto e qual e realizado.
        if par[1] is not None and _e_realizado(cabecalho_tipos, par[0]):
            par = (par[1], par[0])

        if normalizar(rotulo).startswith("total"):
            colunas_total = (par[0], par[1] if par[1] is not None else par[0])
        else:
            meses.append(rotulo)
            colunas_mes.append((par[0], par[1] if par[1] is not None else par[0]))

        indice += 2 if par[1] is not None else 1

    return meses, colunas_mes, colunas_total


def _e_realizado(cabecalho_tipos: list[str], indice: int) -> bool:
    if indice >= len(cabecalho_tipos):
        return False
    return "realizado" in normalizar(cabecalho_tipos[indice])
