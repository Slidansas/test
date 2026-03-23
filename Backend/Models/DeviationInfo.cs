using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AROKIS.Backend.Models;

public class DeviationInfo
{
    public double DeltaX { get; set; }
    public double DeltaY { get; set; }
    public int StepsToReturn { get; set; }
}
