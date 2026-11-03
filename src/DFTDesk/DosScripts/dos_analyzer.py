#!/usr/bin/env python3
"""DFTDesk DOS backend. Uses pymatgen for VASP XML / POSCAR and NumPy for DOSCAR."""
import argparse
import csv
import json
import math
from pathlib import Path

import numpy as np

ORBITALS = ("s", "p", "d")
SPINS = ("up", "down")


def _channels(densities):
    from pymatgen.electronic_structure.core import Spin

    result = {"up": np.asarray(densities[Spin.up], dtype=float)}
    if Spin.down in densities:
        result["down"] = np.asarray(densities[Spin.down], dtype=float)
    return result


def _vasprun(path):
    from pymatgen.io.vasp import Vasprun

    run = Vasprun(str(path), parse_dos=True, parse_eigen=False,
                  parse_projected_eigen=False, parse_potcar_file=False)
    dos = run.complete_dos
    if dos is None:
        raise ValueError("vasprun.xml 不包含 DOS 数据；请检查 NEDOS / 计算是否完成。")
    symbols = sorted({site.specie.symbol for site in dos.structure})
    projected = {}
    if dos.pdos:
        for symbol in symbols:
            spd = dos.get_element_spd_dos(symbol)
            projected[symbol] = {
                orbital.name: _channels(curve.densities)
                for orbital, curve in spd.items() if orbital.name in ORBITALS
            }
    return np.asarray(dos.energies, dtype=float), float(dos.efermi), \
        _channels(dos.densities), projected, "vasprun.xml"


def _parse_lines(handle, count, label):
    rows = []
    for _ in range(count):
        line = handle.readline()
        if not line:
            raise ValueError(f"DOSCAR {label} 数据不完整：预期 {count} 行。")
        rows.append([float(x) for x in line.split()])
    if len({len(row) for row in rows}) != 1:
        raise ValueError(f"DOSCAR {label} 列数不一致。")
    return np.asarray(rows, dtype=float)


def _doscar(path):
    from pymatgen.io.vasp import Poscar

    with path.open("r", encoding="utf-8", errors="replace") as handle:
        initial = handle.readline().split()
        if not initial:
            raise ValueError("DOSCAR 文件为空。")
        nions = int(initial[0])
        for _ in range(4):
            if not handle.readline():
                raise ValueError("DOSCAR 文件头不完整。")
        head = handle.readline().split()
        if len(head) < 4:
            raise ValueError("DOSCAR 缺少 EMAX / EMIN / NEDOS / EFERMI。")
        nedos, efermi = int(float(head[2])), float(head[3])
        if nions < 1 or nedos < 2:
            raise ValueError("DOSCAR 中的离子数或 NEDOS 无效。")
        total = _parse_lines(handle, nedos, "总 DOS")
        if total.shape[1] not in (3, 5):
            raise ValueError("暂不支持非共线或特殊格式的 DOSCAR 总 DOS。")
        spin_count = 2 if total.shape[1] == 5 else 1
        energies = total[:, 0]
        if np.any(np.diff(energies) <= 0):
            raise ValueError("DOSCAR 能量网格应严格递增。")
        tdos = {"up": total[:, 1]}
        if spin_count == 2:
            tdos["down"] = total[:, 2]

        first_header = handle.readline()
        if not first_header.strip():
            return energies, efermi, tdos, {}, "DOSCAR（仅 TDOS，缺少逐原子 PDOS）"

        companion = next((path.with_name(name) for name in ("POSCAR", "CONTCAR")
                          if path.with_name(name).is_file()), None)
        if companion is None:
            return energies, efermi, tdos, {}, \
                "DOSCAR（需要同目录 POSCAR/CONTCAR 才能按元素汇总 PDOS）"

        symbols = [site.specie.symbol for site in Poscar.from_file(str(companion)).structure]
        if len(symbols) != nions:
            raise ValueError(f"{companion.name} 有 {len(symbols)} 个原子，但 DOSCAR 为 {nions} 个；请使用同一次计算的结构。")
        projected = {}
        for atom_index in range(nions):
            if atom_index:
                if not handle.readline():
                    raise ValueError("DOSCAR 逐原子 PDOS 块数量不足。")
            block = _parse_lines(handle, nedos, f"原子 {atom_index + 1}")
            if not np.allclose(block[:, 0], energies, atol=2e-3, rtol=0):
                raise ValueError(f"DOSCAR 原子 {atom_index + 1} 的能量网格与总 DOS 不匹配。")
            columns = block.shape[1] - 1
            if columns % spin_count:
                raise ValueError("DOSCAR 自旋与 PDOS 列数不匹配。")
            groups = columns // spin_count
            if groups in (1, 2, 3, 4):  # LORBIT=10: s, p, d, (f)
                orbitals = ("s", "p", "d", "f")[:groups]
            elif groups in (9, 16):  # LORBIT=11: s, px3, dx5, (fx7)
                orbitals = ("s",) + ("p",) * 3 + ("d",) * 5
                if groups == 16:
                    orbitals += ("f",) * 7
            else:
                raise ValueError("暂不支持此 DOSCAR PDOS 列格式（包括非共线/SOC 四分量数据）。")
            symbol = symbols[atom_index]
            per_element = projected.setdefault(symbol, {})
            for idx, orbital in enumerate(orbitals):
                if orbital not in ORBITALS:
                    continue
                per_orbital = per_element.setdefault(orbital, {})
                for spin_idx, spin in enumerate(SPINS[:spin_count]):
                    if spin not in per_orbital:
                        per_orbital[spin] = np.zeros(nedos)
                    per_orbital[spin] += block[:, 1 + idx * spin_count + spin_idx]
        return energies, efermi, tdos, projected, f"DOSCAR + {companion.name}"


def load(path):
    if not path.is_file():
        raise FileNotFoundError(f"输入文件不存在：{path}")
    if path.name.lower().startswith("doscar"):
        return _doscar(path)
    if path.name.lower().endswith(".xml") or path.name.lower().endswith(".xml.gz"):
        return _vasprun(path)
    raise ValueError("请选择 VASP DOSCAR 或 vasprun.xml 文件。")


def _metadata(data):
    energies, efermi, tdos, projections, source = data
    return {
        "Elements": sorted(projections),
        "HasPdos": bool(projections),
        "SpinPolarized": "down" in tdos,
        "Fermi": efermi,
        "EnergyMin": float(energies[0] - efermi),
        "EnergyMax": float(energies[-1] - efermi),
        "Info": source,
    }


def _window_integral(energy, values, left, right):
    inside = energy[(energy > left) & (energy < right)]
    x = np.concatenate(([left], inside, [right]))
    y = np.interp(x, energy, values)
    integrate = getattr(np, "trapezoid", None) or np.trapz
    return float(integrate(y, x)), float(integrate(x * y, x))


def _analyze(data, args):
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt

    energy, efermi, tdos, projected, _ = data
    relative = energy - efermi
    lower = max(float(args.emin), float(relative[0]))
    upper = min(float(args.emax), float(relative[-1]))
    if not (math.isfinite(lower) and math.isfinite(upper) and upper > lower):
        raise ValueError("选择的能量区间与 DOS 数据无交集。")
    if args.emin >= args.emax:
        raise ValueError("能量窗口必须满足最小值 < 最大值。")
    elements = [x for x in args.elements.split(",") if x]
    orbitals = [x for x in args.orbitals.split(",") if x]
    if any(x not in projected for x in elements):
        raise ValueError("元素选择与当前文件的 PDOS 不一致，请重新导入文件。")
    if any(x not in ORBITALS for x in orbitals):
        raise ValueError("只支持 s / p / d 轨道。")
    if args.spin not in ("both", "up", "down"):
        raise ValueError("无效的自旋选择。")
    if args.spin == "down" and "down" not in tdos:
        raise ValueError("此文件是非自旋极化计算，没有 spin-down 数据。")
    chosen_spins = [s for s in SPINS if s in tdos and (args.spin == "both" or s == args.spin)]
    curves = []
    for spin in chosen_spins:
        curves.append((f"TDOS {spin}", tdos[spin], spin, "#7f8490", "-"))
    colors = {"Pt": "#cf922e", "Fe": "#377fce", "Co": "#258f6e", "Ni": "#8361c5"}
    palette = plt.get_cmap("tab10")
    columns = [(f"TDOS_{s}", tdos[s]) for s in chosen_spins]
    for i, element in enumerate(elements):
        for orbital in orbitals:
            for spin in chosen_spins:
                values = projected[element].get(orbital, {}).get(spin)
                if values is None:
                    continue
                label = f"{element}-{orbital} {spin}"
                color = colors.get(element, palette(i % 10))
                curves.append((label, values, spin, color,
                               {"s": ":", "p": "--", "d": "-"}[orbital]))
                columns.append((f"{element}_{orbital}_{spin}", values))
    if not curves:
        raise ValueError("没有可绘制的 DOS 数据。")

    outdir = Path(args.outdir)
    outdir.mkdir(parents=True, exist_ok=True)
    png, svg, csv_path = outdir / "dos.png", outdir / "dos.svg", outdir / "dos.csv"
    xlsx_path, dat_path = outdir / "dos.xlsx", outdir / "dos_origin.dat"
    fig, ax = plt.subplots(figsize=(10, 5.5), dpi=150, layout="constrained")
    for label, values, spin, color, linestyle in curves:
        sign = -1 if spin == "down" else 1
        ax.plot(relative, sign * values, label=label, color=color,
                linestyle=linestyle, linewidth=1.5 if label.startswith("TDOS") else 1.25,
                alpha=.8 if label.startswith("TDOS") else .95)
    ax.axvline(0, color="#333333", lw=1.0, linestyle="--", alpha=.75)
    ax.axhline(0, color="#888888", lw=.7)
    ax.set_xlim(lower, upper)
    ax.set_xlabel(r"$E-E_F$ (eV)")
    ax.set_ylabel("Density of states (states/eV)")
    ax.set_title("VASP DOS / PDOS" + ("  (spin down mirrored)" if "down" in chosen_spins else ""))
    ax.grid(True, alpha=.17)
    if len(curves) <= 16:
        ax.legend(loc="best", fontsize=8, ncol=2 if len(curves) > 7 else 1)
    else:
        ax.legend(loc="upper center", bbox_to_anchor=(.5, -.2), ncol=4, fontsize=7)
    fig.savefig(png)
    fig.savefig(svg)
    plt.close(fig)

    with csv_path.open("w", newline="", encoding="utf-8-sig") as handle:
        writer = csv.writer(handle)
        writer.writerow(["Energy-Ef (eV)"] + [name for name, _ in columns])
        for index, value in enumerate(relative):
            writer.writerow([float(value)] + [float(values[index]) for _, values in columns])

    with dat_path.open("w", newline="", encoding="utf-8-sig") as handle:
        writer = csv.writer(handle, delimiter="\t")
        writer.writerow(["Energy_minus_Ef_eV"] + [name for name, _ in columns])
        for index, value in enumerate(relative):
            writer.writerow([float(value)] + [float(values[index]) for _, values in columns])

    summary = [f"E_F = {efermi:.4f} eV；积分能窗 [{lower:.3f}, {upper:.3f}] eV（相对 E_F）",
               "下自旋在图中镜像为负值，CSV 保留非负的原始 DOS。"]
    for element in elements:
        for orbital in orbitals:
            channels = projected[element].get(orbital, {})
            for spin in chosen_spins:
                if spin in channels:
                    area, _ = _window_integral(relative, channels[spin], lower, upper)
                    summary.append(f"{element}-{orbital} DOS 积分 ({spin})：{area:.4f} states")
    for element in elements:
        dcurves = projected[element].get("d", {})
        valid = [s for s in chosen_spins if s in dcurves]
        if not valid:
            summary.append(f"{element}：没有 d-PDOS，无法计算 d 带中心。")
            continue
        for spin in valid + (["combined"] if len(valid) == 2 else []):
            values = (sum((dcurves[s] for s in valid), np.zeros_like(relative))
                      if spin == "combined" else dcurves[spin])
            integral, moment = _window_integral(relative, values, lower, upper)
            center = f"{moment / integral:.4f} eV" if abs(integral) > 1e-12 else "无有效 d 态"
            summary.append(f"{element} d 带中心 ({spin})：{center}；d-DOS 积分：{integral:.4f}")
    from openpyxl import Workbook
    workbook = Workbook()
    sheet = workbook.active
    sheet.title = "DOS"
    sheet.append(["Energy-Ef (eV)"] + [name for name, _ in columns])
    for index, value in enumerate(relative):
        sheet.append([float(value)] + [float(values[index]) for _, values in columns])
    sheet.freeze_panes = "B2"
    sheet.auto_filter.ref = sheet.dimensions
    details = workbook.create_sheet("Summary")
    for line in summary:
        details.append([line])
    workbook.save(xlsx_path)
    result = _metadata(data)
    result.update({"Png": str(png), "Svg": str(svg), "Csv": str(csv_path),
                   "Xlsx": str(xlsx_path), "Dat": str(dat_path),
                   "Summary": "\n".join(summary)})
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--action", choices=("check", "inspect", "analyze"), required=True)
    parser.add_argument("--input")
    parser.add_argument("--elements", default="")
    parser.add_argument("--orbitals", default="s,p,d")
    parser.add_argument("--spin", default="both")
    parser.add_argument("--emin", type=float, default=-8.0)
    parser.add_argument("--emax", type=float, default=4.0)
    parser.add_argument("--outdir")
    args = parser.parse_args()
    if args.action == "check":
        from importlib.metadata import version
        import matplotlib
        import openpyxl
        print(json.dumps({"Version": f"pymatgen {version('pymatgen')} · NumPy {np.__version__} · Matplotlib {matplotlib.__version__} · openpyxl {openpyxl.__version__}"}))
        return
    if not args.input:
        parser.error("--input is required")
    data = load(Path(args.input).resolve())
    if args.action == "inspect":
        result = _metadata(data)
    else:
        if not args.outdir:
            parser.error("--outdir is required")
        result = _analyze(data, args)
    print(json.dumps(result, ensure_ascii=True, allow_nan=False))


if __name__ == "__main__":
    main()
