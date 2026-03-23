using AROKIS.Backend.Models;

namespace AROKIS.Backend.Services;

public class ArokisApi
{
    private readonly ShapeGenerator _shapeGenerator;
    private readonly StreamUpdateService _stream;
    public StreamUpdateService Stream => _stream;
    private readonly DataStorageService _data;
    private readonly double _mmPerUnit;

    private readonly Dictionary<int, CableProjection> _cables = new();

    public ArokisApi(ShapeGenerator shapeGenerator, StreamUpdateService stream, DataStorageService data, ArokisSettings settings)
    {
        _shapeGenerator = shapeGenerator;
        _stream         = stream;
        _data           = data;
        _mmPerUnit      = settings.MmPerUnit;   // из appsettings.json → "MmPerUnit": 0.5

        for (int i = 1; i <= 5; i++)
            _cables[i] = _shapeGenerator.GenerateCableProjection(i);
    }

    // ── Толщина в мм (mmPerUnit применяется) ─────────────────────────────
    public double[] GetThicknessMm(int streamNumber, double[] anglesDeg)
    {
        if (!_stream.StreamCables.TryGetValue(streamNumber, out var sim) || anglesDeg == null)
            return Array.Empty<double>();

        lock (sim)
        {
            var pts = sim.Cable.Points;
            return anglesDeg
                .Select(a =>
                {
                    var units = ThicknessCalculator.MeasureUnitsByAngle(pts, a, ThicknessMode.OuterMost);
                    return units.HasValue ? units.Value * _mmPerUnit : double.NaN;
                })
                .ToArray();
        }
    }

    public IReadOnlyDictionary<int, CableProjection> GetCables() => _cables;

    public CableProjection? GetCable(int number)
        => _cables.TryGetValue(number, out var cable) ? cable : null;

    public CableProjection RegenerateCable(int number)
    {
        var cable = _shapeGenerator.GenerateCableProjection(number);
        _cables[number] = cable;
        return cable;
    }

    public CableProjection? GetStreamCable(int number)
    {
        if (!_stream.StreamCables.TryGetValue(number, out var sim)) return null;
        lock (sim)
        {
            return new CableProjection
            {
                Name       = sim.Cable.Name,
                LastUpdate = sim.Cable.LastUpdate,
                Points     = sim.Cable.Points.Select(p => new CablePoint { X = p.X, Y = p.Y }).ToList()
            };
        }
    }

    public bool StartStream(int number)
    {
        if (!_stream.StreamCables.TryGetValue(number, out var sim)) return false;
        sim.IsRunning = true;
        return true;
    }

    public bool StopStream(int number)
    {
        if (!_stream.StreamCables.TryGetValue(number, out var sim)) return false;
        sim.IsRunning = false;
        return true;
    }

    public object? GetStreamStatus(int number)
    {
        if (!_stream.StreamCables.TryGetValue(number, out var sim)) return null;
        lock (sim)
        {
            return new
            {
                number,
                shape          = sim.ShapeName,
                isRunning      = sim.IsRunning,
                modifiedPoints = sim.ModifiedIndices.Count,
                returningPoints= sim.PointDeviations.Count,
                totalPoints    = sim.Cable.Points.Count
            };
        }
    }

    public void SetValue(int idx, double value)
    {
        switch (idx)
        {
            case 1: _data.Value1 = value; break;
            case 2: _data.Value2 = value; break;
            case 3: _data.Value3 = value; break;
            case 4: _data.Value4 = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(idx));
        }
    }

    public double GetValue(int idx) => idx switch
    {
        1 => _data.Value1,
        2 => _data.Value2,
        3 => _data.Value3,
        4 => _data.Value4,
        _ => throw new ArgumentOutOfRangeException(nameof(idx))
    };

    public object GetAllData() => new
    {
        value1  = _data.Value1,
        value2  = _data.Value2,
        value3  = _data.Value3,
        value4  = _data.Value4,
        sum     = _data.GetSum(),
        average = _data.GetAverage(),
        product = _data.GetProduct()
    };

    public void ResetData() => _data.Reset();
}
