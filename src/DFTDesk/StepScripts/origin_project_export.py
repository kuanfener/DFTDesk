#!/usr/bin/env python3
"""Export DFTDesk reaction profiles into a user's existing Origin OPJU project.

The original OPJU stays local: this script opens it in Origin 2021+ on Windows,
preserves its workbook and graph pages, and rebinds *existing* plot objects.
It never uploads or silently replaces the source project.
"""
import argparse
import json
import math
import os
import sys
from collections import OrderedDict
from pathlib import Path


def read_profiles(path):
    from openpyxl import load_workbook
    book = load_workbook(path, data_only=True, read_only=True)
    result = OrderedDict()
    try:
        if "Reaction Profiles" not in book:
            raise ValueError("绘图数据缺少 Reaction Profiles 工作表。")
        sheet = book["Reaction Profiles"]
        header = [str(cell.value or "").strip() for cell in sheet[1]]
        need = ("Material", "Step_Index", "State", "DeltaG_eV")
        if any(name not in header for name in need):
            raise ValueError("Reaction Profiles 缺少必要的 Material/Step_Index/State/DeltaG_eV 列。")
        offsets = {name: header.index(name) for name in need}
        for values in sheet.iter_rows(min_row=2, values_only=True):
            if all(value is None for value in values):
                continue
            name = str(values[offsets["Material"]] or "").strip()
            state = str(values[offsets["State"]] or "").strip()
            step = values[offsets["Step_Index"]]
            energy = values[offsets["DeltaG_eV"]]
            if not name or not state or isinstance(step, bool) or isinstance(energy, bool):
                raise ValueError("台阶图数据有空白的 Material/State 或无效数值。")
            if not isinstance(step, (int, float)) or not isinstance(energy, (int, float)):
                raise ValueError(f"{name} 的 Step_Index 或 DeltaG_eV 不是数值。")
            if not math.isfinite(float(energy)) or int(step) != step:
                raise ValueError(f"{name} 的自由能或反应步序号无效。")
            result.setdefault(name, []).append((int(step), state, float(energy)))
        for name, states in result.items():
            steps = [value[0] for value in states]
            if steps != list(range(len(states))):
                raise ValueError(f"{name} 的反应步序号不是从 0 开始的连续整数：{steps}")
            if len(states) < 2:
                raise ValueError(f"{name} 的反应态不足，无法生成曲线。")
    finally:
        book.close()
    if not result:
        raise ValueError("Reaction Profiles 没有任何催化剂曲线。")
    return result


def staircase_xy(states):
    """Flat segments and short connectors, without inventing intermediates."""
    xs, ys = [], []
    for index, (_, _, energy) in enumerate(states):
        xs.extend((index - .32, index + .32))
        ys.extend((energy, energy))
        if index + 1 < len(states):
            next_energy = states[index + 1][2]
            xs.extend((index + .32, index + .68))
            ys.extend((energy, next_energy))
    return xs, ys


def origin_column(index):
    """0-based column index => Origin worksheet short name."""
    value = index + 1
    label = ""
    while value:
        value, rem = divmod(value - 1, 26)
        label = chr(65 + rem) + label
    return label


def graph_layout(origin):
    """Inspect pre-existing Origin graphs without altering their objects."""
    result = []
    for graph in origin.graph_list("p", True):
        layers = []
        for layer in graph:
            layers.append((layer, list(layer.plot_list())))
        result.append((graph, layers))
    return result


def graph_summary(layout):
    return [{"graph": str(graph), "layers": [len(plots) for _, plots in layers],
             "plots": [str(plot.lt_range()) for _, plots in layers for plot in plots]}
            for graph, layers in layout]


def match_graph(layout, count):
    """Strict one-catalyst-to-one-existing-plot matching prevents style corruption."""
    matched = [(graph, layers) for graph, layers in layout
               if sum(len(plots) for _, plots in layers) == count]
    if len(matched) != 1:
        report = json.dumps(graph_summary(layout), ensure_ascii=False)
        raise ValueError(
            f"原 Origin 工程中需要恰好一张包含 {count} 条已绘制数据曲线的图，"
            f"实际匹配到 {len(matched)} 张。模板图层结构：{report}。"
            "为了保留原 Book 和 Graph 样式，程序不会自动删减或杜撰曲线。"
            "请使用与催化剂数量对应的 Origin 工程模板，或反馈模板图层结构。")
    return matched[0]


def write_project(template, data, destination):
    import originpro as op
    source = Path(template).resolve()
    target = Path(destination).resolve()
    if not source.is_file() or source.suffix.lower() != ".opju":
        raise ValueError("请选择真实存在的 Origin .opju 模板文件。")
    if target == source or target == Path(data).resolve():
        raise ValueError("导出路径不得覆盖原始 Origin 工程或输入数据。")
    if target.exists():
        raise FileExistsError("输出 .opju 文件已存在，请更换保存名称。")
    profiles = read_profiles(data)

    try:
        if op.oext:
            op.set_show(True)
        op.open(str(source), readonly=False, asksave=False)
        layout = graph_layout(op)
        graph, layers = match_graph(layout, len(profiles))
        books = list(op.pages("w"))
        if not books:
            raise ValueError("Origin 模板没有 Book 工作簿，无法保留其 Book 结构。")
        # Add a result sheet *inside the existing original Book*, keeping all
        # original sheets, graph pages, layers, plots and their visual settings.
        sheet = books[0].add_sheet("DFTDesk_Profiles")
        plots = [plot for _, plots_in_layer in layers for plot in plots_in_layer]
        for position, ((material, states), plot) in enumerate(zip(profiles.items(), plots)):
            xs, ys = staircase_xy(states)
            x_col = position * 2
            y_col = x_col + 1
            sheet.from_list(x_col, xs, lname=f"{material} / step", axis="X")
            sheet.from_list(y_col, ys, lname=f"{material} / DeltaG (eV)", units="eV", axis="Y")
            # Retain Origin's existing plot object and all of its formatting.
            plot.change_data(sheet, x=origin_column(x_col), y=origin_column(y_col))
        for layer, _ in layers:
            layer.rescale()
        op.wait()
        op.save(str(target))
        op.wait()
        if not target.is_file() or target.stat().st_size == 0:
            raise IOError("Origin 没有生成有效的 .opju 文件。请检查 Origin 2024 的保存权限。")
        return {"Output": str(target), "SeriesCount": len(profiles),
                "BookCount": len(books), "GraphCount": len(layout),
                "TemplateGraph": str(graph)}
    finally:
        if op.oext:
            op.exit()


def main(argv=None):
    parser = argparse.ArgumentParser(description="Origin 2024 native OPJU project export")
    parser.add_argument("--template", required=True)
    parser.add_argument("--input", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args(argv)
    try:
        result = write_project(args.template, args.input, args.output)
        print(json.dumps(result, ensure_ascii=False))
    except ModuleNotFoundError as exc:
        print("Origin 自动化不可用：请确保 Windows 上安装 Origin 2021+，并在 DFTDesk"
              "「工作引擎」所选的 Python 环境执行 python -m pip install originpro。"
              f" 原始错误：{exc}", file=sys.stderr)
        return 2
    except Exception as exc:
        print(f"Origin OPJU 导出失败：{type(exc).__name__}: {exc}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
