"""Surface-adsorbate CHE Pourbaix integration tests."""
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
from xml.etree import ElementTree

from openpyxl import Workbook

SCRIPT = Path(__file__).resolve().parents[1] / "src" / "DFTDesk" / "SurfaceScripts"
STEPS = Path(__file__).resolve().parents[1] / "src" / "DFTDesk" / "StepScripts"
sys.path.insert(0, str(STEPS))
sys.path.insert(0, str(SCRIPT))

from surface_pourbaix import (COLORS, ELECTRONS, RT_LOG10_F, analyze,
                             compute_map, read_adsorbates, render_maps,
                             stable_rhe_intervals)


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

    def test_exact_phase_boundaries_without_grid_stair_steps(self):
        # Adjacent CHE coexistence potentials are exact straight lines,
        # independent of the sampling density in compute_map/export_grid.
        intervals = stable_rhe_intervals((0.0, 0.73, 1.68, 2.83))
        self.assertAlmostEqual(intervals[0][1], 0.73)
        self.assertAlmostEqual(intervals[1][0], 0.73)
        self.assertAlmostEqual(intervals[1][1], 0.95)
        self.assertAlmostEqual(intervals[2][1], 1.15)
        self.assertAlmostEqual(0.73 - RT_LOG10_F * 14, -0.098225, places=4)
        # A higher-energy intermediate can be skipped by the lower envelope.
        self.assertIsNone(stable_rhe_intervals((0.0, 2.0, 1.68, 2.83))[1])

    def test_horizontal_and_vertical_vector_figures(self):
        import numpy as np
        from matplotlib.axes import Axes
        catalysts = [
            {"material": label, "dg": (0.0, 0.73, 1.68, 2.83)}
            for label in ("Pt4", "Pt3Co", "Pt3Co-CF3")
        ]
        with tempfile.TemporaryDirectory() as folder:
            sizes = {}
            with patch.object(Axes, "pcolormesh", side_effect=AssertionError(
                    "Stable boundaries must not be rendered as a stepped grid")):
                for layout in ("horizontal", "vertical"):
                    svg = Path(folder) / f"{layout}.svg"
                    render_maps(catalysts, np.linspace(0, 14, 40),
                                np.linspace(-1, 2, 40),
                                Path(folder) / f"{layout}.png", svg, layout)
                    figure = ElementTree.parse(svg).getroot()
                    sizes[layout] = (float(figure.attrib["width"].removesuffix("pt")),
                                     float(figure.attrib["height"].removesuffix("pt")))
                    content = svg.read_text(encoding="utf-8")
                    for color in COLORS:
                        self.assertIn(color, content)
            self.assertGreater(sizes["horizontal"][0], sizes["horizontal"][1])
            self.assertGreater(sizes["vertical"][1], sizes["vertical"][0])
            self.assertRaises(ValueError, render_maps, catalysts,
                              np.array([0, 14]), np.array([-1, 2]),
                              Path(folder) / "wrong.png", Path(folder) / "wrong.svg",
                              "diagonal")

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
                self.assertEqual(sum(1 for _ in result["Adsorbates"].iter_rows(values_only=True)), 3)
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
