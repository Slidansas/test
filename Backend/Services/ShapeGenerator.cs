using AROKIS.Backend.Models;

namespace AROKIS.Backend.Services;

public class ShapeGenerator
{
    private readonly DataStorageService? _dataStorage;

    public ShapeGenerator() { }

    public ShapeGenerator(DataStorageService dataStorage)
    {
        _dataStorage = dataStorage;
    }

    public void GenerateAllCables(Dictionary<int, CableProjection> cables)
    {
        for (int i = 1; i <= 5; i++)
        {
            cables[i] = GenerateCableProjection(i);
        }
    }

    public CableProjection GenerateCableProjection(int type)
    {
        var projection = new CableProjection
        {
            Name = $"Cable_{type}_{DateTime.Now:yyyyMMdd_HHmmss}"
        };

        switch (type)
        {
            case 1:
                GenerateCircle(projection);
                break;
            case 2:
                GenerateTriangle(projection);
                break;
            case 3:
                GenerateSquare(projection);
                break;
            case 4:
                GenerateStar(projection);
                break;
            case 5:
                GenerateSpiral(projection);
                break;
            default:
                GenerateCircle(projection);
                break;
        }

        return projection;
    }

    public void GenerateCircle(CableProjection projection)
    {
        double centerX = 50.0;
        double centerY = 50.0;
        double radius = 30.0;

        if (_dataStorage != null)
        {
            radius += _dataStorage.Value1 * 0.1;
        }

        for (int i = 0; i < 360; i++)
        {
            double angle = i * Math.PI / 180.0;
            double x = centerX + radius * Math.Cos(angle);
            double y = centerY + radius * Math.Sin(angle);

            projection.Points.Add(new CablePoint
            {
                X = Math.Round(x, 2),
                Y = Math.Round(y, 2)
            });
        }
    }

    public void GenerateTriangle(CableProjection projection)
    {
        var vertices = new[]
        {
            new { X = 50.0, Y = 85.0 },
            new { X = 20.0, Y = 15.0 },
            new { X = 80.0, Y = 15.0 }
        };

        int pointsPerSide = 120;

        for (int side = 0; side < 3; side++)
        {
            var start = vertices[side];
            var end = vertices[(side + 1) % 3];

            for (int i = 0; i < pointsPerSide; i++)
            {
                double t = (double)i / pointsPerSide;
                double x = start.X + (end.X - start.X) * t;
                double y = start.Y + (end.Y - start.Y) * t;

                projection.Points.Add(new CablePoint
                {
                    X = Math.Round(x, 2),
                    Y = Math.Round(y, 2)
                });
            }
        }
    }

    public void GenerateSquare(CableProjection projection)
    {
        double centerX = 50.0;
        double centerY = 50.0;
        double size = 60.0;
        double halfSize = size / 2;

        int pointsPerSide = 90;

        for (int i = 0; i < pointsPerSide; i++)
        {
            double t = (double)i / pointsPerSide;
            projection.Points.Add(new CablePoint
            {
                X = Math.Round(centerX - halfSize + size * t, 2),
                Y = Math.Round(centerY + halfSize, 2)
            });
        }

        for (int i = 0; i < pointsPerSide; i++)
        {
            double t = (double)i / pointsPerSide;
            projection.Points.Add(new CablePoint
            {
                X = Math.Round(centerX + halfSize, 2),
                Y = Math.Round(centerY + halfSize - size * t, 2)
            });
        }

        for (int i = 0; i < pointsPerSide; i++)
        {
            double t = (double)i / pointsPerSide;
            projection.Points.Add(new CablePoint
            {
                X = Math.Round(centerX + halfSize - size * t, 2),
                Y = Math.Round(centerY - halfSize, 2)
            });
        }

        for (int i = 0; i < pointsPerSide; i++)
        {
            double t = (double)i / pointsPerSide;
            projection.Points.Add(new CablePoint
            {
                X = Math.Round(centerX - halfSize, 2),
                Y = Math.Round(centerY - halfSize + size * t, 2)
            });
        }
    }

    public void GenerateStar(CableProjection projection)
    {
        double centerX = 50.0;
        double centerY = 50.0;
        double outerRadius = 35.0;
        double innerRadius = 15.0;
        int numPoints = 5;

        for (int i = 0; i < 360; i++)
        {
            double angle = i * Math.PI / 180.0;
            double segmentAngle = (2 * Math.PI) / (numPoints * 2);
            int segment = (int)(angle / segmentAngle);
            bool isOuter = segment % 2 == 0;

            double radius = isOuter ? outerRadius : innerRadius;
            double x = centerX + radius * Math.Cos(angle);
            double y = centerY + radius * Math.Sin(angle);

            projection.Points.Add(new CablePoint
            {
                X = Math.Round(x, 2),
                Y = Math.Round(y, 2)
            });
        }
    }

    public void GenerateSpiral(CableProjection projection)
    {
        double centerX = 50.0;
        double centerY = 50.0;
        double maxRadius = 35.0;

        for (int i = 0; i < 360; i++)
        {
            double angle = i * Math.PI / 180.0 * 3;
            double radius = maxRadius * (i / 360.0);
            double x = centerX + radius * Math.Cos(angle);
            double y = centerY + radius * Math.Sin(angle);

            projection.Points.Add(new CablePoint
            {
                X = Math.Round(x, 2),
                Y = Math.Round(y, 2)
            });
        }
    }
}
