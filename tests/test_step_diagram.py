import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

from openpyxl import Workbook, load_workbook

SCRIPT = Path(__file__).resolve().parents[1] / "src" / "DFTDesk" / "StepScripts" / "step_diagram.py"


def run_script(source, outdir=None, success=True, extra=None):
    args = [sys.executable, str(SCRIPT), "--input", str(source)]
    if outdir is not None:
        args += ["--action", "analyze", "--outdir", str(outdir)]
    else:
        args += ["--action", "inspect"]
    args.extend(extra or [])
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
            result_book = load_workbook(result["Xlsx"], read_only=True, data_only=True)
            values = list(result_book.active.values)
            result_book.close()
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


    @staticmethod
    def oxygen_workbook(path, data):
        book = Workbook()
        sheet = book.active
        sheet.title = "Catalysts"
        sheet.append(["Material", "DG_OH", "DG_O", "DG_OOH"])
        for material, values in data:
            sheet.append([material, *values])
        book.save(path)

    def test_oer_four_steps_potential_and_multi_catalyst_exports(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / "OER.xlsx"
            self.oxygen_workbook(source, [
                ("ideal", (1.23, 2.46, 3.69)),
                ("sample", (1.60, 2.80, 3.90)),
            ])
            args = ["--mode", "OER", "--potential", "0", "--reference", "RHE"]
            result = run_script(source, root / "plot", extra=args)
            self.assertEqual(2, result["SeriesCount"])
            self.assertIn("η=0.370 V", result["Summary"])
            output = load_workbook(result["Xlsx"], read_only=True, data_only=True)
            self.assertIn("CHE Metrics", output.sheetnames)
            self.assertIn("CHE Four Steps", output.sheetnames)
            rows = list(output["Reaction Profiles"].values)
            ideal = [row for row in rows[1:] if row[0] == "ideal"]
            self.assertEqual(["*", "*OH", "*O", "*OOH", "O₂ + *"], [row[2] for row in ideal])
            self.assertAlmostEqual(4.92, ideal[-1][3])
            sample = [row for row in list(output["CHE Metrics"].values)[1:]
                      if row[0] == "sample"][0]
            self.assertAlmostEqual(1.60, sample[10])
            self.assertAlmostEqual(.37, sample[12])
            self.assertEqual(1, sample[13])
            output.close()
            self.assertGreater(Path(result["Png"]).stat().st_size, 0)
            self.assertGreater(Path(result["Dat"]).stat().st_size, 0)

            equilibrium = run_script(
                source, root / "eq", extra=["--mode", "OER", "--potential", "1.23"])
            eq = load_workbook(equilibrium["Xlsx"], read_only=True, data_only=True)
            last_ideal = [row for row in list(eq["Reaction Profiles"].values)[1:]
                          if row[0] == "ideal"][-1]
            eq.close()
            self.assertAlmostEqual(0, last_ideal[3], places=9)

    def test_orr_reverse_path_limiting_potential(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / "orr.xlsx"
            self.oxygen_workbook(source, [("PtCo", (1.60, 2.80, 3.90))])
            res = run_script(source, root / "figure", extra=["--mode", "ORR"])
            self.assertIn("η=0.210 V", res["Summary"])
            book = load_workbook(res["Xlsx"], read_only=True, data_only=True)
            row = list(book["CHE Metrics"].values)[1]
            self.assertAlmostEqual(1.02, row[10])
            self.assertAlmostEqual(.21, row[12])
            self.assertEqual(1, row[13])
            values = list(book["Reaction Profiles"].values)
            self.assertEqual("O₂ + *", values[1][2])
            self.assertEqual("2H₂O + *", values[-1][2])
            self.assertAlmostEqual(-4.92, values[-1][3], places=9)
            book.close()

            at_eq = run_script(source, root / "equilibrium",
                               extra=["--mode", "ORR", "--potential", "1.23"])
            book = load_workbook(at_eq["Xlsx"], read_only=True, data_only=True)
            self.assertAlmostEqual(0, list(book["Reaction Profiles"].values)[-1][3], places=9)
            book.close()

    def test_she_pH_conversion_and_gibbs_input(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / "gibbs.xlsx"
            book = Workbook()
            sheet = book.active
            sheet.append(["Material", "G_star", "G_OH", "G_O", "G_OOH", "G_H2O", "G_H2"])
            sheet.append(["sample", 0, .1, 1.8, 1.4, -2, -1])
            book.save(source)
            result = run_script(source, root / "she",
                                extra=["--mode", "OER", "--potential", "0", "--ph", "7",
                                       "--reference", "SHE"])
            book = load_workbook(result["Xlsx"], read_only=True, data_only=True)
            summary = list(book["CHE Metrics"].values)[1]
            self.assertAlmostEqual(7 * .0591593497, summary[9], places=5)
            profile = list(book["Reaction Profiles"].values)
            self.assertAlmostEqual(4.92 - 4 * summary[9], profile[-1][3], places=7)
            self.assertAlmostEqual(summary[10] - 7 * .0591593497, summary[11], places=5)
            book.close()

    def test_raw_vasp_energies_require_all_free_energy_corrections(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / "bad_raw.xlsx"
            book = Workbook()
            sheet = book.active
            sheet.append(["Material", "E_star", "E_OH", "E_O", "E_OOH", "E_H2O", "E_H2"])
            sheet.append(["sample", -100, -109, -104, -112, -14, -7])
            book.save(source)
            message = run_script(source, success=False, extra=["--mode", "ORR"])
            self.assertIn("仅有 VASP E_DFT 不足以计算", message)
            sheet.cell(1, 8, "Corr_star")
            for column, name in enumerate(["Corr_OH", "Corr_O", "Corr_OOH",
                                           "Corr_H2O", "Corr_H2"], start=9):
                sheet.cell(1, column, name)
            for column in range(8, 14):
                sheet.cell(2, column, 0.)
            book.save(source)
            result = run_script(source, extra=["--mode", "ORR"])
            self.assertEqual(1, result["SeriesCount"])


if __name__ == "__main__":
    unittest.main()
