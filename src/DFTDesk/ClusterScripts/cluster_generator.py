#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""DFTDesk 1.1 cluster backend, adapted from make_cluster.py and make_facet_cluster(1).py.

General: triangle (3), tetrahedron (4), trigonal bipyramid (5), octahedron (6),
13-atom icosahedron, otherwise a reference-bulk fragment.
Facet: finite FCC(111)/(100)/(110), or complete-shell icosahedra.
Both output one VASP POSCAR and one CIF per successful structure.
These are geometric starting guesses, NOT DFT-optimized stable structures.
"""
import argparse
from pathlib import Path
import sys
import numpy as np
from ase import Atoms
from ase.build import bulk, fcc111, fcc100, fcc110
from ase.cluster import Icosahedron
from ase.data import atomic_numbers, covalent_radii, reference_states
from ase.io import write

VACUUM = 12.0
BUILDERS = {"111": fcc111, "100": fcc100, "110": fcc110}
ICO_SHELLS = {13: 2, 55: 3, 147: 4, 309: 5, 561: 6, 923: 7}


def normalize_element(value):
    element = value.strip().capitalize()
    if element not in atomic_numbers:
        raise ValueError("无法识别元素符号：" + value)
    return element


def parse_numbers(value):
    numbers = []
    for token in value.replace("，", ",").replace("、", ",").replace(" ", ",").split(","):
        token = token.strip()
        if not token:
            continue
        if ":" in token:
            parts = token.split(":")
            if len(parts) not in (2, 3) or any(not part for part in parts):
                raise ValueError("范围格式必须为 start:end[:step]")
            first, last = int(parts[0]), int(parts[1])
            step = int(parts[2]) if len(parts) == 3 else 1
            if step <= 0 or first > last:
                raise ValueError("范围需要正步长且 start <= end")
            numbers.extend(range(first, last + 1, step))
        else:
            numbers.append(int(token))
    numbers = list(dict.fromkeys(numbers))
    if not numbers or any(n < 2 for n in numbers):
        raise ValueError("原子数必须为至少一个 >= 2 的整数")
    if len(numbers) > 100 or max(numbers) > 1000:
        raise ValueError("单次最多 100 种原子数，每个团簇不超过 1000 个原子")
    return numbers


def parse_facets(value):
    if value.strip().lower() == "all":
        return ["111", "100", "110"]
    facets = list(dict.fromkeys(
        x.strip().lower().strip("()")
        for x in value.replace("，", ",").replace("、", ",").split(",") if x.strip()))
    if not facets or any(x not in (*BUILDERS, "ico") for x in facets):
        raise ValueError("晶面只能为 111,100,110,ico 或 all")
    return facets


def estimate_bond_length(element):
    try:
        unit = bulk(element)
        sample = unit.repeat((3, 3, 3))
        pos = sample.get_positions()
        center = int(np.argmin(np.linalg.norm(pos - pos.mean(axis=0), axis=1)))
        ds = np.linalg.norm(pos - pos[center], axis=1)
        ds = ds[ds > 1e-5]
        if len(ds):
            d = float(np.min(ds))
            if np.isfinite(d) and d > 0:
                return d, "ASE 参考体相最近邻距离"
    except (ValueError, RuntimeError, TypeError, KeyError):
        pass
    d = float(covalent_radii[atomic_numbers[element]]) * 2.0
    return (d, "ASE 共价半径估算") if np.isfinite(d) and d > 0 else (2.50, "通用初猜")


def general_cluster(element, n, d):
    if n == 3:
        h = d * np.sqrt(3.0) / 2.0
        atoms = Atoms(symbols=[element]*3,
                      positions=[(0,0,0), (d,0,0), (d/2,h,0)])
        return atoms, "Triangle", ""
    if n == 4:
        atoms = Atoms(symbols=[element]*4, positions=[
            (0,0,0), (d,0,0), (d/2,d*np.sqrt(3)/2,0),
            (d/2,d*np.sqrt(3)/6,d*np.sqrt(2/3))])
        return atoms, "Tetrahedron", ""
    if n == 5:
        h, z = d*np.sqrt(3)/2, d*np.sqrt(2/3)
        atoms = Atoms(symbols=[element]*5, positions=[
            (0,0,0), (d,0,0), (d/2,h,0),
            (d/2,h/3,z), (d/2,h/3,-z)])
        return atoms, "TrigonalBipyramid", ""
    if n == 6:
        r = d/np.sqrt(2)
        atoms = Atoms(symbols=[element]*6, positions=[
            (r,0,0),(-r,0,0),(0,r,0),(0,-r,0),(0,0,r),(0,0,-r)])
        return atoms, "Octahedron", ""
    if n == 13:
        return Icosahedron(element, noshells=2, latticeconstant=d*np.sqrt(2)), "Icosahedron", ""
    try:
        unit = bulk(element)
        mode = "ASE_reference"
    except (ValueError, RuntimeError, TypeError, KeyError) as ex:
        unit = bulk(element, crystalstructure="fcc", a=d*np.sqrt(2))
        mode = "FCC_fallback (" + type(ex).__name__ + ")"
    count = max(3, int(np.ceil((n / max(len(unit), 1)) ** (1/3))) + 3)
    crystal = unit.repeat((count, count, count))
    while len(crystal) < max(4*n, n+20):
        count += 1
        crystal = unit.repeat((count, count, count))
    pos = crystal.get_positions()
    seed = int(np.argmin(np.linalg.norm(pos-pos.mean(axis=0), axis=1)))
    chosen = np.argsort(np.linalg.norm(pos-pos[seed], axis=1), kind="stable")[:n]
    atoms = crystal[chosen].copy()
    atoms.set_pbc(False)
    atoms.set_cell([0.0, 0.0, 0.0])
    return atoms, "BulkFragment", mode


def initial_fcc_a(element):
    z = atomic_numbers[element]
    state = reference_states[z]
    if state is not None and str(state.get("symmetry", "")).lower() == "fcc" and state.get("a"):
        return float(state["a"]), "ASE FCC 参考晶格常数", False
    d = 2.0 * float(covalent_radii[z])
    if not np.isfinite(d) or d <= 0:
        d = 2.50
    return np.sqrt(2)*d, "共价半径估算的假设 FCC 晶格常数", True


def facet_cluster(element, n, facet, a, vacuum):
    if facet == "ico":
        if n not in ICO_SHELLS:
            raise ValueError("ico 仅支持完整壳层 13、55、147、309、561、923")
        atoms = Icosahedron(element, noshells=ICO_SHELLS[n], latticeconstant=a)
        atoms.set_pbc(False)
        atoms.center(vacuum=vacuum)
        return atoms, "Icosahedron", None
    width = max(9, int(np.ceil(np.sqrt(n)))*3)
    layers = max(7, int(np.ceil(n**(1/3)))*3)
    slab = BUILDERS[facet](element, size=(width,width,layers), a=a,
                           vacuum=None, orthogonal=False)
    pos = slab.get_positions()
    z_top = float(np.max(pos[:,2]))
    top = np.flatnonzero(np.isclose(pos[:,2], z_top, atol=1e-5))
    midpoint = (np.min(pos[:,:2], axis=0)+np.max(pos[:,:2], axis=0))/2.0
    seed = top[np.argmin(np.sum((pos[top,:2]-midpoint)**2, axis=1))]
    lateral = np.sum((pos[:,:2]-pos[seed,:2])**2, axis=1)
    depth = (z_top-pos[:,2])**2
    score = lateral+(1.5**2)*depth
    chosen = np.lexsort((depth,lateral,score))[:n]
    atoms = slab[chosen].copy()
    atoms.set_pbc(False)
    atoms.set_cell([0,0,0])
    atoms.center(vacuum=vacuum)
    top_count = int(np.sum(np.isclose(atoms.positions[:,2],
                                      atoms.positions[:,2].max(), atol=1e-5)))
    return atoms, "FCC"+facet, top_count


def main():
    parser = argparse.ArgumentParser(description="DFTDesk ASE cluster generation")
    parser.add_argument("--mode", choices=("general", "facet"), required=True)
    parser.add_argument("--element", required=True)
    parser.add_argument("--numbers", required=True)
    parser.add_argument("--facets", default="all")
    parser.add_argument("--a", type=float)
    parser.add_argument("--vacuum", type=float, default=VACUUM)
    parser.add_argument("--out", required=True)
    args = parser.parse_args()

    try:
        element = normalize_element(args.element)
        numbers = parse_numbers(args.numbers)
        if not np.isfinite(args.vacuum) or args.vacuum <= 0:
            raise ValueError("真空层必须为正数")
        if args.a is not None and (not np.isfinite(args.a) or args.a <= 0):
            raise ValueError("晶格常数必须为正数")
        facets = parse_facets(args.facets) if args.mode == "facet" else []
        if args.mode == "facet":
            auto_a, source, non_fcc = initial_fcc_a(element)
            a = args.a if args.a is not None else auto_a
        else:
            d, source = estimate_bond_length(element)
    except (ValueError, TypeError) as ex:
        parser.error(str(ex))

    directory = Path(args.out)
    directory.mkdir(parents=True, exist_ok=True)
    print("元素：" + element + " | 原子数：" + str(numbers), flush=True)
    if args.mode == "general":
        print(f"自动最近邻距离：{d:.3f} Å（{source}）；真空：{VACUUM:g} Å/侧", flush=True)
    else:
        print(f"晶面：{facets} | FCC a：{a:.4f} Å（{source}） | 真空：{args.vacuum:g} Å/侧", flush=True)
        if non_fcc:
            print(f"[提醒] {element} 的参考体相不是 FCC；以下 FCC 晶面只是几何初猜，需弛豫。", flush=True)
    ok, skipped = 0, 0
    for n in numbers:
        for facet in (facets if args.mode == "facet" else [None]):
            try:
                if args.mode == "general":
                    atoms, shape, extra = general_cluster(element, n, d)
                    atoms.set_pbc(False)
                    atoms.center(vacuum=VACUUM)
                else:
                    atoms, shape, extra = facet_cluster(element, n, facet, a, args.vacuum)
                stem = f"{element}{n}_{shape}"
                poscar = directory / f"POSCAR_{stem}"
                cif = directory / f"{stem}.cif"
                # Never replace an existing result from this run.
                if poscar.exists() or cif.exists():
                    raise FileExistsError("输出文件已存在：" + stem)
                write(str(poscar), atoms, format="vasp", sort=True, direct=True, vasp5=True)
                try:
                    write(str(cif), atoms, format="cif")
                except Exception:
                    poscar.unlink(missing_ok=True)
                    raise
                print(f"[OK] {stem}: {len(atoms)} 原子 → {poscar.name}, {cif.name}"
                      + (f" | 额外信息：{extra}" if extra is not None and extra != "" else ""), flush=True)
                ok += 1
            except (ValueError, RuntimeError, OSError, TypeError) as ex:
                print(f"[跳过] {element}{n} {facet or ''}: {ex}", flush=True)
                skipped += 1
    print(f"完成：{ok} 个结构、{ok*2} 个文件；跳过 {skipped} 个结构。", flush=True)
    print("注意：所有结构均为初始几何，必须做 VASP 弛豫；真空及晶格常数尚未收敛验证。", flush=True)
    if ok == 0:
        raise SystemExit(2)


if __name__ == "__main__":
    main()
