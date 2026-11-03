# DFTDesk 1.0.0 · DFT 文件与数据处理工作台

这是 DFTDesk 的首个正式版本（1.0.0），面向 DFT 与计算材料研究的 Windows 桌面工具平台。当前首批功能是 POSCAR / CONTCAR ⇄ CIF 转换；后续可扩展结构处理、VASP/Gaussian 输出解析和计算数据分析。界面与部分轮盘组件复用 Filee 开源项目的代码，保留其 MIT 许可与版权声明。

## 侧边栏与原版 Filee 一致性

DFTDesk 使用上游开源组件的导航参数，而不是额外放大：展开/收起宽度为 230/64、内容边距为 (14,18)、导航行内部 Border 的内边距为 (12,9)、导航项间距为 (0,1)、图标尺寸 17、图标与文字间距 12。菜单行使用与原版一致的 Border 包裹结构，由共享的 Soft 样式控制悬停和选中态。

## 功能与操作

- 识别 VASP `POSCAR` / `CONTCAR`（无扩展名）、`.vasp`、`.poscar` 和 `.cif`。
- 主窗口使用上游开源组件的 **Avalonia `Soft.axaml` 样式、图标资源和调色逻辑**，而非此前手绘的 WinForms 界面；左侧导航、首页拖放区、最近转换记录、主题设置、轮盘预览和卡片使用原版设计语言。支持浅色／深色／跟随系统、Material You、强调色、圆角、轮盘透明度及窗口缩放。
- 轮盘**直接链接上游开源组件的 Avalonia `DonutMenu` 源代码**，包括扇区几何、悬停效果、弹簧动画、阴影与原生拖放。独立程序仅提供 CIF / POSCAR 两个目标，主程序轮盘不改动；主窗口预览和资源管理器弹出轮盘共用同一套控件。
- 支持多选文件、拖进窗口和批量转换；轮盘上半部输出 CIF、下半部输出 POSCAR 格式。
- **资源管理器**中选中一个或多个文件，按住 **Ctrl + 鼠标左键拖动**，会在鼠标附近显示独立轮盘；把文件拖入目标扇区并松开即可转换，拖入有效扇区时 Windows 应显示“复制”而非“×”。也支持在资源管理器中选中文件后按 `Ctrl + Alt + Space` 调出轮盘，再**点击**目标格式。
- 主窗口首页也可直接把文件拖入浅紫色区域，或**双击**该区域多选文件；随后在出现的轮盘中点击要导出的格式。“常规”页提供批量队列和转换状态，“环形工具栏”页提供“导出 CIF / 导出 POSCAR”按钮。
- 可设置统一输出文件夹；留空时保存在源文件所在文件夹。遇到同名文件自动加 `(2)`、`(3)`，**不覆盖输入或已有文件**。
- 输出 POSCAR 格式时可选择 `Cartesian`（笛卡尔坐标，**原生引擎默认**）或 `Direct`（分数坐标，ASE 引擎默认）。两种引擎分别记住各自的坐标设置，已有设置文件会自动补全默认值。输出扩展名为 `.vasp`，可按 VASP 习惯重命名为无扩展名 `POSCAR`。
- 数值输出参考 VESTA Export：晶格矢量与 CIF 晶胞参数固定 **10 位小数**，原子坐标固定 **8 位小数**，小量统一写为 `0.0000000000` 而非 `E-16`；内部运算仍保留双精度。固定小数位会舍入极小差别，因此与原始输入不保证逐位相同。
- 全部转换都在本机完成，不上传结构。

> 上游开源组件的默认手势也是 **Ctrl + 左键拖动**，而不是只按一下 Ctrl。如果同时运行两个程序，它们可能响应同一个全局手势；建议只启用其中一个程序的轮盘。

> Windows 若以管理员身份运行本程序，而资源管理器以普通权限运行，系统可能因权限隔离阻止文件拖放；请使用同一权限级别（通常都用普通权限）启动。

> 独立程序的转换功能仍限定为晶体格式；原版 DFTDesk 侧边栏中的预设、自动转换文件夹等位置在这里显示晶体转换相关的设置/入口，不代表把原版图片、PDF 等所有转换引擎也打包进来。界面源码共享不意味着在所有 Windows DPI 及资源管理器版本上已完成像素级验收，建议结合截图核对。

## 可选 ASE 结构转换引擎

DFTDesk 现在提供两种可切换的 POSCAR / CONTCAR ⇄ CIF 引擎：

- **DFTDesk 原生引擎（默认）**：纯 C#，无需 Python；保留现有 VESTA 风格固定小数精度与 CIF P1 处理规则。
- **ASE — Atomic Simulation Environment（可选）**：使用用户本机的 Python + ASE 进行格式转换，适合需要 ASE 晶体结构读写行为的研究场景。ASE 结果可能与原生引擎在数值位数、元素排序和对称结构展开方面有所不同；请在计算前核对晶胞和原子数。

在终端中为你要使用的 Python 环境安装 ASE：

```powershell
python -m pip install ase
```

然后进入 **DFTDesk → 转换引擎**，选择 ASE，填写 `python` 或你的 Conda / venv 环境中 `python.exe` 的**完整路径**，点击 **检测 ASE**。选择会保存在 `%LOCALAPPDATA%\\DFTDesk\\engine.json`；主窗口批量转换和 Ctrl 拖放轮盘都使用选中的引擎。ASE 无法启动或未安装时会显示错误，不会静默切回其他引擎。输出会先写入临时文件，成功后再原子重命名；自动避让已有同名文件，失败则清理临时输出。

**许可证**：ASE 是独立的第三方项目，版权归 ASE contributors，采用 [LGPL-2.1-or-later](https://gitlab.com/ase/ase) 许可证。DFTDesk 通过外部 Python 进程使用 ASE，**不会在 DFTDesk.exe 或 GitHub Actions 发布包中捆绑 ASE、Python 或 NumPy**。如果以后更改为打包分发 ASE，需要按 LGPL 要求附带相应许可和源代码获取／替换机制。

## DFTDesk 赞助码（本地配置）

- 赞助窗口只显示黑白微信 / 支付宝二维码，不提供下载、保存或复制按钮。
- **个人支付二维码和支付链接不提交到公开源码仓库。** 本地运行时，将二维码图片放在：
  - `%LOCALAPPDATA%\\DFTDesk\\Donation\\wechat.png`
  - `%LOCALAPPDATA%\\DFTDesk\\Donation\\alipay.png`
- 本机执行 `dotnet publish` 时，项目会自动把上述本地图片嵌入 EXE；也可以仅放在该目录，由运行中的程序读取。
- 若要让 GitHub Actions 生成带有赞助码的发布包，请在仓库 **Settings → Secrets and variables → Actions** 中添加两个 Repository secrets：`DFTDESK_WECHAT_QR_BASE64` 和 `DFTDESK_ALIPAY_QR_BASE64`。值为对应 PNG 文件的 Base64 内容。工作流只在构建时读取 secrets，不会把支付链接或图片写入源码。
- Windows PowerShell（已安装 GitHub CLI 并登录）可用以下命令设置 secrets：

```powershell
$dir = Join-Path $env:LOCALAPPDATA 'DFTDesk\\Donation'
$wechat = [Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $dir 'wechat.png')))
$alipay = [Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $dir 'alipay.png')))
gh secret set DFTDESK_WECHAT_QR_BASE64 --body $wechat
gh secret set DFTDESK_ALIPAY_QR_BASE64 --body $alipay
```

- 二维码只在窗口中显示，不提供应用内导出入口。但屏幕上可见的二维码仍可能被截图、拍照或从 EXE 中提取，不能承诺技术上阻止传播。若不配置 secrets，CI 生成的公开构建不会包含你的个人收款码。

## 格式支持范围

- POSCAR：VASP 5 的元素行、Direct / Cartesian、常见缩放因子；原子坐标和晶胞完整转换。
- CIF：晶胞长度/角度、原子位置、显式的对称操作；写出 POSCAR 前展开对称等价位置。
- CIF 输出使用 `P 1` 全原子位点；空间群元数据不会往返保留。
- POSCAR 的 Selective dynamics 标记不会写入 CIF。部分占据、无对称操作的非 P1 CIF 以及 VASP 4 不含元素名称的 POSCAR 会给出明确错误，避免静默生成错误结果。

## 下载与构建

GitHub Actions 的独立 Windows 构建完成后，在构建结果的 Artifacts 中下载 `DFTDesk-win-x64`，解压运行 `DFTDesk.exe`。发布包只包含 `DFTDesk.exe`、`README.txt` 和必须保留的 `LICENSE.txt`，不再分发 `Filee.Core`、`Filee.Platform.Windows` 等单独的 DLL/XML/PDB 文件；这些程序依赖被打包在自包含 EXE 内。运行电脑无需另装 .NET。请勿删除 MIT 许可声明。

也可以在安装 .NET 10 SDK 的 Windows 电脑上执行：

```powershell
dotnet test tests/DFTDesk.Tests/DFTDesk.Tests.csproj -c Release
dotnet publish src/DFTDesk/DFTDesk.csproj -c Release -r win-x64 --self-contained true -o dist/DFTDesk
```

这是未签名的开发构建，Windows 可能显示 SmartScreen 提示。请从可信的仓库 Actions 下载并核对来源。


## 许可与致谢

DFTDesk 的晶体结构转换代码由本项目维护。界面样式、图标资源及 DonutMenu 的部分实现复用了 Filee 项目（作者 Myeonggu Kim / KnifeLemon 与贡献者），遵循 MIT License。发布包附带 `LICENSE.txt`；请保留该许可和版权声明。
