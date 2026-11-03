#!/usr/bin/env python3
"""DFTDesk: HER reaction ladders and 4e ORR/OER computational-hydrogen-electrode diagrams.

HER accepts the supplied A-G block worksheet (E(DFT), correction, G, reference),
the historical A-I block layout, or Material/State/DeltaG long tables.
ORR/OER accept catalyst adsorption Gibbs free energies or properly corrected
absolute VASP Gibbs energies, and retain the potential-dependent reaction endpoint. Template downloads preserve
both original example workbooks; raw VASP energies take precedence over stale cached G.
All plotting data remain exportable as Excel, CSV and Origin tab-separated DAT.
"""
import argparse
import csv
import json
import math
import os
import shutil
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


def create_input_template(outdir, mode):
    """Copy the original, user-supplied HER or ORR/OER Excel workbook verbatim."""
    name = ("DFTDesk_HER_Template.xlsx" if mode == "HER"
            else "DFTDesk_ORR_OER_Template.xlsx")
    source = Path(__file__).with_name(name)
    if not source.is_file():
        raise FileNotFoundError("缺少随程序提供的 Excel 原始模板：" + str(source))
    target = Path(outdir) / name
    Path(outdir).mkdir(parents=True, exist_ok=True)
    shutil.copyfile(source, target)
    return str(target)


def _states_from_template(sheet):
    """Read editable adsorption-energy blocks, including original 7-column HER sheets.

    A-G reference rows use E='H2O' or '1/2H2', and intermediate
    energy corrections are in C. Older A-I sheets put the reference
    in G and corrections in E. Absolute free energies are recomputed
    from the raw energy/correction inputs rather than cached formulas.
    """
    groups = OrderedDict()
    current = None
    baseline = None
    reference = None
    correction_col = None
    relative_col = None
    h2o_raw, h2_raw = _number(sheet["B2"].value), _number(sheet["B3"].value)
    gas_h2o = h2o_raw + (_number(sheet["E2"].value) or 0.0) if h2o_raw is not None else None
    gas_h2 = h2_raw + (_number(sheet["E3"].value) or 0.0) if h2_raw is not None else None
    gas_o2 = _number(sheet["F4"].value)
    references = {"H2O": gas_h2o,
                  "1/2H2": gas_h2 / 2 if gas_h2 is not None else None,
                  "H2": gas_h2, "O2": gas_o2}
    for row in sheet.iter_rows(min_row=2, values_only=True):
        label = row[0] if row else None
        raw_energy = _number(row[1]) if len(row) > 1 else None
        col_e = row[4] if len(row) > 4 else None
        col_g = row[6] if len(row) > 6 else None
        # This matches both the newly supplied A-G template and older A-I files.
        if isinstance(label, str) and label.strip():
            source_ref = next((value for value in (col_e, col_g)
                               if isinstance(value, str) and
                               value.strip() in references), None)
            if source_ref is not None:
                current = label.strip()
                baseline = raw_energy
                reference = source_ref.strip()
                correction_col = 2 if col_e == source_ref else 4
                relative_col = 6 if col_e == source_ref else 8
                groups.setdefault(current, [])
                continue
        if not isinstance(label, str) or not label.strip() or current is None:
            continue
        correction = (_number(row[correction_col])
                      if correction_col is not None and len(row) > correction_col else None)
        relative = (_number(row[relative_col])
                    if relative_col is not None and len(row) > relative_col else None)
        gas = references.get(reference)
        if raw_energy is not None and baseline is not None and gas is not None:
            energy = raw_energy + (correction or 0.0) - baseline - gas
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
        raise ValueError("未识别台阶图数据。HER 支持七列原始分块模板、旧版九列模板，或 Material / State / DeltaG 长表。")
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
        # The downloadable template is intentionally empty; unused blocks
        # must not block other catalysts that the user has filled in.
        if all(_number(cell(row, 1)) is None and
               _number(cell(row, 5)) is None and
               _number(cell(row, 6)) is None for row in block):
            index += 5
            continue

        # Recompute from editable DFT B/C values first. The supplied
        # ORR/OER workbook has static D/F/G values (no Excel formulas), so
        # its G column may be stale after the user changes a catalyst.
        slab_energies = [_number(cell(row, 1)) for row in block]
        ads_corrections = [_number(cell(block[k], 2)) for k in (1, 2, 3)]
        if (h2o is not None and h2 is not None and
                all(value is not None for value in slab_energies) and
                all(value is not None for value in ads_corrections)):
            clean = slab_energies[4]
            ooh = slab_energies[1] + ads_corrections[0] - clean - 2 * h2o + 1.5 * h2
            o = slab_energies[2] + ads_corrections[1] - clean - h2o + h2
            oh = slab_energies[3] + ads_corrections[2] - clean - h2o + 0.5 * h2
            energies = [4.92, ooh, o, oh, 0.0]
        else:
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


def draw_profiles(profiles, outdir, mode="HER", metrics=None):
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
    plt.rcParams["svg.fonttype"] = "none"  # Editable text labels in Origin/Inkscape

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
    che_metrics = ({item["Material"]: item for item in metrics or []}
                   if mode in ("ORR", "OER") else {})
    panel_height = max(4.1, 3.8 * len(panels),
                       (2.1 + .66 * len(che_metrics)) if che_metrics else 0)
    if che_metrics:
        fig = plt.figure(figsize=(13.6, panel_height))
        grid = fig.add_gridspec(len(panels), 2, width_ratios=(3.65, 1.55),
                               wspace=.24, hspace=.35)
        axes = [fig.add_subplot(grid[i, 0]) for i in range(len(panels))]
        summaries = [fig.add_subplot(grid[i, 1]) for i in range(len(panels))]
    else:
        fig, grid_axes = plt.subplots(len(panels), 1,
                                     figsize=(10.6, panel_height), squeeze=False)
        axes = [grid_axes[i][0] for i in range(len(panels))]
        summaries = [None] * len(panels)
    for panel_index, (title, paths) in enumerate(panels):
        ax = axes[panel_index]
        all_energies = []
        for profile in paths:
            material = profile[0][0]
            levels = [float(row[2]) for row in profile]
            all_energies.extend(levels)
            color = palette[material]
            metadata = che_metrics.get(material)
            pds_step = metadata["PDS_step"] if metadata else None
            for index, energy in enumerate(levels):
                ax.hlines(energy, index - .31, index + .31,
                          color=color, lw=2.6, zorder=3,
                          label=material if index == 0 else None)
                if index + 1 < len(levels):
                    is_pds = pds_step == index + 1
                    ax.plot([index + .31, index + .69],
                            [energy, levels[index + 1]],
                            color=color, lw=3.4 if is_pds else 1.1,
                            ls="-" if is_pds else (0, (4, 3)),
                            zorder=5 if is_pds else 2)
                    if is_pds:
                        midpoint = (energy + levels[index + 1]) / 2
                        ax.scatter([index + .5], [midpoint], s=67, marker="D",
                                   color=color, edgecolor="white", linewidth=.9,
                                   zorder=6)
                        if len(paths) == 1:
                            ax.annotate("PDS", (index + .5, midpoint),
                                        xytext=(9, 11), textcoords="offset points",
                                        fontsize=9, fontweight="bold", color=color,
                                        bbox=dict(boxstyle="round,pad=.25",
                                                  fc="white", ec=color, lw=1))
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
        sidebar = summaries[panel_index]
        if sidebar is not None:
            sidebar.set_axis_off()
            sidebar.text(.03, .99, f"{mode} | CHE 指标",
                         va="top", fontsize=13, fontweight="bold", color="#243F72",
                         transform=sidebar.transAxes)
            count = max(len(paths), 1)
            pitch = .88 / count
            for i, profile in enumerate(paths):
                material = profile[0][0]
                info = che_metrics.get(material)
                if not info:
                    continue
                short = count > 7
                desc = (f"{material}\nη = {info['Eta_V']:.3f} V · PDS 第 {info['PDS_step']} 步"
                        if short else
                        f"{material}\nη = {info['Eta_V']:.3f} V\nPDS：第 {info['PDS_step']} 步")
                sidebar.text(.03, .90 - i * pitch, desc, ha="left", va="top",
                             fontsize=8.6 if short else 10.1, linespacing=1.32,
                             fontweight="semibold", color=palette[material],
                             bbox=dict(boxstyle="round,pad=.52", fc="#F5F7FC",
                                       ec=palette[material], lw=1.15, alpha=.98),
                             transform=sidebar.transAxes)
            sidebar.text(.03, .02, "PDS = 热力学电位决定步骤\nRDS 需另算动力学能垒",
                         fontsize=8, color="#626B7D", transform=sidebar.transAxes,
                         va="bottom")
    fig.tight_layout()
    png = str(Path(outdir) / "step_diagram.png")
    svg = str(Path(outdir) / "step_diagram.svg")
    fig.savefig(png, dpi=240, bbox_inches="tight")
    fig.savefig(svg, bbox_inches="tight")
    plt.close(fig)
    return png, svg

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--action", choices=("inspect", "analyze", "template"), default="analyze")
    parser.add_argument("--input")
    parser.add_argument("--outdir")
    parser.add_argument("--mode", choices=("HER", "ORR", "OER"), default="HER")
    parser.add_argument("--potential", type=float, default=0.0)
    parser.add_argument("--ph", type=float, default=0.0)
    parser.add_argument("--reference", choices=("RHE", "SHE"), default="RHE")
    args = parser.parse_args()
    if args.action == "template":
        if not args.outdir:
            raise ValueError("生成 Excel 模板需要指定输出目录。")
        Path(args.outdir).mkdir(parents=True, exist_ok=True)
        template_path = create_input_template(args.outdir, args.mode)
        print(json.dumps({"Template": template_path}, ensure_ascii=False))
        return
    if not args.input:
        raise ValueError("请先选择 Excel 原始数据文件。")
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
        result["Png"], result["Svg"] = draw_profiles(profiles, args.outdir, args.mode, metrics)
    print(json.dumps(result, ensure_ascii=False))


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print(f"{type(error).__name__}: {error}", file=sys.stderr)
        sys.exit(1)
