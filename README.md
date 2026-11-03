# DFTDesk 2.3.2

**面向 VASP / DFT / 计算材料研究的 Windows 桌面工作台。**

DFTDesk 将晶体结构处理、金属团簇初猜、自由能台阶图与表面吸附态 Pourbaix 图、VASP DOS/PDOS、LOBSTER COHP/ICOHP 后处理集中在一个本地软件中，并支持导出可在 **Excel** 和 **Origin** 中继续分析的数据。

![DFTDesk](src/DFTDesk/Assets/Icons/dftdesk.png)

[![Windows build](https://github.com/kuanfener/DFTDesk/actions/workflows/dftdesk-windows.yml/badge.svg)](https://github.com/kuanfener/DFTDesk/actions/workflows/dftdesk-windows.yml)

## 下载 Windows 版本

1. 打开 [DFTDesk Windows 构建](https://github.com/kuanfener/DFTDesk/actions/workflows/dftdesk-windows.yml)。
2. 选择最新的绿色成功运行，在 **Artifacts** 下载 **DFTDesk-win-x64**。
3. 解压并直接运行 **DFTDesk.exe**；EXE 自带 .NET 运行时，无需另外安装 .NET。
4. 结构转换、团簇生成、DOS/COHP 分析由用户配置的 Python 环境执行。安装依赖：

   ```powershell
   python -m pip install ase pymatgen numpy matplotlib openpyxl
   ```

5. 在软件「工作引擎」页面填写 **同一环境的 python.exe**，分别点击五张工具卡片上的检测按钮。

> EXE 不捆绑 Python 或科学计算库；本地计算输入不会上传至服务器。

## 十个工作区

| 页面 | 功能 |
| --- | --- |
| 首页 | DFTDesk 2.3.2 总览，结构转换、团簇生成、台阶图、表面 Pourbaix、DOS、COHP 六个功能入口 |
| 结构文件格式转换 | 使用 ASE 进行 POSCAR / CONTCAR ⇄ CIF，拖放、多选、批量导出 |
| 团簇生成 | 任意元素、批量原子数、普通几何或 FCC 晶面取向，生成 POSCAR 和 CIF |
| 绘制台阶图 | HER / ORR / OER 多催化剂自由能台阶，四电子 CHE 反应步、限制电位、过电位和 PDS，导出图片、Excel、Origin DAT |
| 表面 Pourbaix 图 | 用 pymatgen 解析 * / *OH / *O / *OOH 组成，并用四电子 CHE 绘制 pH–SHE 电位稳定区；支持同 Excel 多催化剂与可编辑数据导出 |
| DOS 分析 | VASP DOSCAR、vasprun.xml：TDOS/PDOS、元素 s/p/d、自旋、DOS 积分、d 带中心 |
| COHP / ICOHP 分析 | LOBSTER COHPCAR、ICOHPLIST：多键曲线、自旋分辨、ICOHP(Ef) 键强度 |
| 主题 | 明暗模式、强调色、圆角、窗口尺寸、环形轮盘不透明度与实时预览 |
| 工作引擎 | **ASE、pymatgen、NumPy、Matplotlib、openpyxl 五个独立状态卡片，逐个检测** |
| 关于 | 软件版本、许可与开源链接 |

首页将六个主要功能放在「开始使用」卡片中，顶部简介不重复放快捷按钮。

### 结构转换

- 输入：POSCAR、CONTCAR、`.vasp`、`.poscar`、CIF。
- 输出 POSCAR 默认 **Cartesian（Å）**；可以切换为 Direct。
- 在资源管理器按住 **Ctrl 并用鼠标左键拖动结构文件**，呼出快捷轮盘。
- 云朵导入区提示「点击或拖入添加文件」，载入后切换为文件列表；输出不会覆盖已有同名文件。

### 团簇生成

- 任意 ASE 元素符号，如 Pt、Fe、Co、Ni、Au。
- 输入 `4,5,6,13` 或 `4:12` 生成多个原子数构型。
- 普通初猜与 FCC(111)/(100)/(110) 取向初猜；支持批量输出。
- 按勾选生成 POSCAR 和/或 CIF；自动估计晶格常数并可覆盖。

**科学边界：**所有团簇都是初始几何猜测，需要进一步进行 VASP 结构弛豫与收敛性验证。

### 自动绘制反应台阶图（2.2：HER / ORR / OER）

- 左侧「**绘制台阶图**」紧随「团簇生成」之后；首页也有独立入口。选择 HER / ORR / OER 后可点击 **「下载当前模式 Excel 模板」**，在第一个工作表的黄色区域从第 2 行起直接填入数据。ORR/OER 共用一份 `Material、DG_OH、DG_O、DG_OOH` 模板；HER 使用 `Material、State、DeltaG` 模板。
- 点击云朵或把 Excel .xlsx / .xlsm 拖入。支持提供的碱性 HER 自由能数据模板：第一列为中间态，B 列为 DFT 能量，E 列为自由能修正，G 列为参照分子，I 列为相对自由能。识别同一个催化剂的多段反应并绘图。
- 也支持更直接的 Material / State / DeltaG 三列表格；一个 Excel 可以包含多个体系。
- 按 Origin 模板将可比的催化剂曲线叠加显示，并为具有更多中间态的体系单独绘制完整路径；使用水平能级线、虚线连接和自由能标注，预览后可**勾选所需格式**导出 **PNG、SVG、CSV、Excel XLSX、Origin DAT**。
- 原始数据如有修改，应保存 Excel 后再导入；当前由 Python 读取数值并绘制，不会修改原始文件。
- 附带的 .opju 仅作为 Origin 图形参考。导出的 DAT 可在 Origin 中导入并用于已有模板，但 DFTDesk **不会自动修改或生成 Origin 原生 .opju**，也不会把 Origin 工程上传至公开仓库。

#### ORR / OER 四电子 CHE 模式（2.2 新增）

- 在「绘制台阶图」切换 **ORR / OER**，填写电位 U（V vs RHE）与 pH。**电位参照固定为 RHE**，不再提供 RHE / SHE 下拉选项；在 298.15 K 的 CHE 模型中不会对输入的 RHE 电位重复施加 pH 校正。
- **兼容原始 ORR 模板：**同一工作表内可重复包含多组五行结构块，A 列依次为「催化剂名、*OOH、*O、*OH、催化剂名」，G 列为 U=0V 时的相对自由能（可从 F 列的绝对自由能补算）。自动提取每个结构的 OH/O/OOH 吸附自由能，并生成 ORR 或反向 OER 台阶，无需调整模板。
- 也支持 Excel 一个工作表中每行一个催化剂，最简单的表头如下（单位 eV，数值仅用于演示）：

  | Material | DG_OH | DG_O | DG_OOH |
  | --- | ---: | ---: | ---: |
  | Pt | 1.60 | 2.80 | 3.90 |
  | Co | 1.48 | 2.60 | 3.83 |

- 除了三列吸附自由能，也可提供经过修正的绝对吉布斯自由能：`Material, G_star, G_OH, G_O, G_OOH, G_H2O, G_H2`；或提供六列 `E_*` 加上对应六列 `Corr_*`（`star, OH, O, OOH, H2O, H2`）的 VASP DFT 总能及总自由能修正。**只有 VASP 电子总能而无振动、熵等自由能修正时，软件拒绝计算 CHE 台阶**，避免将未校正的电子能误作自由能。
- OER 四步：`[ΔG_OH, ΔG_O−ΔG_OH, ΔG_OOH−ΔG_O, 4.92−ΔG_OOH]`；ORR 为上述反应逆序并反号。默认采用四电子反应总自由能 4.92 eV，平衡电位 1.23 V（vs RHE）。
- 电位改变每个一电子步骤 `±eU_RHE`，因此 **终态能量不固定为 0 eV**：图像采用两种固定的可比零点：OER 在 0 V 时为 0→+4.92 eV；ORR 按提供的 Origin 模板在 0 V 时为 +4.92→0 eV。电位 U 改变时，OER 末态变为 4.92−4U，ORR 末态变为 4U，不强制归零。
- 软件自动输出 `U_L`、`η`、电位决定步骤 PDS，以及每一步在 0 V 与所选电位下的自由能。**ORR/OER 台阶图上将 PDS 反应步加粗并标菱形标记，右侧按催化剂突出显示 η（V）与 PDS 步数**；Excel 的 **CHE Metrics**、**CHE Four Steps** 表格仍保留原始数值。
- 所计算的为 **CHE 热力学理论过电位**；不包含电荷转移动力学、显式溶剂、覆盖度或其他条件的自动校正，最终结果取决于输入自由能的定义和精度。

### 表面吸附态 Pourbaix 图（2.3.1 新增）

- 左侧「**表面 Pourbaix 图**」位于「绘制台阶图」正下方，首页也有独立快捷卡片。
- **复用台阶图 Excel 输入格式**：支持你提供的多组五行 ORR 原始表（催化剂 / *OOH / *O / *OH / 催化剂），或者每行一个催化剂的 `Material, DG_OH, DG_O, DG_OOH` 宽表；也支持台阶图既有的 G / E+Corr 形式。无需原生 Origin 工程文件。
- **pymatgen 的 `Composition`** 解析吸附物中的 O、H 计量，得到每个吸附态对应的质子–电子转移数 `n_e = 2n_O - n_H`（*、*OH、*O、*OOH 为 0、1、2、3）。结合 298.15 K 的四电子 CHE，自由能按 `Ω_X = ΔG_X(0) − n_e·U_RHE` 修正，再在每个 pH–电位网格上选最低自由能表面态。
- 横轴 pH，纵轴 **U vs SHE**；使用 `U_RHE = U_SHE + 0.05916×pH`，标出 `H₂/H₂O` 和 `O₂/H₂O` 理论水稳定边界。默认 pH 0–14、电位 −1–2 V vs SHE，范围可修改。多个催化剂自动排列在同一张图中。
- 可分别勾选导出 **PNG、SVG、Excel XLSX、CSV、Origin DAT**。表格中包含每个网格的 pH、SHE/RHE 电位、稳定吸附态、PhaseID 以及四种表面态的自由能，可在 Excel/Origin 中继续处理；DAT **不是**原生 `.opju` 工程。
- **科学边界**：这是表面吸附态的 **CHE 下包络稳定区图**，并不是 pymatgen `PourbaixDiagram` 所处理的体相/溶解物种相图；不包含金属溶解、离子浓度、显式溶剂、不同吸附覆盖度及动力学，不能直接用来推断催化剂的抗腐蚀稳定性。

### 统一云朵导入

结构转换、DOS、COHP/ICOHP、台阶图和表面 Pourbaix 页面都使用已修复的「**点击或拖入添加文件**」云朵入口。按钮打开本地文件选择器，支持资源管理器拖放；DOSCAR/vasprun.xml、COHPCAR/ICOHPLIST 会自动匹配所需类型。

### VASP DOS / PDOS

- 读取 `vasprun.xml`（推荐）或 `DOSCAR`。
- 选择 Fe、Co、Ni、Pt 等元素，按 s/p/d 轨道和自旋通道绘制。
- 设定相对费米能级的窗口，计算 DOS 积分与 **d 带中心**。
- DOSCAR 按元素投影需要同目录下对应的 POSCAR / CONTCAR；暂不支持非共线 / SOC 四分量投影。
- 输出 PNG、SVG、CSV、**Excel XLSX**、**Origin DAT**。

### LOBSTER COHP / ICOHP

- 读取 `COHPCAR.lobster` 与 `ICOHPLIST.lobster`，同目录文件会自动关联。
- 按键编号多选、自旋分辨、指定能量窗口，支持标准 **-COHP / -ICOHP** 绘制。
- 查询键长与各自旋 **ICOHP(Ef)**；仅有 ICOHPLIST 时仍能导出键强度表。
- 输出 PNG、SVG、CSV、**Excel XLSX**、**Origin DAT**（没有 COHPCAR 时不生成曲线图片）。

Excel 是可以编辑的工作簿，COHP 文件含 ICOHP(Ef) 汇总和完整曲线工作表；**Origin DAT 是可直接导入 Origin 继续画图的制表符数据文件**，不是原生 `.opju` 工程文件。原始 COHP/ICOHP 符号保留在数值导出中，负的 ICOHP 通常代表成键贡献。

## 导出格式选择（2.3.1）

- 结构转换：原有 CIF、POSCAR 两个格式入口保持独立。
- 团簇生成：勾选 POSCAR、CIF 或两者，未勾选的格式不生成。
- HER/ORR/OER 台阶图、表面 Pourbaix、DOS、COHP/ICOHP：在每个工作区勾选 PNG、SVG、Excel、Origin DAT、CSV 中需要的格式，再点「导出所选格式」；不再强制保存全部文件。纯 ICOHPLIST 数据无法导出不存在的曲线图像。

## 工作引擎：五个独立检测项

| Python 库 | 用途 | 检测方式 |
| --- | --- | --- |
| **ASE** | POSCAR/CIF、团簇与原子操作 | 单独检测 ASE |
| **pymatgen** | VASP、LOBSTER 文件解析及表面吸附物组成计算 | 单独检测 pymatgen |
| **NumPy** | 数组、积分和数值指标 | 单独检测 NumPy |
| **Matplotlib** | DOS / COHP / 台阶图 / 表面 Pourbaix 及 PNG / SVG 导出 | 单独检测 Matplotlib |
| **openpyxl** | Excel `.xlsx` 解析及工作簿写出 | 单独检测 openpyxl |

五项共享用户选择的 Python 可执行文件，但 **状态与检测相互独立**。更改 Python 路径后，旧检测结果会被标记为需要重新检测。

## 源码构建与测试

需要 **.NET 10 SDK**，Windows x64：

```powershell
dotnet test tests/DFTDesk.Tests/DFTDesk.Tests.csproj -c Release
python -m unittest discover -s tests -p test_dos_analyzer.py
python -m unittest discover -s tests -p test_cohp_analyzer.py
python -m unittest discover -s tests -p test_step_diagram.py
python -m unittest discover -s tests -p test_surface_pourbaix.py
dotnet publish src/DFTDesk/DFTDesk.csproj -c Release -r win-x64 --self-contained true -o dist/DFTDesk
```

Windows CI 自动安装上述 Python 库，验证 ASE 转换、五个 Python 库的独立检测、DOS/COHP 数据处理、HER 模板、ORR/OER CHE 数值与终态、Excel/Origin 导出，并对独立 EXE 进行启动检查。

## 开源许可

DFTDesk 采用 [MIT License](LICENSE)。部分 UI、轮盘和 Windows 组件复用 [KnifeLemon/Filee](https://github.com/KnifeLemon/Filee) 的 MIT 代码；版权和第三方许可参见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。ASE 采用 LGPL-2.1-or-later，作为外部 Python 库调用。个人赞助二维码通过本地文件或 Actions Secrets 注入，不提交至公开源码。
