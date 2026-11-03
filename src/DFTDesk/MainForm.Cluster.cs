using System.Windows.Forms;

namespace DFTDesk;

/// <summary>Cluster generation workflow. Uses the Python ASE environment selected in Work Engine.</summary>
internal sealed partial class MainForm
{
    private bool _clusterBusy;
    private CancellationTokenSource? _clusterCancellation;

    private void BrowseClusterFolder()
    {
        if (_clusterBusy) return;
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择保存 POSCAR 和 CIF 团簇文件的根目录"
        };
        if (dialog.ShowDialog() == DialogResult.OK)
            _shell.ClusterOutputPath = dialog.SelectedPath;
    }

    private async Task GenerateClusterAsync()
    {
        if (_clusterBusy || _busy)
        {
            _shell.SetClusterStatus("已有任务正在执行，请先完成或取消当前任务。");
            return;
        }

        var request = _shell.ReadClusterRequest();
        var python = _shell.PythonExecutablePath;
        _shell.SetClusterStatus("正在检查 Python / ASE 环境…");
        try
        {
            var version = await AseConversion.ProbeAsync(python);
            _shell.SetAseStatus($"可用：{version}（{python}）");
        }
        catch (Exception ex)
        {
            _shell.SetClusterStatus("ASE 不可用，请先打开「工作引擎」检查环境。");
            _shell.SetClusterLog(ex.Message);
            MessageBox.Show("无法使用 ASE 生成团簇：" + ex.Message +
                "\n\n请在「工作引擎」中指定已安装 ASE 的 Python 路径。",
                "团簇生成", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _clusterCancellation = new CancellationTokenSource();
        _clusterBusy = true;
        _shell.SetClusterBusy(true);
        _shell.SetClusterStatus("正在构建团簇初始几何…");
        _shell.SetClusterLog("");
        try
        {
            var result = await ClusterGeneration.GenerateAsync(request, python, _clusterCancellation.Token);
            _shell.SetClusterStatus($"已生成 {result.StructureCount} 个团簇、{result.StructureCount * 2} 个文件。");
            _shell.SetClusterLog(result.Log + "\n输出目录：" + result.Directory);
        }
        catch (OperationCanceledException)
        {
            _shell.SetClusterStatus("已取消团簇生成，临时文件已清理。");
            _shell.SetClusterLog("任务已取消。");
        }
        catch (Exception ex)
        {
            _shell.SetClusterStatus("团簇生成失败，请检查参数和下方记录。");
            _shell.SetClusterLog(ex.Message);
        }
        finally
        {
            _clusterCancellation.Dispose();
            _clusterCancellation = null;
            _clusterBusy = false;
            _shell.SetClusterBusy(false);
        }
    }
}
