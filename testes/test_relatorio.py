import csv
import io
import tempfile
import unittest
from contextlib import redirect_stdout, redirect_stderr
from decimal import Decimal
from pathlib import Path

from fluxo_caixa.cli import main
from fluxo_caixa.leitura import ler_planilha
from fluxo_caixa.numeros import ler_valor
from fluxo_caixa.relatorio import RECUO, escrever_relatorio

ORIGEM = Path(__file__).resolve().parent.parent / "dados" / "fluxo_de_caixa_mensal.csv"


def gerar(entrada: Path, destino: Path, **opcoes) -> list[list[str]]:
    escrever_relatorio(ler_planilha(entrada), destino, **opcoes)
    with destino.open(encoding="utf-8-sig", newline="") as arquivo:
        return list(csv.reader(arquivo, delimiter=";"))


@unittest.skipUnless(ORIGEM.exists(), "planilha de exemplo ausente")
class TestRelatorio(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.tmp = tempfile.TemporaryDirectory()
        cls.destino = Path(cls.tmp.name) / "consolidado.csv"
        cls.linhas = gerar(ORIGEM, cls.destino)
        cls.primeiras = [linha[0] if linha else "" for linha in cls.linhas]

    @classmethod
    def tearDownClass(cls):
        cls.tmp.cleanup()

    def test_gera_um_unico_arquivo(self):
        gerados = list(self.destino.parent.glob("*.csv"))
        self.assertEqual(gerados, [self.destino])

    def test_tem_um_bloco_por_mes(self):
        blocos = [nome for nome in self.primeiras if nome.startswith("MÊS ")]
        self.assertEqual(len(blocos), 12)
        self.assertEqual(blocos[0], "MÊS 01 - JAN/2026")
        self.assertEqual(blocos[-1], "MÊS 12 - DEZ/2026")

    def test_secoes_na_ordem_esperada(self):
        secoes = [
            nome
            for nome in self.primeiras
            if nome
            in {
                "RESUMO POR MÊS",
                "MÊS 01 - JAN/2026",
                "TOTAL DO PERÍODO - JAN/2026 A DEZ/2026",
                "TOTAL GERAL",
                "CONFERÊNCIA",
            }
        ]
        self.assertEqual(
            secoes,
            [
                "RESUMO POR MÊS",
                "MÊS 01 - JAN/2026",
                "TOTAL DO PERÍODO - JAN/2026 A DEZ/2026",
                "TOTAL GERAL",
                "CONFERÊNCIA",
            ],
        )

    def test_total_geral_fica_no_fim(self):
        posicao_total = self.primeiras.index("TOTAL GERAL")
        ultimo_mes = max(
            indice
            for indice, nome in enumerate(self.primeiras)
            if nome.startswith("MÊS ")
        )
        self.assertGreater(posicao_total, ultimo_mes)

    def test_total_geral_confere_com_a_origem(self):
        origem = ler_planilha(ORIGEM)
        esperado = {
            "Total de recebimentos": origem.linha("Total de Recebimentos"),
            "Total de pagamentos": origem.linha("Total de Pagamentos"),
            "Geração de caixa do período": origem.linha("Geração de Caixa do Período"),
        }
        inicio = self.primeiras.index("TOTAL GERAL")
        gerado = {
            linha[0]: linha
            for linha in self.linhas[inicio : inicio + 10]
            if linha and linha[0] in esperado
        }

        self.assertEqual(len(gerado), len(esperado))
        for rotulo, linha_origem in esperado.items():
            with self.subTest(rotulo=rotulo):
                self.assertEqual(
                    ler_valor(gerado[rotulo][1]), linha_origem.total_previsto_origem
                )
                self.assertEqual(
                    ler_valor(gerado[rotulo][2]), linha_origem.total_realizado_origem
                )

    def test_conferencia_sem_divergencias(self):
        indice = self.primeiras.index("CONFERÊNCIA")
        self.assertIn("conferem", self.linhas[indice + 1][0])

    def test_soma_das_categorias_de_cada_mes(self):
        """Recebimentos + pagamentos de cada mes = geracao de caixa do mes."""
        origem = ler_planilha(ORIGEM)
        recebimentos = origem.linha("Total de Recebimentos")
        pagamentos = origem.linha("Total de Pagamentos")
        geracao = origem.linha("Geração de Caixa do Período")

        for indice, mes in enumerate(origem.meses):
            with self.subTest(mes=mes):
                soma = recebimentos.realizado[indice] + pagamentos.realizado[indice]
                self.assertLessEqual(
                    abs(soma - geracao.realizado[indice]), Decimal("0.05")
                )

    def test_categorias_zeradas_ficam_de_fora_por_padrao(self):
        inicio = self.primeiras.index("MÊS 12 - DEZ/2026")
        fim = self.primeiras.index("TOTAL DO PERÍODO - JAN/2026 A DEZ/2026")
        bloco = [linha[0].lstrip(RECUO) for linha in self.linhas[inicio:fim] if linha]
        self.assertNotIn("Faturamento", bloco)
        self.assertIn("Saldo Final de Caixa", bloco)

    def test_opcao_incluir_zerados(self):
        with tempfile.TemporaryDirectory() as tmp:
            destino = Path(tmp) / "completo.csv"
            linhas = gerar(ORIGEM, destino, incluir_zerados=True)

        primeiras = [linha[0] if linha else "" for linha in linhas]
        inicio = primeiras.index("MÊS 12 - DEZ/2026")
        bloco = [
            linha[0].lstrip(RECUO)
            for linha in linhas[inicio : inicio + 70]
            if linha
        ]
        self.assertIn("Faturamento", bloco)

    def test_recuo_marca_os_niveis(self):
        recuados = [
            linha[0]
            for linha in self.linhas
            if linha and linha[0].startswith(RECUO)
        ]
        self.assertTrue(recuados)

        with tempfile.TemporaryDirectory() as tmp:
            destino = Path(tmp) / "sem_recuo.csv"
            linhas = gerar(ORIGEM, destino, recuar=False)
        self.assertFalse(
            [linha[0] for linha in linhas if linha and linha[0].startswith(RECUO)]
        )


@unittest.skipUnless(ORIGEM.exists(), "planilha de exemplo ausente")
class TestCli(unittest.TestCase):
    def test_gera_o_arquivo_pedido(self):
        with tempfile.TemporaryDirectory() as tmp:
            destino = Path(tmp) / "sub" / "saida.csv"
            with redirect_stdout(io.StringIO()):
                codigo = main([str(ORIGEM), "-s", str(destino)])

            self.assertEqual(codigo, 0)
            self.assertTrue(destino.exists())

    def test_entrada_inexistente_retorna_erro(self):
        with redirect_stderr(io.StringIO()) as erro:
            codigo = main(["nao_existe.csv", "-s", "ignorado.csv"])

        self.assertEqual(codigo, 1)
        self.assertIn("nao encontrado", erro.getvalue())


if __name__ == "__main__":
    unittest.main()
