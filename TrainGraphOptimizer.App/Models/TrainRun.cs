namespace TrainGraphOptimizer.App.Models;

public class TrainRun
{
    public string TrainNo { get; set; } = string.Empty;
    public Direction Direction { get; set; }
    public int StartMinute { get; set; }
}
