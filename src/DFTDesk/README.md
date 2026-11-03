# DFTDesk 1.1.0 — Windows 科研结构工作台

**DFTDesk 1.1.0 使用 ASE 作为结构转换和团簇生成的统一工作引擎。** 不包含独立的 C# 结构读写引擎。

## 主要功能

- 拖入 POSCAR、CONTCAR、`.vasp`、`.poscar` 或 `.cif` 文件，选择 CIF / POSCAR 输出格式。
- 在转换页面点击云朵导入或拖放结构文件；导入后同区显示文件列表，可继续添加、移除或清空；资源管理器 Ctrl + 左键拖动呼出轮盘。已移除 Ctrl + Alt + Space。
- 导出 POSCAR 默认 **Cartesian**，可在「结构文件格式转换」页面取消勾选切换 Direct。
- 输出前先生成临时文件，成功后以不覆盖已有文件的方式命名；失败时清理临时输出文件。
- 首页为科研工作台；侧边栏只有「首页」「结构文件格式转换」「团簇生成」「主题」「工作引擎」「关于」六个入口。
- 「团簇生成」支持普通团簇、FCC(111)/(100)/(110) 局部取向和完整二十面体壳层魔数；批量输出 POSCAR 和 CIF；输出 POSCAR 默认 Cartesian；保留可选 Direct、真空厚度与晶格常数覆盖。
- 界面仅保留 DFTDesk Soft 风格；主题页统一管理浅色/深色、强调色、圆角、轮盘透明度、动画和实际窗口尺寸。侧边栏展开/收起宽度为 230 / 64。保留上游 MIT 版权信息。

## 配置工作引擎

在可用的 Python / Conda 环境中安装：

```powershell
python -m pip install ase
python -c "import ase,sys; print(ase.__version__); print(sys.executable)"
```

然后在 **DFTDesk → 工作引擎** 填写完整 `python.exe` 路径，点击 **检测 ASE**。程序不会自动安装 Python/ASE，转换前会检查引擎是否可用。旧的 `%LOCALAPPDATA%\DFTDesk\engine.json` 中保存的 Python 路径与 `AseCartesian` 设置仍可读取；旧版 C# 引擎的设定不再使用。

ASE 与 Python/NumPy 均**不随 EXE 打包**。即使下载的是自包含 Windows 单文件 EXE，使用结构转换仍需本地 ASE。ASE 采用 LGPL-2.1-or-later，详情见项目第三方许可说明。

## 团簇生成（新增）

在「团簇生成」页面输入元素和原子数（例如 `Pt` 与 `4,5,6,13`），选择「普通团簇」或「FCC 晶面取向团簇」。晶面可填写 `all`（111/100/110）、指定晶面，或 `ico`（13/55/147/309/561/923 原子完整壳层）。晶格常数自动估算，也可手动覆盖；真空层每侧默认 12 Å。程序为每次运行新建结果文件夹，且每个构型同时输出 POSCAR 和 CIF。所有团簇都只是初猜，需要 VASP 结构弛豫。

## 注意的结构边界

ASE 负责结构读取和写出。当前桥接使用 `read(...,format='vasp'/'cif')` 与 `write(...,format='vasp'/'cif')`，POSCAR 写出启用 `sort=True` 和 `vasp5=True`。ASE 输出精度与 VESTA 并不相同。CIF 部分占位会拒绝写为 POSCAR；Selective Dynamics、空间群元数据和原子顺序不保证跨格式无损保留。重要结构请核对元素顺序、晶胞、原子数与约束。

## 构建和测试

```powershell
dotnet test tests/DFTDesk.Tests/DFTDesk.Tests.csproj -c Release
dotnet publish src/DFTDesk/DFTDesk.csproj -c Release -r win-x64 --self-contained true -o dist/DFTDesk
```

项目自动化测试验证输入格式识别、缺失 Python 的错误反馈、ASE 往返转换、Direct/Cartesian、同名输出保护、团簇模式与临时文件清理。CI 专门安装 ASE 运行集成测试。

## 赞助二维码

为避免将个人收款信息写入公开 Git 历史，默认不提交微信 / 支付宝二维码。软件运行时可从下列目录读取：

```text
%LOCALAPPDATA%\DFTDesk\Donation\wechat.png
%LOCALAPPDATA%\DFTDesk\Donation\alipay.png
```

本机编译会自动将已有的上述 PNG 嵌入 EXE。若要 GitHub Actions 发布的 EXE 自动包含二维码，请在仓库 Actions Secrets 中设置 `DFTDESK_WECHAT_QR_BASE64` 和 `DFTDESK_ALIPAY_QR_BASE64`，内容必须是各自 PNG 文件的 Base64 字符串。构建会验证 PNG 文件头；无效 Secret 不会中断 EXE 发布，但二维码不会嵌入。界面不提供下载按钮，但无法防止截图或从程序中提取图片。

## 版权

DFTDesk 自身以 MIT 授权；图形界面、轮盘及 Windows 支持组件部分沿用 Filee 的 MIT 源码，其原始版权与许可在 LICENSE、THIRD-PARTY-NOTICES 和 third_party/Filee 中保留。
