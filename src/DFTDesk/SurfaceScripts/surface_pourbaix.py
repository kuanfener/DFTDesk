#!/usr/bin/env python3
"""Surface-adsorbate Pourbaix maps from 4e CHE adsorption free energies.

This is a surface-state lower-envelope construction (not a bulk dissolution
PourbaixDiagram). Pymatgen Composition supplies H/O stoichiometry and electron
counts; the verified DFTDesk step-diagram reader supplies adsorption energies.
"""
import argparse
import csv
import json
import math
from pathlib import Path

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.colors import BoundaryNorm, ListedColormap
from matplotlib.patches import Patch
import numpy as np
from openpyxl import Workbook
from pymatgen.core import Composition

from step_diagram import read_oxygen_profiles

RT_LOG10_F = 8.617333262145e-5 * 298.15 * math.log(10)
PHASES = ("*", "*OH", "*O", "*OOH")
FORMULAS = ("", "OH", "O", "OOH")
COLORS = ("#e9e7f6", "#a6c8ec", "#80c5ad", "#efbf98")
ENERGY_COLUMNS = ("Omega_star_eV", "Omega_OH_eV", "Omega_O_eV", "Omega_OOH_eV")


def transfer_electrons(formula):
    """Oxidation * + a H2O -> *O_a H_b + (2a-b)(H+ + e-)."""
    if not formula:
        return 0
    counts = Composition(formula).get_el_amt_dict()
    return int(round(2 * counts.get("O", 0) - counts.get("H", 0)))


ELECTRONS = tuple(transfer_electrons(formula) for formula in FORMULAS)


def read_adsorbates(path):
    # Reuse the same multi-catalyst Excel parsing, G corrections and validation
    # that users already rely on for ORR/OER step diagrams.
    _, metrics = read_oxygen_profiles(path, "OER", 0.0, 0.0, "RHE")
    return [{
        "material": row["Material"],
        "dg": (0.0, row["DG_OH"], row["DG_O"], row["DG_OOH"]),
    } for row in metrics]


def compute_map(dg, ph_values, she_values):
    ph, potential_she = np.meshgrid(ph_values, she_values)
    potential_rhe = potential_she + RT_LOG10_F * ph
    omega = np.stack([
        float(value) - electrons * potential_rhe
        for value, electrons in zip(dg, ELECTRONS)
    ])
    phase_ids = np.argmin(omega, axis=0)
    return ph, potential_she, potential_rhe, omega, phase_ids


def render_maps(catalysts, ph_values, she_values, png_path, svg_path):
    count = len(catalysts)
    columns = min(3, count)
    rows = math.ceil(count / columns)
    fig, axes = plt.subplots(rows, columns, figsize=(6.1 * columns, 4.6 * rows),
                             squeeze=False, constrained_layout=True)
    cmap = ListedColormap(COLORS)
    norm = BoundaryNorm(np.arange(-0.5, 4.5, 1), cmap.N)
    for ax, catalyst in zip(axes.flat, catalysts):
        ph, she, _, _, phase_ids = compute_map(catalyst["dg"], ph_values, she_values)
        ax.pcolormesh(ph, she, phase_ids, cmap=cmap, norm=norm, shading="auto")
        # Dashed thermodynamic water-stability boundaries, not kinetic onset.
        ax.plot(ph_values, -RT_LOG10_F * ph_values, color="#426388",
                linestyle="--", linewidth=1, label="H2/H2O")
        ax.plot(ph_values, 1.23 - RT_LOG10_F * ph_values, color="#a45743",
                linestyle="--", linewidth=1, label="O2/H2O")
        for i, label in enumerate(PHASES):
            where = np.argwhere(phase_ids == i)
            if len(where) < 25:
                continue
            center = where[len(where) // 2]
            ax.text(float(ph[tuple(center)]), float(she[tuple(center)]),
                    label, fontsize=10, ha="center", va="center",
                    color="#20242c", fontweight="semibold",
                    bbox={"boxstyle": "round,pad=0.16", "facecolor": "white",
                          "edgecolor": "none", "alpha": 0.75})
        ax.set(title=catalyst["material"], xlabel="pH",
               ylabel="Potential (V vs SHE)",
               xlim=(ph_values[0], ph_values[-1]),
               ylim=(she_values[0], she_values[-1]))
        ax.grid(alpha=0.13)
    for ax in list(axes.flat)[count:]:
        ax.set_visible(False)
    legend = [Patch(color=color, label=label) for color, label in zip(COLORS, PHASES)]
    fig.legend(handles=legend, loc="outside lower center", ncol=4)
    fig.suptitle("Surface adsorbate Pourbaix (4e CHE, 298.15 K)", fontsize=15)
    fig.savefig(png_path, dpi=180, bbox_inches="tight")
    fig.savefig(svg_path, bbox_inches="tight")
    plt.close(fig)


def export_grid(catalysts, ph_values, she_values, csv_path, dat_path, excel_path):
    headers = ("Material", "pH", "U_SHE_V", "U_RHE_V", "StableState",
               "PhaseID", *ENERGY_COLUMNS)
    workbook = Workbook(write_only=True)
    summary = workbook.create_sheet("Adsorbates")
    summary.append(("Material", "DG_OH_eV", "DG_O_eV", "DG_OOH_eV",
                    "n_e(OH)", "n_e(O)", "n_e(OOH)"))
    grid_sheet = workbook.create_sheet("SurfacePourbaixGrid")
    grid_sheet.append(headers)
    # CSV and Origin DAT are intentionally numeric/editable; PhaseID codes
    # are documented by the separate workbook state table.
    with open(csv_path, "w", newline="", encoding="utf-8-sig") as csv_handle, \
         open(dat_path, "w", newline="", encoding="utf-8") as dat_handle:
        csv_writer = csv.writer(csv_handle)
        dat_writer = csv.writer(dat_handle, delimiter="\t")
        csv_writer.writerow(headers)
        dat_writer.writerow(headers)
        for catalyst in catalysts:
            name, dg = catalyst["material"], catalyst["dg"]
            summary.append((name, dg[1], dg[2], dg[3], *ELECTRONS[1:]))
            ph, she, rhe, omega, ids = compute_map(dg, ph_values, she_values)
            for index in np.ndindex(ids.shape):
                phase_id = int(ids[index])
                record = (name, round(float(ph[index]), 7),
                          round(float(she[index]), 7),
                          round(float(rhe[index]), 7),
                          PHASES[phase_id], phase_id,
                          *(round(float(omega[k][index]), 8) for k in range(4)))
                csv_writer.writerow(record)
                dat_writer.writerow(record)
                grid_sheet.append(record)
    notes = workbook.create_sheet("Readme")
    for row in [
        ("Model", "Surface adsorption only; excludes bulk dissolution, ions and coverage."),
        ("Potential axis", "SHE; U_RHE = U_SHE + 0.059159*pH at 298.15K."),
        ("PhaseID", "0=*; 1=*OH; 2=*O; 3=*OOH."),
        ("Grand potential", "Omega(X) = DG_X(0) - n_e(X)*U_RHE."),
        ("Reference", "Four-electron CHE; DG_X relative to clean slab and H2O/H2."),
        ("Water lines", "HER: -0.059159*pH; OER: 1.23-0.059159*pH."),
        ("Limitation", "No electrolyte ions, dissolution, coverage or kinetic barriers."),
    ]:
        notes.append(row)
    workbook.save(excel_path)


def analyze(input_path, outdir, phmin=0, phmax=14, umin=-1.0, umax=2.0, resolution=121):
    if not (0 <= phmin < phmax <= 14):
        raise ValueError("pH 范围必须满足 0 ≤ 下限 < 上限 ≤ 14。")
    if not (-3 <= umin < umax <= 3):
        raise ValueError("SHE 电位范围需在 -3 至 3 V 之间且上限大于下限。")
    if resolution < 40 or resolution > 201:
        raise ValueError("网格精度必须为 40–201。")
    catalysts = read_adsorbates(input_path)
    if len(catalysts) > 12:
        raise ValueError("一次最多绘制 12 个催化剂，请拆分工作簿。")
    destination = Path(outdir)
    destination.mkdir(parents=True, exist_ok=True)
    output = {ext: str(destination / ("surface_pourbaix." + ext))
              for ext in ("png", "svg", "csv", "dat", "xlsx")}
    ph_values = np.linspace(phmin, phmax, resolution)
    she_values = np.linspace(umin, umax, resolution)
    render_maps(catalysts, ph_values, she_values, output["png"], output["svg"])
    export_grid(catalysts, ph_values, she_values, output["csv"], output["dat"], output["xlsx"])
    return {
        "Png": output["png"], "Svg": output["svg"], "Csv": output["csv"],
        "Dat": output["dat"], "Xlsx": output["xlsx"],
        "SeriesCount": len(catalysts),
        "Summary": (f"{len(catalysts)} 个催化剂；pH {phmin:g}–{phmax:g}，"
                    f"电位 {umin:g}–{umax:g} V vs SHE，"
                    "表面稳定态：*、*OH、*O、*OOH（CHE 近似）"),
    }


def main():
    parser = argparse.ArgumentParser(description="DFTDesk surface-adsorbate Pourbaix maps")
    parser.add_argument("--input", required=True)
    parser.add_argument("--outdir", required=True)
    parser.add_argument("--phmin", type=float, default=0)
    parser.add_argument("--phmax", type=float, default=14)
    parser.add_argument("--umin", type=float, default=-1)
    parser.add_argument("--umax", type=float, default=2)
    parser.add_argument("--resolution", type=int, default=121)
    args = parser.parse_args()
    result = analyze(args.input, args.outdir, args.phmin, args.phmax,
                     args.umin, args.umax, args.resolution)
    print(json.dumps(result, ensure_ascii=False))


if __name__ == "__main__":
    main()
