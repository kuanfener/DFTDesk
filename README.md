# DFTDesk 1.1.0

**面向 DFT / 计算材料研究的 Windows 科研结构工作台。**

DFTDesk 目前提供两项基于 [ASE — Atomic Simulation Environment](https://ase-lib.org/) 的主要功能：**POSCAR / CONTCAR ⇄ CIF 结构转换**与**单元素团簇初始构型生成**。Windows 主程序为自包含单文件 EXE；结构计算调用用户本机已安装的 Python + ASE。

![DFTDesk](src/DFTDesk/Assets/Icons/dftdesk.png)

[![Windows build](https://github.com/kuanfener/DFTDesk/actions/workflows/dftdesk-windows.yml/badge.svg)](https://github.com/kuanfener/DFTDesk/actions/workflows/dftdesk-windows.yml)

## 下载与配置

1. 打开 [DFTDesk Windows 构建](https://github.com/kuanfener/DFTDesk/actions/workflows/dftdesk-windows.yml)，选择成功运行，在 **Artifacts** 下载 `DFTDesk-win-x64`。
2. 解压后运行 `DFTDesk.exe`，不需要另外安装 .NET。
3. 安装或使用已有的 Python 环境，在该环境执行：

```powershell
python -m pip install ase
python -c "import ase,sys; print('ASE', ase.__version__); print(sys.executable)"
```

4. 在软件 **「工作引擎」** 页面粘贴输出的 `python.exe` 完整路径，点击「检测 ASE」。结构转换和团簇生成共用这一环境。

## 六个工作区

| 页面 | 内容 |
| --- | --- |
| 首页 | DFTDesk 工作台、两项功能快捷入口、ASE 状态与使用提示 |
| 结构文件格式转换 | 文件拖放与列表、POSCAR/CIF 互转、批量输出、环形工具栏、历史与进度 |
| 团簇生成 | 输入元素、原子数、普通或 FCC 晶面取向，批量输出 POSCAR 和 CIF |
| 主题 | Soft / Material You、外观、强调色、缩放、圆角和轮盘透明度 |
| 工作引擎 | Python / ASE 路径设置、安装检测 |
| 关于 | 项目版本、开源许可证、相关链接 |

### 结构转换

接受 `POSCAR`、`CONTCAR`、`.vasp`、`.poscar` 和 `.cif`。输出 POSCAR **默认 Cartesian（Å）**，可取消勾选改为 Direct。支持从资源管理器按 Ctrl + 左键拖动或 Ctrl + Alt + Space 打开轮盘，或在软件的同一页面多选文件后按按钮批量转换。同名文件自动避让，不覆盖原始文件。

### 团簇生成

基于仓库中用户提供的两个 ASE 脚本改编实现，支持：

- **元素**：如 Pt、Fe、Co、Ni、Au，按 ASE 元素符号识别。
- **原子数**：输入 `4,5,6,13`、`4:12`、`4:12:2`，支持批量生成。
- **普通团簇**：三原子三角形、四原子四面体、五原子三角双锥、六原子八面体、十三原子二十面体，其他数量使用参考体相截取初猜。
- **晶面取向团簇**：FCC(111)、FCC(100)、FCC(110)，支持 `all` 批量或 `111,100` 自选；`ico` 支持完整壳层魔数 13/55/147/309/561/923。
- **参数与输出**：ASE 参考晶格常数/近邻距离自动估算；晶面模式可手动覆盖 FCC 晶格常数；每侧真空默认 12 Å；POSCAR **默认 Cartesian**、可选 Direct；每个结构同时生成 POSCAR + CIF。
- 每次生成都在所选根目录创建独立结果文件夹，不覆盖已有结果；支持取消和输出日志。

**科学边界：** 生成结果仅为有限团簇的几何初始猜测，不能直接代表最低能量结构；应通过 VASP 弛豫和真空、晶格参数收敛性检查确认。非 FCC 元素的 FCC 晶面是*假设晶型*的初猜，小团簇不具有严格宏观晶面定义。

## 源码构建

需要 .NET 10 SDK；在 Windows x64 中执行：

```powershell
dotnet test tests/DFTDesk.Tests/DFTDesk.Tests.csproj -c Release
dotnet publish src/DFTDesk/DFTDesk.csproj -c Release -r win-x64 --self-contained true -o dist/DFTDesk
```

CI 运行真实 ASE 结构转换和团簇生成测试。外部 Python、NumPy 和 ASE 不随发布的 EXE 分发。

## 仓库结构

```text
src/DFTDesk/                   Avalonia 界面、ASE 转换、团簇生成与 Python 桥接
  ClusterScripts/             改编后的 ASE 团簇生成算法（嵌入 EXE）
tests/DFTDesk.Tests/           转换与团簇生成测试
third_party/Filee/            复用的 MIT UI、轮盘与 Windows 组件
.github/workflows/            Windows 构建
THIRD-PARTY-NOTICES.md        第三方版权及许可
```

## 版权

DFTDesk 采用 MIT License。部分 UI、轮盘和 Windows 交互组件基于 [KnifeLemon/Filee](https://github.com/KnifeLemon/Filee) 的 MIT 代码，版权和许可在 [LICENSE](LICENSE)、[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) 中保留。ASE 采用 LGPL-2.1-or-later，作为用户独立安装的外部 Python 库调用。个人赞助二维码通过本地文件或 Actions Secrets 注入，不上传公开源码。
