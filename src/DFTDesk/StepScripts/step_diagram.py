#!/usr/bin/env python3
"""DFTDesk: convert reaction free energies to publishable staircase diagrams.

Accepted workbooks:
1. The supplied HER source workbook: A=state, B=DFT energy, E=correction,
   G=gas reference, I=relative free energy (when cached).
2. A tidy workbook with columns Material/Catalyst, State/Step, Energy/DeltaG.
All plotting data remain exportable to Excel, CSV and Origin tab-separated DAT.
"""
import argparse
import csv
import json
import math
import os
import sys
from collections import OrderedDict
from pathlib import Path

# Windows consoles may use cp1252; always return UTF-8 JSON/errors to .NET and CLI.
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")
if hasattr(sys.stderr, "reconfigure"):
    sys.stderr.reconfigure(encoding="utf-8")


def _number(value):
    if isinstance(value, bool):
        return None
    if isinstance(value, (int, float)) and math.isfinite(value):
        return float(value)
    return None


def _states_from_template(sheet):
    from openpyxl import load_workbook
    # sheet is a cached-value worksheet; a second formula worksheet supplies raw B/E.
    groups = OrderedDict()
    current = None
    baseline = None
    reference = None
    for row in sheet.iter_rows(min_row=2, values_only=True):
        label = row[0] if row else None
        raw_energy = _number(row[1]) if len(row) > 1 else None
        correction = _number(row[4]) if len(row) > 4 else None
        ref_label = row[6] if len(row) > 6 else None
        relative = _number(row[8]) if len(row) > 8 else None
        if isinstance(ref_label, str) and ref_label.strip() in ("H2O", "1/2H2", "H2", "O2") and isinstance(label, str):
            current = label.strip()
            baseline = raw_energy
            reference = ref_label.strip()
            groups.setdefault(current, [])
            continue
        if not isinstance(label, str) or not label.strip() or current is None:
            continue
        if raw_energy is None and relative is None:
            continue
        # Recalculate from editable B/E columns; cached I is only a fallback.
        h2o = (_number(sheet["B2"].value) or 0.0) + (_number(sheet["E2"].value) or 0.0)
        h2 = (_number(sheet["B3"].value) or 0.0) + (_number(sheet["E3"].value) or 0.0)
        gas = {
            "H2O": h2o,
            "1/2H2": h2 / 2,
            "H2": h2,
            "O2": _number(sheet["F4"].value),
        }.get(reference)
        if raw_energy is not None and baseline is not None and gas is not None:
            energy = raw_energy + (correction or 0.0) - (baseline + gas)
        elif relative is not None:
            energy = relative
        else:
            continue
        groups[current].append((label.strip(), round(energy, 10)))
    return [(name, points) for name, points in groups.items() if points]


def _states_from_tidy(sheet):
    header = [str(c.value or "").strip().lower().replace(" ", "").replace("_", "")
              for c in sheet[1]]
    def locate(*keys):
        return next((header.index(key) for key in keys if key in header), None)
    material_col = locate("catalyst", "material", "system", "sample", "催化剂", "体系")
    label_col = locate("state", "step", "intermediate", "reactionstate", "中间体", "步骤")
    energy_col = locate("deltag", "freeenergy", "energyev", "g/ev", "δg", "自由能")
    if label_col is None or energy_col is None:
        return []
    groups = OrderedDict()
    for row in sheet.iter_rows(min_row=2, values_only=True):
        label = row[label_col] if len(row) > label_col else None
        energy = _number(row[energy_col]) if len(row) > energy_col else None
        if energy is None or label is None or str(label).strip() == "":
            continue
        name = (str(row[material_col]).strip() if material_col is not None and len(row) > material_col and
                row[material_col] is not None else sheet.title)
        groups.setdefault(name, []).append((str(label).strip(), energy))
    return [(name, points) for name, points in groups.items() if points]


def read_profiles(path):
    from openpyxl import load_workbook
    book = load_workbook(path, read_only=True, data_only=True)
    groups = OrderedDict()
    try:
        for sheet in book:
            candidates = _states_from_tidy(sheet)
            if not candidates:
                labels = [sheet.cell(1, col).value for col in range(1, min(sheet.max_column, 10) + 1)]
                if "E(DFT)/eV" in labels or any(
                    isinstance(sheet.cell(i, 7).value, str) and sheet.cell(i, 7).value in ("H2O", "1/2H2")
                    for i in range(2, min(sheet.max_row, 25) + 1)
                ):
                    candidates = _states_from_template(sheet)
            for name, states in candidates:
                groups.setdefault(name, []).extend(states)
    finally:
        book.close()
    if not groups:
        raise ValueError("未识别台阶图数据。支持所提供的 HER Excel 模板，或列为 Material / State / DeltaG 的长表。")
    profiles = []
    for name, states in groups.items():
        # Explicit 0-eV start/end to represent initial and regenerated surfaces.
        steps = [(name, "初态", 0.0)] + [(name, label, value) for label, value in states]
        steps.append((name, "终态", 0.0))
        profiles.append(steps)
    return profiles


def export_profiles(profiles, outdir):
    from openpyxl import Workbook
    from openpyxl.styles import Font, PatternFill
    path = Path(outdir)
    path.mkdir(parents=True, exist_ok=True)
    flat = []
    for profile in profiles:
        prev = None
        for idx, (material, state, energy) in enumerate(profile):
            flat.append([material, idx, state, energy, None if prev is None else energy - prev])
            prev = energy
    headers = ["Material", "Step_Index", "State", "DeltaG_eV", "DeltaG_from_previous_eV"]
    csv_path = path / "step_diagram.csv"
    with csv_path.open("w", newline="", encoding="utf-8-sig") as f:
        writer = csv.writer(f)
        writer.writerow(headers)
        writer.writerows(flat)
    dat_path = path / "step_diagram_origin.dat"
    with dat_path.open("w", newline="", encoding="utf-8") as f:
        writer = csv.writer(f, delimiter="\t")
        writer.writerow(headers)
        writer.writerows(flat)
    book = Workbook()
    sheet = book.active
    sheet.title = "Reaction Profiles"
    sheet.append(headers)
    for row in flat:
        sheet.append(row)
    for cell in sheet[1]:
        cell.font = Font(bold=True, color="FFFFFF")
        cell.fill = PatternFill("solid", fgColor="433E91")
    sheet.freeze_panes = "A2"
    sheet.auto_filter.ref = sheet.dimensions
    for col, width in {"A": 25, "B": 14, "C": 23, "D": 18, "E": 27}.items():
        sheet.column_dimensions[col].width = width
    for row in sheet.iter_rows(min_row=2, min_col=4, max_col=5):
        for cell in row:
            cell.number_format = "0.0000"
    guide = book.create_sheet("Origin Guide")
    guide.append(["数据来源", "原始 Excel 台阶图能量 / 自由能修正"])
    guide.append(["Origin 导入", "File > Import > Single ASCII，选择 step_diagram_origin.dat"])
    guide.append(["Origin 绘图", "X=Step_Index，Y=DeltaG_eV，按 Material 分组"])
    guide.append(["说明", ".dat 为 Origin 可导入数据；不会修改用户的 .opju 原生工程模板。"])
    xlsx_path = path / "step_diagram.xlsx"
    book.save(xlsx_path)
    return str(csv_path), str(xlsx_path), str(dat_path)


def draw_profiles(profiles, outdir):
    """Origin-inspired horizontal energy levels, grouped by comparable pathways."""
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    from matplotlib import font_manager
    available = {f.name for f in font_manager.fontManager.ttflist}
    for name in ("Microsoft YaHei", "SimHei", "Noto Sans CJK SC", "Arial Unicode MS"):
        if name in available:
            plt.rcParams["font.sans-serif"] = [name]
            break
    plt.rcParams["axes.unicode_minus"] = False

    # An Origin project may contain both a shared-intermediate comparison and
    # detailed reaction-coordinate pathways. Infer both without guessing states.
    common = []
    if len(profiles) > 1:
        common = [state for _, state, _ in profiles[0][1:-1]
                  if all(state in {r[1] for r in other[1:-1]} for other in profiles[1:])]
    panels = []
    if len(common) >= 2:
        comparison = []
        for profile in profiles:
            values = {state: energy for _, state, energy in profile[1:-1]}
            comparison.append(
                [(profile[0][0], "初态", 0.0)] +
                [(profile[0][0], state, values[state]) for state in common] +
                [(profile[0][0], "终态", 0.0)])
        panels.append(("共同中间态对比", comparison))

    groups = OrderedDict()
    for profile in profiles:
        signature = tuple(row[1] for row in profile[1:-1])
        groups.setdefault(signature, []).append(profile)
    for signature, members in groups.items():
        if len(common) >= 2 and len(groups) == 1:
            continue  # The comparison already represents the same complete path.
        if len(common) >= 2 and len(members) == 1:
            continue  # Avoid duplicating a lone short path already in comparison.
        title = "完整反应路径" if len(members) > 1 else members[0][0][0]
        panels.append((title, members))
    if not panels:
        panels = [("完整反应路径", profiles)]

    colors = ["#2F66C5", "#09958C", "#42A464", "#C65A7A", "#B37D31"]
    palette = {profile[0][0]: colors[i % len(colors)] for i, profile in enumerate(profiles)}
    fig, axes = plt.subplots(len(panels), 1,
                             figsize=(10.6, max(3.9, 3.7 * len(panels))),
                             squeeze=False)
    for panel_index, (title, paths) in enumerate(panels):
        ax = axes[panel_index][0]
        all_energies = []
        for profile in paths:
            material = profile[0][0]
            levels = [float(row[2]) for row in profile]
            all_energies.extend(levels)
            color = palette[material]
            for index, energy in enumerate(levels):
                ax.hlines(energy, index - .31, index + .31,
                          color=color, lw=2.6, zorder=3,
                          label=material if index == 0 else None)
                if index + 1 < len(levels):
                    ax.plot([index + .31, index + .69],
                            [energy, levels[index + 1]],
                            color=color, lw=1.1, ls=(0, (4, 3)), zorder=2)
            if len(paths) == 1:
                for index, energy in enumerate(levels[1:-1], 1):
                    ax.annotate(f"{energy:+.2f}", (index, energy),
                                xytext=(0, 8), textcoords="offset points",
                                ha="center", fontsize=8.5, color=color)
        labels = [row[1] for row in paths[0]]
        ax.set_xticks(range(len(labels)), labels, rotation=12, ha="center")
        ax.set_xlim(-.58, len(labels) - .4)
        padding = max(.22, (max(all_energies) - min(all_energies)) * .18)
        ax.set_ylim(min(all_energies) - padding, max(all_energies) + padding)
        ax.axhline(0, color="#B6B8C0", ls=":", lw=.85, zorder=1)
        ax.set_title(title, loc="left", fontsize=12, weight="bold")
        ax.set_ylabel("Free Energy (eV)")
        ax.set_xlabel("Reaction Coordinates")
        if len(paths) > 1:
            ax.legend(loc="lower left", frameon=False, fontsize=9, ncol=min(3, len(paths)))
        ax.spines[["top", "right"]].set_visible(False)
        ax.tick_params(direction="in", length=4)
    fig.tight_layout()
    png = str(Path(outdir) / "step_diagram.png")
    svg = str(Path(outdir) / "step_diagram.svg")
    fig.savefig(png, dpi=240, bbox_inches="tight")
    fig.savefig(svg, bbox_inches="tight")
    plt.close(fig)
    return png, svg

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--action", choices=("inspect", "analyze"), default="analyze")
    parser.add_argument("--input", required=True)
    parser.add_argument("--outdir")
    args = parser.parse_args()
    input_path = Path(args.input)
    if not input_path.is_file():
        raise FileNotFoundError(str(input_path))
    if input_path.suffix.lower() not in (".xlsx", ".xlsm"):
        raise ValueError("台阶图输入必须为 Excel .xlsx 或 .xlsm 文件。")
    profiles = read_profiles(input_path)
    message = f"识别 {len(profiles)} 个体系：" + "；".join(
        f"{p[0][0]} ({len(p)-2} 个中间态)" for p in profiles)
    result = {"Summary": message, "SeriesCount": len(profiles),
              "Png": "", "Svg": "", "Csv": "", "Xlsx": "", "Dat": ""}
    if args.action == "analyze":
        if not args.outdir:
            raise ValueError("缺少输出目录。")
        result["Csv"], result["Xlsx"], result["Dat"] = export_profiles(profiles, args.outdir)
        result["Png"], result["Svg"] = draw_profiles(profiles, args.outdir)
    print(json.dumps(result, ensure_ascii=False))


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print(f"{type(error).__name__}: {error}", file=sys.stderr)
        sys.exit(1)
