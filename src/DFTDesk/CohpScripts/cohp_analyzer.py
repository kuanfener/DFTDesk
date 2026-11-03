#!/usr/bin/env python3
"""DFTDesk LOBSTER COHP/ICOHP backend. Parses authoritative LOBSTER outputs via pymatgen."""
import argparse
import csv
import json
import math
from pathlib import Path

import numpy as np

SPINS = ("up", "down")


def _spin_dict(values):
    from pymatgen.electronic_structure.core import Spin
    if not values:
        return {}
    output = {}
    for spin, key in ((Spin.up, "up"), (Spin.down, "down")):
        if spin in values:
            output[key] = values[spin]
    return output


def _companion(source, prefix):
    is_lcfo = ".lcfo." in source.name.lower()
    candidates = ([prefix + ".LCFO.lobster", prefix + ".LCFO.lobster.gz"]
                  if is_lcfo else [prefix + ".lobster", prefix + ".lobster.gz"])
    for candidate in candidates:
        path = source.parent / candidate
        if path.is_file():
            return path
    # Accept names with different casing from Windows, without selecting another LOBSTER mode.
    for path in source.parent.iterdir():
        if path.name.lower() in {candidate.lower() for candidate in candidates} and path.is_file():
            return path
    return None


def _load(source):
    from pymatgen.io.lobster import Cohpcar, Icohplist
    if not source.is_file():
        raise FileNotFoundError(f"输入文件不存在：{source}")
    name = source.name.lower()
    if name.startswith("cohpcar"):
        cohp_path, list_path = source, _companion(source, "ICOHPLIST")
    elif name.startswith("icohplist"):
        list_path, cohp_path = source, _companion(source, "COHPCAR")
    else:
        raise ValueError("请选择 LOBSTER 的 COHPCAR.lobster 或 ICOHPLIST.lobster 文件。")

    cohp = None
    if cohp_path is not None:
        cohp = Cohpcar(filename=str(cohp_path), is_lcfo=".lcfo." in cohp_path.name.lower())
    icohp_list = None
    if list_path is not None:
        icohp_list = Icohplist(filename=str(list_path),
                              is_lcfo=".lcfo." in list_path.name.lower()).icohplist
    if cohp is None and not icohp_list:
        raise ValueError("无法读取 COHPCAR/ICOHPLIST 键数据。")

    labels = []
    if cohp is not None:
        labels.extend(str(label) for label, values in cohp.cohp_data.items()
                      if str(label).lower() != "average" and values.get("COHP"))
    if icohp_list and cohp is None:
        labels.extend(str(label) for label in icohp_list)
    # Keep original order of COHPCAR bond labels; prefer its available curves.
    labels = list(dict.fromkeys(labels))
    if not labels:
        raise ValueError("文件中没有可用的两中心 COHP/ICOHP 键。")
    energies = np.asarray(cohp.energies, dtype=float) if cohp is not None else None
    if energies is not None and (len(energies) < 2 or np.any(np.diff(energies) <= 0)):
        raise ValueError("COHPCAR 能量网格无效。")
    bonds = []
    for label in labels:
        data = cohp.cohp_data.get(label, {}) if cohp is not None else {}
        listed = (icohp_list or {}).get(label, {})
        length = float(listed.get("length") or data.get("length") or 0)
        number = int(listed.get("number_of_bonds") or 1)
        if listed and "icohp" in listed:
            values = {spin: float(value) for spin, value
                      in _spin_dict(listed["icohp"]).items()}
        elif cohp is not None and data.get("ICOHP") is not None:
            curves = _spin_dict(data["ICOHP"])
            values = {spin: float(np.interp(0, energies, np.asarray(curve, dtype=float)))
                      for spin, curve in curves.items()}
        else:
            values = {}
        if data.get("sites") is not None:
            sites = data["sites"]
            site_label = "-".join(str(int(x) + 1) for x in sites)
        else:
            site_label = ""
        display = f"键 {label}" + (f"（原子 {site_label}）" if site_label else "")
        if length:
            display += f" · {length:.3f} Å"
        bonds.append({"Label": label, "Description": display,
                      "Length": length, "Count": number,
                      "IcohpUp": values.get("up"), "IcohpDown": values.get("down")})
    return {"cohp": cohp, "list": icohp_list or {}, "energies": energies,
            "bonds": bonds, "source": str(source.name)}


def _metadata(data):
    energies = data["energies"]
    cohp = data["cohp"]
    return {"Bonds": data["bonds"], "HasCurves": cohp is not None,
            "SpinPolarized": (bool(cohp.is_spin_polarized) if cohp is not None
                              else any(bond["IcohpDown"] is not None for bond in data["bonds"])),
            "EnergyMin": float(energies[0]) if energies is not None else 0.0,
            "EnergyMax": float(energies[-1]) if energies is not None else 0.0,
            "Info": data["source"]}


def _analyze(data, args):
    from openpyxl import Workbook
    cohp = data["cohp"]
    available = {bond["Label"]: bond for bond in data["bonds"]}
    labels = list(dict.fromkeys(label for label in args.labels.split(",") if label))
    if not labels or any(label not in available for label in labels):
        raise ValueError("请在列表中选择至少一个有效的键。")
    if args.spin not in ("both", "up", "down"):
        raise ValueError("无效的自旋选择。")
    if args.spin == "down" and not _metadata(data)["SpinPolarized"]:
        raise ValueError("当前文件不包含自旋向下的数据。")
    chosen_spins = [spin for spin in SPINS if args.spin == "both" or spin == args.spin]
    bonds = [available[label] for label in labels]
    output = Path(args.outdir)
    output.mkdir(parents=True, exist_ok=True)
    csv_file = output / "cohp.csv"
    dat_file = output / "cohp_origin.dat"
    xlsx_file = output / "cohp.xlsx"
    png_file = output / "cohp.png"
    svg_file = output / "cohp.svg"

    summary_rows = []
    for bond in bonds:
        up = bond["IcohpUp"] if "up" in chosen_spins else None
        down = bond["IcohpDown"] if "down" in chosen_spins else None
        total = sum(x for x in (up, down) if x is not None)
        summary_rows.append([bond["Label"], bond["Description"],
                             bond["Length"], bond["Count"], up, down, total])
    heading = ["Bond label", "Bond description", "Length (Angstrom)", "Multiplicity",
               "ICOHP(Ef) up (eV)", "ICOHP(Ef) down (eV)", "ICOHP(Ef) selected spins sum (eV)"]

    energies = data["energies"]
    curve_rows, curve_headers = [], []
    if cohp is not None:
        if not (math.isfinite(args.emin) and math.isfinite(args.emax) and args.emax > args.emin):
            raise ValueError("能量窗口无效：下限应小于上限。")
        lower, upper = max(args.emin, float(energies[0])), min(args.emax, float(energies[-1]))
        if upper <= lower:
            raise ValueError("所选能量窗口与 COHPCAR 没有交集。")
        columns = []
        for label in labels:
            cohp_dict = cohp.cohp_data[label]
            for spin in chosen_spins:
                for kind in ("COHP", "ICOHP"):
                    value = _spin_dict(cohp_dict.get(kind)).get(spin)
                    if value is not None:
                        columns.append((label, spin, kind, np.asarray(value, dtype=float)))
        if not columns:
            raise ValueError("选择的键没有可用 COHP 曲线。")
        curve_headers = ["Energy-Ef (eV)"] + [
            f"{kind}_{label}_{spin}" for label, spin, kind, _ in columns]
        curve_rows = [[float(e)] + [float(array[i]) for _, _, _, array in columns]
                      for i, e in enumerate(energies)]

        import matplotlib
        matplotlib.use("Agg")
        import matplotlib.pyplot as plt
        fig, axes = plt.subplots(1, 2, figsize=(12, 6.5), dpi=150,
                                 sharey=True, layout="constrained")
        palette = plt.get_cmap("tab20")
        sign = -1 if args.negative else 1
        for i, label in enumerate(labels):
            curve_data = cohp.cohp_data[label]
            for spin in chosen_spins:
                co = _spin_dict(curve_data.get("COHP")).get(spin)
                ico = _spin_dict(curve_data.get("ICOHP")).get(spin)
                style = "-" if spin == "up" else "--"
                if co is not None:
                    axes[0].plot(sign * np.asarray(co, dtype=float), energies,
                                 label=f"{label} {spin}", color=palette(i % 20),
                                 linestyle=style, linewidth=1.35)
                if ico is not None:
                    axes[1].plot(sign * np.asarray(ico, dtype=float), energies,
                                 label=f"{label} {spin}", color=palette(i % 20),
                                 linestyle=style, linewidth=1.35)
        for ax, name in zip(axes, ("COHP", "ICOHP")):
            ax.axvline(0, color="#666666", lw=0.85)
            ax.axhline(0, color="#555555", lw=0.85, linestyle=":")
            ax.set_ylim(lower, upper)
            ax.set_xlabel(("-" if args.negative else "") + name + (" (eV)" if name == "ICOHP" else ""))
            ax.set_title(name + " by LOBSTER")
            ax.grid(True, alpha=.18)
            if len(labels) <= 10:
                ax.legend(loc="best", fontsize=8)
        axes[0].set_ylabel(r"$E-E_F$ (eV)")
        fig.savefig(png_file)
        fig.savefig(svg_file)
        plt.close(fig)

    with csv_file.open("w", newline="", encoding="utf-8-sig") as handle:
        writer = csv.writer(handle)
        if curve_rows:
            writer.writerow(curve_headers)
            writer.writerows(curve_rows)
        else:
            writer.writerow(heading)
            writer.writerows(summary_rows)
    with dat_file.open("w", newline="", encoding="utf-8-sig") as handle:
        writer = csv.writer(handle, delimiter="\t")
        if curve_rows:
            writer.writerow(["Energy_minus_Ef_eV"] +
                            [column.replace(" ", "_") for column in curve_headers[1:]])
            writer.writerows(curve_rows)
        else:
            writer.writerow(heading)
            writer.writerows(summary_rows)
    workbook = Workbook()
    sheet = workbook.active
    sheet.title = "ICOHP at Ef"
    sheet.append(heading)
    for row in summary_rows:
        sheet.append(row)
    sheet.freeze_panes = "A2"
    sheet.auto_filter.ref = sheet.dimensions
    if curve_rows:
        curves = workbook.create_sheet("COHP curves")
        curves.append(curve_headers)
        for row in curve_rows:
            curves.append(row)
        curves.freeze_panes = "B2"
        curves.auto_filter.ref = curves.dimensions
    workbook.save(xlsx_file)
    notes = ["ICOHP(Ef) 是 LOBSTER 给出的积分值；负值通常代表成键贡献。",
             "COHP/ICOHP 曲线与导出数据按键编号排列；CSV/Excel/Origin DAT 中保留原始 COHP 符号。"]
    if cohp is None:
        notes.append("仅有 ICOHPLIST：已生成 ICOHP(Ef) 表格；绘图需要 COHPCAR.lobster。")
    else:
        notes.append(f"曲线能量窗口 [{lower:.3f}, {upper:.3f}] eV（相对 Ef）。")
    for bond in bonds:
        up = bond["IcohpUp"]
        down = bond["IcohpDown"]
        channel = f"↑ {up:.4f}" if up is not None and "up" in chosen_spins else ""
        if down is not None and "down" in chosen_spins:
            channel += f"；↓ {down:.4f}"
        notes.append(f'{bond["Description"]}：ICOHP(Ef) {channel or "无数据"} eV')
    result = _metadata(data)
    result.update({"Csv": str(csv_file), "Dat": str(dat_file),
                   "Xlsx": str(xlsx_file), "Png": str(png_file) if cohp is not None else "",
                   "Svg": str(svg_file) if cohp is not None else "",
                   "Summary": "\n".join(notes)})
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--action", choices=("check", "inspect", "analyze"), required=True)
    parser.add_argument("--input")
    parser.add_argument("--labels", default="")
    parser.add_argument("--spin", default="both")
    parser.add_argument("--emin", type=float, default=-8)
    parser.add_argument("--emax", type=float, default=4)
    parser.add_argument("--negative", action="store_true")
    parser.add_argument("--outdir")
    args = parser.parse_args()
    if args.action == "check":
        from importlib.metadata import version
        from pymatgen.io.lobster import Cohpcar, Icohplist
        from openpyxl import Workbook
        import matplotlib
        print(json.dumps({"Version": f"pymatgen {version('pymatgen')} · NumPy {np.__version__} · openpyxl {version('openpyxl')} · Matplotlib {matplotlib.__version__}"}))
        return
    if not args.input:
        parser.error("--input is required")
    data = _load(Path(args.input).resolve())
    if args.action == "inspect":
        result = _metadata(data)
    else:
        if not args.outdir:
            parser.error("--outdir is required")
        result = _analyze(data, args)
    print(json.dumps(result, ensure_ascii=True, allow_nan=False))


if __name__ == "__main__":
    main()
