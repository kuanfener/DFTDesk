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
    def test_generated_fill_in_templates_and_visible_che_labels(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            oxygen_run = subprocess.run(
                [sys.executable, str(SCRIPT), "--action", "template",
                 "--mode", "ORR", "--outdir", str(root)],
                capture_output=True, text=True, encoding="utf-8", check=True)
            oxygen_path = Path(json.loads(oxygen_run.stdout)["Template"])
            self.assertEqual("DFTDesk_ORR_OER_Template.xlsx", oxygen_path.name)
            oxygen = load_workbook(oxygen_path)
            sheet = oxygen.active
            self.assertEqual("E(DFT)/eV", sheet["B1"].value)
            self.assertEqual("△G/eV", sheet["C7"].value)
            self.assertEqual(["Pt4", "*OOH", "*O", "*OH", "Pt4"],
                             [sheet.cell(row, 1).value for row in range(8, 13)])
            self.assertEqual("U=0V", sheet["G7"].value)
            self.assertFalse(any(c.value is not None and c.data_type in ("n", "f")
                                 for row in sheet for c in row))
            # Fill two of the three blocks; the untouched third must be ignored.
            sheet["B2"], sheet["B3"] = -14., -6.
            sheet["E2"], sheet["E3"] = 0., 0.
            for start, bare, ooh, o, oh in [
                (8, -100., -115.1, -105.2, -109.4),
                (15, -120., -135.17, -125.4, -129.52),
            ]:
                for row, energy in zip(range(start, start + 5),
                                       [bare, ooh, o, oh, bare]):
                    sheet.cell(row, 2, energy)
                for row in range(start + 1, start + 4):
                    sheet.cell(row, 3, 0.)
            # A stale G value must not override newly entered B/C energies.
            sheet["G9"] = -1234.
            oxygen.save(oxygen_path)
            oxygen.close()
            for mode in ("ORR", "OER"):
                result = run_script(oxygen_path, root / mode, extra=["--mode", mode])
                self.assertEqual(2, result["SeriesCount"])
                svg = Path(result["Svg"]).read_text(encoding="utf-8")
                self.assertIn("PDS", svg)
                self.assertIn("η = ", svg)
                self.assertIn("Pt4", svg)
                output = load_workbook(result["Xlsx"], read_only=True, data_only=True)
                first = list(output["CHE Metrics"].values)[1]
                self.assertAlmostEqual(1.60, first[3], places=6)
                output.close()

            her_run = subprocess.run(
                [sys.executable, str(SCRIPT), "--action", "template",
                 "--mode", "HER", "--outdir", str(root)],
                capture_output=True, text=True, encoding="utf-8", check=True)
            her_path = Path(json.loads(her_run.stdout)["Template"])
            self.assertEqual("DFTDesk_HER_Template.xlsx", her_path.name)
            her_book = load_workbook(her_path)
            her = her_book.active
            self.assertEqual("E(DFT)/eV", her["B1"].value)
            self.assertEqual("△G/eV", her["C6"].value)
            self.assertEqual("CoP-Sc", her["A7"].value)
            self.assertEqual("H2O", her["E7"].value)
            self.assertEqual("1/2H2", her["E11"].value)
            self.assertEqual("*H", her["A12"].value)
            self.assertFalse(any(c.value is not None and c.data_type in ("n", "f")
                                 for row in her for c in row))
            her["B2"], her["B3"] = -14., -6.
            her["E2"], her["E3"] = 0., 0.
            for row, energy in [(7, -100.), (8, -114.25), (9, -113.90),
                                (11, -100.), (12, -102.76)]:
                her.cell(row, 2, energy)
            for row in (8, 9, 12):
                her.cell(row, 3, 0.)
            her_book.save(her_path)
            her_book.close()
            result = run_script(her_path, root / "her", extra=["--mode", "HER"])
            self.assertEqual(1, result["SeriesCount"])
            out = load_workbook(result["Xlsx"], read_only=True, data_only=True)
            records = [row for row in out.active.values if row[0] == "CoP-Sc"]
            self.assertEqual(["初态", "*H2O", "*H-OH", "*H", "终态"],
                             [row[2] for row in records])
            self.assertAlmostEqual(-0.25, records[1][3], places=6)
            self.assertAlmostEqual(0.24, records[3][3], places=6)
            out.close()

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
            self.assertAlmostEqual(4.92, values[1][3], places=9)
            self.assertAlmostEqual(0, values[-1][3], places=9)
            book.close()

            at_eq = run_script(source, root / "equilibrium",
                               extra=["--mode", "ORR", "--potential", "1.23"])
            book = load_workbook(at_eq["Xlsx"], read_only=True, data_only=True)
            self.assertAlmostEqual(4.92, list(book["Reaction Profiles"].values)[-1][3], places=9)
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


    def test_repeated_five_row_orr_template_and_reversed_oer(self):
        """Matches the user's repeated-block layout without adding DG columns."""
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / "repeated_ORR.xlsx"
            book = Workbook()
            s = book.active
            s.append(["", "E(DFT)/eV", "Pressure/bar", "Temperature/K", "△G/eV", "G/eV"])
            s.append(["H2O", -14.22, .035, 298.15, .002, -14.22])
            s.append(["H2", -6.77, 1, 298.15, -.044, -6.81])
            s.append(["O2", None, None, None, None, -9.89])
            s.append([])
            s.append(["oh3"])
            s.append(["", "E(DFT)/eV", "△G/eV", "G/eV", "", "", "U=0V"])
            for name, levels in [
                ("Pt4", [4.92, 3.108, 1.415, .160, 0]),
                ("Pt3Co", [4.92, 4.149, 2.708, 1.299, 0]),
                ("Pt3Co-CF3", [4.92, 4.193, 2.628, 1.084, 0]),
            ]:
                if name != "Pt4":
                    s.append([])
                    s.append(["", "E(DFT)/eV", "△G/eV", "G/eV", "", "", "U=0V"])
                for state, value in zip(
                        [name, "*OOH", "*O", "*OH", name], levels):
                    s.append([state, None, None, None, None, None, value])
            book.save(source)

            o = run_script(source, root / "orr", extra=["--mode", "ORR"])
            self.assertEqual(3, o["SeriesCount"])
            book = load_workbook(o["Xlsx"], read_only=True, data_only=True)
            profile = [r for r in list(book["Reaction Profiles"].values)[1:] if r[0] == "Pt3Co"]
            self.assertEqual(["O₂ + *", "*OOH", "*O", "*OH", "2H₂O + *"],
                             [r[2] for r in profile])
            self.assertEqual([4.92, 4.149, 2.708, 1.299, 0],
                             [r[3] for r in profile])
            metrics = [r for r in list(book["CHE Metrics"].values)[1:] if r[0] == "Pt3Co"][0]
            self.assertEqual("legacy-ORR-template", metrics[2])
            self.assertAlmostEqual(.771, metrics[10], places=3)
            self.assertAlmostEqual(.459, metrics[12], places=3)
            book.close()

            e = run_script(source, root / "oer", extra=["--mode", "OER"])
            book = load_workbook(e["Xlsx"], read_only=True, data_only=True)
            profile = [r for r in list(book["Reaction Profiles"].values)[1:] if r[0] == "Pt3Co"]
            self.assertEqual(["*", "*OH", "*O", "*OOH", "O₂ + *"],
                             [r[2] for r in profile])
            self.assertEqual([0, 1.299, 2.708, 4.149, 4.92],
                             [r[3] for r in profile])
            book.close()

    def test_five_row_block_recovers_from_absolute_gibbs_when_g_missing(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / "uncached_relative.xlsx"
            book = Workbook()
            s = book.active
            s.append(["", "E(DFT)/eV"])
            for _ in range(3):
                s.append([])
            s.append(["Pt4", None, None, 10, "O2+2H2", 4.92])
            s.append(["*OOH", None, None, 9, "3/2H2", 3.108])
            s.append(["*O", None, None, 8, "H2O+H2", 1.415])
            s.append(["*OH", None, None, 7, "H2O+1/2H2", .160])
            s.append(["Pt4", None, None, 6, "2H2O", 0])
            book.save(source)
            result = run_script(source, extra=["--mode", "ORR"])
            self.assertEqual(1, result["SeriesCount"])


if __name__ == "__main__":
    unittest.main()
