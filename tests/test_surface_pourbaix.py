"""Surface-adsorbate CHE Pourbaix integration tests."""
import sys
import tempfile
import unittest
from pathlib import Path

from openpyxl import Workbook

SCRIPT = Path(__file__).resolve().parents[1] / "src" / "DFTDesk" / "SurfaceScripts"
STEPS = Path(__file__).resolve().parents[1] / "src" / "DFTDesk" / "StepScripts"
sys.path.insert(0, str(STEPS))
sys.path.insert(0, str(SCRIPT))

from surface_pourbaix import (ELECTRONS, analyze, compute_map, read_adsorbates)


class SurfacePourbaixTests(unittest.TestCase):
    def test_electron_count_and_ph_she_mapping(self):
        self.assertEqual(ELECTRONS, (0, 1, 2, 3))
        import numpy as np
        ph, she, rhe, energies, ids = compute_map(
            (0, 1.0, 2.0, 3.0), np.array([0.0, 14.0]),
            np.array([0.0, 1.5]))
        self.assertEqual(int(ids[0, 0]), 0)  # clean surface at U_RHE=0
        self.assertEqual(int(ids[1, 0]), 3)  # OOH at high U_RHE
        self.assertAlmostEqual(rhe[0, 1] - she[0, 1], 14 * 0.059159, places=4)

    def test_multiple_catalysts_and_export(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "energies.xlsx"
            book = Workbook()
            sheet = book.active
            sheet.append(("Material", "DG_OH", "DG_O", "DG_OOH"))
            sheet.append(("Pt4", 1.16, 2.50, 3.8))
            sheet.append(("Pt3Co", 1.3, 2.7, 4.1))
            book.save(path)
            catalysts = read_adsorbates(path)
            self.assertEqual([a["material"] for a in catalysts], ["Pt4", "Pt3Co"])
            output = analyze(str(path), str(Path(folder) / "output"),
                             0, 14, -1.0, 2.0, resolution=40)
            self.assertEqual(output["SeriesCount"], 2)
            for ext in ("png", "svg", "csv", "xlsx", "dat"):
                self.assertGreater(Path(output[ext.title() if ext != "xlsx" else "Xlsx"]).stat().st_size, 0)
            from openpyxl import load_workbook
            result = load_workbook(output["Xlsx"], read_only=True)
            try:
                self.assertIn("SurfacePourbaixGrid", result.sheetnames)
                self.assertEqual(result["Adsorbates"].max_row, 3)
            finally:
                result.close()

    def test_legacy_repeated_five_row_template(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "legacy.xlsx"
            book = Workbook()
            sheet = book.active
            for material, values in [
                ("Pt4", [4.92, 3.8, 2.5, 1.16, 0]),
                ("Pt3Co", [4.92, 4.1, 2.7, 1.3, 0]),
            ]:
                for label, energy in zip(
                    [material, "*OOH", "*O", "*OH", material], values):
                    row = [None] * 7
                    row[0] = label
                    row[6] = energy
                    sheet.append(row)
                sheet.append([None] * 7)
            book.save(path)
            catalysts = read_adsorbates(path)
            self.assertEqual(len(catalysts), 2)
            self.assertAlmostEqual(catalysts[0]["dg"][1], 1.16)
            self.assertAlmostEqual(catalysts[1]["dg"][3], 4.1)


if __name__ == "__main__":
    unittest.main()
