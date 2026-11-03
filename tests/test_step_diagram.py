import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

from openpyxl import Workbook, load_workbook

SCRIPT = Path(__file__).resolve().parents[1] / "src" / "DFTDesk" / "StepScripts" / "step_diagram.py"


def run_script(source, outdir=None, success=True):
    args = [sys.executable, str(SCRIPT), "--input", str(source)]
    if outdir is not None:
        args += ["--action", "analyze", "--outdir", str(outdir)]
    else:
        args += ["--action", "inspect"]
    process = subprocess.run(args, capture_output=True, text=True, encoding="utf-8", check=False)
    if success:
        assert process.returncode == 0, process.stderr
        return json.loads(process.stdout.strip())
    assert process.returncode != 0, process.stdout
    return process.stderr


class TestStepDiagram(unittest.TestCase):
    def test_original_her_layout_and_exports(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / "raw_HER.xlsx"
            workbook = Workbook()
            sh = workbook.active
            sh.append(["", "E(DFT)/eV", "Pressure/bar", "Temperature/K", "△G/eV", "G/eV", "Reference", "H", "Relative"])
            sh.append(["H2O", -14.22, .035, 298.15, .002])
            sh.append(["H2", -6.77, 1, 298.15, -.044])
            sh.cell(2, 6, -14.218)
            sh.cell(3, 6, -6.814)
            sh.append(["O2"])
            sh.append([])
            sh.append(["Ru", -586.772, None, None, None, None, "H2O"])
            sh.append(["*H2O", -602.03, None, 298.15, .619])
            sh.append(["*H-OH", -602.401, None, 298.15, .510])
            sh.append([])
            sh.append(["Ru", -586.772, None, None, None, None, "1/2H2"])
            sh.append(["*H", -590.868, None, 298.15, .170])
            sh.append([])
            sh.append(["Pt", -100., None, None, None, None, "H2O"])
            sh.append(["*H2O", -114.4, None, None, .2])
            workbook.save(source)

            inspected = run_script(source)
            self.assertEqual(2, inspected["SeriesCount"])
            result = run_script(source, root / "outputs")
            for key in ("Png", "Svg", "Csv", "Xlsx", "Dat"):
                self.assertGreater(Path(result[key]).stat().st_size, 0)
            rows = load_workbook(result["Xlsx"], read_only=True, data_only=True).active
            values = list(rows.values)
            self.assertEqual(["Material", "Step_Index", "State", "DeltaG_eV", "DeltaG_from_previous_eV"], list(values[0]))
            ru = [row for row in values[1:] if row[0] == "Ru"]
            self.assertEqual(["初态", "*H2O", "*H-OH", "*H", "终态"], [x[2] for x in ru])
            self.assertAlmostEqual(-.421, ru[1][3], places=6)
            self.assertAlmostEqual(-.519, ru[3][3], places=6)
            self.assertTrue(Path(result["Svg"]).read_text(encoding="utf-8").lstrip().startswith("<?xml"))

    def test_long_table_format(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            workbook = Workbook()
            sheet = workbook.active
            sheet.append(["Material", "State", "DeltaG"])
            sheet.append(["Fe", "*H2O", -.25])
            sheet.append(["Fe", "*H", -.54])
            sheet.append(["Co", "*H2O", .11])
            source = root / "tidy.xlsx"
            workbook.save(source)
            info = run_script(source)
            self.assertEqual(2, info["SeriesCount"])

    def test_unsupported_input_is_explained(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            workbook = Workbook()
            workbook.active.append(["garbage", "nothing"])
            source = root / "unsupported.xlsx"
            workbook.save(source)
            message = run_script(source, success=False)
            self.assertIn("未识别台阶图数据", message)


if __name__ == "__main__":
    unittest.main()
