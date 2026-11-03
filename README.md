# DFTDesk

**面向 DFT / 计算材料研究的 Windows 文件与数据处理工作台。**

DFTDesk 从 **POSCAR / CONTCAR ⇄ CIF** 起步，逐步整合结构转换、VASP 与 Gaussian 结果整理、批量处理及可视化工具。主程序为独立的 Windows x64 桌面应用；不需要安装 .NET。

![DFTDesk logo](src/DFTDesk/Assets/Icons/dftdesk.png)

[![Windows build](https://github.com/kuanfener/DFTDesk/actions/workflows/dftdesk-windows.yml/badge.svg)](https://github.com/kuanfener/DFTDesk/actions/workflows/dftdesk-windows.yml)

## 下载与使用

1. 打开 **[DFTDesk Windows Actions](https://github.com/kuanfener/DFTDesk/actions/workflows/dftdesk-windows.yml)**，选择一次成功的构建。
2. 在 **Artifacts** 下载 `DFTDesk-win-x64`，解压并运行 `DFTDesk.exe`。
3. 将 POSCAR、CONTCAR、VASP 结构或 CIF 拖入界面，可在环形工具栏或批量任务中转换。

发布包只有 `DFTDesk.exe`、`README.txt`、`LICENSE.txt` 三个文件。不附带原项目的多格式转换程序、安装器或额外的 DLL。

## 功能

| 模块 | 当前功能 |
| --- | --- |
| 晶体结构转换 | POSCAR / CONTCAR ⇄ CIF，批量队列与文件拖放 |
| 原生引擎 | C# 实现，无需 Python；POSCAR 默认 **Cartesian**（可选 Direct） |
| ASE 引擎 | 可选外部 Python + ASE；POSCAR 默认 Direct（可独立调整） |
| 输出安全 | 不覆盖原始文件，同名自动追加序号 |
| 格式 | 晶格 10 位、原子坐标 8 位定点小数（原生引擎） |
| 界面 | Soft / Material You、亮暗主题、Ctrl + 左键拖动环形轮盘 |
| 赞助 | 可由维护者在构建时通过 GitHub Secrets 注入黑白二维码，不在公开仓库保存收款码 |

注意：CIF 对称性展开、非整占位、Selective Dynamics 等元数据在不同格式中并不保证完全往返。重要 VASP 结构应核查原子数、元素顺序、坐标和固定原子约束。

## ASE 可选安装

DFTDesk 默认直接使用 C# 引擎，不依赖 Python。如果想用 ASE：

```powershell
python -m pip install ase
python -c "import sys; print(sys.executable)"
```

在 **转换引擎** 页面填入第二条命令显示的 `python.exe` 完整路径，然后点击 **检测 ASE**。ASE 通过外部 Python 环境运行，**不包含在 DFTDesk.exe 中**。

## 从源码构建

需 .NET 10 SDK；Windows x64 发布：

```powershell
dotnet test tests/DFTDesk.Tests/DFTDesk.Tests.csproj -c Release
dotnet publish src/DFTDesk/DFTDesk.csproj -c Release -r win-x64 --self-contained true -o dist/DFTDesk
```

原生和 ASE 的自动化测试在 GitHub Actions 上运行，后者仅在 **CI 测试环境**安装 ASE，不会进入下载包。

## 项目结构

```text
src/DFTDesk/                    Windows 桌面程序、原生/ASE 转换引擎
tests/DFTDesk.Tests/            转换与兼容性测试
third_party/Filee/             必须保留的上游 MIT 代码（界面、轮盘、Windows 支持）
.github/workflows/             DFTDesk 构建与发布
THIRD-PARTY-NOTICES.md         依赖、版权及许可说明
```

## 许可证和来源

DFTDesk 采用 **MIT License**。部分界面样式、轮盘与 Windows 组件源自 [Filee (KnifeLemon)](https://github.com/KnifeLemon/Filee)，相关原作者版权声明、MIT 许可和必要源代码保留在本仓库。详细信息见 [LICENSE](LICENSE)、[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) 和 [third_party/Filee](third_party/Filee)。

可选 **ASE (Atomic Simulation Environment)** 属于独立第三方项目，采用 **LGPL-2.1-or-later**，未随 DFTDesk 分发。

本仓库目前是 GitHub 上的 Filee fork；为保留来源和历史，仓库内部的第三方代码及 GitHub 的 fork 标识会继续存在，但 DFTDesk 的软件入口和发布包独立维护。
