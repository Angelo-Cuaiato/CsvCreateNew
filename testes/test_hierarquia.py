import unittest
from decimal import Decimal
from pathlib import Path

from fluxo_caixa.hierarquia import montar_arvore
from fluxo_caixa.leitura import Linha, ler_planilha

ORIGEM = Path(__file__).resolve().parent.parent / "dados" / "fluxo_de_caixa_mensal.csv"


def linha(rotulo: str, *valores: str) -> Linha:
    """Cria uma linha com um mes: (previsto, realizado) por par de valores."""
    numeros = [Decimal(valor) for valor in valores]
    return Linha(
        rotulo=rotulo,
        previsto=numeros[0::2],
        realizado=numeros[1::2],
    )


def desenhar(nos, prefixo="") -> list[str]:
    saida = []
    for no in nos:
        saida.append(f"{prefixo}{no.rotulo}")
        saida.extend(desenhar(no.filhos, prefixo + "  "))
    return saida


class TestMontarArvore(unittest.TestCase):
    def test_pai_recebe_os_filhos_que_somam_o_valor_dele(self):
        arvore = montar_arvore(
            [
                linha("Grupo", "100", "90"),
                linha("Item A", "60", "50"),
                linha("Item B", "40", "40"),
            ]
        )
        self.assertEqual(desenhar(arvore), ["Grupo", "  Item A", "  Item B"])

    def test_reconhece_tres_niveis(self):
        arvore = montar_arvore(
            [
                linha("Grupo", "100", "100"),
                linha("Subgrupo", "70", "70"),
                linha("Folha 1", "30", "30"),
                linha("Folha 2", "40", "40"),
                linha("Outro item", "30", "30"),
            ]
        )
        self.assertEqual(
            desenhar(arvore),
            ["Grupo", "  Subgrupo", "    Folha 1", "    Folha 2", "  Outro item"],
        )

    def test_linhas_que_nao_somam_ficam_no_mesmo_nivel(self):
        arvore = montar_arvore(
            [
                linha("Item A", "100", "100"),
                linha("Item B", "7", "7"),
                linha("Item C", "13", "13"),
            ]
        )
        self.assertEqual(desenhar(arvore), ["Item A", "Item B", "Item C"])

    def test_soma_precisa_bater_em_todos_os_meses(self):
        # O total anual bate (100 = 60 + 40), mas a distribuicao mensal nao.
        pai = Linha(
            rotulo="Grupo",
            previsto=[Decimal("50"), Decimal("50")],
            realizado=[Decimal("50"), Decimal("50")],
        )
        filho_a = Linha(
            rotulo="Item A",
            previsto=[Decimal("60"), Decimal("0")],
            realizado=[Decimal("60"), Decimal("0")],
        )
        filho_b = Linha(
            rotulo="Item B",
            previsto=[Decimal("40"), Decimal("0")],
            realizado=[Decimal("40"), Decimal("0")],
        )
        self.assertEqual(
            desenhar(montar_arvore([pai, filho_a, filho_b])),
            ["Grupo", "Item A", "Item B"],
        )

    def test_linhas_de_resumo_nunca_viram_filhas(self):
        arvore = montar_arvore(
            [
                linha("Saldo do Mês Anterior", "100", "100"),
                linha("Total de Recebimentos", "60", "60"),
                linha("Vendas", "60", "60"),
                linha("Total de Pagamentos", "-40", "-40"),
            ]
        )
        self.assertEqual(
            desenhar(arvore),
            [
                "Saldo do Mês Anterior",
                "Total de Recebimentos",
                "  Vendas",
                "Total de Pagamentos",
            ],
        )

    def test_grupo_com_um_unico_filho(self):
        arvore = montar_arvore(
            [
                linha("Despesas Financeiras", "-10", "-10"),
                linha("Despesas Bancárias", "-10", "-10"),
            ]
        )
        self.assertEqual(
            desenhar(arvore), ["Despesas Financeiras", "  Despesas Bancárias"]
        )


@unittest.skipUnless(ORIGEM.exists(), "planilha de exemplo ausente")
class TestHierarquiaReal(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.planilha = ler_planilha(ORIGEM)
        cls.arvore = montar_arvore(cls.planilha.linhas)
        cls.por_rotulo = {
            no.rotulo: no for raiz in cls.arvore for no in raiz.percorrer()
        }

    def test_nenhuma_categoria_se_perde(self):
        self.assertEqual(len(self.por_rotulo), len(self.planilha.linhas))

    def test_raizes_sao_as_linhas_de_resumo(self):
        self.assertEqual(
            [no.rotulo for no in self.arvore],
            [
                "Saldo do Mês Anterior",
                "Total de Recebimentos",
                "Total de Pagamentos",
                "Geração de Caixa do Período",
                "Total de Transferências",
                "Saldo Final de Caixa",
            ],
        )

    def test_grupos_conhecidos(self):
        def filhos(rotulo: str) -> list[str]:
            return [no.rotulo for no in self.por_rotulo[rotulo].filhos]

        self.assertEqual(
            filhos("Total de Recebimentos"),
            ["Encerramento de Contrato", "Receitas de Vendas"],
        )
        self.assertEqual(
            filhos("Salários"), ["Salários - Folha", "Salários - Médicos"]
        )
        self.assertEqual(len(filhos("Total de Pagamentos")), 5)
        self.assertEqual(self.por_rotulo["Faturamento"].filhos, [])

    def test_cada_grupo_e_a_soma_dos_filhos_mes_a_mes(self):
        for no in self.por_rotulo.values():
            if not no.filhos:
                continue
            for indice, mes in enumerate(self.planilha.meses):
                for campo in ("previsto", "realizado"):
                    pai = getattr(no.linha, campo)[indice] or Decimal("0")
                    soma = sum(
                        (getattr(f.linha, campo)[indice] or Decimal("0"))
                        for f in no.filhos
                    )
                    self.assertLessEqual(
                        abs(pai - soma),
                        Decimal("0.05"),
                        f"{no.rotulo} / {mes} / {campo}: {pai} != {soma}",
                    )


if __name__ == "__main__":
    unittest.main()
