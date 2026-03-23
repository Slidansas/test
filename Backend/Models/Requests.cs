using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AROKIS.Backend.Models;

public record ValueRequest(double Value);
public record AllValuesRequest(double Value1, double Value2, double Value3, double Value4);

public class CalculationResult
{
    public double Sum { get; set; }
    public double Product { get; set; }
    public double SumTimesProduct { get; set; }
    public double Average { get; set; }
    public string Message { get; set; } = "";
}
