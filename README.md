# DFTDesk

**面向 DFT / 计算材料研究的 Windows 科研文件与数据处理工作台。**

DFTDesk 目前专注于 VASP 结构文件 **POSCAR / CONTCAR ⇄ CIF** 的转换、批处理和文件拖放，后续可扩展 VASP / Gaussian 数据处理。**结构转换只使用 ASE（Atomic Simulation Environment）；原生 C# POSCAR/CIF 转换器已移除。**

![DFTDesk](src/DFTDesk/Assets/Icons/dftdesk.png)

[![Windows build](https://github.com/kuanfener/DFTDesk/actions/workflows/dftdesk-windows.yml/badge.svg)](https://github.com/kuanfener/DFTDesk/actions/workflows/dftdesk-windows.yml)

## 下载

1. 前往 [DFTDesk Windows 构建](https://github.com/kuanfener/DFTDesk/actions/workflows/dftdesk-windows.yml)，打开成功的工作流运行。
2. 在 **Artifacts** 下载 `DFTDesk-win-x64`，解压后双击 `DFTDesk.exe`。
3. 程序为 Windows x64 自包含单文件 EXE，**无需额外安装 .NET，但转换功能需要另行安装 Python + ASE**。

发布包包含 `DFTDesk.exe`、`README.txt` 与 `LICENSE.txt`。

## 首次使用：配置 ASE（必需）

DFTDesk 使用已安装的 Python 环境调用 ASE，不随 EXE 捆绑 Python、ASE 或 NumPy。

在 PowerShell、Miniconda Prompt 或 PyCharm 的 Python 环境中运行：

```powershell
python -m pip install ase
python -c "import ase,sys; print('ASE:', ase.__version__); print('Python:', sys.executable)"
```

将输出的 `python.exe` 完整路径填入 **DFTDesk → 工作引擎 → Python 可执行文件**，点击 **检测 ASE**。检测成功即可使用轮盘和批量转换。已安装 Miniconda 的用户应填写对应环境下的 `python.exe`，不必重复安装 Python。

## 当前功能

| 功能 | 说明 |
| --- | --- |
| 晶体结构转换 | 使用 ASE 读写 POSCAR / CONTCAR ⇄ CIF |
| 工作引擎 | **仅 ASE**；可设置 Python 路径并检测环境 |
| POSCAR 坐标 | 默认为 **Direct**，可在「预设」勾选 **Cartesian** |
| 批量与拖放 | 多选文件、拖放、环形工具栏、Ctrl + 鼠标左键拖动 |
| 文件安全 | 自动避让同名文件，不覆盖源文件；失败清理临时文件 |
| 外观 | DFTDesk 主题、Soft / Material You、浅色 / 深色模式 |
| 赞助 | 微信和支付宝二维码可在本地配置或构建时通过 GitHub Secrets 注入 |

**注意：** ASE 写出的 POSCAR 在本项目中使用 `sort=True` 按元素排序。CIF 的部分占位、空间群、POSCAR 的 Selective Dynamics 等信息无法保证完整往返；重要 VASP 输入文件需核对晶胞、元素顺序、原子数和约束。ASE 使用其自身输出精度，不保证与 VESTA 的固定小数格式完全相同。

## 构建

Windows 开发需要 .NET 10 SDK：

```powershell
dotnet test tests/DFTDesk.Tests/DFTDesk.Tests.csproj -c Release
dotnet publish src/DFTDesk/DFTDesk.csproj -c Release -r win-x64 --self-contained true -o dist/DFTDesk
```

GitHub CI 会在测试机中安装 ASE 并执行真实转换测试；这些 Python 依赖不会进入最终下载的 EXE。

## 仓库结构

```text
src/DFTDesk/                   DFTDesk UI、ASE 桥接、格式检测、批处理
tests/DFTDesk.Tests/           ASE 往返转换及安全测试
third_party/Filee/            复用的 Filee MIT 组件（轮盘、界面、Windows 交互）
.github/workflows/            Windows 构建
THIRD-PARTY-NOTICES.md        第三方版权及许可
```

## 许可证

DFTDesk 采用 **MIT License**。部分 UI、环形轮盘和 Windows 交互组件基于 [KnifeLemon/Filee](https://github.com/KnifeLemon/Filee) 的 MIT 开源代码，原作者版权与许可在 [LICENSE](LICENSE)、[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) 中保留。第三方源码在 [third_party/Filee](third_party/Filee) 中单独维护。

**ASE** 由 ASE contributors 开发，采用 LGPL-2.1-or-later 许可，作为外部 Python 软件运行，不随 DFTDesk 分发。本仓库的 GitHub fork 关系仅用于保留上游来源。
