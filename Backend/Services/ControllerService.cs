using AROKIS.Backend.Models;

namespace AROKIS.Backend.Services;

/// <summary>
/// Оркестратор: управляет НЕСКОЛЬКИМИ источниками данных одновременно.
/// </summary>
public class ControllerService : IDisposable
{
    private readonly Dictionary<string, IControllerSource> _sources = new();
    private readonly StreamUpdateService _stream;
    private bool _disposed;

    public bool   IsConnected => _sources.Values.Any(s => s.IsConnected);
    public string SourceName  => string.Join(", ", _sources.Values
        .Where(s => s.IsConnected).Select(s => s.SourceName)) is { Length: > 0 } s ? s : "—";

    public event Action<bool>?           OnConnectionChanged;
    public event Action<string>?         OnFormatDetected;
    public event Action<string>?         OnRawData;
    public event Action<byte[]>?         OnRawBytes;
    public event Action<string, string, string>? OnLog; // (sourceName, level, message)

    public ControllerService(StreamUpdateService stream)
    {
        _stream = stream;
    }

    // ── Добавить источник ──────────────────────────────────────────────────

    public async Task StartAsync(IControllerSource source)
    {
        if (_sources.TryGetValue(source.SourceName, out var old))
        {
            Unsubscribe(old);
            old.Disconnect();
            old.Dispose();
            _sources.Remove(source.SourceName);
        }

        Subscribe(source);
        _sources[source.SourceName] = source;
        await source.ConnectAsync();
    }

    public async Task AddSourceAsync(IControllerSource source)
    {
        if (_sources.ContainsKey(source.SourceName))
        {
            await StartAsync(source);
            return;
        }
        Subscribe(source);
        _sources[source.SourceName] = source;
        await source.ConnectAsync();
    }

    public void RemoveSource(string sourceName)
    {
        if (!_sources.TryGetValue(sourceName, out var src)) return;
        Unsubscribe(src);
        src.Disconnect();
        src.Dispose();
        _sources.Remove(sourceName);
        ResetAllRealDataFlags();
        OnConnectionChanged?.Invoke(IsConnected);
    }

    public async Task SwitchSourceAsync(IControllerSource newSource)
    {
        StopAll();
        await AddSourceAsync(newSource);
    }

    public void Stop() => StopAll();

    private void StopAll()
    {
        foreach (var (_, src) in _sources.ToList())
        {
            Unsubscribe(src);
            src.Disconnect();
            src.Dispose();
        }
        _sources.Clear();
        ResetAllRealDataFlags();
    }

    private void Subscribe(IControllerSource src)
    {
        src.OnRawData           += HandleRawData;
        src.OnRawBytes          += HandleRawBytes;
        src.OnConnectionChanged += HandleConnectionChanged;
        src.OnLog               += (level, msg) => OnLog?.Invoke(src.SourceName, level, msg);
    }

    private void Unsubscribe(IControllerSource src)
    {
        src.OnRawData           -= HandleRawData;
        src.OnRawBytes          -= HandleRawBytes;
        src.OnConnectionChanged -= HandleConnectionChanged;
        // OnLog — лямбда, нельзя отписать напрямую; источник будет уничтожен
    }

    // ── Информация о источниках ───────────────────────────────────────────

    public List<(string Name, bool Connected)> GetSources() =>
        _sources.Values.Select(s => (s.SourceName, s.IsConnected)).ToList();

    public IReadOnlyDictionary<string, IControllerSource> Sources => _sources;

    // ── Обработка данных ──────────────────────────────────────────────────

    private void HandleConnectionChanged(bool connected)
    {
        if (!connected) ResetAllRealDataFlags();
        OnConnectionChanged?.Invoke(connected);
    }

    private void HandleRawBytes(byte[] bytes)
    {
        OnRawBytes?.Invoke(bytes);
    }

    private void HandleRawData(string raw)
    {
        OnRawData?.Invoke(raw);

        var result = ControllerDataConverter.TryConvert(raw);

        if (result == null)
        {
            // Пробрасываем лог-событие на уровень сервиса
            OnLog?.Invoke("ControllerService", "WARN",
                $"Формат не распознан: \"{raw[..Math.Min(80, raw.Length)]}\"");
            return;
        }

        OnLog?.Invoke("ControllerService", "PARSE",
            $"Формат: {result.Format} | Точек: {result.Points.Count} | Stream: {result.SourceId}");
        OnFormatDetected?.Invoke(result.Format);
        WriteToStream(result.SourceId, result.Points);
    }

    // ── Запись в стрим ────────────────────────────────────────────────────

    private void WriteToStream(int sourceId, List<CablePoint> points)
    {
        if (!_stream.StreamCables.TryGetValue(sourceId, out var sim))
        {
            OnLog?.Invoke("ControllerService", "WARN", $"Stream {sourceId} не найден в StreamCables");
            return;
        }

        lock (sim)
        {
            if (!sim.HasRealData)
            {
                sim.HasRealData = true;
                sim.PointDeviations.Clear();
                sim.ModifiedIndices.Clear();
                OnLog?.Invoke("ControllerService", "OK",
                    $"Stream {sourceId} ({sim.ShapeName}): симуляция отключена, начат приём реальных данных");
            }

            sim.Cable.Points.Clear();
            sim.Cable.Points.AddRange(points);
            sim.OriginalPoints   = points.Select(p => new CablePoint { X = p.X, Y = p.Y }).ToList();
            sim.Cable.LastUpdate = DateTime.Now;
        }
    }

    private void ResetAllRealDataFlags()
    {
        foreach (var sim in _stream.StreamCables.Values)
        {
            lock (sim)
            {
                if (sim.HasRealData)
                {
                    sim.HasRealData = false;
                    OnLog?.Invoke("ControllerService", "INFO",
                        $"Stream {sim.ShapeName}: симуляция возобновлена (источник отключился)");
                }
            }
        }
    }

    public void ResetRealDataFlag(int sourceId)
    {
        if (!_stream.StreamCables.TryGetValue(sourceId, out var sim)) return;
        lock (sim) { sim.HasRealData = false; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopAll();
    }
}
