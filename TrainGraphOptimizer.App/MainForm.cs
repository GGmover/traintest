using System.Data;
using System.Text;
using TrainGraphOptimizer.App.Models;
using TrainGraphOptimizer.App.Services;

namespace TrainGraphOptimizer.App;

public sealed class MainForm : Form
{
    private readonly DataGridView _stationGrid = new();
    private readonly DataGridView _existingTrainGrid = new();
    private readonly DataGridView _resultGrid = new();

    private readonly TextBox _txtHeadway = new() { Text = "6" };
    private readonly TextBox _txtTurnback = new() { Text = "10" };
    private readonly TextBox _txtHorizon = new() { Text = "1440" };
    private readonly TextBox _txtStep = new() { Text = "1" };

    private readonly Label _lblPairs = new() { AutoSize = true, Font = new Font("Microsoft YaHei", 10, FontStyle.Bold) };

    private readonly SchedulerService _scheduler = new();

    public MainForm()
    {
        Text = "单线铁路运行图最大加开列车对数测试工具";
        Width = 1300;
        Height = 800;
        StartPosition = FormStartPosition.CenterScreen;

        InitializeLayout();
        SeedDemoData();
    }

    private void InitializeLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 35));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 35));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 30));

        Controls.Add(root);

        root.Controls.Add(BuildControlPanel(), 0, 0);
        root.Controls.Add(BuildStationPanel(), 0, 1);
        root.Controls.Add(BuildTrainPanel(), 0, 2);
        root.Controls.Add(BuildResultPanel(), 0, 3);
    }

    private Control BuildControlPanel()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
            AutoScroll = true,
        };

        panel.Controls.Add(new Label { Text = "安全间隔(分钟)", AutoSize = true, Margin = new Padding(10, 8, 2, 0) });
        panel.Controls.Add(_txtHeadway);
        panel.Controls.Add(new Label { Text = "最小折返间隔(分钟)", AutoSize = true, Margin = new Padding(10, 8, 2, 0) });
        panel.Controls.Add(_txtTurnback);
        panel.Controls.Add(new Label { Text = "求解时间窗(分钟)", AutoSize = true, Margin = new Padding(10, 8, 2, 0) });
        panel.Controls.Add(_txtHorizon);
        panel.Controls.Add(new Label { Text = "搜索步长(分钟)", AutoSize = true, Margin = new Padding(10, 8, 2, 0) });
        panel.Controls.Add(_txtStep);

        var solveBtn = new Button { Text = "开始求解", Width = 120, Height = 32, Margin = new Padding(16, 3, 3, 3) };
        solveBtn.Click += (_, _) => Solve();
        panel.Controls.Add(solveBtn);

        var exportBtn = new Button { Text = "导出结果CSV", Width = 120, Height = 32 };
        exportBtn.Click += (_, _) => ExportCsv();
        panel.Controls.Add(exportBtn);

        var loadBtn = new Button { Text = "导入既有列车CSV", Width = 130, Height = 32 };
        loadBtn.Click += (_, _) => ImportExistingTrainCsv();
        panel.Controls.Add(loadBtn);

        panel.Controls.Add(_lblPairs);
        return panel;
    }

    private Control BuildStationPanel()
    {
        var group = new GroupBox { Text = "线路与车站参数", Dock = DockStyle.Fill };

        _stationGrid.Dock = DockStyle.Fill;
        _stationGrid.AllowUserToAddRows = true;
        _stationGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _stationGrid.Columns.Add("Station", "车站名称");
        _stationGrid.Columns.Add("RunToNext", "至下一站运行时分(分钟)");
        _stationGrid.Columns.Add("Dwell", "本站停站时分(分钟)");

        group.Controls.Add(_stationGrid);
        return group;
    }

    private Control BuildTrainPanel()
    {
        var group = new GroupBox { Text = "既有列车运行图输入", Dock = DockStyle.Fill };

        _existingTrainGrid.Dock = DockStyle.Fill;
        _existingTrainGrid.AllowUserToAddRows = true;
        _existingTrainGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _existingTrainGrid.Columns.Add("TrainNo", "车次");

        var directionCol = new DataGridViewComboBoxColumn
        {
            Name = "Direction",
            HeaderText = "方向",
            DataSource = new[] { "Up", "Down" }
        };

        _existingTrainGrid.Columns.Add(directionCol);
        _existingTrainGrid.Columns.Add("StartTime", "始发时刻(HH:mm)");

        group.Controls.Add(_existingTrainGrid);
        return group;
    }

    private Control BuildResultPanel()
    {
        var group = new GroupBox { Text = "输出：最大可开行列车对数及时刻表", Dock = DockStyle.Fill };

        _resultGrid.Dock = DockStyle.Fill;
        _resultGrid.AllowUserToAddRows = false;
        _resultGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _resultGrid.Columns.Add("TrainNo", "车次");
        _resultGrid.Columns.Add("Direction", "方向");
        _resultGrid.Columns.Add("Station", "车站");
        _resultGrid.Columns.Add("Arrival", "到达");
        _resultGrid.Columns.Add("Departure", "出发");

        group.Controls.Add(_resultGrid);
        return group;
    }

    private void SeedDemoData()
    {
        _stationGrid.Rows.Add("A", "12", "2");
        _stationGrid.Rows.Add("B", "10", "2");
        _stationGrid.Rows.Add("C", "15", "2");
        _stationGrid.Rows.Add("D", "", "0");

        _existingTrainGrid.Rows.Add("K101", "Up", "06:00");
        _existingTrainGrid.Rows.Add("K102", "Down", "06:30");
        _existingTrainGrid.Rows.Add("K103", "Up", "07:10");
        _existingTrainGrid.Rows.Add("K104", "Down", "07:40");
    }

    private void Solve()
    {
        try
        {
            var stations = new List<string>();
            var runToNext = new List<int>();
            var dwell = new List<int>();

            foreach (DataGridViewRow row in _stationGrid.Rows)
            {
                if (row.IsNewRow) continue;
                var station = row.Cells["Station"].Value?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(station)) continue;

                stations.Add(station);
                dwell.Add(ParseInt(row.Cells["Dwell"].Value, "停站时分"));

                if (stations.Count > 1 || !string.IsNullOrWhiteSpace(row.Cells["RunToNext"].Value?.ToString()))
                {
                    var run = ParseOptionalInt(row.Cells["RunToNext"].Value, 0);
                    runToNext.Add(run);
                }
            }

            if (runToNext.Count == stations.Count)
            {
                runToNext.RemoveAt(runToNext.Count - 1);
            }

            var existing = new List<TrainRun>();
            foreach (DataGridViewRow row in _existingTrainGrid.Rows)
            {
                if (row.IsNewRow) continue;
                var no = row.Cells["TrainNo"].Value?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(no)) continue;

                var directionText = row.Cells["Direction"].Value?.ToString() ?? "Up";
                var timeText = row.Cells["StartTime"].Value?.ToString() ?? "00:00";

                existing.Add(new TrainRun
                {
                    TrainNo = no,
                    Direction = Enum.TryParse<Direction>(directionText, true, out var dir) ? dir : Direction.Up,
                    StartMinute = SchedulerService.ParseMinute(timeText)
                });
            }

            var parameters = new OptimizationParameters
            {
                SafetyHeadwayMinutes = ParseInt(_txtHeadway.Text, "安全间隔"),
                MinTurnbackMinutes = ParseInt(_txtTurnback.Text, "折返间隔"),
                HorizonMinutes = ParseInt(_txtHorizon.Text, "求解时间窗"),
                SearchStepMinutes = ParseInt(_txtStep.Text, "搜索步长")
            };

            var result = _scheduler.Optimize(stations, runToNext, dwell, existing, parameters);

            _lblPairs.Text = $"最大可加开列车对数：{result.MaxPairs} 对（共加开 {result.AddedTrains.Count} 列）";
            RenderResult(result.TimetableRows);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "求解失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RenderResult(IReadOnlyList<TimetableRow> rows)
    {
        _resultGrid.Rows.Clear();
        foreach (var row in rows)
        {
            _resultGrid.Rows.Add(row.TrainNo, row.Direction, row.Station, row.Arrival, row.Departure);
        }
    }

    private void ExportCsv()
    {
        if (_resultGrid.Rows.Count == 0)
        {
            MessageBox.Show("暂无结果可导出，请先求解。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog { Filter = "CSV 文件|*.csv", FileName = "result_timetable.csv" };
        if (dialog.ShowDialog() != DialogResult.OK) return;

        var sb = new StringBuilder();
        sb.AppendLine("TrainNo,Direction,Station,Arrival,Departure");

        foreach (DataGridViewRow row in _resultGrid.Rows)
        {
            if (row.IsNewRow) continue;
            var values = new[]
            {
                row.Cells[0].Value?.ToString() ?? string.Empty,
                row.Cells[1].Value?.ToString() ?? string.Empty,
                row.Cells[2].Value?.ToString() ?? string.Empty,
                row.Cells[3].Value?.ToString() ?? string.Empty,
                row.Cells[4].Value?.ToString() ?? string.Empty,
            };
            sb.AppendLine(string.Join(',', values.Select(EscapeCsv)));
        }

        File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
        MessageBox.Show("导出成功。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ImportExistingTrainCsv()
    {
        using var dialog = new OpenFileDialog { Filter = "CSV 文件|*.csv" };
        if (dialog.ShowDialog() != DialogResult.OK) return;

        var lines = File.ReadAllLines(dialog.FileName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        if (lines.Length <= 1)
        {
            MessageBox.Show("CSV 内容为空。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _existingTrainGrid.Rows.Clear();
        foreach (var line in lines.Skip(1))
        {
            var parts = line.Split(',');
            if (parts.Length < 3) continue;
            _existingTrainGrid.Rows.Add(parts[0].Trim(), parts[1].Trim(), parts[2].Trim());
        }
    }

    private static int ParseInt(object? value, string field)
    {
        if (!int.TryParse(value?.ToString(), out var number) || number < 0)
        {
            throw new InvalidOperationException($"{field} 请输入非负整数。");
        }

        return number;
    }

    private static int ParseOptionalInt(object? value, int defaultValue)
    {
        var text = value?.ToString();
        if (string.IsNullOrWhiteSpace(text)) return defaultValue;
        return ParseInt(text, "区间运行时分");
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
