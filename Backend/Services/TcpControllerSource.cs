using System.Net.Sockets;
using System.Text;

namespace AROKIS.Backend.Services;

/// <summary>
/// Источник данных через TCP.
/// Поддерживает два режима:
///   — Modbus TCP: парсит бинарные MBAP-фреймы (0x0000 Protocol ID)
///   — Text TCP:   читает строки разделённые \n (fallback)
/// </summary>
public class TcpControllerSource : IControllerSource
{
    private TcpClient?     _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    public string Host { get; }
    public int    Port { get; }

    public event Action<string>?       OnRawData;
    public event Action<byte[]>?       OnRawBytes;
    public event Action<bool>?         OnConnectionChanged;
    public event Action<string, string>? OnLog;

    public bool   IsConnected { get; private set; }
    public string SourceName  => $"TCP:{Host}:{Port}";

    public TcpControllerSource(string host, int port)
    {
        Host = host;
        Port = port;
    }

    public async Task ConnectAsync()
    {
        Log("INFO", $"Попытка подключения к {Host}:{Port}...");
        try
        {
            _client = new TcpClient();
            _client.ReceiveTimeout = 0; // без таймаута — ждём данные сколько нужно
            await _client.ConnectAsync(Host, Port);
            _stream = _client.GetStream();
            _cts    = new CancellationTokenSource();

            Task.Run(() => ReadLoop(_cts.Token));

            IsConnected = true;
            OnConnectionChanged?.Invoke(true);
            Log("OK", $"Подключён. LocalEndPoint: {_client.Client.LocalEndPoint}  →  RemoteEndPoint: {_client.Client.RemoteEndPoint}");
        }
        catch (Exception ex)
        {
            IsConnected = false;
            Log("ERROR", $"Ошибка подключения: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private async Task ReadLoop(CancellationToken token)
    {
        var buffer  = new byte[65536];
        var pending = new List<byte>(4096);
        long totalBytes = 0;
        int  packetCount = 0;

        Log("INFO", "Ожидание данных от контроллера...");

        try
        {
            while (!token.IsCancellationRequested && _stream != null)
            {
                int read = await _stream.ReadAsync(buffer, token);
                if (read == 0)
                {
                    Log("WARN", "Соединение закрыто сервером (read = 0)");
                    break;
                }

                totalBytes  += read;
                packetCount++;
                var chunk = buffer[..read];
                OnRawBytes?.Invoke(chunk);

                // Hex-превью первых байт
                var hexPreview = BitConverter.ToString(chunk.Take(16).ToArray()).Replace("-", " ")
                                 + (read > 16 ? $" …(+{read - 16})" : "");
                // Попробуем прочитать как текст
                string asText = "";
                try { asText = Encoding.UTF8.GetString(chunk).Trim().Replace("\r","").Replace("\n"," ↵ "); } catch { }
                var textHint  = string.IsNullOrWhiteSpace(asText) ? "" : $" | text: \"{(asText.Length > 60 ? asText[..60] + "…" : asText)}\"";

                Log("DATA", $"#{packetCount} +{read} байт (итого {totalBytes}) | hex: {hexPreview}{textHint}");

                pending.AddRange(chunk);
                int before = pending.Count;
                ProcessBuffer(pending);
                int consumed = before - pending.Count;
                if (consumed > 0)
                    Log("PARSE", $"Буфер обработан: потреблено {consumed} байт, осталось {pending.Count} байт");
                else if (pending.Count > 0)
                    Log("WARN", $"Буфер не обработан: накоплено {pending.Count} байт — нет разделителя \\n или неполный фрейм. Ожидаем ещё данных...");
            }
        }
        catch (OperationCanceledException)
        {
            Log("INFO", "ReadLoop отменён (Disconnect)");
        }
        catch (Exception ex)
        {
            Log("ERROR", $"Ошибка чтения: {ex.GetType().Name}: {ex.Message}");
        }

        IsConnected = false;
        OnConnectionChanged?.Invoke(false);
        Log("INFO", $"Отключён от {Host}:{Port}. Всего получено: {totalBytes} байт в {packetCount} пакетах");
    }

    /// <summary>
    /// Пробует разобрать накопленные байты.
    /// Если начало — Modbus TCP (Protocol ID = 0x0000), парсим фреймы.
    /// Иначе — текстовый режим (разбивка по \n).
    /// </summary>
    private void ProcessBuffer(List<byte> buf)
    {
        while (buf.Count > 0)
        {
            // Определяем режим по первым байтам
            if (buf.Count >= 4)
            {
                ushort protocolId = (ushort)((buf[2] << 8) | buf[3]);
                if (protocolId == 0x0000)
                {
                    Log("INFO", $"Обнаружен Modbus TCP фрейм (Protocol ID=0x0000), буфер: {buf.Count} байт");
                    if (!TryParseModbusFrame(buf)) break;
                    continue;
                }
            }

            // Text fallback — ищем \n
            int nl = buf.IndexOf((byte)'\n');
            if (nl < 0) break;

            var line = Encoding.UTF8.GetString(buf.Take(nl).ToArray()).Trim();
            buf.RemoveRange(0, nl + 1);

            if (!string.IsNullOrWhiteSpace(line))
            {
                Console.WriteLine($"[TCP TEXT] {line}");
                OnRawData?.Invoke(line);
            }
        }
    }

    /// <summary>
    /// Парсит один Modbus TCP фрейм из буфера.
    /// Возвращает false если данных недостаточно (ждём ещё).
    /// </summary>
    private bool TryParseModbusFrame(List<byte> buf)
    {
        if (buf.Count < 7) return false;

        ushort length    = (ushort)((buf[4] << 8) | buf[5]);
        int totalFrame   = 6 + length;
        if (buf.Count < totalFrame)
        {
            Log("WARN", $"Неполный Modbus фрейм: нужно {totalFrame} байт, есть {buf.Count}. Ждём ещё...");
            return false;
        }

        if (totalFrame < 8)
        {
            buf.RemoveRange(0, totalFrame);
            return true;
        }

        byte funcCode = buf[7];
        var frameData = buf.Skip(8).Take(totalFrame - 8).ToArray();

        Log("INFO", $"Modbus фрейм: FC=0x{funcCode:X2} ({ModbusFcName(funcCode)}), данных: {frameData.Length} байт");

        byte[]? registers = ExtractRegisters(funcCode, frameData);

        if (registers != null && registers.Length >= 2)
        {
            var hex = string.Join(" ", Enumerable.Range(0, registers.Length / 2)
                .Select(i => ((ushort)((registers[i * 2] << 8) | registers[i * 2 + 1])).ToString("X4")));

            Log("PARSE", $"Modbus регистры ({registers.Length / 2} шт): {(hex.Length > 100 ? hex[..100] + "…" : hex)}");
            OnRawData?.Invoke(hex);
        }
        else
        {
            Log("WARN", $"Modbus FC=0x{funcCode:X2} — регистры не извлечены (неизвестный FC или мало данных)");
        }

        buf.RemoveRange(0, totalFrame);
        return true;
    }

    private static string ModbusFcName(byte fc) => fc switch
    {
        0x01 => "Read Coils",
        0x02 => "Read Discrete Inputs",
        0x03 => "Read Holding Registers",
        0x04 => "Read Input Registers",
        0x10 => "Write Multiple Registers",
        0x16 => "Write Multiple Registers (alt)",
        0x17 => "Read/Write Multiple Registers",
        _    => "Unknown FC"
    };

    private static byte[]? ExtractRegisters(byte funcCode, byte[] data)
    {
        return funcCode switch
        {
            0x03 or 0x04 when data.Length >= 1 => data.Skip(1).Take(data[0]).ToArray(),
            0x01 or 0x02 when data.Length >= 1 => data.Skip(1).Take(data[0]).ToArray(),
            0x10 when data.Length >= 5         => data.Skip(5).Take(data[4]).ToArray(),
            0x16 when data.Length >= 5         => data.Skip(5).Take(data[4]).ToArray(),
            0x17 when data.Length >= 1         => data.Skip(1).Take(data[0]).ToArray(),
            _                                  => null
        };
    }

    public async Task SendCommandAsync(string command)
    {
        if (_stream == null || !IsConnected) return;
        var data = Encoding.UTF8.GetBytes(command + "\n");
        await _stream.WriteAsync(data);
        Log("INFO", $"Отправлено (text): {command}");
    }

    /// <summary>
    /// Отправить Modbus TCP запрос на чтение регистров.
    /// Переподключается если соединение было закрыто сервером.
    /// </summary>
    public async Task<bool> SendModbusRequestAsync(byte unitId, byte funcCode, ushort startAddress, ushort quantity)
    {
        // Если соединение пропало — переподключаемся
        if (!IsConnected || _stream == null || _client?.Connected != true)
        {
            Log("INFO", "Соединение закрыто, переподключение перед запросом...");
            Disconnect();
            await ConnectAsync();
            if (!IsConnected)
            {
                Log("ERROR", "Переподключение не удалось, запрос отменён");
                return false;
            }
        }

        // Строим MBAP + PDU
        // Transaction ID: 0x0001, Protocol ID: 0x0000, Length: 0x0006
        var frame = new byte[12];
        frame[0]  = 0x00; frame[1] = 0x01;          // Transaction ID
        frame[2]  = 0x00; frame[3] = 0x00;          // Protocol ID (Modbus)
        frame[4]  = 0x00; frame[5] = 0x06;          // Length = 6
        frame[6]  = unitId;                          // Unit ID
        frame[7]  = funcCode;                        // Function Code
        frame[8]  = (byte)(startAddress >> 8);       // Start Address Hi
        frame[9]  = (byte)(startAddress & 0xFF);     // Start Address Lo
        frame[10] = (byte)(quantity >> 8);           // Quantity Hi
        frame[11] = (byte)(quantity & 0xFF);         // Quantity Lo

        var hexFrame = BitConverter.ToString(frame).Replace("-", " ");
        Log("INFO", $"Modbus запрос → Unit={unitId} FC=0x{funcCode:X2} ({ModbusFcName(funcCode)}) Addr={startAddress} Qty={quantity} | {hexFrame}");

        try
        {
            await _stream!.WriteAsync(frame);
            return true;
        }
        catch (Exception ex)
        {
            Log("ERROR", $"Ошибка отправки запроса: {ex.Message}");
            IsConnected = false;
            return false;
        }
    }

    // ── Авто-поллинг ─────────────────────────────────────────────────────

    private CancellationTokenSource? _pollCts;

    public void StartPolling(byte unitId, byte funcCode, ushort startAddress, ushort quantity, int intervalMs)
    {
        StopPolling();
        _pollCts = new CancellationTokenSource();
        var token = _pollCts.Token;
        Log("INFO", $"Авто-поллинг запущен: каждые {intervalMs} мс, Unit={unitId} FC=0x{funcCode:X2} Addr={startAddress} Qty={quantity}");
        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                await SendModbusRequestAsync(unitId, funcCode, startAddress, quantity);
                try { await Task.Delay(intervalMs, token); } catch (OperationCanceledException) { break; }
            }
            Log("INFO", "Авто-поллинг остановлен");
        }, token);
    }

    public void StopPolling()
    {
        if (_pollCts == null) return;
        _pollCts.Cancel();
        _pollCts = null;
    }

    public bool IsPolling => _pollCts != null && !_pollCts.IsCancellationRequested;

    public void Disconnect()
    {
        Log("INFO", "Disconnect() вызван");
        StopPolling();
        _cts?.Cancel();
        _stream?.Close();
        _client?.Close();
        IsConnected = false;
    }

    private void Log(string level, string message)
    {
        Console.WriteLine($"[TCP:{Host}:{Port}][{level}] {message}");
        OnLog?.Invoke(level, message);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Disconnect();
        _cts?.Dispose();
        _stream?.Dispose();
        _client?.Dispose();
    }
}
