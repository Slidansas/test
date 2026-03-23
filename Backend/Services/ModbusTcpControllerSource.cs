using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace AROKIS.Backend.Services;

/// <summary>
/// Копия логики Deltaa — читает XR/YR/XL/YL с Modbus TCP устройства.
/// IP передаётся снаружи (из UI).
/// </summary>
public class ModbusTcpControllerSource : IControllerSource
{
    // Карта регистров — 1:1 из оригинала
    private const ushort ADDR_XR     = 2000;
    private const ushort ADDR_YR     = 2720;
    private const ushort ADDR_XL     = 3440;
    private const ushort ADDR_YL     = 4160;
    private const int    POINT_COUNT = 360;
    private const int    CHUNK_SIZE  = 100;
    private const byte   UNIT_ID_DEF = 1;

    public string Host           { get; }
    public int    Port           { get; }
    public byte   UnitId         { get; }
    public int    PollIntervalMs { get; }

    private TcpClient?               _client;
    private NetworkStream?           _netStream;
    private CancellationTokenSource? _cts;
    private bool                     _disposed;
    private ushort                   _txId;

    public event Action<string>?         OnRawData;
    public event Action<byte[]>?         OnRawBytes;
    public event Action<bool>?           OnConnectionChanged;
    public event Action<string, string>? OnLog;

    public bool   IsConnected { get; private set; }
    public string SourceName  => $"ModbusTCP:{Host}:{Port}";

    public ModbusTcpControllerSource(string host, int port = 502, byte unitId = UNIT_ID_DEF, int pollIntervalMs = 1000)
    {
        Host           = host;
        Port           = port;
        UnitId         = unitId;
        PollIntervalMs = pollIntervalMs;
    }

    public async Task ConnectAsync()
    {
        Log("INFO", $"Подключение к {Host}:{Port}...");
        try
        {
            _client    = new TcpClient();
            await _client.ConnectAsync(Host, Port);
            _netStream = _client.GetStream();
            _cts       = new CancellationTokenSource();
            // IsConnected станет true только после успешного первого чтения регистров
            Log("INFO", $"TCP handshake OK: {_client.Client.RemoteEndPoint} — проверяю связь (читаю регистры)...");
            Task.Run(() => PollLoop(_cts.Token));
        }
        catch (Exception ex)
        {
            IsConnected = false;
            Log("ERROR", $"Ошибка подключения: {ex.Message}");
        }
    }

    // ── Цикл поллинга ─────────────────────────────────────────────────────

    private async Task PollLoop(CancellationToken token)
    {
        int n = 0;
        while (!token.IsCancellationRequested)
        {
            n++;
            try
            {
                await DoPoll(n, token);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Log("ERROR", $"Опрос #{n}: {ex.Message}");
                IsConnected = false;
                OnConnectionChanged?.Invoke(false);

                try { await Task.Delay(2000, token); } catch { break; }

                try
                {
                    _netStream?.Close(); _client?.Close();
                    _client    = new TcpClient();
                    await _client.ConnectAsync(Host, Port);
                    _netStream  = _client.GetStream();
                    Log("INFO", "TCP переподключён — ожидаю данных для подтверждения...");
                }
                catch (Exception rex)
                {
                    Log("ERROR", $"Переподключение не удалось: {rex.Message}");
                    try { await Task.Delay(3000, token); } catch { break; }
                }
                continue;
            }

            try { await Task.Delay(PollIntervalMs, token); } catch (OperationCanceledException) { break; }
        }

        IsConnected = false;
        OnConnectionChanged?.Invoke(false);
        Log("INFO", "Поллинг остановлен");
    }

    // ── Один опрос — точная копия Deltaa.Main ─────────────────────────────

    private async Task DoPoll(int n, CancellationToken token)
    {
        Log("INFO", $"Опрос #{n}");

        var xr = await ReadFloatArray(ADDR_XR, POINT_COUNT, token);
        if (xr == null)
        {
            Log("ERROR", "XR не прочитан — сервер не отвечает или не Modbus slave");
            return;
        }

        // Подтверждаем соединение только после реального ответа с данными
        if (!IsConnected)
        {
            IsConnected = true;
            OnConnectionChanged?.Invoke(true);
            Log("OK", "Данные получены — соединение подтверждено");
        }

        var yr = await ReadFloatArray(ADDR_YR, POINT_COUNT, token);
        var xl = await ReadFloatArray(ADDR_XL, POINT_COUNT, token);
        var yl = await ReadFloatArray(ADDR_YL, POINT_COUNT, token);

        if (yr == null || xl == null || yl == null)
        {
            Log("ERROR", "Ошибка чтения данных");
            return;
        }

        // Собираем пары — как в оригинале:
        // var rightCoords = xr.Zip(yr, (x, y) => (x, y)).ToList();
        // var leftCoords  = xl.Zip(yl, (x, y) => (x, y)).ToList();
        var rightCoords = xr.Zip(yr, (x, y) => (x, y)).ToList();
        var leftCoords  = xl.Zip(yl, (x, y) => (x, y)).ToList();

        Log("PARSE", $"Правые точки: {rightCoords.Count} | X:[{xr.Min():F2}..{xr.Max():F2}] Y:[{yr.Min():F2}..{yr.Max():F2}]");
        Log("PARSE", $"Левые точки:  {leftCoords.Count}  | X:[{xl.Min():F2}..{xl.Max():F2}] Y:[{yl.Min():F2}..{yl.Max():F2}]");

        // Вывод первых 5 строк в "сырые данные" — как консольный вывод оригинала
        var sb = new StringBuilder();
        sb.AppendLine($"=== ПРАВЫЕ (XR, YR) — опрос #{n} ===");
        for (int i = 0; i < Math.Min(5, rightCoords.Count); i++)
            sb.AppendLine($"{i:D3}: X={rightCoords[i].x:F2}, Y={rightCoords[i].y:F2}");
        sb.AppendLine($"... всего {rightCoords.Count} точек");
        sb.AppendLine($"=== ЛЕВЫЕ (XL, YL) ===");
        for (int i = 0; i < Math.Min(5, leftCoords.Count); i++)
            sb.AppendLine($"{i:D3}: X={leftCoords[i].x:F2}, Y={leftCoords[i].y:F2}");
        sb.AppendLine($"... всего {leftCoords.Count} точек");

        // Отправляем читаемый текст в лог сырых данных
        OnRawBytes?.Invoke(Encoding.UTF8.GetBytes(sb.ToString()));

        // Передаём точки в стримы через стандартный F03 конвертер
        var rightPoints = rightCoords.Select(p => new { x = (double)p.x, y = (double)p.y }).ToList();
        var leftPoints  = leftCoords.Select(p  => new { x = (double)p.x, y = (double)p.y }).ToList();

        OnRawData?.Invoke(JsonSerializer.Serialize(new { sourceId = 1, points = rightPoints }));
        OnRawData?.Invoke(JsonSerializer.Serialize(new { sourceId = 2, points = leftPoints  }));
    }

    // ── Читаем массив float — копия Deltaa.ReadFloatArray ─────────────────

    private async Task<float[]?> ReadFloatArray(ushort startAddress, int count, CancellationToken token)
    {
        var values    = new List<float>();
        int totalRegs = count * 2;

        for (int offset = 0; offset < totalRegs; offset += CHUNK_SIZE)
        {
            int    chunk   = Math.Min(CHUNK_SIZE, totalRegs - offset);
            ushort address = (ushort)(startAddress + offset);

            ushort[]? registers = await ReadHoldingRegisters(UNIT_ID_DEF, address, (ushort)chunk, token);
            if (registers == null)
            {
                Log("ERROR", $"Ошибка чтения {address}: null");
                return null;
            }

            // Точная копия inner loop из Deltaa.ReadFloatArray
            for (int i = 0; i < registers.Length; i += 2)
            {
                if (i + 1 >= registers.Length) break;

                ushort reg_hi = registers[i];
                ushort reg_lo = registers[i + 1];

                byte[] buffer = new byte[4];
                buffer[0] = (byte)(reg_lo >> 8);
                buffer[1] = (byte)(reg_lo & 0xFF);
                buffer[2] = (byte)(reg_hi >> 8);
                buffer[3] = (byte)(reg_hi & 0xFF);

                if (BitConverter.IsLittleEndian)
                    Array.Reverse(buffer);

                float value = BitConverter.ToSingle(buffer, 0);
                if (float.IsNaN(value) || float.IsInfinity(value))
                    value = 0f;

                values.Add(value);
            }

            await Task.Delay(10, token); // Thread.Sleep(10) из оригинала
        }

        return values.ToArray();
    }

    // ── Modbus TC FC03 ─────────────────────────────────────────────────────

    private async Task<ushort[]?> ReadHoldingRegisters(byte unitId, ushort address, ushort count, CancellationToken token)
    {
        if (_netStream == null) return null;

        _txId++;
        var req = new byte[12];
        req[0]  = (byte)(_txId >> 8);    req[1]  = (byte)(_txId & 0xFF);
        req[2]  = 0x00;                  req[3]  = 0x00;
        req[4]  = 0x00;                  req[5]  = 0x06;
        req[6]  = unitId;
        req[7]  = 0x03;
        req[8]  = (byte)(address >> 8);  req[9]  = (byte)(address & 0xFF);
        req[10] = (byte)(count >> 8);    req[11] = (byte)(count & 0xFF);

        await _netStream.WriteAsync(req, token);

        var header = new byte[9];
        await ReadExact(header, token);

        if ((header[7] & 0x80) != 0)
        {
            Log("ERROR", $"Modbus Exception 0x{header[8]:X2} на адресе {address}");
            return null;
        }

        byte byteCount = header[8];
        var  data      = new byte[byteCount];
        await ReadExact(data, token);

        var regs = new ushort[byteCount / 2];
        for (int i = 0; i < regs.Length; i++)
            regs[i] = (ushort)((data[i * 2] << 8) | data[i * 2 + 1]);

        return regs;
    }

    private async Task ReadExact(byte[] buf, CancellationToken token)
    {
        int read = 0;
        while (read < buf.Length)
        {
            int r = await _netStream!.ReadAsync(buf.AsMemory(read, buf.Length - read), token);
            if (r == 0) throw new Exception("Сервер закрыл соединение");
            read += r;
        }
    }

    private void Log(string level, string message)
    {
        Console.WriteLine($"[ModbusTCP:{Host}:{Port}][{level}] {message}");
        OnLog?.Invoke(level, message);
    }

    public void Disconnect()
    {
        Log("INFO", "Disconnect()");
        _cts?.Cancel();
        _netStream?.Close();
        _client?.Close();
        IsConnected = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Disconnect();
        _cts?.Dispose();
        _netStream?.Dispose();
        _client?.Dispose();
    }
}
