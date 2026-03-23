using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AROKIS.Backend.Models;

public class CableProjection
{
    public string Name { get; set; } = "Cable";
    public DateTime LastUpdate { get; set; } = DateTime.Now;
    public List<CablePoint> Points { get; set; } = new();
}
