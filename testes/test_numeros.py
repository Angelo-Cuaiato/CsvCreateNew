import unittest
from decimal import Decimal

from fluxo_caixa.numeros import (
    formatar_percentual,
    formatar_valor,
    ler_valor,
    percentual,
)


class TestLerValor(unittest.TestCase):
    def test_le_formato_brasileiro(self):
        self.assertEqual(ler_valor("43.623.381,07"), Decimal("43623381.07"))
        self.assertEqual(ler_valor("0,00"), Decimal("0.00"))
        self.assertEqual(ler_valor("-1.234,56"), Decimal("-1234.56"))

    def test_le_variacoes_comuns(self):
        self.assertEqual(ler_valor("R$ 1.234,56"), Decimal("1234.56"))
        self.assertEqual(ler_valor("(1.234,56)"), Decimal("-1234.56"))
        self.assertEqual(ler_valor("1.234,56-"), Decimal("-1234.56"))

    def test_celula_vazia_vira_none(self):
        self.assertIsNone(ler_valor(""))
        self.assertIsNone(ler_valor("   "))
        self.assertIsNone(ler_valor(None))
        self.assertIsNone(ler_valor("-"))
        self.assertIsNone(ler_valor("texto"))


class TestFormatar(unittest.TestCase):
    def test_formata_com_separador_de_milhar(self):
        self.assertEqual(formatar_valor(Decimal("43623381.07")), "43.623.381,07")
        self.assertEqual(formatar_valor(Decimal("-1234.5")), "-1.234,50")
        self.assertEqual(formatar_valor(Decimal("0")), "0,00")
        self.assertEqual(formatar_valor(Decimal("999.99")), "999,99")

    def test_none_vira_celula_vazia(self):
        self.assertEqual(formatar_valor(None), "")

    def test_ida_e_volta(self):
        for texto in ("1.234.567,89", "-98,70", "0,00"):
            self.assertEqual(formatar_valor(ler_valor(texto)), texto)


class TestPercentual(unittest.TestCase):
    def test_calcula_percentual(self):
        self.assertEqual(percentual(Decimal("50"), Decimal("100")), Decimal("50.0"))
        self.assertEqual(formatar_percentual(Decimal("98.74")), "98,7%")

    def test_previsto_zerado_nao_tem_percentual(self):
        self.assertIsNone(percentual(Decimal("10"), Decimal("0")))
        self.assertIsNone(percentual(Decimal("10"), None))
        self.assertEqual(formatar_percentual(None), "-")


if __name__ == "__main__":
    unittest.main()
