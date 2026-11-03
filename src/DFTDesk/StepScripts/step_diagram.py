#!/usr/bin/env python3
"""DFTDesk: HER reaction ladders and 4e ORR/OER computational-hydrogen-electrode diagrams.

HER accepts a historical B/E/G/I worksheet or Material/State/DeltaG long table.
ORR/OER accept catalyst adsorption Gibbs free energies or properly corrected
absolute VASP Gibbs energies, and retain the potential-dependent reaction endpoint.
All plotting data remain exportable as Excel, CSV and Origin tab-separated DAT.
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


def _key(header):
    """Accept DG_OH, ΔG_*OH, E(*OOH), and common spreadsheet separators."""
    import re
    value = str(header or "").strip().lower()
    value = value.replace("Δ", "d").replace("δ", "d").replace("∆", "d")
    value = value.replace("*", "star")
    return re.sub(r"[^a-z0-9\u4e00-\u9fff]", "", value)



def _legacy_orr_records(sheet):
    """Read repeated five-row ORR blocks, e.g. Pt4/*OOH/*O/*OH/Pt4.

    The historical Excel template uses column G (U=0V) for cumulative Gibbs
    levels, optionally column F for unshifted absolute Gibbs values. Each
    block is an independent catalyst, and the same adsorption free energies
    can be used to plot ORR or the reverse OER.
    """
    rows = list(sheet.iter_rows(values_only=True))
    found = []
    index = 0

    def cell(row, col):
        return row[col] if len(row) > col else None

    def ref_energy(label, h2o, h2, o2):
        key = str(label or "").lower().replace(" ", "").replace("₂", "2")
        if key == "o2+2h2":
            return o2 + 2 * h2
        if key in ("3/2h2", "1.5h2"):
            return 1.5 * h2
        if key == "h2o+h2":
            return h2o + h2
        if key in ("h2o+1/2h2", "h2o+0.5h2"):
            return h2o + 0.5 * h2
        if key == "2h2o":
            return 2 * h2o
        return None

    h2o = h2 = o2 = None
    if len(rows) >= 4:
        e_h2o, c_h2o = _number(cell(rows[1], 1)), _number(cell(rows[1], 4))
        e_h2, c_h2 = _number(cell(rows[2], 1)), _number(cell(rows[2], 4))
        if e_h2o is not None and e_h2 is not None:
            h2o = e_h2o + (c_h2o or 0)
            h2 = e_h2 + (c_h2 or 0)
        o2 = _number(cell(rows[3], 5))

    while index + 4 < len(rows):
        block = rows[index:index + 5]
        material = str(cell(block[0], 0) or "").strip()
        last = str(cell(block[4], 0) or "").strip()
        states = [str(cell(block[k], 0) or "").strip().upper().replace(" ", "")
                  for k in (1, 2, 3)]
        if (not material or material != last or material.startswith("*") or
                states != ["*OOH", "*O", "*OH"]):
            index += 1
            continue

        energies = [_number(cell(row, 6)) for row in block]
        if any(value is None for value in energies):
            absolute = [_number(cell(row, 5)) for row in block]
            if any(value is None for value in absolute) and all(
                    value is not None for value in (h2o, h2, o2)):
                absolute = []
                for row in block:
                    slab = _number(cell(row, 3))
                    gas = ref_energy(cell(row, 4), h2o, h2, o2)
                    absolute.append(slab + gas if slab is not None and gas is not None else None)
            if any(value is None for value in absolute):
                raise ValueError(
                    f"{sheet.title} 第 {index+1}–{index+5} 行 {material}："
                    "缺少 G 列的 U=0V 自由能，且无法从 F 列或 D/E 列和气体参考恢复。"
                    "若使用 Excel 公式，请先重新计算并保存。")
            energies = [value - absolute[-1] for value in absolute]

        zero = energies[4]
        if abs((energies[0] - zero) - 4.92) > 0.25:
            raise ValueError(f"{sheet.title} 第 {index+1} 行 {material}："
                             "ORR 模板 U=0V 的首末态差值应接近 4.92 eV，"
                             "请检查 G 列是否为四电子反应自由能。")
        found.append((index + 1, (material, energies[3] - zero,
                                   energies[2] - zero, energies[1] - zero)))
        index += 5
    return found


def read_oxygen_profiles(path, mode, potential, ph, reference):
    """Build 4e ORR/OER CHE profiles from adsorption free energies or corrected G."""
    from openpyxl import load_workbook
    if mode not in ("ORR", "OER"):
        raise ValueError("需要选择 ORR 或 OER 模式。")
    if reference not in ("RHE", "SHE"):
        raise ValueError("电位参考必须是 RHE 或 SHE。")
    if not math.isfinite(potential) or not math.isfinite(ph):
        raise ValueError("电位与 pH 必须是有效数字。")
    if not 0 <= ph <= 14:
        raise ValueError("仅支持 pH 0–14 的溶液条件。")
    alpha = 8.617333262145e-5 * 298.15 * math.log(10.0)
    u_rhe = potential if reference == "RHE" else potential + alpha * ph
    book = load_workbook(path, read_only=True, data_only=True)
    profiles, metrics, existing = [], [], set()
    def col(headers, *aliases):
        return next((headers[a] for a in aliases if a in headers), None)
    try:
        for sheet in book:
            if sheet.max_row < 2:
                continue
            headers = {_key(cell.value): index for index, cell in enumerate(sheet[1]) if cell.value is not None}
            material_i = col(headers, "material", "catalyst", "system", "sample", "体系", "催化剂")
            adsorption = [
                col(headers, "dgoh", "dgstaroh", "deltagoh", "deltagstaroh"),
                col(headers, "dgo", "dgstaro", "deltago", "deltagstaro"),
                col(headers, "dgooh", "dgstarooh", "deltagooh", "deltagstarooh"),
            ]
            corrected = [col(headers, "gstar", "gsite", "gclean", "gslab"),
                         col(headers, "goh", "gstaroh"),
                         col(headers, "go", "gstaro"),
                         col(headers, "gooh", "gstarooh"),
                         col(headers, "gh2o", "gwater"),
                         col(headers, "gh2", "ghydrogen")]
            raw = [col(headers, "estar", "esite", "eclean", "eslab"),
                   col(headers, "eoh", "estaroh"),
                   col(headers, "eo", "estaro"),
                   col(headers, "eooh", "estarooh"),
                   col(headers, "eh2o", "ewater"),
                   col(headers, "eh2", "ehydrogen")]
            corrections = [col(headers, "corrstar", "correctionstar"),
                           col(headers, "corroh", "correctionoh"),
                           col(headers, "corro", "correctiono"),
                           col(headers, "corrooh", "correctionooh"),
                           col(headers, "corrh2o", "correctionh2o"),
                           col(headers, "corrh2", "correctionh2")]
            custom_rows = None
            if all(i is not None for i in adsorption):
                source_type, columns = "adsorption", adsorption
            elif all(i is not None for i in corrected):
                source_type, columns = "corrected-G", corrected
            elif all(i is not None for i in raw):
                if not all(i is not None for i in corrections):
                    raise ValueError(
                        f"{sheet.title}：仅有 VASP E_DFT 不足以计算 ΔG，请补充 Corr_star/OH/O/OOH/H2O/H2，"
                        "或直接提供 DG_OH、DG_O、DG_OOH。")
                source_type, columns = "DFT+correction", raw
            else:
                legacy = _legacy_orr_records(sheet)
                if not legacy:
                    continue
                source_type, columns, material_i = "legacy-ORR-template", [1, 2, 3], 0
                custom_rows = [(row_i, record) for row_i, record in legacy]
            row_source = (custom_rows if custom_rows is not None else
                          enumerate(sheet.iter_rows(min_row=2, values_only=True), start=2))
            for row_i, row in row_source:
                if all(value is None for value in row):
                    continue
                if material_i is not None:
                    if material_i >= len(row) or row[material_i] is None or not str(row[material_i]).strip():
                        raise ValueError(f"{sheet.title} 第 {row_i} 行缺少 Material / Catalyst。")
                    material = str(row[material_i]).strip()
                else:
                    material = sheet.title
                if material in existing:
                    raise ValueError(f"重复的催化剂名称：{material}。请为每一行使用唯一 Material 名称。")
                values = []
                for index in columns:
                    number = _number(row[index]) if index is not None and index < len(row) else None
                    if number is None:
                        raise ValueError(f"{sheet.title} 第 {row_i} 行 {material} 存在缺失/无效自由能。"
                                         "如果单元格为公式，请先在 Excel 中重新计算并保存。")
                    values.append(number)
                if source_type in ("adsorption", "legacy-ORR-template"):
                    dgoh, dgo, dgooh = values
                else:
                    if source_type == "DFT+correction":
                        adjusted = []
                        for index, corr in zip(values, corrections):
                            number = _number(row[corr]) if corr is not None and corr < len(row) else None
                            if number is None:
                                raise ValueError(f"{sheet.title} 第 {row_i} 行缺少某项 Corr 自由能修正。")
                            adjusted.append(index + number)
                        values = adjusted
                    gs, goh, go, gooh, gh2o, gh2 = values
                    dgoh = goh - gs - (gh2o - .5 * gh2)
                    dgo = go - gs - (gh2o - gh2)
                    dgooh = gooh - gs - (2 * gh2o - 1.5 * gh2)
                # Four one-electron steps in their actual forward reaction order.
                if mode == "OER":
                    changes_0 = [dgoh, dgo - dgoh, dgooh - dgo, 4.92 - dgooh]
                    labels = ["*", "*OH", "*O", "*OOH", "O₂ + *"]
                    changes_u = [value - u_rhe for value in changes_0]
                    limiting = max(changes_0)
                    eta = limiting - 1.23
                    pds = changes_0.index(limiting) + 1
                else:
                    changes_0 = [dgooh - 4.92, dgo - dgooh, dgoh - dgo, -dgoh]
                    labels = ["O₂ + *", "*OOH", "*O", "*OH", "2H₂O + *"]
                    changes_u = [value + u_rhe for value in changes_0]
                    limiting = min(-value for value in changes_0)
                    eta = 1.23 - limiting
                    pds = changes_0.index(max(changes_0)) + 1
                # Match the supplied ORR Origin convention: at U=0, O₂ + *
                # starts at +4.92 eV and the regenerated * ends at 0 eV.
                cumulative = [4.92 if mode == "ORR" else 0.0]
                for value in changes_u:
                    cumulative.append(cumulative[-1] + value)
                # Never force the end to zero at U ≠ 1.23 V vs RHE.
                expected = (4.92 - 4 * u_rhe) if mode == "OER" else 4 * u_rhe
                if not math.isclose(cumulative[-1], expected, rel_tol=0, abs_tol=1e-8):
                    raise ValueError("四步反应自由能与 CHE 反应总能不守恒。")
                profiles.append([(material, state, round(energy, 10))
                                 for state, energy in zip(labels, cumulative)])
                metrics.append({
                    "Material": material, "Mode": mode, "Input": source_type,
                    "DG_OH": dgoh, "DG_O": dgo, "DG_OOH": dgooh,
                    "Potential_V": potential, "Reference": reference,
                    "pH": ph, "U_RHE_V": u_rhe,
                    "U_lim_RHE_V": limiting,
                    "U_lim_selected_V": limiting if reference == "RHE" else limiting - alpha * ph,
                    "Eta_V": eta, "PDS_step": pds,
                    "DG0_steps": changes_0, "DG_U_steps": changes_u,
                })
                existing.add(material)
    finally:
        book.close()
    if not profiles:
        raise ValueError(
            "未识别 ORR/OER 数据。支持五行一组的原始 ORR 模板（催化剂/*OOH/*O/*OH/催化剂，G 列 U=0V），" 
            "或 Material, DG_OH, DG_O, DG_OOH；"
            "或六列 G_star/G_OH/G_O/G_OOH/G_H2O/G_H2；"
            "或六列 E_* 加六列 Corr_* 的完整数据。")
    return profiles, metrics


def export_profiles(profiles, outdir, metrics=None):
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
    if metrics:
        summary = book.create_sheet("CHE Metrics", 0)
        keys = ["Material", "Mode", "Input", "DG_OH", "DG_O", "DG_OOH",
                "Potential_V", "Reference", "pH", "U_RHE_V",
                "U_lim_RHE_V", "U_lim_selected_V", "Eta_V", "PDS_step"]
        summary.append(keys)
        for entry in metrics:
            summary.append([entry[key] for key in keys])
        summary.freeze_panes = "A2"
        summary.auto_filter.ref = summary.dimensions
        for cell in summary[1]:
            cell.font = Font(bold=True, color="FFFFFF")
            cell.fill = PatternFill("solid", fgColor="433E91")
        for col in "ABCDEFGHIJKLMN":
            summary.column_dimensions[col].width = 16 if col != "A" else 24
        step_sheet = book.create_sheet("CHE Four Steps", 1)
        step_sheet.append(["Material", "Reaction", "Step", "DG_U0_eV", "DG_U_eV", "PDS"])
        for entry in metrics:
            for index, (g0, gu) in enumerate(zip(entry["DG0_steps"], entry["DG_U_steps"]), start=1):
                step_sheet.append([entry["Material"], entry["Mode"], index, g0, gu,
                                   index == entry["PDS_step"]])
        step_sheet.freeze_panes = "A2"
        step_sheet.auto_filter.ref = step_sheet.dimensions
        for cell in step_sheet[1]:
            cell.font = Font(bold=True, color="FFFFFF")
            cell.fill = PatternFill("solid", fgColor="433E91")
        for col, width in {"A": 24, "B": 15, "C": 12, "D": 18, "E": 18, "F": 12}.items():
            step_sheet.column_dimensions[col].width = width
    guide = book.create_sheet("Origin Guide")
    guide.append(["数据来源", "HER 自由能台阶；或 ORR/OER 四电子 CHE 反应自由能（eV）"])
    guide.append(["Origin 导入", "File > Import > Single ASCII，选择 step_diagram_origin.dat"])
    guide.append(["Origin 绘图", "X=Step_Index，Y=DeltaG_eV，按 Material 分组"])
    guide.append(["说明", "ORR/OER 数据请看 CHE Metrics / CHE Four Steps 两表；.dat 是 Origin 可导入数据，不会修改 .opju。"])
    xlsx_path = path / "step_diagram.xlsx"
    book.save(xlsx_path)
    return str(csv_path), str(xlsx_path), str(dat_path)


def draw_profiles(profiles, outdir, mode="HER"):
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
    if mode == "HER" and len(profiles) > 1:
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
    parser.add_argument("--mode", choices=("HER", "ORR", "OER"), default="HER")
    parser.add_argument("--potential", type=float, default=0.0)
    parser.add_argument("--ph", type=float, default=0.0)
    parser.add_argument("--reference", choices=("RHE", "SHE"), default="RHE")
    args = parser.parse_args()
    input_path = Path(args.input)
    if not input_path.is_file():
        raise FileNotFoundError(str(input_path))
    if input_path.suffix.lower() not in (".xlsx", ".xlsm"):
        raise ValueError("台阶图输入必须为 Excel .xlsx 或 .xlsm 文件。")
    metrics = []
    if args.mode == "HER":
        profiles = read_profiles(input_path)
        message = f"识别 {len(profiles)} 个体系：" + "；".join(
            f"{p[0][0]} ({len(p)-2} 个中间态)" for p in profiles)
    else:
        profiles, metrics = read_oxygen_profiles(
            input_path, args.mode, args.potential, args.ph, args.reference)
        message = (f"{args.mode} | U={args.potential:.3f} V vs {args.reference}, pH={args.ph:g}；"
                   f"参考 298.15 K，4e CHE；" +
                   "；".join(f"{item['Material']}: η={item['Eta_V']:.3f} V,"
                            f" U_L={item['U_lim_selected_V']:.3f} V vs {args.reference},"
                            f" PDS=第 {item['PDS_step']} 步"
                            for item in metrics))
    result = {"Summary": message, "SeriesCount": len(profiles),
              "Png": "", "Svg": "", "Csv": "", "Xlsx": "", "Dat": ""}
    if args.action == "analyze":
        if not args.outdir:
            raise ValueError("缺少输出目录。")
        result["Csv"], result["Xlsx"], result["Dat"] = export_profiles(profiles, args.outdir, metrics)
        result["Png"], result["Svg"] = draw_profiles(profiles, args.outdir, args.mode)
    print(json.dumps(result, ensure_ascii=False))


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print(f"{type(error).__name__}: {error}", file=sys.stderr)
        sys.exit(1)
