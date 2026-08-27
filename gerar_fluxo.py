#!/usr/bin/env python3
"""Atalho para gerar o CSV consolidado.

    python gerar_fluxo.py
    python gerar_fluxo.py dados/fluxo_de_caixa_mensal.csv -s saida/relatorio.csv
"""

from fluxo_caixa.cli import main

if __name__ == "__main__":
    raise SystemExit(main())
