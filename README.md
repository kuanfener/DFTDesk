# DFTDesk 2.0.0

**面向 VASP / DFT / 计算材料研究的 Windows 桌面工作台。**

DFTDesk 将晶体结构处理、金属团簇初猜、VASP DOS/PDOS、LOBSTER COHP/ICOHP 后处理集中在一个本地软件中，并支持导出可在 **Excel** 和 **Origin** 中继续分析的数据。

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

## 八个工作区

| 页面 | 功能 |
| --- | --- |
| 首页 | DFTDesk 2.0 总览，结构转换、团簇生成、DOS、COHP 四个功能入口 |
| 结构文件格式转换 | 使用 ASE 进行 POSCAR / CONTCAR ⇄ CIF，拖放、多选、批量导出 |
| 团簇生成 | 任意元素、批量原子数、普通几何或 FCC 晶面取向，生成 POSCAR 和 CIF |
| DOS 分析 | VASP DOSCAR、vasprun.xml：TDOS/PDOS、元素 s/p/d、自旋、DOS 积分、d 带中心 |
| COHP / ICOHP 分析 | LOBSTER COHPCAR、ICOHPLIST：多键曲线、自旋分辨、ICOHP(Ef) 键强度 |
| 主题 | 明暗模式、强调色、圆角、窗口尺寸、环形轮盘不透明度与实时预览 |
| 工作引擎 | **ASE、pymatgen、NumPy、Matplotlib、openpyxl 五个独立状态卡片，逐个检测** |
| 关于 | 软件版本、许可与开源链接 |

首页将四个主要功能放在「开始使用」卡片中，顶部简介不重复放快捷按钮。

### 结构转换

- 输入：POSCAR、CONTCAR、`.vasp`、`.poscar`、CIF。
- 输出 POSCAR 默认 **Cartesian（Å）**；可以切换为 Direct。
- 在资源管理器按住 **Ctrl 并用鼠标左键拖动结构文件**，呼出快捷轮盘。
- 云朵导入区在载入后切换为文件列表；输出不会覆盖已有同名文件。

### 团簇生成

- 任意 ASE 元素符号，如 Pt、Fe、Co、Ni、Au。
- 输入 `4,5,6,13` 或 `4:12` 生成多个原子数构型。
- 普通初猜与 FCC(111)/(100)/(110) 取向初猜；支持批量输出。
- 同时生成 POSCAR + CIF；自动估计晶格常数并可覆盖。

**科学边界：**所有团簇都是初始几何猜测，需要进一步进行 VASP 结构弛豫与收敛性验证。

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

## 工作引擎：五个独立检测项

| Python 库 | 用途 | 检测方式 |
| --- | --- | --- |
| **ASE** | POSCAR/CIF、团簇与原子操作 | 单独检测 ASE |
| **pymatgen** | VASP 与 LOBSTER 文件解析 | 单独检测 pymatgen |
| **NumPy** | 数组、积分和数值指标 | 单独检测 NumPy |
| **Matplotlib** | DOS / COHP 曲线及 PNG / SVG 导出 | 单独检测 Matplotlib |
| **openpyxl** | Excel `.xlsx` 工作簿写出 | 单独检测 openpyxl |

五项共享用户选择的 Python 可执行文件，但 **状态与检测相互独立**。更改 Python 路径后，旧检测结果会被标记为需要重新检测。

## 源码构建与测试

需要 **.NET 10 SDK**，Windows x64：

```powershell
dotnet test tests/DFTDesk.Tests/DFTDesk.Tests.csproj -c Release
python -m unittest discover -s tests -p test_dos_analyzer.py
python -m unittest discover -s tests -p test_cohp_analyzer.py
dotnet publish src/DFTDesk/DFTDesk.csproj -c Release -r win-x64 --self-contained true -o dist/DFTDesk
```

Windows CI 自动安装上述 Python 库，验证 ASE 转换、五个 Python 库的独立检测、DOS/COHP 数据处理、Excel/Origin 导出，并对独立 EXE 进行启动检查。

## 开源许可

DFTDesk 采用 [MIT License](LICENSE)。部分 UI、轮盘和 Windows 组件复用 [KnifeLemon/Filee](https://github.com/KnifeLemon/Filee) 的 MIT 代码；版权和第三方许可参见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。ASE 采用 LGPL-2.1-or-later，作为外部 Python 库调用。个人赞助二维码通过本地文件或 Actions Secrets 注入，不提交至公开源码。
