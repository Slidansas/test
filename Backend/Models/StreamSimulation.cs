namespace AROKIS.Backend.Models;

public class StreamSimulation
{
    public CableProjection Cable { get; set; } = new CableProjection();
    public List<CablePoint> OriginalPoints { get; set; } = new();
    public HashSet<int> ModifiedIndices { get; set; } = new();
    public Dictionary<int, DeviationInfo> PointDeviations { get; set; } = new();
    public bool IsRunning { get; set; } = false;
    public string ShapeName { get; set; } = "";

    /// <summary>
    /// true — стрим получает реальные данные от контроллера.
    /// Пока true симуляция (случайные отклонения) не применяется.
    /// Сбрасывается в false при отключении контроллера.
    /// </summary>
    public bool HasRealData { get; set; } = false;
}
