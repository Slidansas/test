using ConsoleGtp.Core.Controllers;
using ConsoleGtp.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleGtp.Tests
{
    public class WriteReadTest
    {
        private readonly DeltaControllerWrapper _controller;

        public WriteReadTest(DeltaControllerWrapper controller)
        {
            _controller = controller;
        }

        public void Run()
        {
            Console.Clear();
            ConsoleHelper.WriteHeader("ЗАПИСЬ ЗНАЧЕНИЙ");

            try
            {
                Console.WriteLine("Доступные адреса для записи:");
                Console.WriteLine($"100 - Счетчик (текущее: {GetCurrentValue(100)})");
                Console.WriteLine($"150 - Красный свет (текущее: {GetCurrentValue(150)})");
                Console.WriteLine($"151 - Желтый свет (текущее: {GetCurrentValue(151)})");
                Console.WriteLine($"152 - Зеленый свет (текущее: {GetCurrentValue(152)})");
                Console.WriteLine($"160 - Сигнал тревоги (текущее: {GetCurrentValue(160)})");

                Console.Write("\nВведите адрес для записи: ");
                if (!int.TryParse(Console.ReadLine(), out int address))
                {
                    ConsoleHelper.WriteError("Неверный адрес");
                    return;
                }

                Console.Write("Введите значение (0-65535): ");
                if (!int.TryParse(Console.ReadLine(), out int value) || value < 0 || value > 65535)
                {
                    ConsoleHelper.WriteError("Неверное значение");
                    return;
                }

                _controller.WriteValue(address, value);
                ConsoleHelper.WriteSuccess($"Записано значение {value} по адресу {address}");

                Thread.Sleep(100);
                int newValue = GetCurrentValue(address);
                ConsoleHelper.WriteSuccess($"Подтверждение: прочитано значение {newValue}");
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteError($"Ошибка: {ex.Message}");
            }
        }

        private int GetCurrentValue(int address)
        {
            return _controller.ReadHoldingRegister(address);
        }
    }
}
