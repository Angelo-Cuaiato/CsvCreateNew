"""Linha de comando do gerador de fluxo de caixa consolidado."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

from .leitura import ler_planilha
from .relatorio import escrever_relatorio

ENTRADA_PADRAO = Path("dados/fluxo_de_caixa_mensal.csv")
SAIDA_PADRAO = Path("saida/fluxo_de_caixa_consolidado.csv")


def montar_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="gerar_fluxo",
        description=(
            "Le a planilha de fluxo de caixa exportada em CSV e gera UM unico "
            "arquivo consolidado, separado por mes, com o total geral no final."
        ),
    )
    parser.add_argument(
        "entrada",
        nargs="?",
        default=ENTRADA_PADRAO,
        type=Path,
        help=f"CSV de origem (padrao: {ENTRADA_PADRAO})",
    )
    parser.add_argument(
        "-s",
        "--saida",
        default=SAIDA_PADRAO,
        type=Path,
        help=f"arquivo a ser gerado (padrao: {SAIDA_PADRAO})",
    )
    parser.add_argument(
        "--separador-entrada",
        default=None,
        help="separador do arquivo de origem (padrao: detectado automaticamente)",
    )
    parser.add_argument(
        "--separador-saida",
        default=";",
        help="separador do arquivo gerado (padrao: ;)",
    )
    parser.add_argument(
        "--incluir-zerados",
        action="store_true",
        help="mantem no relatorio as categorias sem movimento no mes",
    )
    parser.add_argument(
        "--sem-recuo",
        action="store_true",
        help="nao recua os subitens dentro da coluna Categoria",
    )
    return parser


def main(argv: list[str] | None = None) -> int:
    args = montar_parser().parse_args(argv)

    if not args.entrada.exists():
        print(f"Arquivo de origem nao encontrado: {args.entrada}", file=sys.stderr)
        return 1

    try:
        planilha = ler_planilha(args.entrada, separador=args.separador_entrada)
    except ValueError as erro:
        print(f"Nao foi possivel ler a planilha: {erro}", file=sys.stderr)
        return 1

    destino = escrever_relatorio(
        planilha,
        args.saida,
        separador=args.separador_saida,
        incluir_zerados=args.incluir_zerados,
        recuar=not args.sem_recuo,
    )

    meses = f"{planilha.meses[0]} a {planilha.meses[-1]}" if planilha.meses else "-"
    print(f"Origem lida: {args.entrada} ({planilha.encoding}, separador '{planilha.separador}')")
    print(f"Categorias: {len(planilha.linhas)} | Meses: {len(planilha.meses)} ({meses})")
    for aviso in planilha.avisos:
        print(f"Aviso: {aviso}")
    print(f"Arquivo gerado: {destino}")
    return 0


if __name__ == "__main__":  # pragma: no cover
    raise SystemExit(main())
