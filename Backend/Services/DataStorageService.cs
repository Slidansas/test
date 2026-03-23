using AROKIS.Backend.Models;

namespace AROKIS.Backend.Services;

public class DataStorageService
{
    private readonly object _lock = new();
    private double _value1;
    private double _value2;
    private double _value3;
    private double _value4;

    public double Value1 { get { lock (_lock) return _value1; } set { lock (_lock) _value1 = value; } }
    public double Value2 { get { lock (_lock) return _value2; } set { lock (_lock) _value2 = value; } }
    public double Value3 { get { lock (_lock) return _value3; } set { lock (_lock) _value3 = value; } }
    public double Value4 { get { lock (_lock) return _value4; } set { lock (_lock) _value4 = value; } }

    public double GetSum()
    {
        lock (_lock) return _value1 + _value2 + _value3 + _value4;
    }

    public double GetProduct()
    {
        lock (_lock) return _value1 * _value2 * _value3 * _value4;
    }

    public double GetAverage() => GetSum() / 4.0;

    public double[] GetAll()
    {
        lock (_lock) return new[] { _value1, _value2, _value3, _value4 };
    }

    public void SetAll(double value)
    {
        lock (_lock)
        {
            _value1 = value;
            _value2 = value;
            _value3 = value;
            _value4 = value;
        }
    }

    public void Reset() => SetAll(0.0);

    public CalculationResult CalculateExample()
    {
        lock (_lock)
        {
            double sum = _value1 + _value2 + _value3 + _value4;
            double product = _value1 * _value2 * _value3 * _value4;
            double sumTimesProduct = sum * product;
            double average = sum / 4.0;

            string message =
                $"Сумма: {sum:F2}, Произведение: {product:F2}, " +
                $"Сумма * Произведение: {sumTimesProduct:F2}, Среднее: {average:F2}";

            Console.WriteLine(message);

            return new CalculationResult
            {
                Sum = sum,
                Product = product,
                SumTimesProduct = sumTimesProduct,
                Average = average,
                Message = message
            };
        }
    }

    public double CalculateWeightedSum(double w1, double w2, double w3, double w4)
    {
        lock (_lock)
        {
            return (_value1 * w1) + (_value2 * w2) + (_value3 * w3) + (_value4 * w4);
        }
    }

    public double GetMax()
    {
        lock (_lock) return Math.Max(Math.Max(_value1, _value2), Math.Max(_value3, _value4));
    }

    public double GetMin()
    {
        lock (_lock) return Math.Min(Math.Min(_value1, _value2), Math.Min(_value3, _value4));
    }

    public bool AllValuesAbove(double threshold)
    {
        lock (_lock)
        {
            return _value1 > threshold && _value2 > threshold &&
                   _value3 > threshold && _value4 > threshold;
        }
    }
}
