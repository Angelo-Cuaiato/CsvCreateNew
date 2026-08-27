"""Conversao de numeros no formato brasileiro (1.234.567,89)."""

from __future__ import annotations

from decimal import Decimal, InvalidOperation

ZERO = Decimal("0.00")
CENTAVO = Decimal("0.01")


def ler_valor(texto: str | None) -> Decimal | None:
    """Converte "1.234,56" em Decimal. Devolve None quando a celula esta vazia.

    Aceita tambem valores entre parenteses (contabil: "(1.234,56)" = negativo),
    sinal no fim ("1.234,56-") e simbolo de moeda.
    """
    if texto is None:
        return None

    limpo = texto.strip()
    if not limpo:
        return None

    limpo = limpo.replace("R$", "").replace("\xa0", " ").strip()

    negativo = False
    if limpo.startswith("(") and limpo.endswith(")"):
        negativo = True
        limpo = limpo[1:-1].strip()
    if limpo.endswith("-"):
        negativo = True
        limpo = limpo[:-1].strip()

    limpo = limpo.replace(".", "").replace(" ", "").replace(",", ".")
    if not limpo or limpo in {"-", "+"}:
        return None

    try:
        valor = Decimal(limpo)
    except InvalidOperation:
        return None

    if negativo:
        valor = -valor
    return valor.quantize(CENTAVO)


def formatar_valor(valor: Decimal | None, vazio: str = "") -> str:
    """Formata Decimal no padrao brasileiro: -1.234.567,89."""
    if valor is None:
        return vazio

    valor = valor.quantize(CENTAVO)
    sinal = "-" if valor < 0 else ""
    inteiro, _, decimais = str(abs(valor)).partition(".")
    decimais = (decimais + "00")[:2]

    grupos = []
    while len(inteiro) > 3:
        grupos.insert(0, inteiro[-3:])
        inteiro = inteiro[:-3]
    grupos.insert(0, inteiro)

    return f"{sinal}{'.'.join(grupos)},{decimais}"


def formatar_percentual(valor: Decimal | None, vazio: str = "-") -> str:
    """Formata um percentual ja calculado: 98,7%."""
    if valor is None:
        return vazio
    valor = valor.quantize(Decimal("0.1"))
    return f"{valor}".replace(".", ",") + "%"


def percentual(realizado: Decimal | None, previsto: Decimal | None) -> Decimal | None:
    """Quanto do previsto foi realizado. None quando o previsto e zero/vazio."""
    if realizado is None or previsto is None or previsto == 0:
        return None
    return (realizado / previsto * 100).quantize(Decimal("0.1"))
