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
    public class CounterTest
    {
        private readonly DeltaControllerWrapper _controller;

        public CounterTest(DeltaControllerWrapper controller)
        {
            _controller = controller;
        }

        public void Run()
        {
            var running = true;

            while (running)
            {
                Console.Clear();
                ConsoleHelper.WriteHeader("УПРАВЛЕНИЕ СЧЕТЧИКОМ");

                try
                {
                    _controller.ReadData();
                    Console.WriteLine($"Текущее значение счетчика: {_controller.Data.Counter}");

                    Console.WriteLine("\n1. Установить новое значение");
                    Console.WriteLine("2. Инкремент (+1)");
                    Console.WriteLine("3. Декремент (-1)");
                    Console.WriteLine("4. Сбросить (0)");
                    Console.WriteLine("0. Назад");
                    Console.Write("\nВыберите действие: ");

                    var key = Console.ReadKey(true);
                    Console.WriteLine();

                    int newValue = _controller.Data.Counter;

                    switch (key.KeyChar)
                    {
                        case '1':
                            Console.Write("Введите новое значение: ");
                            if (int.TryParse(Console.ReadLine(), out newValue))
                            {
                                _controller.WriteValue(СntDeltaModbus.modbusAdrHoldingCount, newValue);
                                ConsoleHelper.WriteSuccess($"Счетчик установлен на {newValue}");
                            }
                            break;
                        case '2':
                            newValue = _controller.Data.Counter + 1;
                            _controller.WriteValue(СntDeltaModbus.modbusAdrHoldingCount, newValue);
                            ConsoleHelper.WriteSuccess($"Счетчик увеличен до {newValue}");
                            break;
                        case '3':
                            newValue = _controller.Data.Counter - 1;
                            _controller.WriteValue(СntDeltaModbus.modbusAdrHoldingCount, newValue);
                            ConsoleHelper.WriteSuccess($"Счетчик уменьшен до {newValue}");
                            break;
                        case '4':
                            _controller.WriteValue(СntDeltaModbus.modbusAdrHoldingCount, 0);
                            ConsoleHelper.WriteSuccess("Счетчик сброшен");
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

                if (running)
                {
                    ConsoleHelper.WaitForKeyPress();
                }
            }
        }
    }
}
