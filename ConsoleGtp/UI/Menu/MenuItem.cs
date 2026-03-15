using ConsoleGtp.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleGtp.UI.Menu
{
    public class MenuItem
    {
        public string Key { get; }
        public string Description { get; }
        private readonly Func<Task> _asyncAction;
        private readonly Action _syncAction;

        public MenuItem(string key, string description, Func<Task> action)
        {
            Key = key;
            Description = description;
            _asyncAction = action;
        }

        public MenuItem(string key, string description, Action action)
        {
            Key = key;
            Description = description;
            _syncAction = action;
        }

        public async Task ExecuteAsync()
        {
            try
            {
                if (_asyncAction != null)
                {
                    await _asyncAction();
                }
                else
                {
                    await Task.Run(_syncAction);
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteError($"Ошибка: {ex.Message}");
            }

            if (Key != "9") // Не для выхода
            {
                ConsoleHelper.WaitForKeyPress("Нажмите любую клавишу для продолжения...");
            }
        }
    }
}
