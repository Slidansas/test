using ConsoleGtp.Core;
using ConsoleGtp.Core.Models;
using ConsoleGtp.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleGtp.UI.Display
{
    public static class ConsoleDisplay
    {
        public static void ShowControllerData(cntDeltaData data)
        {
            Console.Clear();
            ConsoleHelper.WriteHeader("ДАННЫЕ КОНТРОЛЛЕРА", DateTime.Now.ToString("HH:mm:ss"));

            var table = new ConsoleTable(
                ("Параметр", 25),
                ("Значение", 15)
            );

            table.AddRow("Счетчик", data.Counter.ToString());
            table.AddRow("Статус системы", $"{data.Reset_Alarm_Success} - {GetSystemStatus(data.Reset_Alarm_Success)}");
            table.AddRow("Длина (метры)", $"{data.Meters / 1000.0:F3} м");
            table.AddRow("Импульсы", data.Meters.ToString());
            table.AddRow("Номера катушек", data.CoilNumbers.ToString());

            table.Print();

            ShowButtonStates(data);
        }

        private static string GetSystemStatus(int status)
        {
            return status switch
            {
                0 => "Система остановлена",
                1 => "Система в работе",
                2 => "Режим наладки",
                _ => "Неизвестный статус"
            };
        }

        public static void ShowButtonStates(cntDeltaData data)
        {
            Console.WriteLine("\n--- СОСТОЯНИЕ КНОПОК ---");
            var buttonTable = new ConsoleTable(
                ("Кнопка", 15),
                ("Состояние", 15)
            );

            if (data.bitsButton != null)
            {
                buttonTable.AddRow("ON/OFF", data.bitsButton.Length > 0 && data.bitsButton[0] ? "Нажата ✓" : "Отжата ✗");
                buttonTable.AddRow("SELECT", data.bitsButton.Length > 1 && data.bitsButton[1] ? "Нажата ✓" : "Отжата ✗");
                buttonTable.AddRow("CANCEL", data.bitsButton.Length > 2 && data.bitsButton[2] ? "Нажата ✓" : "Отжата ✗");
                buttonTable.AddRow("KK", data.bitsButton.Length > 3 && data.bitsButton[3] ? "Открыта ✓" : "Закрыта ✗");
            }
            else
            {
                buttonTable.AddRow("ON/OFF", "Н/Д");
                buttonTable.AddRow("SELECT", "Н/Д");
                buttonTable.AddRow("CANCEL", "Н/Д");
                buttonTable.AddRow("KK", "Н/Д");
            }

            buttonTable.Print();
        }

        public static void ShowLightColumnState(LightColumnState state)
        {
            Console.WriteLine("\n--- СВЕТОВАЯ КОЛОННА ---");
            var table = new ConsoleTable(
                ("Цвет", 15),
                ("Состояние", 15)
            );

            table.AddRow("Красный", GetLightStateText(state.RedLight));
            table.AddRow("Желтый", GetLightStateText(state.YellowLight));
            table.AddRow("Зеленый", GetLightStateText(state.GreenLight));

            table.Print();
        }

        private static string GetLightStateText(int state)
        {
            return state switch
            {
                0 => "Выключен",
                1 => "Горит",
                2 => "Мигает",
                _ => $"Неизвестно ({state})"
            };
        }
    }

    public class ConsoleTable
    {
        private readonly List<(string Header, int Width)> _columns;
        private readonly List<string[]> _rows = new();

        public ConsoleTable(params (string Header, int Width)[] columns)
        {
            _columns = columns.ToList();
        }

        public void AddRow(params string[] values)
        {
            _rows.Add(values);
        }

        public void Print()
        {
            // Заголовки
            foreach (var col in _columns)
            {
                Console.Write(col.Header.PadRight(col.Width));
            }
            Console.WriteLine();

            // Разделитель
            foreach (var col in _columns)
            {
                Console.Write(new string('─', col.Width - 1) + " ");
            }
            Console.WriteLine();

            // Данные
            foreach (var row in _rows)
            {
                for (int i = 0; i < _columns.Count && i < row.Length; i++)
                {
                    Console.Write(row[i].PadRight(_columns[i].Width));
                }
                Console.WriteLine();
            }
        }
    }

    public class LightColumnState
    {
        public int RedLight { get; set; }
        public int YellowLight { get; set; }
        public int GreenLight { get; set; }
    }
}
