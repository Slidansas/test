using AROKIS.Backend.Models;
using AROKIS.Backend.Services;
using System.Runtime.InteropServices;
using Photino.Blazor.AROKIS.AppConfig;
using AppConfigModel = Photino.Blazor.AROKIS.AppConfig.AppConfig;

namespace Photino.Blazor.AROKIS;

[ClassInterface(ClassInterfaceType.AutoDual)]
[ComVisible(true)]
public class ArokisInterop
{
    private readonly ArokisApi          _api;
    private readonly ControllerService  _controller;
    private AppConfigModel _config;

    private readonly Queue<object> _rawLog      = new();
    private readonly Queue<object> _connLog     = new();
    private const int LogMaxSize = 200;

    public ArokisInterop(ArokisApi api, ArokisSettings settings, ControllerService controller, AppConfigModel config)
    {
        _api        = api;
        _controller = controller;
        _config     = config;

        // ── Лог подключения: все события от источников ────────────────────
        _controller.OnLog += (source, level, message) =>
        {
            lock (_connLog)
            {
                _connLog.Enqueue(new {
                    time    = DateTime.Now.ToString("HH:mm:ss.fff"),
                    source,
                    level,
                    message
                });
                if (_connLog.Count > LogMaxSize) _connLog.Dequeue();
            }
        };

        // ── Лог сырых байт ────────────────────────────────────────────────
        _controller.OnRawBytes += bytes =>
        {
            lock (_rawLog)
            {
                string asText;
                try { asText = System.Text.Encoding.UTF8.GetString(bytes).Trim(); }
                catch { asText = ""; }

                var hex = BitConverter.ToString(bytes.Take(64).ToArray()).Replace("-", " ")
                          + (bytes.Length > 64 ? $" …(+{bytes.Length - 64})" : "");

                ConvertResult? result = null;
                if (!string.IsNullOrWhiteSpace(asText))
                    result = ControllerDataConverter.TryConvert(asText);

                var display = string.IsNullOrWhiteSpace(asText) ? hex
                              : (asText.Length > 200 ? asText[..200] + "…" : asText);

                _rawLog.Enqueue(new {
                    time   = DateTime.Now.ToString("HH:mm:ss.fff"),
                    raw    = display,
                    hex,
                    bytes  = bytes.Length,
                    format = result?.Format ?? (string.IsNullOrWhiteSpace(asText) ? "binary" : "unknown"),
                    points = result?.Points.Count ?? 0,
                    ok     = result != null
                });
                if (_rawLog.Count > LogMaxSize) _rawLog.Dequeue();
            }
        };
    }

    // ── Данные стримов ────────────────────────────────────────────────────
    public string GetStreamCable(int n)
    {
        var c = _api.GetStreamCable(n);
        if (c is null) return "null";
        return System.Text.Json.JsonSerializer.Serialize(new {
            name       = c.Name,
            lastUpdate = c.LastUpdate.ToString("o"),
            points     = c.Points.Select(p => new { x = p.X, y = p.Y }).ToArray()
        });
    }

    public string GetThicknessMm(int n, string anglesJson)
    {
        var angles = System.Text.Json.JsonSerializer.Deserialize<double[]>(anglesJson) ?? [];
        return System.Text.Json.JsonSerializer.Serialize(_api.GetThicknessMm(n, angles));
    }

    public string GetAllStreamsThickness(string anglesJson)
    {
        var angles = System.Text.Json.JsonSerializer.Deserialize<double[]>(anglesJson) ?? [];
        var result = new double[4][];
        for (int s = 1; s <= 4; s++) result[s - 1] = _api.GetThicknessMm(s, angles);
        return System.Text.Json.JsonSerializer.Serialize(result);
    }

    public void SetK(int index, double value) => _api.SetValue(index, value);

    public void StartAllStreams()
    {
        for (int i = 1; i <= 4; i++) _api.StartStream(i);
    }

    // ── Управление симуляцией ─────────────────────────────────────────────
    public void PauseStream(int streamId)  => _api.StopStream(streamId);
    public void ResumeStream(int streamId) => _api.StartStream(streamId);

    public void PauseAllStreams()
    {
        for (int i = 1; i <= 4; i++) _api.StopStream(i);
    }

    public void ResumeAllStreams()
    {
        for (int i = 1; i <= 4; i++) _api.StartStream(i);
    }

    public string GetSimulationStatus()
    {
        var result = new System.Text.Json.Nodes.JsonObject();
        for (int i = 1; i <= 4; i++)
        {
            var status = _api.GetStreamStatus(i) as dynamic;
            result[i.ToString()] = new System.Text.Json.Nodes.JsonObject {
                ["running"]     = status?.isRunning ?? false,
                ["hasRealData"] = false
            };
        }
        return result.ToJsonString();
    }

    // ── Одиночное подключение (обратная совместимость) ────────────────────

    public string GetConnectionType() => _config.ConnectionTypeConfig;

    public async void ConnectSerial(string port, int baudRate)
    {
        _config.SerialPortConfig     = port;
        _config.SerialBaudRateConfig = baudRate;
        _config.ConnectionTypeConfig = "Serial";
        ConfigManager.SaveConfig(_config);

        var source = new SerialControllerSource(port, baudRate);
        await _controller.StartAsync(source);
    }

    public async void ConnectTcp(string ip, int port)
    {
        _config.ControllerIpConfig   = ip;
        _config.ControllerPortConfig = port;
        _config.ConnectionTypeConfig = "TCP";
        ConfigManager.SaveConfig(_config);

        var source = new TcpControllerSource(ip, port);
        await _controller.StartAsync(source);
    }

    public void DisconnectController()
    {
        _controller.Stop();
        _config.ConnectionTypeConfig = "None";
        _config.Devices.Clear();
        ConfigManager.SaveConfig(_config);
    }

    // ── Мульти-устройства ─────────────────────────────────────────────────

    /// <summary>
    /// Добавить TCP-устройство к списку активных подключений.
    /// Возвращает JSON { ok, sourceName, error }
    /// </summary>
    public string AddTcpDevice(string ip, int port, string label = "")
    {
        try
        {
            var device = new DeviceConfig { Type = "TCP", IpAddress = ip, TcpPort = port, Label = label };

            // Сохраняем в конфиг
            var existing = _config.Devices.FirstOrDefault(d =>
                d.Type == "TCP" && d.IpAddress == ip && d.TcpPort == port);
            if (existing == null) _config.Devices.Add(device);
            ConfigManager.SaveConfig(_config);

            var source = new TcpControllerSource(ip, port);
            _ = _controller.AddSourceAsync(source);

            return System.Text.Json.JsonSerializer.Serialize(new {
                ok = true, sourceName = $"TCP:{ip}:{port}", error = ""
            });
        }
        catch (Exception ex)
        {
            return System.Text.Json.JsonSerializer.Serialize(new {
                ok = false, sourceName = "", error = ex.Message
            });
        }
    }

    /// <summary>
    /// Добавить Serial-устройство к списку активных подключений.
    /// </summary>
    public string AddSerialDevice(string portName, int baudRate, string label = "")
    {
        try
        {
            var device = new DeviceConfig { Type = "Serial", SerialPort = portName, BaudRate = baudRate, Label = label };

            var existing = _config.Devices.FirstOrDefault(d =>
                d.Type == "Serial" && d.SerialPort == portName);
            if (existing == null) _config.Devices.Add(device);
            ConfigManager.SaveConfig(_config);

            var source = new SerialControllerSource(portName, baudRate);
            _ = _controller.AddSourceAsync(source);

            return System.Text.Json.JsonSerializer.Serialize(new {
                ok = true, sourceName = $"Serial:{portName}", error = ""
            });
        }
        catch (Exception ex)
        {
            return System.Text.Json.JsonSerializer.Serialize(new {
                ok = false, sourceName = "", error = ex.Message
            });
        }
    }

    /// <summary>Отключить устройство по sourceName ("TCP:ip:port" или "Serial:COM3").</summary>
    public void RemoveDevice(string sourceName)
    {
        _controller.RemoveSource(sourceName);

        // Удаляем из конфига
        var toRemove = _config.Devices.Where(d => BuildSourceName(d) == sourceName).ToList();
        foreach (var d in toRemove) _config.Devices.Remove(d);
        ConfigManager.SaveConfig(_config);
    }

    /// <summary>
    /// JSON-список всех подключённых источников:
    /// [{ sourceName, connected, label }, ...]
    /// </summary>
    public string GetDeviceList()
    {
        var list = _controller.Sources.Values.Select(s => new {
            sourceName = s.SourceName,
            connected  = s.IsConnected,
            label      = _config.Devices.FirstOrDefault(d => BuildSourceName(d) == s.SourceName)?.Label ?? ""
        }).ToList();

        return System.Text.Json.JsonSerializer.Serialize(list);
    }

    /// <summary>
    /// JSON общий статус подключения:
    /// { connected, sourceCount, sources: [...], type }
    /// </summary>
    public string GetConnectionStatus()
    {
        return System.Text.Json.JsonSerializer.Serialize(new {
            connected   = _controller.IsConnected,
            sourceName  = _controller.SourceName,
            sourceCount = _controller.Sources.Count,
            type        = _config.ConnectionTypeConfig,
            sources     = _controller.Sources.Values.Select(s => new {
                name      = s.SourceName,
                connected = s.IsConnected
            }).ToArray()
        });
    }

    public string GetSerialPorts()
    {
        return System.Text.Json.JsonSerializer.Serialize(SerialControllerSource.GetAvailablePorts());
    }

    // ── Автоподключение при старте ────────────────────────────────────────

    public async Task AutoConnectAsync()
    {
        // Приоритет: мульти-устройства из Devices
        if (_config.Devices.Count > 0)
        {
            foreach (var dev in _config.Devices)
            {
                IControllerSource source = dev.Type == "Serial"
                    ? new SerialControllerSource(dev.SerialPort, dev.BaudRate)
                    : new TcpControllerSource(dev.IpAddress, dev.TcpPort);

                await _controller.AddSourceAsync(source);
            }
            return;
        }

        // Fallback: одиночное подключение (обратная совместимость)
        switch (_config.ConnectionTypeConfig)
        {
            case "Serial":
                await _controller.StartAsync(
                    new SerialControllerSource(_config.SerialPortConfig, _config.SerialBaudRateConfig));
                break;
            case "TCP":
                await _controller.StartAsync(
                    new TcpControllerSource(_config.ControllerIpConfig, _config.ControllerPortConfig));
                break;
        }
    }

    // ── Диагностика и тестовый ввод ────────────────────────────────────────

    /// <summary>Принять строку в любом формате, распарсить и отправить в стрим.</summary>
    public string InjectRawData(string raw, int streamId = 1)
    {
        try
        {
            var result = ControllerDataConverter.TryConvert(raw);
            if (result == null)
                return System.Text.Json.JsonSerializer.Serialize(new {
                    ok = false, format = "unknown", points = 0,
                    error = "Формат не распознан. Проверьте данные."
                });

            var targetId = streamId > 0 ? streamId : result.SourceId;
            if (_api.Stream.StreamCables.TryGetValue(targetId, out var sim))
            {
                lock (sim)
                {
                    sim.HasRealData = true;
                    sim.PointDeviations.Clear();
                    sim.ModifiedIndices.Clear();
                    sim.Cable.Points.Clear();
                    sim.Cable.Points.AddRange(result.Points);
                    sim.Cable.LastUpdate = DateTime.Now;
                    sim.OriginalPoints   = result.Points
                        .Select(p => new CablePoint { X = p.X, Y = p.Y }).ToList();
                }
            }

            return System.Text.Json.JsonSerializer.Serialize(new {
                ok       = true,
                format   = result.Format,
                points   = result.Points.Count,
                sourceId = targetId,
                error    = ""
            });
        }
        catch (Exception ex)
        {
            return System.Text.Json.JsonSerializer.Serialize(new {
                ok = false, format = "error", points = 0, error = ex.Message
            });
        }
    }

    /// <summary>
    /// Inject одной строки сразу во ВСЕ 4 стрима (один и тот же набор точек).
    /// Удобно для тестирования без контроллера.
    /// </summary>
    public string InjectRawDataToAll(string raw)
    {
        var results = new List<object>();
        for (int sid = 1; sid <= 4; sid++)
        {
            var r = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                InjectRawData(raw, sid));
            results.Add(r);
        }
        return System.Text.Json.JsonSerializer.Serialize(results);
    }

    /// <summary>Лог подключения — все события (попытки, ошибки, байты, парсинг).</summary>
    public string GetConnectionLog(int take = 100)
    {
        lock (_connLog)
        {
            var items = _connLog.TakeLast(take).ToArray();
            return System.Text.Json.JsonSerializer.Serialize(items);
        }
    }

    /// <summary>Очистить лог подключения.</summary>
    public void ClearConnectionLog()
    {
        lock (_connLog) { _connLog.Clear(); }
    }

    /// <summary>Последние N пакетов сырых данных с контроллера.</summary>
    public string GetRawLog(int take = 50)
    {
        lock (_rawLog)
        {
            var items = _rawLog.TakeLast(take).ToArray();
            return System.Text.Json.JsonSerializer.Serialize(items);
        }
    }

    /// <summary>Очистить лог сырых данных.</summary>
    public void ClearRawLog()
    {
        lock (_rawLog) { _rawLog.Clear(); }
    }

    /// <summary>Диагностика стрима — первые N точек в читаемом виде.</summary>
    public string DiagnoseStream(int streamNumber, int take = 10)
    {
        var c = _api.GetStreamCable(streamNumber);
        if (c is null) return "Стрим не найден";
        var sample = c.Points.Take(take)
            .Select((p, i) => $"[{i:D3}] X={p.X:F4}  Y={p.Y:F4}")
            .ToList();
        return $"Stream {streamNumber} ({c.Name}): {c.Points.Count} points\n"
             + string.Join("\n", sample);
    }

    /// <summary>
    /// Диагностика всех стримов одним вызовом.
    /// Возвращает JSON [{streamId, name, points, hasRealData}, ...]
    /// </summary>
    public string DiagnoseAllStreams()
    {
        var list = new List<object>();
        for (int i = 1; i <= 4; i++)
        {
            var c = _api.GetStreamCable(i);
            if (_api.Stream.StreamCables.TryGetValue(i, out var sim))
            {
                list.Add(new {
                    streamId    = i,
                    name        = sim.ShapeName,
                    points      = c?.Points.Count ?? 0,
                    hasRealData = sim.HasRealData,
                    isRunning   = sim.IsRunning,
                    lastUpdate  = c?.LastUpdate.ToString("o") ?? ""
                });
            }
        }
        return System.Text.Json.JsonSerializer.Serialize(list);
    }

    // ── Ping ──────────────────────────────────────────────────────────────
    public string GetControllerIp() => _config.ControllerIpConfig;
    public int    GetControllerPort() => _config.ControllerPortConfig;

    public async void SetControllerIp(string ip, int port = 0)
    {
        _config.ControllerIpConfig   = ip;
        if (port > 0) _config.ControllerPortConfig = port;
        _config.ConnectionTypeConfig = "TCP";
        ConfigManager.SaveConfig(_config);

        var source = new TcpControllerSource(ip, _config.ControllerPortConfig);
        await _controller.StartAsync(source);
    }

    public bool PingController()
    {
        try {
            if (string.IsNullOrEmpty(_config.ControllerIpConfig)) return false;
            var ping  = new System.Net.NetworkInformation.Ping();
            var reply = ping.Send(_config.ControllerIpConfig, 1000);
            return reply.Status == System.Net.NetworkInformation.IPStatus.Success;
        } catch { return false; }
    }

    // ── Статус подключения по стриму (для индикаторов на карточках) ─────────

    /// <summary>
    /// Возвращает статус подключения для конкретного стрима (головы).
    /// Ищет источник у которого StreamId == streamId в конфиге Devices.
    /// Если явного маппинга нет — возвращает общий статус (IsConnected).
    /// JSON: { connected, sourceName, hasDevice }
    /// </summary>
    public string GetStreamConnectionStatus(int streamId)
    {
        // Ищем устройство явно привязанное к этому стриму
        var device = _config.Devices.FirstOrDefault(d => d.StreamId == streamId);
        if (device != null)
        {
            var sn  = BuildSourceName(device);
            var src = _controller.Sources.TryGetValue(sn, out var s) ? s : null;
            return System.Text.Json.JsonSerializer.Serialize(new {
                connected  = src?.IsConnected ?? false,
                sourceName = sn,
                hasDevice  = true
            });
        }

        // Нет явной привязки — если вообще нет устройств, нет подключения
        if (_config.Devices.Count == 0 && _config.ConnectionTypeConfig == "None")
        {
            return System.Text.Json.JsonSerializer.Serialize(new {
                connected  = false,
                sourceName = "",
                hasDevice  = false
            });
        }

        // Устройства есть, но без привязки к стримам — возвращаем общий статус
        return System.Text.Json.JsonSerializer.Serialize(new {
            connected  = _controller.IsConnected,
            sourceName = _controller.SourceName,
            hasDevice  = _config.Devices.Count > 0 || _config.ConnectionTypeConfig != "None"
        });
    }


    // ── Modbus TCP подключение ────────────────────────────────────────────

    public async Task<string> ConnectModbusTcp(string ip, int port, int unitId, int pollIntervalMs)
    {
        var source = new ModbusTcpControllerSource(ip, port, (byte)unitId, pollIntervalMs);
        await _controller.StartAsync(source);

        // Ждём реального результата подключения (таймаут 5 сек)
        for (int i = 0; i < 50; i++)
        {
            await Task.Delay(100);
            if (source.IsConnected)
                return System.Text.Json.JsonSerializer.Serialize(new { ok = true,  message = $"Подключён к {ip}:{port}" });
            if (!_controller.Sources.ContainsKey(source.SourceName))
                break; // был удалён из-за ошибки
        }

        // Если не подключился — проверяем последнюю запись лога
        string lastError = "";
        lock (_connLog)
        {
            var last = _connLog.LastOrDefault(e =>
            {
                var el = (System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                    System.Text.Json.JsonSerializer.Serialize(e)));
                return el.GetProperty("level").GetString() == "ERROR";
            });
            if (last != null)
            {
                var el = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                    System.Text.Json.JsonSerializer.Serialize(last));
                lastError = el.GetProperty("message").GetString() ?? "";
            }
        }

        return System.Text.Json.JsonSerializer.Serialize(new { ok = false, message = lastError.Length > 0 ? lastError : $"Не удалось подключиться к {ip}:{port}" });
    }

    // ── Modbus поллинг ────────────────────────────────────────────────────

    /// <summary>
    /// Отправить одиночный Modbus TCP запрос на чтение регистров.
    /// sourceName: "TCP:ip:port" или "" — первый TCP-источник.
    /// </summary>
    public string SendModbusRequest(string sourceName, int unitId, int funcCode, int startAddress, int quantity)
    {
        var src = FindTcpSource(sourceName);
        if (src == null)
            return Err("TCP-источник не найден. Сначала подключитесь.");

        _ = src.SendModbusRequestAsync((byte)unitId, (byte)funcCode, (ushort)startAddress, (ushort)quantity);
        return Ok($"Запрос отправлен: Unit={unitId} FC=0x{funcCode:X2} Addr={startAddress} Qty={quantity}");
    }

    /// <summary>Запустить авто-поллинг Modbus.</summary>
    public string StartModbusPolling(string sourceName, int unitId, int funcCode, int startAddress, int quantity, int intervalMs)
    {
        var src = FindTcpSource(sourceName);
        if (src == null)
            return Err("TCP-источник не найден.");

        src.StartPolling((byte)unitId, (byte)funcCode, (ushort)startAddress, (ushort)quantity, Math.Max(100, intervalMs));
        return Ok($"Поллинг запущен: каждые {intervalMs} мс, Unit={unitId} FC=0x{funcCode:X2} Addr={startAddress} Qty={quantity}");
    }

    /// <summary>Остановить авто-поллинг.</summary>
    public string StopModbusPolling(string sourceName)
    {
        var src = FindTcpSource(sourceName);
        if (src == null) return Err("TCP-источник не найден.");
        src.StopPolling();
        return Ok("Поллинг остановлен");
    }

    /// <summary>Статус поллинга всех TCP-источников.</summary>
    public string GetPollingStatus()
    {
        var list = _controller.Sources.Values
            .OfType<TcpControllerSource>()
            .Select(s => new { sourceName = s.SourceName, isPolling = s.IsPolling })
            .ToArray();
        return System.Text.Json.JsonSerializer.Serialize(list);
    }

    private TcpControllerSource? FindTcpSource(string sourceName)
    {
        if (!string.IsNullOrEmpty(sourceName) &&
            _controller.Sources.TryGetValue(sourceName, out var s) &&
            s is TcpControllerSource named)
            return named;

        return _controller.Sources.Values.OfType<TcpControllerSource>().FirstOrDefault();
    }

    private static string Ok(string msg)  => System.Text.Json.JsonSerializer.Serialize(new { ok = true,  message = msg });
    private static string Err(string msg) => System.Text.Json.JsonSerializer.Serialize(new { ok = false, message = msg });

    // ── Вспомогательные ───────────────────────────────────────────────────
    private static string BuildSourceName(DeviceConfig d) =>
        d.Type == "Serial" ? $"Serial:{d.SerialPort}" : $"TCP:{d.IpAddress}:{d.TcpPort}";
}
