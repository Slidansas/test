using ConsoleGtp.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleGtp.UI.Menu
{
    public class MenuManager
    {
        private readonly List<MenuItem> _items = new();
        private readonly string _title;

        public MenuManager(string title)
        {
            _title = title;
        }

        public void AddItem(MenuItem item)
        {
            _items.Add(item);
        }

        public void Show()
        {
            Console.WriteLine();
            ConsoleHelper.DrawSeparator();
            ConsoleHelper.WriteColored(_title, ConsoleColor.Yellow);
            ConsoleHelper.DrawSeparator();

            foreach (var item in _items)
            {
                Console.WriteLine($"{item.Key}. {item.Description}");
            }

            ConsoleHelper.DrawSeparator();
            Console.Write("Выберите опцию: ");
        }

        public async Task ExecuteAsync(string choice)
        {
            var item = _items.FirstOrDefault(i => i.Key == choice);
            if (item != null)
            {
                await item.ExecuteAsync();
            }
            else
            {
                ConsoleHelper.WriteError("Неверный выбор. Попробуйте снова.");
            }
        }
    }

    
}
