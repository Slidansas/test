using AROKIS.Backend.Models;

namespace AROKIS.Backend.Services;

public class StreamUpdateService : IDisposable
{
    private readonly Dictionary<int, StreamSimulation> _streamCables = new();
    private readonly Random _random = new();
    private readonly ShapeGenerator _shapeGenerator;
    private readonly DataStorageService _dataStorage;

    private readonly CancellationTokenSource _cts = new();
    private Task? _loopTask;

    private const int UPDATE_INTERVAL_MS = 100;
    private const int MAX_MODIFIED_POINTS = 500;

    public StreamUpdateService(DataStorageService dataStorage)
    {
        _dataStorage    = dataStorage;
        _shapeGenerator = new ShapeGenerator(dataStorage);

        // InitializeStreamSimulations(); // отключено
        // Start(); // генератор отключён
    }

    public IReadOnlyDictionary<int, StreamSimulation> StreamCables => _streamCables;

    private void Start()
    {
        _loopTask = Task.Run(UpdateLoop);
    }

    private async Task UpdateLoop()
    {
        var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(UPDATE_INTERVAL_MS));
        try
        {
            while (await timer.WaitForNextTickAsync(_cts.Token))
            {
                foreach (var kvp in _streamCables.Where(s => s.Value.IsRunning && !s.Value.HasRealData))
                    UpdateStream(kvp.Value);
            }
        }
        catch (OperationCanceledException) { }
    }

    private void UpdateStream(StreamSimulation sim)
    {
        lock (sim)
        {
            // ── Если стрим получает реальные данные — симуляцию не трогаем ──
            if (sim.HasRealData)
            {
                sim.Cable.LastUpdate = DateTime.Now;
                return;
            }

            // ── Симуляция: возврат отклонённых точек на место ────────────────
            var toRemove = new List<int>();
            foreach (var kvp in sim.PointDeviations)
            {
                var idx       = kvp.Key;
                var deviation = kvp.Value;

                if (deviation.StepsToReturn > 0)
                {
                    sim.Cable.Points[idx].X += -deviation.DeltaX / deviation.StepsToReturn;
                    sim.Cable.Points[idx].Y += -deviation.DeltaY / deviation.StepsToReturn;
                    sim.PointDeviations[idx].StepsToReturn--;
                }
                else
                {
                    sim.Cable.Points[idx].X = sim.OriginalPoints[idx].X;
                    sim.Cable.Points[idx].Y = sim.OriginalPoints[idx].Y;
                    toRemove.Add(idx);
                    sim.ModifiedIndices.Remove(idx);
                }
            }
            foreach (var idx in toRemove)
                sim.PointDeviations.Remove(idx);

            if (sim.ModifiedIndices.Count > MAX_MODIFIED_POINTS)
            {
                var trim = sim.ModifiedIndices.Take(MAX_MODIFIED_POINTS / 2).ToList();
                foreach (var idx in trim)
                {
                    sim.ModifiedIndices.Remove(idx);
                    sim.PointDeviations.Remove(idx);
                }
            }

            // ── Симуляция: добавляем новые случайные отклонения ──────────────
            double amp   = 1.0 + (_dataStorage.GetAverage() * 0.01);
            int    count = _random.Next(1, 6);

            for (int i = 0; i < count; i++)
            {
                int idx = _random.Next(sim.Cable.Points.Count);
                if (sim.ModifiedIndices.Contains(idx)) continue;

                double dx = (_random.NextDouble() - 0.5) * 10 * amp;
                double dy = (_random.NextDouble() - 0.5) * 10 * amp;

                sim.Cable.Points[idx].X += dx;
                sim.Cable.Points[idx].Y += dy;

                sim.PointDeviations[idx] = new DeviationInfo
                {
                    DeltaX        = dx,
                    DeltaY        = dy,
                    StepsToReturn = _random.Next(5, 15)
                };
                sim.ModifiedIndices.Add(idx);
            }

            sim.Cable.LastUpdate = DateTime.Now;
        }
    }

    private void InitializeStreamSimulations()
    {
        AddStream(1, "Circle",   _shapeGenerator.GenerateCircle);
        AddStream(2, "Triangle", _shapeGenerator.GenerateTriangle);
        AddStream(3, "Square",   _shapeGenerator.GenerateSquare);
        AddStream(4, "Star",     _shapeGenerator.GenerateStar);
    }

    private void AddStream(int id, string name, Action<CableProjection> gen)
    {
        var sim = new StreamSimulation { ShapeName = name };
        gen(sim.Cable);
        sim.OriginalPoints = sim.Cable.Points
            .Select(p => new CablePoint { X = p.X, Y = p.Y })
            .ToList();
        _streamCables[id] = sim;
    }

    public void Dispose()
    {
        _cts.Cancel();
        _loopTask?.Wait(500);
    }
}
