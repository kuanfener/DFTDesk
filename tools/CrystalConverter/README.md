# Filee Crystal · 独立 POSCAR / CIF 转换器

这是一个 **Windows 独立小程序**，与 Filee 主程序分开运行、设置和打包；不会替换或改动主程序的格式分类、轮盘、预设、引擎优先级。

## 功能与操作

- 识别 VASP `POSCAR` / `CONTCAR`（无扩展名）、`.vasp`、`.poscar` 和 `.cif`。
- 主窗口参照 Filee Soft **浅色**界面：左侧导航、首页拖放区、最近转换记录；批量队列、轮盘预览、转换设置和主题位于单独页面，窗口可缩放。
- 轮盘**直接链接原版 Filee 的 Avalonia `DonutMenu` 源代码**，包括扇区几何、悬停效果、弹簧动画、阴影与原生拖放。独立程序仅提供 CIF / POSCAR 两个目标，主程序轮盘不改动；主窗口预览和资源管理器弹出轮盘共用同一套控件。
- 支持多选文件、拖进窗口和批量转换；轮盘上半部输出 CIF、下半部输出 POSCAR 格式。
- **资源管理器**中选中一个或多个文件，按住 **Ctrl + 鼠标左键拖动**，会在鼠标附近显示独立轮盘；把文件拖入目标扇区并松开即可转换，拖入有效扇区时 Windows 应显示“复制”而非“×”。也支持在资源管理器中选中文件后按 `Ctrl + Alt + Space` 调出轮盘，再**点击**目标格式。
- 主窗口首页也可直接把文件拖入浅紫色区域，或点击该区域多选文件；随后在出现的轮盘中点击要导出的格式。批量转换页也保留“导出 CIF / 导出 POSCAR”按钮。
- 可设置统一输出文件夹；留空时保存在源文件所在文件夹。遇到同名文件自动加 `(2)`、`(3)`，**不覆盖输入或已有文件**。
- 输出 POSCAR 格式时可选择 `Direct`（分数坐标，默认）或 `Cartesian`（笛卡尔坐标）。输出扩展名为 `.vasp`，可按 VASP 习惯重命名为无扩展名 `POSCAR`。
- 数值输出参考 VESTA Export：晶格矢量与 CIF 晶胞参数固定 **10 位小数**，原子坐标固定 **8 位小数**，小量统一写为 `0.0000000000` 而非 `E-16`；内部运算仍保留双精度。固定小数位会舍入极小差别，因此与原始输入不保证逐位相同。
- 全部转换都在本机完成，不上传结构。

> Filee 主程序的默认手势也是 **Ctrl + 左键拖动**，而不是只按一下 Ctrl。如果同时运行两个程序，它们可能响应同一个全局手势；建议只启用其中一个程序的轮盘。

> Windows 若以管理员身份运行本程序，而资源管理器以普通权限运行，系统可能因权限隔离阻止文件拖放；请使用同一权限级别（通常都用普通权限）启动。

## 格式支持范围

- POSCAR：VASP 5 的元素行、Direct / Cartesian、常见缩放因子；原子坐标和晶胞完整转换。
- CIF：晶胞长度/角度、原子位置、显式的对称操作；写出 POSCAR 前展开对称等价位置。
- CIF 输出使用 `P 1` 全原子位点；空间群元数据不会往返保留。
- POSCAR 的 Selective dynamics 标记不会写入 CIF。部分占据、无对称操作的非 P1 CIF 以及 VASP 4 不含元素名称的 POSCAR 会给出明确错误，避免静默生成错误结果。

## 下载与构建

GitHub Actions 的 **Crystal Converter (standalone)** 工作流完成后，在构建结果的 Artifacts 中下载 `Filee-CrystalConverter-win-x64`，解压运行 `Filee.CrystalConverter.exe`。它使用 .NET 自包含单文件发布，运行电脑不需要额外安装 .NET。

也可以在安装 .NET 10 SDK 的 Windows 电脑上执行：

```powershell
dotnet test tools/CrystalConverter.Tests/CrystalConverter.Tests.csproj -c Release
dotnet publish tools/CrystalConverter/CrystalConverter.csproj -c Release -r win-x64 --self-contained true -o dist/CrystalConverter
```

这是未签名的开发构建，Windows 可能显示 SmartScreen 提示。请从可信的仓库 Actions 下载并核对来源。
