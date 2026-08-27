"""Reconstrucao da hierarquia (grupo -> subgrupo -> item) da planilha.

O CSV de origem vem sem indentacao: todas as categorias aparecem no mesmo
nivel. A relacao pai/filho, porem, continua gravada nos numeros - o valor de um
grupo e a soma dos valores dos filhos que vem logo abaixo dele, mes a mes. Este
modulo refaz essa arvore comparando os vetores mensais.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from decimal import Decimal

from .leitura import Linha, normalizar

# Diferenca aceita por celula (o arquivo de origem ja vem arredondado).
TOLERANCIA = Decimal("0.05")

# Linhas de resumo do relatorio: nunca sao filhas de ninguem.
ANCORAS = (
    "saldo do mes anterior",
    "total de recebimentos",
    "total de pagamentos",
    "geracao de caixa do periodo",
    "total de transferencias",
    "saldo final de caixa",
)

# Linhas de saldo: sao fotografias de um momento, nao se somam ao longo do ano.
SALDOS = ("saldo do mes anterior", "saldo final de caixa")


@dataclass
class No:
    """Uma categoria e os subitens que a compoem."""

    linha: Linha
    nivel: int = 0
    filhos: list["No"] = field(default_factory=list)

    @property
    def rotulo(self) -> str:
        return self.linha.rotulo

    def percorrer(self):
        """Devolve o no e todos os descendentes, em ordem de leitura."""
        yield self
        for filho in self.filhos:
            yield from filho.percorrer()


def e_ancora(linha: Linha) -> bool:
    return normalizar(linha.rotulo) in ANCORAS


def e_saldo(linha: Linha) -> bool:
    return normalizar(linha.rotulo) in SALDOS


def montar_arvore(linhas: list[Linha]) -> list[No]:
    """Monta a arvore de categorias a partir das linhas na ordem original."""
    return _Construtor(linhas).montar(0, len(linhas), nivel=0)


class _Construtor:
    """Percorre as linhas uma unica vez, guardando os spans ja calculados.

    `fim_dos_filhos` e chamada repetidamente para os mesmos indices durante a
    montagem; sem o cache a busca vira exponencial em planilhas com varios
    niveis de subtotal.
    """

    def __init__(self, linhas: list[Linha]) -> None:
        self.linhas = linhas
        self.valores = [linha.valores() for linha in linhas]
        self.totais = [sum(valores) for valores in self.valores]
        self.ancoras = [e_ancora(linha) for linha in linhas]
        self.saldos = [e_saldo(linha) for linha in linhas]
        self._cache: dict[int, int | None] = {}

    def montar(self, inicio: int, limite: int, nivel: int) -> list[No]:
        nos: list[No] = []
        indice = inicio
        while indice < limite:
            fim = self.fim_dos_filhos(indice)
            if fim is None or fim > limite:
                nos.append(No(linha=self.linhas[indice], nivel=nivel))
                indice += 1
                continue

            filhos = self.montar(indice + 1, fim, nivel + 1)
            nos.append(No(linha=self.linhas[indice], nivel=nivel, filhos=filhos))
            indice = fim
        return nos

    def fim_dos_filhos(self, indice: int) -> int | None:
        """Ate onde vao os filhos da linha `indice`, ou None se ela for folha.

        Soma as linhas seguintes - ja agrupadas em subarvores - ate bater com o
        valor do pai em todos os meses. Se estourar o valor do pai, se o sinal
        mudar ou se aparecer uma linha de resumo, conclui que nao ha filhos.
        """
        if indice in self._cache:
            return self._cache[indice]

        # Marca a chamada em andamento: protege contra recursao circular.
        self._cache[indice] = None
        self._cache[indice] = self._calcular_fim(indice)
        return self._cache[indice]

    def _calcular_fim(self, indice: int) -> int | None:
        alvo = self.valores[indice]
        if self.saldos[indice] or all(valor == 0 for valor in alvo):
            return None

        total_pai = self.totais[indice]
        sinal_pai = 1 if total_pai >= 0 else -1
        limite_absoluto = abs(total_pai) + TOLERANCIA

        acumulado = [Decimal("0.00")] * len(alvo)
        posicao = indice + 1
        while posicao < len(self.linhas):
            if self.ancoras[posicao]:
                return None

            total_candidata = self.totais[posicao]
            if total_candidata != 0 and (1 if total_candidata > 0 else -1) != sinal_pai:
                return None

            acumulado = [a + b for a, b in zip(acumulado, self.valores[posicao])]
            if abs(sum(acumulado)) > limite_absoluto:
                return None

            # A candidata pode ser um subgrupo; nesse caso ela ja engloba os
            # proprios filhos e o irmao seguinte vem depois deles.
            fim_subarvore = self.fim_dos_filhos(posicao)
            posicao = fim_subarvore if fim_subarvore is not None else posicao + 1

            if _iguais(acumulado, alvo):
                return posicao

        return None


def _iguais(a: list[Decimal], b: list[Decimal]) -> bool:
    return all(abs(x - y) <= TOLERANCIA for x, y in zip(a, b))
