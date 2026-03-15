using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleGtp.Core.Models
{
    public static class ModbusAddresses
    {
        // Счетчик и статус
        public const int Counter = 100;
        public const int SystemStatus = 44;
        public const int Meters = 250;
        public const int CoilNumbers = 260;

        // Световая колонна
        public const int LightColumnRed = 150;
        public const int LightColumnYellow = 151;
        public const int LightColumnGreen = 152;

        // Сигнализация
        public const int MuteWarning = 153;
        public const int ResetLength = 154;
        public const int AlarmSignal = 160;
        public const int ConfirmAlarm = 161;

        // Диапазоны адресов
        public static readonly int[] LightColumnAddresses =
            { LightColumnRed, LightColumnYellow, LightColumnGreen };

        public static readonly int[] AlarmAddresses =
            { AlarmSignal, ConfirmAlarm, MuteWarning };

        public static string GetAddressDescription(int address)
        {
            return address switch
            {
                Counter => "Счетчик",
                SystemStatus => "Статус системы",
                Meters => "Метры (импульсы)",
                CoilNumbers => "Номера катушек",
                LightColumnRed => "Красный свет",
                LightColumnYellow => "Желтый свет",
                LightColumnGreen => "Зеленый свет",
                MuteWarning => "Отключение звука",
                ResetLength => "Сброс длины",
                AlarmSignal => "Сигнал тревоги",
                ConfirmAlarm => "Подтверждение тревоги",
                _ => $"Адрес {address}"
            };
        }
    }

    public static class ModbusConstants
    {
        public const float SpeedSmK = 0.6f;
        public const float SpeedMmK = 0.06f;
        public const int ReadTimeoutMs = 1000;
        public const int WriteTimeoutMs = 1000;
        public const int DefaultPort = 502;
    }
}
