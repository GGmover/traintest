using TrainGraphOptimizer.App.Models;

namespace TrainGraphOptimizer.App.Services;

public class SchedulerService
{
    public OptimizationResult Optimize(
        IReadOnlyList<string> stations,
        IReadOnlyList<int> sectionRunMinutes,
        IReadOnlyList<int> dwellMinutes,
        IReadOnlyList<TrainRun> existingTrains,
        OptimizationParameters parameters)
    {
        ValidateInput(stations, sectionRunMinutes, dwellMinutes);

        var allTrains = existingTrains
            .Select(x => new TrainRun { TrainNo = x.TrainNo, Direction = x.Direction, StartMinute = x.StartMinute })
            .ToList();

        var added = new List<TrainRun>();
        var pairIndex = 1;

        while (true)
        {
            var upStart = FindEarliestFeasibleStart(Direction.Up, allTrains, stations.Count, sectionRunMinutes, dwellMinutes, parameters);
            var downStart = FindEarliestFeasibleStart(Direction.Down, allTrains, stations.Count, sectionRunMinutes, dwellMinutes, parameters);

            if (upStart is null || downStart is null)
            {
                break;
            }

            var upTrain = new TrainRun
            {
                TrainNo = $"A{pairIndex:000}U",
                Direction = Direction.Up,
                StartMinute = upStart.Value
            };

            allTrains.Add(upTrain);
            added.Add(upTrain);

            var adjustedDownStart = Math.Max(downStart.Value, upTrain.StartMinute + parameters.MinTurnbackMinutes);
            var downCandidate = FindEarliestFeasibleStart(Direction.Down, allTrains, stations.Count, sectionRunMinutes, dwellMinutes, parameters, adjustedDownStart);
            if (downCandidate is null)
            {
                allTrains.Remove(upTrain);
                added.Remove(upTrain);
                break;
            }

            var downTrain = new TrainRun
            {
                TrainNo = $"A{pairIndex:000}D",
                Direction = Direction.Down,
                StartMinute = downCandidate.Value
            };

            allTrains.Add(downTrain);
            added.Add(downTrain);
            pairIndex++;
        }

        var timetableRows = BuildTimetableRows(stations, sectionRunMinutes, dwellMinutes, allTrains);

        return new OptimizationResult
        {
            MaxPairs = added.Count / 2,
            AddedTrains = added.OrderBy(t => t.StartMinute).ToList(),
            TimetableRows = timetableRows
        };
    }

    private static List<TimetableRow> BuildTimetableRows(
        IReadOnlyList<string> stations,
        IReadOnlyList<int> sectionRunMinutes,
        IReadOnlyList<int> dwellMinutes,
        IReadOnlyList<TrainRun> trains)
    {
        var rows = new List<TimetableRow>();

        foreach (var train in trains.OrderBy(x => x.StartMinute).ThenBy(x => x.TrainNo))
        {
            var schedule = BuildStationSchedule(train, stations.Count, sectionRunMinutes, dwellMinutes);
            for (var i = 0; i < schedule.Count; i++)
            {
                rows.Add(new TimetableRow
                {
                    TrainNo = train.TrainNo,
                    Direction = train.Direction.ToString(),
                    Station = train.Direction == Direction.Up ? stations[i] : stations[^(i + 1)],
                    Arrival = FormatMinute(schedule[i].ArrivalMinute),
                    Departure = FormatMinute(schedule[i].DepartureMinute)
                });
            }
        }

        return rows;
    }

    private int? FindEarliestFeasibleStart(
        Direction direction,
        IReadOnlyList<TrainRun> current,
        int stationCount,
        IReadOnlyList<int> sectionRunMinutes,
        IReadOnlyList<int> dwellMinutes,
        OptimizationParameters parameters,
        int fromMinute = 0)
    {
        var latestStart = parameters.HorizonMinutes - TotalRouteMinutes(sectionRunMinutes, dwellMinutes);

        for (var t = Math.Max(0, fromMinute); t <= latestStart; t += Math.Max(1, parameters.SearchStepMinutes))
        {
            var candidate = new TrainRun
            {
                TrainNo = "CAND",
                Direction = direction,
                StartMinute = t
            };

            if (IsFeasible(candidate, current, stationCount, sectionRunMinutes, dwellMinutes, parameters.SafetyHeadwayMinutes))
            {
                return t;
            }
        }

        return null;
    }

    private bool IsFeasible(
        TrainRun candidate,
        IReadOnlyList<TrainRun> current,
        int stationCount,
        IReadOnlyList<int> sectionRunMinutes,
        IReadOnlyList<int> dwellMinutes,
        int safetyHeadway)
    {
        var candidateSegments = BuildSegmentOccupancies(candidate, stationCount, sectionRunMinutes, dwellMinutes);

        foreach (var existing in current)
        {
            var existingSegments = BuildSegmentOccupancies(existing, stationCount, sectionRunMinutes, dwellMinutes);

            foreach (var c in candidateSegments)
            {
                foreach (var e in existingSegments.Where(x => x.SegmentIndex == c.SegmentIndex))
                {
                    if (IntervalsConflict(c.EntryMinute, c.ExitMinute, e.EntryMinute, e.ExitMinute, safetyHeadway))
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    private static bool IntervalsConflict(int aStart, int aEnd, int bStart, int bEnd, int buffer)
    {
        return aStart < bEnd + buffer && bStart < aEnd + buffer;
    }

    private static List<SegmentOccupancy> BuildSegmentOccupancies(
        TrainRun train,
        int stationCount,
        IReadOnlyList<int> sectionRunMinutes,
        IReadOnlyList<int> dwellMinutes)
    {
        var schedule = BuildStationSchedule(train, stationCount, sectionRunMinutes, dwellMinutes);
        var occupancies = new List<SegmentOccupancy>();

        for (var i = 0; i < stationCount - 1; i++)
        {
            var from = schedule[i];
            var to = schedule[i + 1];

            occupancies.Add(new SegmentOccupancy
            {
                SegmentIndex = train.Direction == Direction.Up ? i : stationCount - 2 - i,
                EntryMinute = from.DepartureMinute,
                ExitMinute = to.ArrivalMinute
            });
        }

        return occupancies;
    }

    private static List<StationTime> BuildStationSchedule(
        TrainRun train,
        int stationCount,
        IReadOnlyList<int> sectionRunMinutes,
        IReadOnlyList<int> dwellMinutes)
    {
        var schedule = new List<StationTime>(stationCount);
        var current = train.StartMinute;

        for (var i = 0; i < stationCount; i++)
        {
            var isOrigin = i == 0;
            var isTerminal = i == stationCount - 1;

            var arrival = isOrigin ? current : current;
            var departure = isTerminal ? current : current + dwellMinutes[i];

            schedule.Add(new StationTime { ArrivalMinute = arrival, DepartureMinute = departure });

            if (!isTerminal)
            {
                current = departure + sectionRunMinutes[i];
            }
        }

        return schedule;
    }

    private static int TotalRouteMinutes(IReadOnlyList<int> sectionRunMinutes, IReadOnlyList<int> dwellMinutes)
    {
        return sectionRunMinutes.Sum() + dwellMinutes.Take(dwellMinutes.Count - 1).Sum();
    }

    private static void ValidateInput(IReadOnlyList<string> stations, IReadOnlyList<int> sectionRunMinutes, IReadOnlyList<int> dwellMinutes)
    {
        if (stations.Count < 2)
        {
            throw new InvalidOperationException("至少需要2个车站。");
        }

        if (sectionRunMinutes.Count != stations.Count - 1)
        {
            throw new InvalidOperationException("区间运行时分数量必须等于车站数量减1。");
        }

        if (dwellMinutes.Count != stations.Count)
        {
            throw new InvalidOperationException("停站时分数量必须等于车站数量。");
        }
    }

    public static int ParseMinute(string hhmm)
    {
        if (!TimeSpan.TryParse(hhmm, out var ts))
        {
            throw new InvalidOperationException($"时间格式错误：{hhmm}，应为 HH:mm。");
        }

        return (int)ts.TotalMinutes;
    }

    public static string FormatMinute(int minute)
    {
        minute %= 24 * 60;
        if (minute < 0)
        {
            minute += 24 * 60;
        }

        return TimeSpan.FromMinutes(minute).ToString(@"hh\:mm");
    }

    private sealed class SegmentOccupancy
    {
        public int SegmentIndex { get; init; }
        public int EntryMinute { get; init; }
        public int ExitMinute { get; init; }
    }

    private sealed class StationTime
    {
        public int ArrivalMinute { get; init; }
        public int DepartureMinute { get; init; }
    }
}
