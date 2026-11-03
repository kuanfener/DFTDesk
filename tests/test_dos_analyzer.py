"""Integration test for DFTDesk's embedded VASP DOSCAR engine."""
import csv
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

SCRIPT = Path(__file__).resolve().parents[1] / "src" / "DFTDesk" / "DosScripts" / "dos_analyzer.py"


class DosAnalyzerTests(unittest.TestCase):
    def test_spin_resolved_element_pdos_and_outputs(self):
        with tempfile.TemporaryDirectory(prefix="dftdesk-dos-test-") as tmp:
            folder = Path(tmp)
            (folder / "POSCAR").write_text(
                "Fe Pt\n1.0\n10 0 0\n0 10 0\n0 0 10\nFe Pt\n1 1\nDirect\n"
                "0 0 0\n0.5 0.5 0.5\n", encoding="utf-8"
            )
            energies = [-2, -1, 0, 1, 2]
            header = "2 -2 5 0.5 1\n"
            lines = [
                "2 2 1 0\n", "1 1 1 1 1\n", "300\n", "CAR\n", "test\n", header,
            ]
            lines += [f"{e} 10 9 0 0\n" for e in energies]
            lines += [header] + [f"{e} .2 .1 .3 .15 1 .5\n" for e in energies]
            lines += [header] + [f"{e} .2 .1 .3 .15 2 1\n" for e in energies]
            (folder / "DOSCAR").write_text("".join(lines), encoding="utf-8")

            def run(*args):
                result = subprocess.run(
                    [sys.executable, str(SCRIPT), *args], text=True,
                    capture_output=True, check=True, timeout=120,
                )
                return json.loads(result.stdout)

            info = run("--action", "inspect", "--input", str(folder / "DOSCAR"))
            self.assertEqual(info["Elements"], ["Fe", "Pt"])
            self.assertTrue(info["SpinPolarized"])
            self.assertTrue(info["HasPdos"])
            self.assertAlmostEqual(info["Fermi"], 0.5)
            self.assertAlmostEqual(info["EnergyMin"], -2.5)

            result = run(
                "--action", "analyze", "--input", str(folder / "DOSCAR"),
                "--elements", "Fe,Pt", "--orbitals", "d", "--spin", "both",
                "--emin", "-2", "--emax", "1",
                "--outdir", str(folder / "out"),
            )
            for key in ("Png", "Svg", "Csv"):
                self.assertGreater(Path(result[key]).stat().st_size, 100)
            self.assertIn("Fe d 带中心 (up)：-0.5000 eV", result["Summary"])
            with Path(result["Csv"]).open(encoding="utf-8-sig", newline="") as handle:
                rows = list(csv.reader(handle))
            self.assertEqual(len(rows), 6)
            self.assertIn("Fe_d_up", rows[0])
            self.assertIn("Pt_d_down", rows[0])


if __name__ == "__main__":
    unittest.main()
