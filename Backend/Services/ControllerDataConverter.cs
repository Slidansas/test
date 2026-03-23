using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using AROKIS.Backend.Models;

namespace AROKIS.Backend.Services;

/// <summary>
/// Универсальный конвертер данных от контроллера в список CablePoint.
///
/// ТЕКСТОВЫЕ ФОРМАТЫ (TryConvert):
/// ─────────────────────────────────────────────────────────────
///  F01  XY JSON массив
///       [{"x":80.0,"y":50.0}, ...]
///
///  F02  Polar JSON массив (угол + дистанция)
///       [{"angle":0,"distance":30.0}, ...]
///       [{"a":0,"r":30.0}, ...]
///
///  F03  Envelope JSON (обёртка с sourceId)
///       {"sourceId":1,"points":[...]}
///       {"stream":2,"data":[...]}
///       {"sourceId":1,"distances":[30.0,30.1,...],"step":1.0}
///
///  F04  Flat XY array (плоский массив x0,y0,x1,y1...)
///       [80.0, 50.0, 79.98, 50.52, ...]
///
///  F05  Distance array (массив дистанций, угол по индексу)
///       [30.0, 30.1, 29.8, ...]  — 360 значений = шаг 1°
///
///  F06  CSV x,y
///       80.0,50.0\n79.98,50.52\n...
///
///  F07  CSV angle,distance (полярный CSV)
///       0,30.0\n1,30.1\n...
///
///  F08  Modbus hex строка
///       "5000 3200 4FFE 3234 ..."
///
///  F09  NMEA-подобный протокол
///       $CABLE,1,80.0,50.0,79.98,50.52*FF
///
///  F10  Semicolon envelope (промышленный стиль)
///       STREAM=1;COUNT=360;DATA=80.0,50.0,...
///
/// БИНАРНЫЕ ФОРМАТЫ (TryConvertBinary):
/// ─────────────────────────────────────────────────────────────
///  B01  XY float32     — 8 байт/точка
///  B02  XY float64     — 16 байт/точка
///  B03  XY int16       — 4 байта/точка, масштаб 0.01
///  B04  Polar float32  — angle:float32 + dist:float32
///  B05  Distance int16 — массив дистанций, угол по индексу
///  B06  Framed         — [0xAA][0x55][srcId][count:2][XY float32...]
/// </summary>
public static class ControllerDataConverter
{
    private const double CX = 50.0; // центр X (совпадает с симуляцией)
    private const double CY = 50.0; // центр Y

    // ═══════════════════════════════════════════════════════
    // ТОЧКА ВХОДА — текст
    // ═══════════════════════════════════════════════════════

    public static ConvertResult? TryConvert(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        raw = raw.Trim();

        return
            TryParseEnvelopeJson(raw)      ??
            TryParseXyJson(raw)            ??
            TryParsePolarJson(raw)         ??
            TryParseDistanceArrayJson(raw) ??
            TryParseFlatArray(raw)         ??
            TryParseNmea(raw)              ??
            TryParseSemicolonEnvelope(raw) ??
            TryParseModbusHex(raw)         ??
            TryParsePolarCsv(raw)          ??
            TryParseCsv(raw);
    }

    // ═══════════════════════════════════════════════════════
    // ТОЧКА ВХОДА — бинарные данные
    // ═══════════════════════════════════════════════════════

    public static ConvertResult? TryConvertBinary(
        byte[] data,
        BinaryFormat format = BinaryFormat.AutoDetect,
        int sourceId = 1,
        double angleStepDeg = 1.0)
    {
        if (data == null || data.Length < 4) return null;

        return format switch
        {
            BinaryFormat.XyFloat32     => ParseBinaryXyFloat32(data, sourceId),
            BinaryFormat.XyFloat64     => ParseBinaryXyFloat64(data, sourceId),
            BinaryFormat.XyInt16       => ParseBinaryXyInt16(data, sourceId),
            BinaryFormat.PolarFloat32  => ParseBinaryPolarFloat32(data, sourceId),
            BinaryFormat.DistanceInt16 => ParseBinaryDistanceInt16(data, sourceId, angleStepDeg),
            BinaryFormat.Framed        => ParseBinaryFramed(data),
            _                          => AutoDetectBinary(data, sourceId, angleStepDeg)
        };
    }

    // ═══════════════════════════════════════════════════════
    // ТЕКСТОВЫЕ ПАРСЕРЫ
    // ═══════════════════════════════════════════════════════

    // F03
    private static ConvertResult? TryParseEnvelopeJson(string raw)
    {
        if (!raw.StartsWith("{")) return null;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            int sourceId = 1;
            foreach (var key in new[] { "sourceId","source_id","source","stream","streamId","channel","id" })
                if (root.TryGetProperty(key, out var sid)) { sourceId = sid.GetInt32(); break; }

            // Массив точек
            foreach (var key in new[] { "points","data","pts","measurements","values","coords" })
            {
                if (!root.TryGetProperty(key, out var arr) || arr.ValueKind != JsonValueKind.Array) continue;
                var pts = ParsePointArray(arr);
                if (pts?.Count > 0) return new ConvertResult(sourceId, pts, "F03-envelope");
            }

            // Массив дистанций
            foreach (var key in new[] { "distances","dist","readings","samples" })
            {
                if (!root.TryGetProperty(key, out var arr) || arr.ValueKind != JsonValueKind.Array) continue;
                double step = 1.0;
                if (root.TryGetProperty("step",      out var sv))  step = sv.GetDouble();
                if (root.TryGetProperty("angleStep", out var asv)) step = asv.GetDouble();
                var dists = arr.EnumerateArray().Select(e => e.GetDouble()).ToList();
                var pts   = DistancesToPoints(dists, step);
                if (pts.Count > 0) return new ConvertResult(sourceId, pts, "F05-envelope");
            }

            return null;
        }
        catch { return null; }
    }

    // F01
    private static ConvertResult? TryParseXyJson(string raw)
    {
        if (!raw.StartsWith("[")) return null;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;
            var pts = ParsePointArray(doc.RootElement);
            return pts?.Count > 0 ? new ConvertResult(1, pts, "F01-xy-json") : null;
        }
        catch { return null; }
    }

    // F02
    private static ConvertResult? TryParsePolarJson(string raw)
    {
        if (!raw.StartsWith("[")) return null;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;

            var pts = new List<CablePoint>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object) return null;
                double? angle = null, dist = null;
                foreach (var k in new[] { "angle","a","theta","deg","phi" })
                    if (el.TryGetProperty(k, out var v)) { angle = v.GetDouble(); break; }
                foreach (var k in new[] { "distance","dist","r","radius","d","range" })
                    if (el.TryGetProperty(k, out var v)) { dist = v.GetDouble(); break; }
                if (angle == null || dist == null) return null;
                pts.Add(PolarToXy(angle.Value, dist.Value));
            }
            return pts.Count > 0 ? new ConvertResult(1, pts, "F02-polar-json") : null;
        }
        catch { return null; }
    }

    // F05 — [30.0, 30.1, ...] 360 или 180 значений
    private static ConvertResult? TryParseDistanceArrayJson(string raw)
    {
        if (!raw.StartsWith("[")) return null;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;
            var items = doc.RootElement.EnumerateArray().ToList();
            if (!items.All(e => e.ValueKind == JsonValueKind.Number)) return null;
            var dists = items.Select(e => e.GetDouble()).ToList();
            // Отличаем от F04: кол-во кратно 360/180/720
            if (dists.Count != 360 && dists.Count != 180 && dists.Count != 720 && dists.Count % 2 == 0)
                return null;
            double step = 360.0 / dists.Count;
            var pts = DistancesToPoints(dists, step);
            return pts.Count > 0 ? new ConvertResult(1, pts, "F05-distance-array") : null;
        }
        catch { return null; }
    }

    // F04
    private static ConvertResult? TryParseFlatArray(string raw)
    {
        if (!raw.StartsWith("[")) return null;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;
            var nums = doc.RootElement.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.Number)
                .Select(e => e.GetDouble()).ToList();
            if (nums.Count < 4 || nums.Count % 2 != 0) return null;
            var pts = new List<CablePoint>(nums.Count / 2);
            for (int i = 0; i < nums.Count; i += 2)
                pts.Add(new CablePoint { X = nums[i], Y = nums[i + 1] });
            return new ConvertResult(1, pts, "F04-flat-array");
        }
        catch { return null; }
    }

    // F09 — $CABLE,1,80.0,50.0,...*FF
    private static ConvertResult? TryParseNmea(string raw)
    {
        if (!raw.StartsWith("$")) return null;
        try
        {
            var data  = raw.Contains('*') ? raw[..raw.LastIndexOf('*')] : raw;
            var parts = data.TrimStart('$').Split(',');
            if (parts.Length < 4) return null;

            int sourceId  = 1;
            int dataStart = 1;
            if (int.TryParse(parts[1], out var sid)) { sourceId = sid; dataStart = 2; }

            var nums = parts[dataStart..]
                .Select(p => double.TryParse(p.Trim(),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var v) ? (double?)v : null)
                .Where(v => v.HasValue).Select(v => v!.Value).ToList();

            if (nums.Count < 4 || nums.Count % 2 != 0) return null;
            var pts = new List<CablePoint>(nums.Count / 2);
            for (int i = 0; i < nums.Count; i += 2)
                pts.Add(new CablePoint { X = nums[i], Y = nums[i + 1] });
            return new ConvertResult(sourceId, pts, "F09-nmea");
        }
        catch { return null; }
    }

    // F10 — STREAM=1;DATA=80.0,50.0,...
    private static ConvertResult? TryParseSemicolonEnvelope(string raw)
    {
        if (!raw.Contains('=') || !raw.Contains(';')) return null;
        try
        {
            var fields = raw.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(f => f.Trim().Split('=', 2))
                .Where(p => p.Length == 2)
                .ToDictionary(p => p[0].ToUpperInvariant(), p => p[1]);

            int sourceId = 1;
            foreach (var k in new[] { "STREAM","SOURCE","CHANNEL" })
                if (fields.TryGetValue(k, out var sv) && int.TryParse(sv, out var s)) { sourceId = s; break; }

            string? dataStr = null;
            foreach (var k in new[] { "DATA","POINTS","COORDS","VALUES" })
                if (fields.TryGetValue(k, out var d)) { dataStr = d; break; }
            if (dataStr == null) return null;

            var nums = dataStr.Split(',')
                .Select(p => double.TryParse(p.Trim(),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var v) ? (double?)v : null)
                .Where(v => v.HasValue).Select(v => v!.Value).ToList();

            if (nums.Count < 4 || nums.Count % 2 != 0) return null;
            var pts = new List<CablePoint>(nums.Count / 2);
            for (int i = 0; i < nums.Count; i += 2)
                pts.Add(new CablePoint { X = nums[i], Y = nums[i + 1] });
            return new ConvertResult(sourceId, pts, "F10-semicolon");
        }
        catch { return null; }
    }

    // F08 — Modbus hex "5000 3200 4FFE ..."
    private static ConvertResult? TryParseModbusHex(string raw)
    {
        var tokens = raw.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 4) return null;
        if (!tokens.All(t => t.Length == 4 && t.All(c => "0123456789ABCDEFabcdef".Contains(c)))) return null;
        try
        {
            var nums = tokens.Select(t => (double)(short)Convert.ToUInt16(t, 16) / 100.0).ToList();
            if (nums.Count % 2 != 0) return null;
            var pts = new List<CablePoint>(nums.Count / 2);
            for (int i = 0; i < nums.Count; i += 2)
                pts.Add(new CablePoint { X = nums[i], Y = nums[i + 1] });
            return new ConvertResult(1, pts, "F08-modbus-hex");
        }
        catch { return null; }
    }

    // F07 — CSV angle,distance
    private static ConvertResult? TryParsePolarCsv(string raw)
    {
        try
        {
            var lines = raw.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 4) return null;
            var pts = new List<CablePoint>();
            var sep = new[] { ',', ';', '\t' };
            foreach (var line in lines)
            {
                var parts = line.Trim().Split(sep, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) return null;
                if (!double.TryParse(parts[0], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var a)) return null;
                if (!double.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var d)) return null;
                if (a < 0 || a > 360) return null;
                pts.Add(PolarToXy(a, d));
            }
            return pts.Count > 2 ? new ConvertResult(1, pts, "F07-polar-csv") : null;
        }
        catch { return null; }
    }

    // F06 — CSV x,y
    private static ConvertResult? TryParseCsv(string raw)
    {
        try
        {
            var lines = raw.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2) return null;
            var pts = new List<CablePoint>();
            var sep = new[] { ',', ';', '\t', ' ' };
            foreach (var line in lines)
            {
                var parts = line.Trim().Split(sep, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) return null;
                if (!double.TryParse(parts[0], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var x)) return null;
                if (!double.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var y)) return null;
                pts.Add(new CablePoint { X = x, Y = y });
            }
            return pts.Count > 1 ? new ConvertResult(1, pts, "F06-csv") : null;
        }
        catch { return null; }
    }

    // ═══════════════════════════════════════════════════════
    // БИНАРНЫЕ ПАРСЕРЫ
    // ═══════════════════════════════════════════════════════

    private static ConvertResult? ParseBinaryXyFloat32(byte[] d, int src)
    {
        if (d.Length % 8 != 0) return null;
        var pts = new List<CablePoint>(d.Length / 8);
        for (int i = 0; i < d.Length; i += 8)
            pts.Add(new CablePoint { X = BitConverter.ToSingle(d, i), Y = BitConverter.ToSingle(d, i + 4) });
        return new ConvertResult(src, pts, "B01-xy-float32");
    }

    private static ConvertResult? ParseBinaryXyFloat64(byte[] d, int src)
    {
        if (d.Length % 16 != 0) return null;
        var pts = new List<CablePoint>(d.Length / 16);
        for (int i = 0; i < d.Length; i += 16)
            pts.Add(new CablePoint { X = BitConverter.ToDouble(d, i), Y = BitConverter.ToDouble(d, i + 8) });
        return new ConvertResult(src, pts, "B02-xy-float64");
    }

    private static ConvertResult? ParseBinaryXyInt16(byte[] d, int src)
    {
        if (d.Length % 4 != 0) return null;
        var pts = new List<CablePoint>(d.Length / 4);
        for (int i = 0; i < d.Length; i += 4)
            pts.Add(new CablePoint { X = BitConverter.ToInt16(d, i) * 0.01, Y = BitConverter.ToInt16(d, i + 2) * 0.01 });
        return new ConvertResult(src, pts, "B03-xy-int16");
    }

    private static ConvertResult? ParseBinaryPolarFloat32(byte[] d, int src)
    {
        if (d.Length % 8 != 0) return null;
        var pts = new List<CablePoint>(d.Length / 8);
        for (int i = 0; i < d.Length; i += 8)
            pts.Add(PolarToXy(BitConverter.ToSingle(d, i), BitConverter.ToSingle(d, i + 4)));
        return new ConvertResult(src, pts, "B04-polar-float32");
    }

    private static ConvertResult? ParseBinaryDistanceInt16(byte[] d, int src, double step)
    {
        if (d.Length % 2 != 0) return null;
        var pts = new List<CablePoint>(d.Length / 2);
        for (int i = 0; i < d.Length; i += 2)
            pts.Add(PolarToXy((i / 2) * step, BitConverter.ToUInt16(d, i) * 0.01));
        return new ConvertResult(src, pts, "B05-distance-int16");
    }

    private static ConvertResult? ParseBinaryFramed(byte[] d)
    {
        if (d.Length < 5 || d[0] != 0xAA || d[1] != 0x55) return null;
        int src   = d[2];
        int count = BitConverter.ToUInt16(d, 3);
        if (d.Length < 5 + count * 8) return null;
        var pts = new List<CablePoint>(count);
        for (int i = 0; i < count; i++)
        {
            int off = 5 + i * 8;
            pts.Add(new CablePoint { X = BitConverter.ToSingle(d, off), Y = BitConverter.ToSingle(d, off + 4) });
        }
        return new ConvertResult(src, pts, "B06-framed");
    }

    private static ConvertResult? AutoDetectBinary(byte[] d, int src, double step)
    {
        if (d.Length >= 5 && d[0] == 0xAA && d[1] == 0x55) return ParseBinaryFramed(d);
        if (d.Length % 8 == 0 && d.Length / 8 >= 90)        return ParseBinaryXyFloat32(d, src);
        if (d.Length == 720 || d.Length == 360)              return ParseBinaryDistanceInt16(d, src, step);
        if (d.Length % 4 == 0 && d.Length / 4 >= 90)        return ParseBinaryXyInt16(d, src);
        return null;
    }

    // ═══════════════════════════════════════════════════════
    // ВСПОМОГАТЕЛЬНЫЕ
    // ═══════════════════════════════════════════════════════

    private static CablePoint PolarToXy(double angleDeg, double distance) => new()
    {
        X = Math.Round(CX + distance * Math.Cos(angleDeg * Math.PI / 180.0), 4),
        Y = Math.Round(CY + distance * Math.Sin(angleDeg * Math.PI / 180.0), 4)
    };

    private static List<CablePoint> DistancesToPoints(List<double> distances, double stepDeg) =>
        distances.Select((d, i) => PolarToXy(i * stepDeg, d)).ToList();

    private static List<CablePoint>? ParsePointArray(JsonElement arr)
    {
        var pts = new List<CablePoint>();
        foreach (var el in arr.EnumerateArray())
        {
            if (el.ValueKind == JsonValueKind.Array)
            {
                var vals = el.EnumerateArray().Select(v => v.GetDouble()).ToList();
                if (vals.Count >= 2) pts.Add(new CablePoint { X = vals[0], Y = vals[1] });
                continue;
            }
            if (el.ValueKind != JsonValueKind.Object) continue;

            double? x = null, y = null;
            foreach (var k in new[] { "x","X","px","posX","position_x" })
                if (el.TryGetProperty(k, out var v)) { x = v.GetDouble(); break; }
            foreach (var k in new[] { "y","Y","py","posY","position_y" })
                if (el.TryGetProperty(k, out var v)) { y = v.GetDouble(); break; }
            if (x != null && y != null) { pts.Add(new CablePoint { X = x.Value, Y = y.Value }); continue; }

            double? angle = null, dist = null;
            foreach (var k in new[] { "angle","a","theta","deg","phi" })
                if (el.TryGetProperty(k, out var v)) { angle = v.GetDouble(); break; }
            foreach (var k in new[] { "distance","dist","r","radius","d","range" })
                if (el.TryGetProperty(k, out var v)) { dist = v.GetDouble(); break; }
            if (angle != null && dist != null) { pts.Add(PolarToXy(angle.Value, dist.Value)); continue; }

            return null;
        }
        return pts.Count > 0 ? pts : null;
    }
}

public class ConvertResult
{
    public int              SourceId { get; }
    public List<CablePoint> Points   { get; }
    public string           Format   { get; }

    public ConvertResult(int sourceId, List<CablePoint> points, string format)
    {
        SourceId = sourceId;
        Points   = points;
        Format   = format;
    }
}

public enum BinaryFormat
{
    AutoDetect,
    XyFloat32,
    XyFloat64,
    XyInt16,
    PolarFloat32,
    DistanceInt16,
    Framed
}
