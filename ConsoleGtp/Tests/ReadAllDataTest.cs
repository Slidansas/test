using ConsoleGtp.Core;
using ConsoleGtp.Core.Controllers;
using ConsoleGtp.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleGtp.Tests
{
    public class ReadAllDataTest
    {
        private readonly DeltaControllerWrapper _controller;

        public ReadAllDataTest(DeltaControllerWrapper controller)
        {
            _controller = controller;
        }

        public void Run()
        {
            Console.Clear();
            ConsoleHelper.WriteHeader("ЧТЕНИЕ ВСЕХ ДАННЫХ");

            try
            {
                _controller.ReadData();

                Console.WriteLine($"Счетчик (100): {_controller.Data.Counter}");
                Console.WriteLine($"Статус системы (44): {_controller.Data.Reset_Alarm_Success} - {GetSystemStatus(_controller.Data.Reset_Alarm_Success)}");
                Console.WriteLine($"Метры (250): {_controller.Data.Meters}");
                Console.WriteLine($"Номера катушек (260): {_controller.Data.CoilNumbers}");

                Console.WriteLine("\n--- Состояние световой колонны ---");
                Console.WriteLine($"Красный свет (150): {GetLightState(СntDeltaModbus.modbusAdrLightColumnRedLight)}");
                Console.WriteLine($"Желтый свет (151): {GetLightState(СntDeltaModbus.modbusAdrLightColumnYellowLight)}");
                Console.WriteLine($"Зеленый свет (152): {GetLightState(СntDeltaModbus.modbusAdrLightColumnGreenLight)}");

                Console.WriteLine("\n--- Сигнализация ---");
                Console.WriteLine($"Сигнал тревоги (160): {GetAlarmState(СntDeltaModbus.modbusAdrAlarmSignal)}");
                Console.WriteLine($"Mute (153): {GetMuteState(СntDeltaModbus.modbusAdrMuteWarningSignal)}");

                if (_controller.Data.bitsButton != null)
                {
                    Console.WriteLine("\n--- Состояние кнопок ---");
                    Console.WriteLine($"ON/OFF: {(_controller.Data.bitsButton.Length > 0 ? (_controller.Data.bitsButton[0] ? "Нажата" : "Отжата") : "Н/Д")}");
                    Console.WriteLine($"SELECT: {(_controller.Data.bitsButton.Length > 1 ? (_controller.Data.bitsButton[1] ? "Нажата" : "Отжата") : "Н/Д")}");
                    Console.WriteLine($"CANCEL: {(_controller.Data.bitsButton.Length > 2 ? (_controller.Data.bitsButton[2] ? "Нажата" : "Отжата") : "Н/Д")}");
                    Console.WriteLine($"KK: {(_controller.Data.bitsButton.Length > 3 ? (_controller.Data.bitsButton[3] ? "Открыта" : "Закрыта") : "Н/Д")}");
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteError($"Ошибка: {ex.Message}");
            }
        }

        private string GetSystemStatus(int status)
        {
            return status switch
            {
                0 => "Система остановлена",
                1 => "Система в работе",
                2 => "Режим наладки",
                _ => "Неизвестный статус"
            };
        }

        private string GetLightState(int address)
        {
            int value = _controller.ReadHoldingRegister(address);
            return value switch
            {
                0 => "Выключен",
                1 => "Горит",
                2 => "Мигает",
                _ => $"Неизвестно ({value})"
            };
        }

        private string GetAlarmState(int address)
        {
            int value = _controller.ReadHoldingRegister(address);
            return value switch
            {
                0 => "Нет",
                1 => "Предупреждение",
                2 => "Дефект",
                _ => $"Неизвестно ({value})"
            };
        }

        private string GetMuteState(int address)
        {
            int value = _controller.ReadHoldingRegister(address);
            return value == 1 ? "Активен" : "Не активен";
        }
    }

    public class ResetLengthTest
    {
        private readonly DeltaControllerWrapper _controller;

        public ResetLengthTest(DeltaControllerWrapper controller)
        {
            _controller = controller;
        }

        public void Run()
        {
            Console.Clear();
            ConsoleHelper.WriteHeader("СБРОС ДЛИНЫ");

            try
            {
                _controller.ReadData();
                Console.WriteLine($"Текущая длина: {_controller.Data.Meters / 1000.0:F3} м ({_controller.Data.Meters} импульсов)");

                Console.Write("Подтвердите сброс (y/n): ");
                var key = Console.ReadKey(true);
                Console.WriteLine();

                if (key.KeyChar == 'y' || key.KeyChar == 'Y' || key.KeyChar == 'д' || key.KeyChar == 'Д')
                {
                    _controller.WriteValue(СntDeltaModbus.modbusAdrResetLength, 1);
                    Thread.Sleep(100);
                    _controller.WriteValue(СntDeltaModbus.modbusAdrResetLength, 0);

                    Thread.Sleep(200);
                    _controller.ReadData();
                    ConsoleHelper.WriteSuccess($"Длина сброшена. Новая длина: {_controller.Data.Meters / 1000.0:F3} м");
                }
                else
                {
                    ConsoleHelper.WriteInfo("Сброс отменен");
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteError($"Ошибка: {ex.Message}");
            }
        }
    }
}
