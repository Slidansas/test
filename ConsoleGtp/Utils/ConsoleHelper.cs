using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleGtp.Utils
{
    public static class ConsoleHelper
    {
        public static void WriteHeader(string title, string subtitle = "")
        {
            Console.Clear();
            DrawSeparator('═', ConsoleColor.Cyan);
            WriteColored($" {title} ", ConsoleColor.Yellow);
            if (!string.IsNullOrEmpty(subtitle))
            {
                WriteColored($" [{subtitle}]", ConsoleColor.Gray);
            }
            Console.WriteLine();
            DrawSeparator('═', ConsoleColor.Cyan);
        }

        public static void WriteSuccess(string message)
        {
            WriteColored($"✓ {message}", ConsoleColor.Green);
        }

        public static void WriteError(string message)
        {
            WriteColored($"✗ {message}", ConsoleColor.Red);
        }

        public static void WriteInfo(string message)
        {
            WriteColored($"ℹ {message}", ConsoleColor.Blue);
        }

        public static void WriteWarning(string message)
        {
            WriteColored($"⚠ {message}", ConsoleColor.Yellow);
        }

        public static void WriteColored(string message, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        public static void DrawSeparator(char c = '─', ConsoleColor? color = null)
        {
            if (color.HasValue)
                Console.ForegroundColor = color.Value;

            Console.WriteLine(new string(c, Console.WindowWidth - 1));

            if (color.HasValue)
                Console.ResetColor();
        }

        public static string GetInput(string prompt, string defaultValue = "")
        {
            Console.Write($"{prompt} [{defaultValue}]: ");
            string? input = Console.ReadLine();
            return string.IsNullOrWhiteSpace(input) ? defaultValue : input;
        }

        public static string GetChoice()
        {
            return Console.ReadKey(true).KeyChar.ToString();
        }

        public static void WaitForKeyPress(string message = "Нажмите любую клавишу для продолжения...")
        {
            Console.WriteLine();
            WriteInfo(message);
            Console.ReadKey(true);
        }
    }
}
