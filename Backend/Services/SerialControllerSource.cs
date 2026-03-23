using System.IO.Ports;
using System.Text;

namespace AROKIS.Backend.Services;

/// <summary>
/// Источник данных через Serial (COM-порт, USB, RS485).
/// </summary>
public class SerialControllerSource : IControllerSource
{
    private SerialPort? _port;
    private CancellationTokenSource? _cts;
    private readonly StringBuilder _buffer = new();
    private bool _disposed;

    public string PortName { get; }
    public int BaudRate    { get; }

    public event Action<string>?         OnRawData;
    public event Action<byte[]>?         OnRawBytes;
    public event Action<bool>?           OnConnectionChanged;
    public event Action<string, string>? OnLog;

    public bool   IsConnected { get; private set; }
    public string SourceName  => $"Serial:{PortName}";

    public SerialControllerSource(string portName, int baudRate = 115200)
    {
        PortName = portName;
        BaudRate = baudRate;
    }

    public Task ConnectAsync()
    {
        Log("INFO", $"Открытие порта {PortName} @ {BaudRate} baud...");

        // Проверяем доступные порты
        var available = SerialPort.GetPortNames();
        Log("INFO", $"Доступные COM-порты: {(available.Length > 0 ? string.Join(", ", available) : "(нет)")}");

        if (!available.Contains(PortName))
            Log("WARN", $"Порт {PortName} не найден в списке доступных!");

        try
        {
            _port = new SerialPort(PortName, BaudRate)
            {
                ReadTimeout  = 1000,
                WriteTimeout = 1000,
                NewLine      = "\n"
            };
            _port.Open();
            _cts = new CancellationTokenSource();
            Task.Run(() => ReadLoop(_cts.Token));

            IsConnected = true;
            OnConnectionChanged?.Invoke(true);
            Log("OK", $"Порт открыт: {PortName} @ {BaudRate} baud. DataBits={_port.DataBits}, Parity={_port.Parity}, StopBits={_port.StopBits}");
        }
        catch (Exception ex)
        {
            IsConnected = false;
            Log("ERROR", $"Ошибка открытия порта: {ex.GetType().Name}: {ex.Message}");
        }
        return Task.CompletedTask;
    }

    private async Task ReadLoop(CancellationToken token)
    {
        long totalBytes  = 0;
        int  packetCount = 0;

        Log("INFO", "Ожидание данных из порта...");

        while (!token.IsCancellationRequested && _port?.IsOpen == true)
        {
            try
            {
                while (_port?.BytesToRead > 0)
                {
                    var buf = new byte[_port.BytesToRead];
                    _port.Read(buf, 0, buf.Length);

                    totalBytes  += buf.Length;
                    packetCount++;
                    OnRawBytes?.Invoke(buf);

                    var hexPreview = BitConverter.ToString(buf.Take(16).ToArray()).Replace("-", " ")
                                     + (buf.Length > 16 ? $" …(+{buf.Length - 16})" : "");
                    string asText = "";
                    try { asText = Encoding.UTF8.GetString(buf).Trim().Replace("\r","").Replace("\n"," ↵ "); } catch { }
                    var textHint = string.IsNullOrWhiteSpace(asText) ? "" : $" | text: \"{(asText.Length > 60 ? asText[..60] + "…" : asText)}\"";

                    Log("DATA", $"#{packetCount} +{buf.Length} байт (итого {totalBytes}) | hex: {hexPreview}{textHint}");

                    _buffer.Append(Encoding.UTF8.GetString(buf));

                    int linesFound = 0;
                    while (_buffer.ToString().Contains('\n'))
                    {
                        int idx  = _buffer.ToString().IndexOf('\n');
                        var line = _buffer.ToString()[..idx].Trim();
                        _buffer.Remove(0, idx + 1);
                        linesFound++;

                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            Console.WriteLine($"[Serial RAW] {line}");
                            OnRawData?.Invoke(line);
                        }
                    }

                    if (linesFound == 0 && _buffer.Length > 0)
                        Log("WARN", $"Нет разделителя \\n в буфере ({_buffer.Length} символов). Ожидаем ещё данных...");
                }
                await Task.Delay(10, token);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Log("ERROR", $"Ошибка чтения: {ex.GetType().Name}: {ex.Message}");
                await Task.Delay(200, token);
            }
        }

        IsConnected = false;
        OnConnectionChanged?.Invoke(false);
        Log("INFO", $"Порт закрыт: {PortName}. Всего получено: {totalBytes} байт в {packetCount} пакетах");
    }

    public void Disconnect()
    {
        Log("INFO", "Disconnect() вызван");
        _cts?.Cancel();
        if (_port?.IsOpen == true) { _port.Close(); _port.Dispose(); _port = null; }
        IsConnected = false;
        Console.WriteLine($"[Serial] Disconnected: {PortName}");
    }

    private void Log(string level, string message)
    {
        Console.WriteLine($"[Serial:{PortName}][{level}] {message}");
        OnLog?.Invoke(level, message);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Disconnect();
        _cts?.Dispose();
    }

    public static string[] GetAvailablePorts() => SerialPort.GetPortNames();
}
