# DFTDesk — Windows 工具程序

**DFTDesk 仅使用 ASE (Atomic Simulation Environment) 作为 POSCAR / CONTCAR ⇄ CIF 工作引擎。** 原生 C# 晶体结构读写引擎已经删除。

## 主要功能

- 拖入 POSCAR、CONTCAR、`.vasp`、`.poscar` 或 `.cif` 文件，选择 CIF / POSCAR 输出格式。
- 批量转换、资源管理器 Ctrl + 左键拖动轮盘、`Ctrl + Alt + Space` 轮盘快捷操作。
- 导出 POSCAR 可选择 **Direct（默认）** 或 **Cartesian**；此选项位于「预设」页面，独立于 ASE 的 Python 环境设置。
- 输出前先生成临时文件，成功后以不覆盖已有文件的方式命名；失败时清理临时输出文件。
- 使用 Avalonia Soft / Material You UI，导航栏展开/收起宽度为 230 / 64，保留上游 MIT 版权信息。

## 配置工作引擎

在可用的 Python / Conda 环境中安装：

```powershell
python -m pip install ase
python -c "import ase,sys; print(ase.__version__); print(sys.executable)"
```

然后在 **DFTDesk → 工作引擎** 填写完整 `python.exe` 路径，点击 **检测 ASE**。程序不会自动安装 Python/ASE，转换前会检查引擎是否可用。旧的 `%LOCALAPPDATA%\DFTDesk\engine.json` 中保存的 Python 路径与 `AseCartesian` 设置仍可读取；旧版 C# 引擎的设定不再使用。

ASE 与 Python/NumPy 均**不随 EXE 打包**。即使下载的是自包含 Windows 单文件 EXE，使用结构转换仍需本地 ASE。ASE 采用 LGPL-2.1-or-later，详情见项目第三方许可说明。

## 注意的结构边界

ASE 负责结构读取和写出。当前桥接使用 `read(...,format='vasp'/'cif')` 与 `write(...,format='vasp'/'cif')`，POSCAR 写出启用 `sort=True` 和 `vasp5=True`。ASE 输出精度与 VESTA 并不相同。CIF 部分占位会拒绝写为 POSCAR；Selective Dynamics、空间群元数据和原子顺序不保证跨格式无损保留。重要结构请核对元素顺序、晶胞、原子数与约束。

## 构建和测试

```powershell
dotnet test tests/DFTDesk.Tests/DFTDesk.Tests.csproj -c Release
dotnet publish src/DFTDesk/DFTDesk.csproj -c Release -r win-x64 --self-contained true -o dist/DFTDesk
```

项目自动化测试验证输入格式识别、缺失 Python 的错误反馈、ASE 往返转换、Direct/Cartesian、同名输出保护和临时文件清理。CI 专门安装 ASE 运行集成测试。

## 赞助二维码

为避免将个人收款信息写入公开 Git 历史，默认不提交微信 / 支付宝二维码。软件运行时可从下列目录读取：

```text
%LOCALAPPDATA%\DFTDesk\Donation\wechat.png
%LOCALAPPDATA%\DFTDesk\Donation\alipay.png
```

本机编译会自动将已有的上述 PNG 嵌入 EXE。若要 GitHub Actions 发布的 EXE 自动包含二维码，请在仓库 Actions Secrets 中设置 `DFTDESK_WECHAT_QR_BASE64` 和 `DFTDESK_ALIPAY_QR_BASE64`，内容必须是各自 PNG 文件的 Base64 字符串。构建会验证 PNG 文件头；无效 Secret 不会中断 EXE 发布，但二维码不会嵌入。界面不提供下载按钮，但无法防止截图或从程序中提取图片。

## 版权

DFTDesk 自身以 MIT 授权；图形界面、轮盘及 Windows 支持组件部分沿用 Filee 的 MIT 源码，其原始版权与许可在 LICENSE、THIRD-PARTY-NOTICES 和 third_party/Filee 中保留。
