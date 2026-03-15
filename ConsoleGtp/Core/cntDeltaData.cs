using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleGtp.Core
{
    public class cntDeltaData
    {
        //активный счетчик контроллера
        public int Counter { get; set; }
        public int SystemStatus { get; set; }
        public int MuteWarningSignal { get; set; }
        public bool[]? bitsButton { get; set; }
        public int Reset_Alarm_Success { get; set; }
        public int Meters { get; set; }
        public int CoilNumbers { get; set; }
    }
}
