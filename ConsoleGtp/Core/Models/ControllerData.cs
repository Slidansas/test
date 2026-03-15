using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleGtp.Core.Models
{
    public class ControllerData
    {
        public int Counter { get; set; }
        public int SystemStatus { get; set; }
        public int MuteWarningSignal { get; set; }
        public bool[] ButtonBits { get; set; }
        public int ResetAlarmSuccess { get; set; }
        public int Meters { get; set; }
        public int CoilNumbers { get; set; }

        // Вычисляемые свойства
        public double MetersInMillimeters => Meters / 1000.0;
        public double MetersInCentimeters => Meters / 100.0;
        public string SystemStatusText => GetSystemStatusText();
        public bool IsOnOffPressed => ButtonBits?[0] ?? false;
        public bool IsSelectPressed => ButtonBits?[1] ?? false;
        public bool IsCancelPressed => ButtonBits?[2] ?? false;
        public bool IsKKOpen => ButtonBits?[3] ?? false;

        private string GetSystemStatusText()
        {
            return ResetAlarmSuccess switch
            {
                0 => "Система остановлена",
                1 => "Система в работе",
                2 => "Режим наладки",
                _ => "Неизвестный статус"
            };
        }

        public static ControllerData FromCntDeltaData(cntDeltaData data)
        {
            return new ControllerData
            {
                Counter = data.Counter,
                SystemStatus = data.SystemStatus,
                MuteWarningSignal = data.MuteWarningSignal,
                ButtonBits = data.bitsButton,
                ResetAlarmSuccess = data.Reset_Alarm_Success,
                Meters = data.Meters,
                CoilNumbers = data.CoilNumbers
            };
        }
    }

    public class LightColumnState
    {
        public int RedLight { get; set; }
        public int YellowLight { get; set; }
        public int GreenLight { get; set; }

        public string GetRedLightText() => GetLightStateText(RedLight);
        public string GetYellowLightText() => GetLightStateText(YellowLight);
        public string GetGreenLightText() => GetLightStateText(GreenLight);

        private string GetLightStateText(int state)
        {
            return state switch
            {
                0 => "Выключен",
                1 => "Горит",
                2 => "Мигает",
                _ => "Неизвестно"
            };
        }
    }
}
