"""Offline tests of native Origin project data preparation (no Origin license required)."""
import importlib.util
import tempfile
import unittest
from pathlib import Path
from unittest import mock

from openpyxl import Workbook


SOURCE = Path(__file__).resolve().parents[1] / "src" / "DFTDesk" / "StepScripts" / "origin_project_export.py"
SPEC = importlib.util.spec_from_file_location("origin_project_export", SOURCE)
mod = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(mod)


class FakePlot:
    def __init__(self):
        self.updated = []

    def change_data(self, sheet, **kwargs):
        self.updated.append((sheet, kwargs))

    def lt_range(self):
        return "[Book1]Sheet1!(A,B)"


class FakeLayer:
    def __init__(self, plots):
        self.plots = plots
        self.rescales = 0

    def plot_list(self):
        return self.plots

    def rescale(self):
        self.rescales += 1


class FakeGraph:
    def __init__(self, layers):
        self.layers = layers

    def __iter__(self):
        return iter(self.layers)

    def __str__(self):
        return "Graph1"


class FakeSheet:
    def __init__(self):
        self.columns = []

    def from_list(self, index, values, **kwargs):
        self.columns.append((index, list(values), kwargs))


class FakeBook:
    def __init__(self):
        self.sheet = FakeSheet()
        self.sheet_names = []

    def add_sheet(self, name):
        self.sheet_names.append(name)
        return self.sheet


class FakeOrigin:
    def __init__(self, plots):
        self.oext = False
        self.graph = FakeGraph([FakeLayer(plots)])
        self.book = FakeBook()
        self.saved = []
        self.opened = []
        self.waited = 0

    def open(self, *args, **kwargs):
        self.opened.append((args, kwargs))

    def graph_list(self, selection, embeds):
        assert (selection, embeds) == ("p", True)
        return [self.graph]

    def pages(self, kind):
        assert kind == "w"
        return [self.book]

    def wait(self):
        self.waited += 1

    def save(self, filename):
        self.saved.append(filename)
        Path(filename).write_bytes(b"fake-native-opju-for-test")


def demo_book(path):
    workbook = Workbook()
    sheet = workbook.active
    sheet.title = "Reaction Profiles"
    sheet.append(["Material", "Step_Index", "State", "DeltaG_eV", "DeltaG_from_previous_eV"])
    for label, values in (("Pt4", [4.92, 3.4, 2.4, 1.0, 0.0]),
                          ("FeN4", [4.92, 3.5, 2.6, .8, 0.0])):
        for i, energy in enumerate(values):
            sheet.append([label, i, ["*", "*OOH", "*O", "*OH", "*"][i], energy, None])
    workbook.save(path)


class TestOriginProjectExport(unittest.TestCase):
    def test_reader_preserves_series_and_reaction_order(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "data.xlsx"
            demo_book(path)
            profiles = mod.read_profiles(path)
            self.assertEqual(["Pt4", "FeN4"], list(profiles))
            self.assertEqual([0, 1, 2, 3, 4], [item[0] for item in profiles["Pt4"]])
            xs, ys = mod.staircase_xy(profiles["Pt4"])
            self.assertEqual(18, len(xs))
            self.assertEqual(4.92, ys[0])
            self.assertEqual(0.0, ys[-1])

    def test_column_labels_after_z(self):
        self.assertEqual("A", mod.origin_column(0))
        self.assertEqual("Z", mod.origin_column(25))
        self.assertEqual("AA", mod.origin_column(26))
        self.assertEqual("AB", mod.origin_column(27))

    def test_reuses_existing_book_and_plot_objects(self):
        with tempfile.TemporaryDirectory() as tmp:
            base = Path(tmp)
            source = base / "original.opju"
            source.write_bytes(b"private-original")
            xlsx = base / "output.xlsx"
            demo_book(xlsx)
            dest = base / "result.opju"
            plots = [FakePlot(), FakePlot()]
            fake = FakeOrigin(plots)
            with mock.patch.dict("sys.modules", {"originpro": fake}):
                result = mod.write_project(source, xlsx, dest)
            self.assertEqual(2, result["SeriesCount"])
            self.assertEqual(["DFTDesk_Profiles"], fake.book.sheet_names)
            self.assertEqual(4, len(fake.book.sheet.columns))
            self.assertEqual(1, len(plots[0].updated))
            self.assertEqual({"x": "A", "y": "B"}, plots[0].updated[0][1])
            self.assertEqual({"x": "C", "y": "D"}, plots[1].updated[0][1])
            self.assertEqual(b"private-original", source.read_bytes())
            self.assertTrue(dest.exists())
            self.assertEqual(1, fake.graph.layers[0].rescales)

    def test_refuses_plot_count_mismatch_without_writing(self):
        with tempfile.TemporaryDirectory() as tmp:
            base = Path(tmp)
            original = base / "template.opju"
            original.write_bytes(b"private-original")
            xlsx = base / "out.xlsx"
            demo_book(xlsx)
            dest = base / "out.opju"
            fake = FakeOrigin([FakePlot()])
            with mock.patch.dict("sys.modules", {"originpro": fake}):
                with self.assertRaisesRegex(ValueError, "2 条已绘制数据曲线"):
                    mod.write_project(original, xlsx, dest)
            self.assertFalse(dest.exists())
            self.assertEqual([], fake.saved)
            self.assertEqual(b"private-original", original.read_bytes())


if __name__ == "__main__":
    unittest.main()
