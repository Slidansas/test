namespace AROKIS.Backend.Services;

/// <summary>
/// Общий интерфейс для любого источника данных контроллера.
/// Реализации: SerialControllerSource, TcpControllerSource.
/// </summary>
public interface IControllerSource : IDisposable
{
    /// <summary>Вызывается при получении новой строки/фрейма данных (после парсинга).</summary>
    event Action<string> OnRawData;

    /// <summary>Вызывается при получении любых байт ДО парсинга — для диагностики.</summary>
    event Action<byte[]>? OnRawBytes;

    /// <summary>Вызывается при изменении статуса подключения.</summary>
    event Action<bool> OnConnectionChanged;

    /// <summary>Лог-событие: (level, message). Levels: INFO, OK, WARN, ERROR, DATA, PARSE</summary>
    event Action<string, string>? OnLog;

    bool IsConnected { get; }
    string SourceName { get; } // "Serial:COM3" или "TCP:192.168.1.100:5000"

    Task ConnectAsync();
    void Disconnect();
}
