namespace TrainGraphOptimizer.App.Models;

public class OptimizationParameters
{
    public int SafetyHeadwayMinutes { get; set; } = 6;
    public int MinTurnbackMinutes { get; set; } = 10;
    public int HorizonMinutes { get; set; } = 24 * 60;
    public int SearchStepMinutes { get; set; } = 1;
}
