namespace TrainGraphOptimizer.App.Models;

public class OptimizationResult
{
    public int MaxPairs { get; set; }
    public List<TrainRun> AddedTrains { get; set; } = new();
    public List<TimetableRow> TimetableRows { get; set; } = new();
}

public class TimetableRow
{
    public string TrainNo { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public string Station { get; set; } = string.Empty;
    public string Arrival { get; set; } = string.Empty;
    public string Departure { get; set; } = string.Empty;
}
