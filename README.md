# DFTDesk v2.3.11

**面向 VASP / DFT / 计算材料研究的 Windows 桌面工作台。**

DFTDesk 将晶体结构处理、金属团簇初猜、自由能台阶图与表面吸附态 Pourbaix 图、VASP DOS/PDOS、LOBSTER COHP/ICOHP 后处理集中在一个本地软件中，并支持导出可在 **Excel** 和 **Origin** 中继续分析的数据。

![DFTDesk](src/DFTDesk/Assets/Icons/dftdesk.png)

[![Windows build](https://github.com/kuanfener/DFTDesk/actions/workflows/dftdesk-windows.yml/badge.svg)](https://github.com/kuanfener/DFTDesk/actions/workflows/dftdesk-windows.yml)

## v2.3.11 最新更新

- **Pourbaix 相界平滑**：由四电子 CHE 自由能交点直接求稳定区域分界，绘制连续直线，消除离散网格造成的锯齿；Excel/CSV/Origin DAT 逐网格数值输出不变。
- **保留原始配色并提高可读性**：`*`、`*OH`、`*O`、`*OOH` 四种稳定区沿用既有颜色；加大字号、提高 PNG 分辨率，SVG 保持矢量输出。
- **横向 / 竖向排列可选**：「表面 Pourbaix 图」绘制前选择多催化剂排布方式，默认横向；竖向布局的预览高度随图像比例调整。

## v2.3.10 更新记录

- **POSCAR 默认 Direct 分数坐标**：结构转换和团簇生成两处导出入口均改为默认 Direct。Cartesian 仍作为可勾选的可选模式。
- **兼容旧版本设置**：旧 `engine.json` 中的 Cartesian 默认值在首次升级时切换为 Direct；新版本中手动选用 Cartesian 后会继续保存并在下次启动时恢复。
- **原子与元素顺序不变更**：保持 v2.3.9 的 VASP 同元素分组规则；仅修改导出坐标的表示方式，不改变真实结构。

- **环形工具栏快捷键改为 Ctrl + 鼠标中键单击**：先在 Windows 资源管理器中选中结构文件，然后按住 Ctrl 单击鼠标滚轮呼出转换轮盘；使用左键选择 CIF / POSCAR，Esc 或点击轮盘外关闭。
- **恢复 Windows 原生文件操作**：取消全局 Ctrl + 左键拖动手势，让 Windows 文件多选与 Ctrl + 拖动复制不受 DFTDesk 干扰；中键快捷键仅在文件管理器范围识别。
- **事件隔离与测试**：新的 Windows 同步鼠标监听仅消费有效 Ctrl + 中键事件，避免资源管理器同时处理该次中键点击；加入按键组合与点击移动阈值测试。

## v2.3.9 更新记录

- **VASP 规范元素分组**：CIF → POSCAR 时，相同元素只写入一个连续区段。元素种类遵循输入结构中**首次出现的顺序**，不会按字母排序；同种元素内部原子保持相对顺序，坐标与约束随原子一起重排。
- **修复重复元素头部**：例如 `Co P Sc H O H`（`63 64 1 1 1 1`）导出为 `Co P Sc H O`（`63 64 1 2 1`）。避免重复 H 影响与 POTCAR 的元素区段匹配。
- **回归测试**：新增 CIF 中 H–O–H 非相邻原子的用例，验证 POSCAR 只有一个 H 区段，数量合并、坐标对应正确；原有双向转换测试继续保留。

## v2.3.8 更新记录

- **移除字母排序**：停止在 POSCAR 输出时按元素名称排序。v2.3.9 进一步补全 VASP 所需的相同元素归组逻辑；对于非 P1 CIF，对称性展开可能增加原子，仍须核对。
- **双向回归测试**：新增非字母顺序的元素数据，检查原子顺序、坐标与 Direct/Cartesian 输出。

## v2.3.7 更新记录

- **开机自动启动（可选）**：在「主题 → Windows 启动」勾选「登录 Windows 后自动启动」，点击「保存设置」后使用当前用户的 Windows 启动项，无需管理员权限；**默认关闭**。自动启动时优先在系统托盘后台运行。
- **最小化到系统托盘**：默认开启；点击最小化按钮后窗口隐藏到任务栏右下角通知区域。双击 DFTDesk 图标恢复窗口，或右键选择「打开 DFTDesk」「退出 DFTDesk」。关闭窗口的 **×** 仍代表正常退出；如需保留后台运行，请使用最小化按钮。
- **持久化选项**：开机自启与托盘行为随主题设置保存。关闭「最小化到系统托盘」后，最小化窗口会保留在任务栏；如果移动了 `DFTDesk.exe`，请重新保存开机自启选项以更新启动路径。

## v2.3.6 更新记录

- **OriginPro 连接检测**：工作引擎新增第六张检测卡片，先检查所选 Python 的 `originpro` 包，再检查是否能连接本机 Origin 自动化接口；错误信息区分“缺少 Python 包”和“Origin 连接失败”。
- **原生 Origin 工程安全导出**：绘制 HER / ORR / OER 台阶图时，选用自己的 Origin `.opju` 模板进行原生工程导出，会先自动检查 Origin 环境；不满足条件则停止导出，不会生成无效的 OPJU 文件。原模板保留，输出另存为新工程。
- **Origin 2024 适配**：面向 Windows 本机 Origin 2021+；Origin 2024 可以使用，但实际 COM 连接需要在用户电脑上确认。非 OPJU 功能不要求安装 Origin。
- **构建与验证**：Windows CI 已通过 .NET 测试、ASE/DOS/COHP、HER/ORR/OER、表面 Pourbaix、原生 OPJU 的模拟映射测试、EXE 打包及独立启动检查。

**[下载最新版 DFTDesk（Windows x64）](https://github.com/kuanfener/DFTDesk/actions/workflows/dftdesk-windows.yml)** · [查看源码](https://github.com/kuanfener/DFTDesk)

## 下载 Windows 版本

1. 打开 [DFTDesk Windows 构建](https://github.com/kuanfener/DFTDesk/actions/workflows/dftdesk-windows.yml)。
2. 选择最新的绿色成功运行，在 **Artifacts** 下载 **DFTDesk-win-x64**。
3. 解压并直接运行 **DFTDesk.exe**；EXE 自带 .NET 运行时，无需另外安装 .NET。
4. 结构转换、团簇生成、DOS/COHP 分析由用户配置的 Python 环境执行。安装依赖：

   ```powershell
   python -m pip install ase pymatgen numpy matplotlib openpyxl
   # 仅当需要导出原生 Origin .opju 工程时，额外安装：
   python -m pip install originpro
   ```

5. 在软件「工作引擎」页面填写 **同一环境的 python.exe**，分别点击六张工具卡片上的检测按钮。

> EXE 不捆绑 Python 或科学计算库；本地计算输入不会上传至服务器。

## 十个工作区

| 页面 | 功能 |
| --- | --- |
| 首页 | DFTDesk 2.3.11 总览，结构转换、团簇生成、台阶图、表面 Pourbaix、DOS、COHP 六个功能入口 |
| 结构文件格式转换 | 使用 ASE 进行 POSCAR / CONTCAR ⇄ CIF；导出 POSCAR 时按元素首次出现顺序稳定分组，避免同种元素重复区段；支持拖放、多选、批量导出 |
| 团簇生成 | 任意元素、批量原子数、普通几何或 FCC 晶面取向，生成 POSCAR 和 CIF |
| 绘制台阶图 | HER / ORR / OER 多催化剂自由能台阶，四电子 CHE 反应步、限制电位、过电位和 PDS，导出图片、Excel、Origin DAT / 原生 OPJU |
| 表面 Pourbaix 图 | 用 pymatgen 解析 * / *OH / *O / *OOH 组成，并用四电子 CHE 绘制 pH–SHE 电位稳定区；支持同 Excel 多催化剂与可编辑数据导出 |
| DOS 分析 | VASP DOSCAR、vasprun.xml：TDOS/PDOS、元素 s/p/d、自旋、DOS 积分、d 带中心 |
| COHP / ICOHP 分析 | LOBSTER COHPCAR、ICOHPLIST：多键曲线、自旋分辨、ICOHP(Ef) 键强度 |
| 主题 | 明暗模式、强调色、圆角、窗口尺寸、环形轮盘预览、开机自启和托盘最小化 |
| 工作引擎 | **ASE、pymatgen、NumPy、Matplotlib、openpyxl、OriginPro 六个独立状态卡片，逐个检测** |
| 关于 | 软件版本、许可与开源链接 |

首页将六个主要功能放在「开始使用」卡片中，顶部简介不重复放快捷按钮。

### 统一的文件导入区（2.3.7）

- 结构转换、台阶图、表面 Pourbaix、DOS 和 COHP/ICOHP 均支持点击云朵或直接拖入文件。
- 台阶图、表面 Pourbaix、DOS、COHP/ICOHP：载入后云朵在同一个导入框内替换为清晰的完整文件路径，支持「更换文件」和「删除文件」；删除后恢复云朵，并清空该功能的旧预览和选择状态。
- 拖入新文件可直接替换当前文件；结构转换仍保留多文件列表，适用于批量导入。

### 结构转换

- 输入：POSCAR、CONTCAR、`.vasp`、`.poscar`、CIF。
- 输出 POSCAR 默认 **Direct（分数坐标）**；勾选 Cartesian 后可切换为笛卡尔坐标。
- 在资源管理器中先选中结构文件，再按 **Ctrl + 鼠标中键单击** 呼出快捷轮盘；Ctrl + 左键拖动仍用于文件复制。
- 云朵导入区提示「点击或拖入添加文件」，载入后切换为文件列表；输出不会覆盖已有同名文件。

### 团簇生成

- 任意 ASE 元素符号，如 Pt、Fe、Co、Ni、Au。
- 输入 `4,5,6,13` 或 `4:12` 生成多个原子数构型。
- 普通初猜与 FCC(111)/(100)/(110) 取向初猜；支持批量输出。
- 按勾选生成 POSCAR 和/或 CIF；自动估计晶格常数并可覆盖。

**科学边界：**所有团簇都是初始几何猜测，需要进一步进行 VASP 结构弛豫与收敛性验证。

### 自动绘制反应台阶图（HER / ORR / OER）

- 左侧「**绘制台阶图**」紧随「团簇生成」之后；首页也有独立入口。选择 HER / ORR / OER 后可点击 **「下载当前模式 Excel 模板」**，ORR/OER 模板从第 2 行填入数据；HER 下载的是原始七列分块 Excel（含 H2O / H2 参照及十个催化剂分块）；所有数值均已清空，请填写参考气体能量和催化剂的 DFT 能量/修正值。ORR/OER 共用一份 `Material、DG_OH、DG_O、DG_OOH` 模板。
- 点击云朵或把 Excel .xlsx / .xlsm 拖入。支持提供的碱性 HER 自由能数据模板：第一列为催化剂/中间态，B 列为 DFT 能量，C 列为中间态校正，E 列为参照分子；D/F/G 原来的计算结果已清空，软件会根据 B/C 和气体参考重新计算（U=0 V）。也兼容旧九列表（B/E/G/I）和三列长表。ORR/OER 使用原始五行分块（Pt4、Pt3Co 等名称保留），数值同样全部清空；程序读取 B/C 输入并计算，避免引用陈旧的 G 列值。识别同一个催化剂的多段反应并绘图。
- 也支持更直接的 Material / State / DeltaG 三列表格；一个 Excel 可以包含多个体系。
- 按 Origin 模板将可比的催化剂曲线叠加显示，并为具有更多中间态的体系单独绘制完整路径；使用水平能级线、虚线连接和自由能标注，预览后可**勾选所需格式**导出 **PNG、SVG、CSV、Excel XLSX、Origin DAT / 原生 OPJU**。
- 原始数据如有修改，应保存 Excel 后再导入；当前由 Python 读取数值并绘制，不会修改原始文件。
- **Origin DAT** 是可导入 Origin 的制表符数据；**原生 Origin OPJU** 是在本机 Origin 自动化接口支持下，打开用户指定的原始工程、更新符合匹配条件的已有图形并另存新工程。导出前会自动检测 OriginPro；不会覆盖用户原模板，也不会将工程上传至公开仓库。

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
- 可分别勾选导出 **PNG、SVG、Excel XLSX、CSV、Origin DAT / 原生 OPJU**。表格中包含每个网格的 pH、SHE/RHE 电位、稳定吸附态、PhaseID 以及四种表面态的自由能，可在 Excel/Origin 中继续处理；DAT **不是**原生 `.opju` 工程。
- **科学边界**：这是表面吸附态的 **CHE 下包络稳定区图**，并不是 pymatgen `PourbaixDiagram` 所处理的体相/溶解物种相图；不包含金属溶解、离子浓度、显式溶剂、不同吸附覆盖度及动力学，不能直接用来推断催化剂的抗腐蚀稳定性。

### 统一云朵导入

结构转换、DOS、COHP/ICOHP、台阶图和表面 Pourbaix 页面都使用已修复的「**点击或拖入添加文件**」云朵入口。按钮打开本地文件选择器，支持资源管理器拖放；DOSCAR/vasprun.xml、COHPCAR/ICOHPLIST 会自动匹配所需类型。

### VASP DOS / PDOS

- 读取 `vasprun.xml`（推荐）或 `DOSCAR`。
- 选择 Fe、Co、Ni、Pt 等元素，按 s/p/d 轨道和自旋通道绘制。
- 设定相对费米能级的窗口，计算 DOS 积分与 **d 带中心**。
- DOSCAR 按元素投影需要同目录下对应的 POSCAR / CONTCAR；暂不支持非共线 / SOC 四分量投影。
- 输出 PNG、SVG、CSV、**Excel XLSX**、**Origin DAT / 原生 OPJU**。

### LOBSTER COHP / ICOHP

- 读取 `COHPCAR.lobster` 与 `ICOHPLIST.lobster`，同目录文件会自动关联。
- 按键编号多选、自旋分辨、指定能量窗口，支持标准 **-COHP / -ICOHP** 绘制。
- 查询键长与各自旋 **ICOHP(Ef)**；仅有 ICOHPLIST 时仍能导出键强度表。
- 输出 PNG、SVG、CSV、**Excel XLSX**、**Origin DAT / 原生 OPJU**（没有 COHPCAR 时不生成曲线图片）。

Excel 是可以编辑的工作簿，COHP 文件含 ICOHP(Ef) 汇总和完整曲线工作表；**Origin DAT** 是可在 Origin 中导入的制表符数据文件，**Origin OPJU** 是 Origin 原生工程文件，两者不能混淆。原始 COHP/ICOHP 符号保留在数值导出中，负的 ICOHP 通常代表成键贡献。

## 导出格式选择（2.3.1）

- 结构转换：原有 CIF、POSCAR 两个格式入口保持独立。
- 团簇生成：勾选 POSCAR、CIF 或两者，未勾选的格式不生成。
- HER/ORR/OER 台阶图、表面 Pourbaix、DOS、COHP/ICOHP：在每个工作区勾选 PNG、SVG、Excel、Origin DAT / 原生 OPJU、CSV 中需要的格式，再点「导出所选格式」；不再强制保存全部文件。纯 ICOHPLIST 数据无法导出不存在的曲线图像。

## 工作引擎：六个独立检测项

| Python 库 | 用途 | 检测方式 |
| --- | --- | --- |
| **ASE** | POSCAR/CIF、团簇与原子操作 | 单独检测 ASE |
| **pymatgen** | VASP、LOBSTER 文件解析及表面吸附物组成计算 | 单独检测 pymatgen |
| **NumPy** | 数组、积分和数值指标 | 单独检测 NumPy |
| **Matplotlib** | DOS / COHP / 台阶图 / 表面 Pourbaix 及 PNG / SVG 导出 | 单独检测 Matplotlib |
| **openpyxl** | Excel `.xlsx` 解析及工作簿写出 | 单独检测 openpyxl |
| **OriginPro** | Windows Origin 2024 原生 `.opju` 自动化导出 | 检查 `originpro` 包及实际 Origin 自动化连接 |

六项共享用户选择的 Python 可执行文件，但 **状态与检测相互独立**。更改 Python 路径后，旧检测结果会被标记为需要重新检测。OriginPro 检测会短暂启动并关闭自动化 Origin 实例；建议先保存正在编辑的 Origin 工程。

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

Windows CI 自动安装科学计算所需 Python 库，验证 ASE 转换、基础 Python 包检测与 OriginPro 不可用时的错误提示、DOS/COHP 数据处理、HER 模板、ORR/OER CHE 数值与终态、Excel/Origin 导出，并对独立 EXE 进行启动检查。Origin 2024 的真实 COM 连接仍须在本机测试。

## 开源许可

DFTDesk 采用 [MIT License](LICENSE)。部分 UI、轮盘和 Windows 组件复用 [KnifeLemon/Filee](https://github.com/KnifeLemon/Filee) 的 MIT 代码；版权和第三方许可参见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。ASE 采用 LGPL-2.1-or-later，作为外部 Python 库调用。个人赞助二维码通过本地文件或 Actions Secrets 注入，不提交至公开源码。

### Origin 2024 原生 OPJU 导出

在「绘制台阶图」中勾选 **Origin OPJU（保留模板图）**。导出时选择你自己电脑上的原始 `.opju` 项目，再选择保存目录。此功能需要 Windows 本机安装 Origin 2021+（Origin 2024 支持），并在「工作引擎」配置已安装 `originpro` 的 Python。软件从原工程打开已有的 Book 和 Graph，向已有 Book 添加 `DFTDesk_Profiles` 工作表，将计算结果关联到已有绘图对象，并另存新 `.opju`；不会将原工程或原始数值上传到 GitHub，也不会覆盖原工程。

**保真限制：** 为避免损坏原图版式，目前仅在原工程恰有一张图、其已有绘图曲线数量与催化剂数相同、且能唯一匹配时自动重绑。若 Origin 模板使用多条子曲线构成一个台阶图、存在多张候选图、固定文字标注或特殊图层关系，需要在 Origin 中根据提示核验或建立对应映射；当前 CI 没有 Origin 许可证，只能自动测试数据映射与安全检查，不能声称已对用户上传的原始 OPJU 做过视觉一致性验收。


### OriginPro 运行环境检测（2.3.7）

「工作引擎」现有 ASE、pymatgen、NumPy、Matplotlib、openpyxl 检测旁增加 **OriginPro** 检测卡片。此检测先检查当前配置 Python 的 `originpro` 包版本，再通过独立限时进程验证 Windows Origin 自动化连接（不会打开或更改个人 `.opju` 项目）；若安装了 Python 包但无法连接 Origin，会明确区分错误原因。导出原生 `.opju` 前自动执行相同检查，失败时不输出冒充有效工程的文件。只有原生 OPJU 功能要求 Origin 2021+，其余 DFTDesk 模块不依赖它。

在「工作引擎」指定的 Python 环境安装：`python -m pip install originpro`。Origin 2024 可用；连接检查可能短暂启动一个独立的 Origin 自动化实例。
