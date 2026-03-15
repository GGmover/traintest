# traintest

单线铁路运行图最大加开列车对数测试软件（C# + WinForms）。

## 功能

- 录入既有列车运行图（车次、方向、始发时刻）。
- 录入线路参数（车站、区间运行时分、停站时分）。
- 设置求解参数（安全间隔、最小折返间隔、求解时间窗、搜索步长）。
- 自动计算在既有图条件下可加开的**最大列车对数**。
- 输出完整列车时刻表（含既有列车 + 新增列车）。
- 支持既有列车 CSV 导入与结果 CSV 导出。

## 项目结构

- `TrainGraphOptimizer.sln`：解决方案。
- `TrainGraphOptimizer.App/`：WinForms 应用。
  - `MainForm.cs`：界面、输入输出、导入导出。
  - `Services/SchedulerService.cs`：求解核心逻辑。

## 运行说明

> 需要 Windows 环境 + .NET 8 SDK（WinForms）。

```bash
dotnet build TrainGraphOptimizer.sln
```

在 Visual Studio 中打开 `TrainGraphOptimizer.sln` 可直接运行。

## CSV 格式示例

既有列车导入文件（首行为表头）：

```csv
TrainNo,Direction,StartTime
K101,Up,06:00
K102,Down,06:30
```

