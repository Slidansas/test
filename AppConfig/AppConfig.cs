namespace Photino.Blazor.AROKIS.AppConfig;

/// <summary>Описание одного устройства контроллера.</summary>
public class DeviceConfig
{
    /// <summary>"Serial" | "TCP"</summary>
    public string Type { get; set; } = "TCP";

    // TCP
    public string IpAddress { get; set; } = "192.168.1.100";
    public int    TcpPort   { get; set; } = 502;

    // Serial
    public string SerialPort { get; set; } = "COM3";
    public int    BaudRate   { get; set; } = 115200;

    /// <summary>Стрим (1-4), в который пишет это устройство.
    /// 0 = автоопределение из пакета (по sourceId).</summary>
    public int StreamId { get; set; } = 0;

    /// <summary>Человекочитаемое название для UI.</summary>
    public string Label { get; set; } = "";
}

public class AppConfig
{
    // ── Обратная совместимость: одиночное подключение ───────────────────
    /// <summary>"None" | "Serial" | "TCP" — используется если Devices пуст.</summary>
    public string ConnectionTypeConfig { get; set; } = "None";
    public string SerialPortConfig     { get; set; } = "COM3";
    public int    SerialBaudRateConfig { get; set; } = 115200;
    public string ControllerIpConfig   { get; set; } = "192.168.1.100";
    public int    ControllerPortConfig { get; set; } = 502;

    // ── Мульти-устройства ───────────────────────────────────────────────
    /// <summary>Список устройств для одновременного подключения.</summary>
    public List<DeviceConfig> Devices { get; set; } = new();

    // ── Измерения ────────────────────────────────────────────────────────
    public double MmPerUnitConfig { get; set; } = 0.5;
}
