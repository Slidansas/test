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
    public class AlarmTest
    {
        private readonly DeltaControllerWrapper _controller;

        public AlarmTest(DeltaControllerWrapper controller)
        {
            _controller = controller;
        }

        public void Run()
        {
            var running = true;

            while (running)
            {
                Console.Clear();
                ConsoleHelper.WriteHeader("ТЕСТ СИГНАЛИЗАЦИИ");

                Console.WriteLine("1. Предупреждение");
                Console.WriteLine("2. Дефект");
                Console.WriteLine("3. Сброс тревоги");
                Console.WriteLine("4. Отключить звук (Mute)");
                Console.WriteLine("0. Назад");
                Console.Write("\nВыберите режим: ");

                var key = Console.ReadKey(true);
                Console.WriteLine();

                try
                {
                    switch (key.KeyChar)
                    {
                        case '1':
                            _controller.WriteValue(СntDeltaModbus.modbusAdrAlarmSignal, 1);
                            ConsoleHelper.WriteSuccess("Сигнал 'Предупреждение' активирован");
                            break;
                        case '2':
                            _controller.WriteValue(СntDeltaModbus.modbusAdrAlarmSignal, 2);
                            ConsoleHelper.WriteSuccess("Сигнал 'Дефект' активирован");
                            break;
                        case '3':
                            _controller.WriteValue(СntDeltaModbus.modbusAdrConfirmAlarm, 1);
                            Thread.Sleep(100);
                            _controller.WriteValue(СntDeltaModbus.modbusAdrConfirmAlarm, 0);
                            ConsoleHelper.WriteSuccess("Сброс тревоги выполнен");
                            break;
                        case '4':
                            _controller.WriteValue(СntDeltaModbus.modbusAdrMuteWarningSignal, 1);
                            Thread.Sleep(100);
                            _controller.WriteValue(СntDeltaModbus.modbusAdrMuteWarningSignal, 0);
                            ConsoleHelper.WriteSuccess("Звук отключен");
                            break;
                        case '0':
                            running = false;
                            break;
                        default:
                            ConsoleHelper.WriteError("Неверный выбор");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    ConsoleHelper.WriteError($"Ошибка: {ex.Message}");
                }

                if (running && key.KeyChar != '0')
                {
                    ConsoleHelper.WaitForKeyPress();
                }
            }
        }
    }
}
