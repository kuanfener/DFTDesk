"""Integration tests for DFTDesk's LOBSTER COHP/ICOHP analysis engine."""
import csv
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

from openpyxl import load_workbook

SCRIPT = Path(__file__).resolve().parents[1] / "src" / "DFTDesk" / "CohpScripts" / "cohp_analyzer.py"


def execute(*args):
    result = subprocess.run(
        [sys.executable, str(SCRIPT), *args], capture_output=True,
        text=True, check=False, timeout=120,
    )
    if result.returncode:
        raise AssertionError(
            f"COHP subprocess exited {result.returncode}:\n{result.stderr}\n{result.stdout}"
        )
    return json.loads(result.stdout)


class CohpAnalyzerTests(unittest.TestCase):
    def test_icohplist_only_and_cohpcar_with_spin(self):
        with tempfile.TemporaryDirectory(prefix="dftdesk-cohp-test-") as tmp:
            folder = Path(tmp)
            icohp = folder / "ICOHPLIST.lobster"
            icohp.write_text(
                "  COHP#  atomMU  atomNU     distance          ICOHP(eF) avg. over # bonds  for spin  1\n"
                "      1     Fe1     Fe2      2.50000           -0.50000                 1\n"
                "      2     Fe2     Fe3      2.60000           -0.80000                 1\n"
                "  COHP#  atomMU  atomNU     distance          ICOHP(eF) avg. over # bonds  for spin  2\n"
                "      1     Fe1     Fe2      2.50000           -1.00000                 1\n"
                "      2     Fe2     Fe3      2.60000           -1.20000                 1\n",
                encoding="utf-8",
            )
            metadata = execute("--action", "inspect", "--input", str(icohp))
            self.assertFalse(metadata["HasCurves"])
            self.assertTrue(metadata["SpinPolarized"])
            self.assertEqual([bond["Label"] for bond in metadata["Bonds"]], ["1", "2"])
            self.assertAlmostEqual(metadata["Bonds"][0]["IcohpDown"], -1.0)

            result = execute(
                "--action", "analyze", "--input", str(icohp),
                "--labels", "1,2", "--spin", "both", "--emin", "-2", "--emax", "1",
                "--outdir", str(folder / "list-only"),
            )
            self.assertFalse(result["HasCurves"])
            self.assertEqual(result["Png"], "")
            for key in ("Csv", "Xlsx", "Dat"):
                self.assertGreater(Path(result[key]).stat().st_size, 100)
            book = load_workbook(result["Xlsx"], read_only=True)
            self.assertEqual(book["ICOHP at Ef"].max_row, 3)
            self.assertAlmostEqual(book["ICOHP at Ef"]["G2"].value, -1.5)
            book.close()
            self.assertIn("ICOHP(Ef)", Path(result["Dat"]).read_text(encoding="utf-8-sig"))

            # Minimal, genuine LOBSTER COHPCAR layout: two bonds, two spin channels.
            # Column order is E, avg up, bond1 up, bond2 up, avg down, bond1 down, bond2 down
            # with each COHP followed by its already-integrated ICOHP.
            cohp = folder / "COHPCAR.lobster"
            lines = [
                "LOBSTER COHP test\n",
                "3 2 5 -2 2 0.5\n",
                "COHP data\n",
                "No.1:Fe1->Fe2(2.50000)\n",
                "No.2:Fe2->Fe3(2.60000)\n",
            ]
            for energy in (-2.0, -1.0, 0.0, 1.0, 2.0):
                values = [energy, -1.1, -0.5, -0.2, -0.5, -0.9, -0.8,
                          -1.0, -0.4, -0.3, -1.0, -0.7, -1.2]
                # COHP importer expects 1 + 2*(2+1)*2 = 13 columns.
                lines.append(" ".join(str(value) for value in values) + "\n")
            cohp.write_text("".join(lines), encoding="utf-8")

            metadata = execute("--action", "inspect", "--input", str(cohp))
            self.assertTrue(metadata["HasCurves"])
            self.assertTrue(metadata["SpinPolarized"])
            self.assertEqual([bond["Label"] for bond in metadata["Bonds"]], ["1", "2"])
            result = execute(
                "--action", "analyze", "--input", str(cohp),
                "--labels", "1,2", "--spin", "both", "--negative",
                "--emin", "-1", "--emax", "1", "--outdir", str(folder / "cohp-output"),
            )
            for key in ("Png", "Svg", "Csv", "Xlsx", "Dat"):
                self.assertGreater(Path(result[key]).stat().st_size, 100)
            with Path(result["Csv"]).open(encoding="utf-8-sig", newline="") as handle:
                rows = list(csv.reader(handle))
            self.assertEqual(len(rows), 6)
            self.assertIn("COHP_1_up", rows[0])
            self.assertIn("ICOHP_2_down", rows[0])
            book = load_workbook(result["Xlsx"], read_only=True)
            self.assertEqual(book["COHP curves"].max_row, 6)
            self.assertEqual(book["ICOHP at Ef"].max_row, 3)
            book.close()


if __name__ == "__main__":
    unittest.main()
