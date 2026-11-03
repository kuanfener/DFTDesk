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
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    from matplotlib import font_manager
    # Use installed Chinese fonts when available; otherwise show English labels.
    available = {f.name for f in font_manager.fontManager.ttflist}
    for name in ("Microsoft YaHei", "SimHei", "Noto Sans CJK SC", "Arial Unicode MS"):
        if name in available:
            plt.rcParams["font.sans-serif"] = [name]
            break
    plt.rcParams["axes.unicode_minus"] = False
    count = len(profiles)
    fig, axes = plt.subplots(count, 1, figsize=(10.8, max(3.8, count * 3.5)), squeeze=False)
    colors = ["#5753A3", "#218F83", "#DA7B3A", "#BC5477", "#4477B8"]
    for i, profile in enumerate(profiles):
        ax = axes[i][0]
        color = colors[i % len(colors)]
        energies = [value for _, _, value in profile]
        labels = [state for _, state, _ in profile]
        for j, energy in enumerate(energies):
            ax.hlines(energy, j - .32, j + .32, color=color, lw=3, zorder=3)
            if j + 1 < len(energies):
                ax.plot([j + .32, j + .68], [energy, energies[j + 1]],
                        lw=1.2, ls="--", color="#9B9DA9", zorder=1)
            ax.annotate(f"{energy:+.2f}", (j, energy), textcoords="offset points",
                        xytext=(0, 7), ha="center", fontsize=8.5, color=color)
        ax.axhline(0, color="#A9ADB8", ls=":", lw=.9)
        ax.set_title(profile[0][0], loc="left", fontsize=12, fontweight="bold")
        ax.set_ylabel("Free energy (eV)")
        ax.set_xticks(list(range(len(labels))), labels, rotation=25, ha="right")
        ax.set_xlim(-.6, len(labels) - .4)
        pad = max(.23, (max(energies) - min(energies)) * .18)
        ax.set_ylim(min(energies) - pad, max(energies) + pad)
        ax.spines[["top", "right"]].set_visible(False)
        ax.grid(axis="y", color="#E9EAF0", lw=.65)
    fig.suptitle("Reaction free-energy step diagram", fontsize=15, weight="bold")
    fig.tight_layout(rect=(0, 0, 1, .98))
    png = str(Path(outdir) / "step_diagram.png")
    svg = str(Path(outdir) / "step_diagram.svg")
    fig.savefig(png, dpi=220, bbox_inches="tight")
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
