import tempfile
import unittest
from decimal import Decimal
from pathlib import Path

from fluxo_caixa.leitura import ler_planilha, normalizar

ORIGEM = Path(__file__).resolve().parent.parent / "dados" / "fluxo_de_caixa_mensal.csv"

CSV_EXEMPLO = (
    "FLUXO DE CAIXA;Previsto (R$);Realizado (R$);Previsto (R$);Realizado (R$);"
    "Previsto (R$);Realizado (R$)\r\n"
    "CATEGORIAS;JAN/2026;JAN/2026;FEV/2026;FEV/2026;Total;Total\r\n"
    "Total de Recebimentos;100,00;90,00;200,00;250,00;300,00;340,00\r\n"
    "Vendas;100,00;90,00;200,00;250,00;300,00;340,00\r\n"
)


def escrever(tmp: Path, conteudo: str, encoding: str = "utf-8") -> Path:
    caminho = tmp / "entrada.csv"
    caminho.write_text(conteudo, encoding=encoding)
    return caminho


class TestNormalizar(unittest.TestCase):
    def test_remove_acento_e_caixa(self):
        self.assertEqual(normalizar("Saldo do Mês Anterior"), "saldo do mes anterior")
        self.assertEqual(normalizar("  Geração   de  Caixa "), "geracao de caixa")


class TestLerPlanilha(unittest.TestCase):
    def test_le_cabecalho_e_valores(self):
        with tempfile.TemporaryDirectory() as tmp:
            planilha = ler_planilha(escrever(Path(tmp), CSV_EXEMPLO))

        self.assertEqual(planilha.meses, ["JAN/2026", "FEV/2026"])
        self.assertEqual(len(planilha.linhas), 2)

        recebimentos = planilha.linha("Total de Recebimentos")
        self.assertIsNotNone(recebimentos)
        self.assertEqual(recebimentos.previsto, [Decimal("100.00"), Decimal("200.00")])
        self.assertEqual(recebimentos.realizado, [Decimal("90.00"), Decimal("250.00")])
        self.assertEqual(recebimentos.total_previsto_origem, Decimal("300.00"))
        self.assertEqual(recebimentos.total_realizado_origem, Decimal("340.00"))

    def test_busca_de_linha_ignora_acento(self):
        with tempfile.TemporaryDirectory() as tmp:
            planilha = ler_planilha(escrever(Path(tmp), CSV_EXEMPLO))
        self.assertIsNotNone(planilha.linha("total de recebimentos"))
        self.assertIsNone(planilha.linha("Nao existe"))

    def test_avisa_quando_a_linha_tem_menos_colunas(self):
        curto = CSV_EXEMPLO + "Outra;10,00\r\n"
        with tempfile.TemporaryDirectory() as tmp:
            planilha = ler_planilha(escrever(Path(tmp), curto))

        self.assertTrue(any("Outra" in aviso for aviso in planilha.avisos))
        outra = planilha.linha("Outra")
        self.assertEqual(outra.previsto, [Decimal("10.00"), None])

    def test_arquivo_sem_dados_falha_com_mensagem(self):
        with tempfile.TemporaryDirectory() as tmp:
            caminho = escrever(Path(tmp), "FLUXO DE CAIXA;Previsto (R$)\r\n")
            with self.assertRaises(ValueError):
                ler_planilha(caminho)


@unittest.skipUnless(ORIGEM.exists(), "planilha de exemplo ausente")
class TestPlanilhaReal(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.planilha = ler_planilha(ORIGEM)

    def test_detecta_encoding_e_separador(self):
        self.assertEqual(self.planilha.separador, ";")
        self.assertIn(self.planilha.encoding, ("cp1252", "latin-1", "utf-8-sig"))

    def test_le_os_doze_meses_e_todas_as_categorias(self):
        self.assertEqual(len(self.planilha.meses), 12)
        self.assertEqual(self.planilha.meses[0], "JAN/2026")
        self.assertEqual(self.planilha.meses[-1], "DEZ/2026")
        self.assertEqual(len(self.planilha.linhas), 59)
        self.assertEqual(self.planilha.avisos, [])

    def test_acentos_preservados(self):
        self.assertIsNotNone(self.planilha.linha("Saldo do Mês Anterior"))
        self.assertIsNotNone(self.planilha.linha("Salários - Médicos"))


if __name__ == "__main__":
    unittest.main()
