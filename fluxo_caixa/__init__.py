"""Gerador de fluxo de caixa consolidado em CSV.

Le a planilha mensal exportada em CSV (formato largo, com Previsto/Realizado por
mes) e gera UM unico arquivo, separado por mes, com o total geral no final.
"""

from .hierarquia import No, montar_arvore
from .leitura import Linha, Planilha, ler_planilha
from .relatorio import Relatorio, escrever_relatorio

__all__ = [
    "Linha",
    "No",
    "Planilha",
    "Relatorio",
    "escrever_relatorio",
    "ler_planilha",
    "montar_arvore",
]

__version__ = "1.0.0"
