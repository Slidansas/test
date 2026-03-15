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
    public class CoordinateTest
    {
        private readonly DeltaControllerWrapper _controller;

        public CoordinateTest(DeltaControllerWrapper controller)
        {
            _controller = controller;
        }

        public void Run()
        {
            Console.Clear();
            ConsoleHelper.WriteHeader("ТЕСТ С КООРДИНАТАМИ");
            ConsoleHelper.WriteInfo("Имитация движения с преобразованием координат");

            try
            {
                _controller.ReadData();
                int startMeters = _controller.Data.Meters;

                Console.WriteLine($"Начальная длина: {startMeters / 1000.0:F3} м ({startMeters} имп)");
                Console.WriteLine($"Коэффициенты: {СntDeltaModbus.speedMmK:F3} мм/имп, {СntDeltaModbus.speedSmK:F3} см/имп");
                Console.WriteLine("\nВведите координаты (X,Y) или 'q' для выхода:");

                while (true)
                {
                    Console.Write("\nКоордината (X,Y): ");
                    string? input = Console.ReadLine();

                    if (input?.ToLower() == "q")
                        break;

                    if (TryParseCoordinate(input, out double x, out double y))
                    {
                        ProcessCoordinate(x, y, startMeters);
                    }
                    else
                    {
                        ConsoleHelper.WriteError("Неверный формат. Используйте X,Y (например: 10.5,20.3)");
                    }
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteError($"Ошибка: {ex.Message}");
            }
        }

        private void ProcessCoordinate(double x, double y, int startMeters)
        {
            _controller.ReadData();
            int deltaMeters = _controller.Data.Meters - startMeters;
            double deltaMetersDouble = deltaMeters / 1000.0;

            Console.WriteLine("\n--- РЕЗУЛЬТАТЫ ---");
            Console.WriteLine($"Текущая позиция: X = {x:F3}, Y = {y:F3}");
            Console.WriteLine($"Пройдено метров: {deltaMetersDouble:F3} м");
            Console.WriteLine($"Импульсов энкодера: {deltaMeters}");

            Console.WriteLine("\n--- ПРЕОБРАЗОВАНИЯ ---");
            Console.WriteLine($"В миллиметрах: {deltaMetersDouble * 1000:F1} мм");
            Console.WriteLine($"В сантиметрах: {deltaMetersDouble * 100:F1} см");

            Console.WriteLine($"По коэф. мм: {deltaMeters * СntDeltaModbus.speedMmK:F1} мм");
            Console.WriteLine($"По коэф. см: {deltaMeters * СntDeltaModbus.speedSmK:F1} см");

            double distance = Math.Sqrt(x * x + y * y);
            Console.WriteLine($"\nРасстояние до точки: {distance:F3} м");
            if (СntDeltaModbus.speedMmK > 0)
            {
                Console.WriteLine($"Требуется импульсов: {distance * 1000 / СntDeltaModbus.speedMmK:F0}");
            }
        }

        private bool TryParseCoordinate(string? input, out double x, out double y)
        {
            x = 0;
            y = 0;

            if (string.IsNullOrWhiteSpace(input))
                return false;

            var parts = input.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
                return false;

            return double.TryParse(parts[0].Trim(), out x) &&
                   double.TryParse(parts[1].Trim(), out y);
        }
    }
}
