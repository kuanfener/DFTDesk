# DFTDesk 2.3.7 — Windows 科研结构工作台

**DFTDesk 2.3.7 使用 ASE 处理结构，使用 pymatgen / NumPy 处理 VASP DOS 与 LOBSTER COHP/ICOHP，使用 openpyxl 输出 Excel。** 不包含独立的 C# 结构读写引擎。

## v2.3.7 Windows 启动与托盘

- 「主题 → Windows 启动」新增「登录 Windows 后自动启动」（默认关闭）和「最小化到系统托盘」（默认开启），修改后按「保存设置」生效。
- 自启写入当前用户 `HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run` 的 `DFTDesk` 项，使用已安装的 `DFTDesk.exe` 完整路径和 `--tray` 参数；无需管理员权限，不会为所有用户配置开机启动。关闭该选项时删除此项。
- 最小化时隐藏桌面窗口，仅保留 Windows 通知区域的 DFTDesk 图标；双击图标恢复，右键菜单可「打开 DFTDesk」或「退出 DFTDesk」。点击窗口右上角关闭按钮仍会正常退出。
- 已配置自启后如移动或更换 EXE 位置，请重新保存设置以刷新启动路径。仅 Windows 支持此功能。

## 主要功能

- 拖入 POSCAR、CONTCAR、`.vasp`、`.poscar` 或 `.cif` 文件，选择 CIF / POSCAR 输出格式。
- 在结构转换、台阶图、DOS 与 COHP 分析页面点击云朵或拖放文件（提示统一为「点击或拖入添加文件」）；导入后同区显示文件列表，可继续添加、移除或清空；资源管理器 Ctrl + 左键拖动呼出轮盘。已移除 Ctrl + Alt + Space。
- 导出 POSCAR 默认 **Cartesian**，可在「结构文件格式转换」页面取消勾选切换 Direct。
- 输出前先生成临时文件，成功后以不覆盖已有文件的方式命名；失败时清理临时输出文件。
- 首页为科研工作台；侧边栏包含「首页」「结构文件格式转换」「团簇生成」「绘制台阶图」「表面 Pourbaix 图」「DOS 分析」「COHP / ICOHP 分析」「主题」「工作引擎」「关于」十个入口。
- 「DOS 分析」支持导入 VASP DOSCAR 或 vasprun.xml，选择元素、s/p/d 轨道、自旋与能量区间，生成 TDOS/PDOS 曲线，计算 DOS 积分及 d 带中心，输出 CSV/Excel XLSX/Origin DAT / 原生 OPJU/PNG/SVG。
- 「团簇生成」支持普通团簇、FCC(111)/(100)/(110) 局部取向和完整二十面体壳层魔数；批量输出 POSCAR 和 CIF；输出 POSCAR 默认 Cartesian；保留可选 Direct、真空厚度与晶格常数覆盖。
- 界面仅保留 DFTDesk Soft 风格；主题页统一管理浅色/深色、强调色、圆角、轮盘透明度、动画和实际窗口尺寸。**轮盘不透明度右侧显示实时轮盘预览**，滑块和强调色调整时立即刷新，与资源管理器实际轮盘使用同一渲染器。侧边栏展开/收起宽度为 230 / 64。保留上游 MIT 版权信息。

## 配置工作引擎

在可用的 Python / Conda 环境中安装：

```powershell
python -m pip install ase pymatgen numpy matplotlib openpyxl
   # 仅当需要导出原生 Origin .opju 工程时，额外安装：
   python -m pip install originpro
python -c "import ase,sys; print(ase.__version__); print(sys.executable)"
```

然后在 **DFTDesk → 工作引擎** 填写完整 `python.exe` 路径，点击 **检测 ASE** 或 **检测 DOS 引擎**。程序不会自动安装这些 Python 包；结构转换前会检测 ASE。旧的 `%LOCALAPPDATA%\DFTDesk\engine.json` 中保存的 Python 路径与 `AseCartesian` 设置仍可读取；旧版 C# 引擎的设定不再使用。

ASE、pymatgen、NumPy、Matplotlib、openpyxl 均**不随 EXE 打包**。即使下载的是自包含 Windows 单文件 EXE，使用结构转换 / DOS 分析仍需本地 Python 环境。ASE 采用 LGPL-2.1-or-later，详情见项目第三方许可说明。

## 工作引擎（六项独立检测）

工作引擎页有六张分开的状态卡片：**ASE、pymatgen、NumPy、Matplotlib、openpyxl、OriginPro**。每张卡片有各自的「单独检测」按钮和版本/错误状态；六项共用上方设置的 Python 路径，互相不合并。更改路径时旧状态会标记需要重新检查。

首页「开始使用」提供结构转换、团簇生成、台阶图、表面 Pourbaix、DOS 与 COHP/ICOHP 六个快捷卡片，分别进入对应工作区；页面顶部保持简洁，不重复添加一排入口。

## 文件导入与删除（2.3.7）

台阶图、表面 Pourbaix、DOS、COHP/ICOHP 的导入区沿用结构转换的云朵卡片交互：初始显示云朵；点击或拖入后原位置显示完整文件路径和「更换文件」「删除文件」按钮；删除后恢复云朵，同时清理当前数据预览与可选项。文件已导入时仍可把新文件拖到卡片上替换。结构转换继续支持多文件列表。

## 团簇生成

在「团簇生成」页面输入元素和原子数（例如 `Pt` 与 `4,5,6,13`），选择「普通团簇」或「FCC 晶面取向团簇」。晶面可填写 `all`（111/100/110）、指定晶面，或 `ico`（13/55/147/309/561/923 原子完整壳层）。晶格常数自动估算，也可手动覆盖；真空层每侧默认 12 Å。程序为每次运行新建结果文件夹，按勾选生成 POSCAR、CIF 或两者。所有团簇都只是初猜，需要 VASP 结构弛豫。

## HER 原始七列表格模板

在「绘制台阶图」选择 HER 并点击下载模板，得到用户提供的原始七列 Excel 分块表，包含 H2O / H2 参照、十组 CoP-X 催化剂分块，以及 *H2O、*H-OH、*H 中间态。上传到 GitHub 的两份模板均已清除全部数值和旧公式结果，保留版式与文字标识。请填写 B 列 DFT 能量、C 列自由能修正及气体参考 B2/B3（必要时 E2/E3）；新增催化剂时复制同样的分块结构。HER 读取器也接受缺少公式缓存的文件，重新用原始能量和校正值计算相对自由能；旧版九列 HER 表和三列长表仍可导入。

## 表面吸附态 Pourbaix 图

在「绘制台阶图」下方点击「表面 Pourbaix 图」。软件使用你已有的 ORR/OER 自由能 Excel 数据（包含多组五行模板或 Material / DG_OH / DG_O / DG_OOH 宽表），通过 pymatgen `Composition` 得到吸附态 O/H 计量和电子转移数，再以四电子 CHE 求 *、*OH、*O、*OOH 的表面自由能最低态。输出 pH–SHE 电位二维相图，并标出理论水稳定边界。pH、电位范围可设置；可勾选 PNG、SVG、Excel、CSV、Origin DAT / 原生 OPJU。

Excel/CSV/DAT 包含每一网格的 pH、电位、稳定吸附态和各吸附态自由能，适合在 Origin 中修改绘图。此图只刻画理想化表面吸附态，**不包括固体溶解、溶液离子、覆盖度、显式溶剂及动力学**；不是固液相平衡的完整 PourbaixDiagram。原始 Excel 只读，DAT 不会自动生成原生 .opju。

## DOS / PDOS 分析

在「DOS 分析」选择收敛后的 VASP DOSCAR 或 vasprun.xml：

- **vasprun.xml**：由 pymatgen 直接读取总 DOS、元素与 s/p/d 轨道投影；优先推荐此格式。
- **DOSCAR**：读取 VASP 标准共线自旋格式；若需要按元素汇总 PDOS，须在**同目录**放入与 DOSCAR 原子顺序一致的 POSCAR（或 CONTCAR），以便通过 pymatgen 识别元素。缺少结构文件时仍可查看 TDOS。不支持非共线 / SOC 四分量 PDOS。
- 可多选元素（优先预选 Pt、Fe、Co、Ni）、勾选 s/p/d，选择 ↑ / ↓ / 双自旋，并设定相对于费米能级的能量窗口。图中自旋 ↓ 镜像显示为负值，CSV 保留物理非负 DOS。
- d 带中心以选定能量窗口内的**元素 d-PDOS** 计算：ε_d = ∫(E−Ef)ρ_d(E)dE / ∫ρ_d(E)dE。对自旋极化体系分别报告 ↑、↓ 和合并结果。窗口会被限制到实际 DOS 能量范围。
- 「分析并绘图」后可将 CSV 原始曲线、PNG 与 SVG 图像导出到指定文件夹，使用不覆盖已有文件的命名方式。所有输入文件只读。

**建议：** VASP 完成静态 DOS 计算后再分析。对结构弛豫输出的 DOSCAR，DOS 可能在多个离子步之间平均；元素 d-PDOS 的定义也依赖 VASP 的 LORBIT 投影，不能直接作为严格的 Wannier d 带中心。

## 注意的结构边界

ASE 负责结构读取和写出。当前桥接使用 `read(...,format='vasp'/'cif')` 与 `write(...,format='vasp'/'cif')`，POSCAR 写出启用 `sort=True` 和 `vasp5=True`。ASE 输出精度与 VESTA 并不相同。CIF 部分占位会拒绝写为 POSCAR；Selective Dynamics、空间群元数据和原子顺序不保证跨格式无损保留。重要结构请核对元素顺序、晶胞、原子数与约束。

## 构建和测试

```powershell
dotnet test tests/DFTDesk.Tests/DFTDesk.Tests.csproj -c Release
dotnet publish src/DFTDesk/DFTDesk.csproj -c Release -r win-x64 --self-contained true -o dist/DFTDesk
```

项目自动化测试验证输入格式识别、缺失 Python 的错误反馈、ASE 往返转换、Direct/Cartesian、同名输出保护、团簇模式与临时文件清理。CI 专门安装 ASE、pymatgen、NumPy、Matplotlib、openpyxl，运行结构、合成 DOSCAR 与 LOBSTER COHP/ICOHP 导入、绘图及 CSV/XLSX/DAT/PNG/SVG 导出集成测试。

## COHP / ICOHP 分析

左侧「COHP / ICOHP 分析」是独立页面，与 VASP DOS 分析互不干扰。导入 LOBSTER 的 `COHPCAR.lobster` 或 `ICOHPLIST.lobster`（支持 `.gz`，识别 LCFO 名称）：同一目录存在对应配套文件时会自动合并读取。

- **COHPCAR**：使用 pymatgen 直接读取各编号键的 COHP、ICOHP 能量曲线；可以多选键、选择自旋和相对费米能级的能窗。默认绘制 **-COHP 与 -ICOHP**，展示成键/反键贡献，原始数值不会被反号存储。
- **ICOHPLIST**：统计各键的键长、等价键数和上/下自旋 **ICOHP(Ef)**；仅导入 ICOHPLIST 时仍可导出键列表，但无法绘制能量曲线。
- 结果包含 **CSV、Excel（.xlsx）、Origin 可读制表符（.dat）**，有 COHPCAR 时额外生成 **PNG / SVG**。Excel 中包含 ICOHP(Ef) 汇总页及完整曲线页，曲线保持 LOBSTER 原始符号。Origin DAT / 原生 OPJU 是数据导入文件，不是只能用 Origin 创建的原生工程 `.opju`。
- LOBSTER COHP 的负值一般对应成键、正值对应反键；**ICOHP(Ef) 是 LOBSTER 已积分的物理量，不应与普通 DOS 积分混淆**。比较不同键时请注意是否以每键均值还是等价键总和表示。

## 绘制台阶图（2.2：HER / ORR / OER）

左侧「绘制台阶图」紧跟「团簇生成」；支持点击云朵选取或拖入 Excel 原始自由能文件（.xlsx、.xlsm），并新增 HER、ORR、OER 三种专用模式。模式右侧的「下载当前模式 Excel 模板」会生成可直接填写的标准工作簿：HER 为 Material/State/DeltaG 三列，ORR/OER 为 Material/DG_OH/DG_O/DG_OOH 四列。第一个工作表从第 2 行填写，不改动表头，说明页不参与计算。数据引擎支持提供的碱性 HER 模板：B 列 DFT 能量、E 列自由能校正、G 列气相参照和 I 列相对自由能；同一催化剂可分为多个计算阶段并在一条路径中展示。也支持列名为 Material、State、DeltaG 的简化长表。

点击「绘制台阶图」后可预览可比催化剂叠加的能量平台、虚线连接及能量标注，并区分完整中间态路径；一键导出 PNG、SVG、CSV、Excel XLSX 和 Origin DAT / 原生 OPJU。软件不会更改原 Excel，不会把用户的 Origin .opju 原生工程提交至 GitHub。DAT 可导入用户自己的 .opju 并继续编辑；自动原生 .opju 绘制仍需 Origin。

### ORR / OER 模式的输入与计算约定

- **输入最小模板：**`Material,DG_OH,DG_O,DG_OOH`（宽表，一行一个催化剂；吸附自由能单位 eV）。
- **高级模板：**`Material,G_star,G_OH,G_O,G_OOH,G_H2O,G_H2`（已经计算完自由能修正的吉布斯自由能），或 `E_star,E_OH,E_O,E_OOH,E_H2O,E_H2` 和对应 `Corr_star,Corr_OH,Corr_O,Corr_OOH,Corr_H2O,Corr_H2`。不能将无修正的 VASP 总能直接解释为 Gibbs 自由能。
- 四电子 CHE、298.15 K、反应总能 4.92 eV；台阶图的电位统一输入为 V vs RHE，不显示电位参照下拉框，且不重复施加 pH 修正。pH 输入用于记录溶液条件。
- OER 前进步骤（0 V）：`ΔG_OH, ΔG_O−ΔG_OH, ΔG_OOH−ΔG_O, 4.92−ΔG_OOH`，`U_L = max(ΔG_i)`，`η = U_L−1.23`；ORR 为逆序还原反应，`U_L = min(−ΔG_i)`，`η = 1.23−U_L`。
- 在 U 下逐个一电子步骤修正 `−U_RHE`（OER）、`+U_RHE`（ORR）。ORR/OER 终态能量随电位变动，不应自动重置零点。
- 多催化剂按相同中间态叠加绘图；**图上以粗线和菱形突出 PDS，右侧逐个催化剂显示 η 和 PDS 步数**；导出 XLSX 的 **CHE Metrics、CHE Four Steps、Reaction Profiles** 三个表，兼有 CSV、Origin DAT / 原生 OPJU、PNG、SVG。PDS 表示热力学潜在决定步骤，不是动力学活化能最高的一步（RDS 需要动力学能垒）。

### ORR 原始多结构模板（2.2.1 修复）

支持单张 Excel 中多组重复的五行结构块，A 列是「催化剂、*OOH、*O、*OH、催化剂」，G 列 U=0V 的自由能台阶；G 列为空时尝试读取 F 列或依据 D 列和气相参照恢复。每组独立产生 ORR 曲线，OER 反转中间态顺序；同样兼容原有 DG_OH/DG_O/DG_OOH 宽表和修正后 Gibbs 数据。

绘图统一与所提供的 Origin 模板零点对应：ORR 0V 为 4.92→0 eV，OER 0V 为 0→4.92 eV；不同电位下四个单电子反应步分别按 ±U(RHE) 调整，首末状态不会被错误强制归零。Excel、Origin DAT / 原生 OPJU 和图片均保留完整反应能量。

### 拖放与按需导出

云朵导入对不同分析类型分别处理，避免结构格式过滤器丢弃 DOSCAR、COHPCAR 和 Excel；同时在云朵内部的按钮上注册拖放事件。分析页面可多选 PNG、SVG、Excel、CSV 和 Origin DAT / 原生 OPJU，只有勾选的格式才被保存到输出目录。团簇生成可选择 POSCAR 或 CIF。

## DOS 新增 Excel 与 Origin 导出

DOS 分析除 CSV、PNG、SVG 外，也输出 `.xlsx` 和 Origin 可导入的制表符 `.dat`：第一列为相对费米能级的能量，之后依次为选定元素、轨道与自旋的原始 DOS。Excel 的独立工作表包含积分及 d 带中心摘要。

## 赞助二维码

为避免将个人收款信息写入公开 Git 历史，默认不提交微信 / 支付宝二维码。软件运行时可从下列目录读取：

```text
%LOCALAPPDATA%\DFTDesk\Donation\wechat.png
%LOCALAPPDATA%\DFTDesk\Donation\alipay.png
```

本机编译会自动将已有的上述 PNG 嵌入 EXE。若要 GitHub Actions 发布的 EXE 自动包含二维码，请在仓库 Actions Secrets 中设置 `DFTDESK_WECHAT_QR_BASE64` 和 `DFTDESK_ALIPAY_QR_BASE64`，内容必须是各自 PNG 文件的 Base64 字符串。构建会验证 PNG 文件头；无效 Secret 不会中断 EXE 发布，但二维码不会嵌入。界面不提供下载按钮，但无法防止截图或从程序中提取图片。

## 版权

DFTDesk 自身以 MIT 授权；图形界面、轮盘及 Windows 支持组件部分沿用 Filee 的 MIT 源码，其原始版权与许可在 LICENSE、THIRD-PARTY-NOTICES 和 third_party/Filee 中保留。

### Origin 2024 原生 OPJU 导出

在「绘制台阶图」中勾选 **Origin OPJU（保留模板图）**。导出时选择你自己电脑上的原始 `.opju` 项目，再选择保存目录。此功能需要 Windows 本机安装 Origin 2021+（Origin 2024 支持），并在「工作引擎」配置已安装 `originpro` 的 Python。软件从原工程打开已有的 Book 和 Graph，向已有 Book 添加 `DFTDesk_Profiles` 工作表，将计算结果关联到已有绘图对象，并另存新 `.opju`；不会将原工程或原始数值上传到 GitHub，也不会覆盖原工程。

**保真限制：** 为避免损坏原图版式，目前仅在原工程恰有一张图、其已有绘图曲线数量与催化剂数相同、且能唯一匹配时自动重绑。若 Origin 模板使用多条子曲线构成一个台阶图、存在多张候选图、固定文字标注或特殊图层关系，需要在 Origin 中根据提示核验或建立对应映射；当前 CI 没有 Origin 许可证，只能自动测试数据映射与安全检查，不能声称已对用户上传的原始 OPJU 做过视觉一致性验收。


### OriginPro 运行环境检测（2.3.7）

「工作引擎」现有 ASE、pymatgen、NumPy、Matplotlib、openpyxl 检测旁增加 **OriginPro** 检测卡片。此检测先检查当前配置 Python 的 `originpro` 包版本，再通过独立限时进程验证 Windows Origin 自动化连接（不会打开或更改个人 `.opju` 项目）；若安装了 Python 包但无法连接 Origin，会明确区分错误原因。导出原生 `.opju` 前自动执行相同检查，失败时不输出冒充有效工程的文件。只有原生 OPJU 功能要求 Origin 2021+，其余 DFTDesk 模块不依赖它。

在「工作引擎」指定的 Python 环境安装：`python -m pip install originpro`。Origin 2024 可用；连接检查可能短暂启动一个独立的 Origin 自动化实例。
