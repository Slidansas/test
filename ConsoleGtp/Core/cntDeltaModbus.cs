using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleGtp.Core
{
    /// <summary>
    /// Адресация памяти контролеллера по модбасу
    /// </summary>
    public static class СntDeltaModbus
    {
        //ENCODE KOEFF
        public static float speedSmK = 0.6f;
        public static float speedMmK = 0.06f;

        /// <summary>
        /// Адрес счетчика для проверки состояния
        /// </summary>
        public static int modbusAdrHoldingCount = 100;

        /// <summary>
        /// Параметр счетчика для проверки состояния
        /// </summary>
        public static int modbusCountHoldingCount = 1;
        /// <summary>
        ///  Адрес параметра статуса работы системы
        /// </summary>
        public static int modbusAdrSystemStatus = 110;
        /// <summary>
        ///  Параметра работы системы Система в работе (0 - Система остановлена, 1 - Система в работе, 2 - Система в ином состоянии, например наладка)
        /// </summary>
        public static int modbusSystemStatusCount = 1;

        /// <summary>
        ///  Адрес для включения красного света на световой колонне
        /// </summary>
        public static int modbusAdrLightColumnRedLight = 150;
        /// <summary>
        ///  Параметр работы cветовой колонны, жёлтый цвет (0 - Откл, 1 - Горит, 2 - Мигает)
        /// </summary>
        public static int modbusLightColumnRedLightCount = 1;

        /// <summary>
        /// Адрес для включения жёлтого света на световой колонне
        /// </summary>
        public static int modbusAdrLightColumnYellowLight = 151;
        /// <summary>
        /// Параметр работы cветовой колонны, жёлтый цвет (0 - Откл, 1 - Горит, 2 - Мигает)
        /// </summary>
        public static int modbusLightColumnYellowLightCount = 1;

        /// <summary>
        /// Адрес для включения зелёного света на световой колонне
        /// </summary>
        public static int modbusAdrLightColumnGreenLight = 152;
        /// <summary>
        /// Параметр работы cветовой колонны, зелёный цвет (0 - Откл, 1 - Горит, 2 - Мигает)
        /// </summary>
        public static int modbusLightColumnGreenLightCount = 1;

        /// <summary>
        /// Адрес для отключения звуковой сигнализации на некоторое время
        /// </summary>
        public static int modbusAdrMuteWarningSignal = 153;
        /// <summary>
        /// Параметр для отключения звуковой сигнализации на некотрое время (Импульс)
        /// </summary>
        public static int modbusMuteWarningSignalCount = 1;

        /// <summary>
        /// Адрес для сброса длины 
        /// </summary>
        public static int modbusAdrResetLength = 154;
        /// <summary>
        /// Параметр для сброса длины 
        /// </summary>
        public static int modbusResetLengthCount = 1;

        /// <summary>
        /// Адрес для подачи сигнала Тревоги
        /// </summary>
        public static int modbusAdrAlarmSignal = 160;
        /// <summary>
        /// Параметр для подачи сигнала Тревоги (0 - Нет, 1 - Предупреждение, 2 - Дефект)
        /// </summary>
        public static int modbusAlarmSignalCount = 1;

        /// <summary>
        /// Адрес для сброса Тревоги
        /// </summary>
        public static int modbusAdrConfirmAlarm = 161;
        /// <summary>
        /// Параметр для сброса тревоги (имульс)
        /// </summary>
        public static int modbusConfirmAlarmCount = 1;
    }
}
