using ConsoleGtp.Core.Controllers;
using ConsoleGtp.Tests;
using ConsoleGtp.UI.Menu;
using ConsoleGtp.Utils;

namespace ConsoleGtp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.Title = "Delta Controller Test v1.0";

            try
            {
                // Инициализация приложения
                var app = new Application();
                await app.RunAsync();
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteError($"Критическая ошибка: {ex.Message}");
                ConsoleHelper.WaitForKeyPress("Нажмите любую клавишу для выхода...");
            }
        }
    }

    public class Application
    {
        private DeltaControllerWrapper _controller;
        private MenuManager _menuManager;
        private bool _isRunning = true;

        public async Task RunAsync()
        {
            ConsoleHelper.WriteHeader("Тестовое приложение для контроллера Delta", "v1.0");

            // Настройка подключения
            var ip = ConsoleHelper.GetInput("Введите IP адрес контроллера", "192.168.1.100");
            var port = int.Parse(ConsoleHelper.GetInput("Введите порт", "502"));

            try
            {
                // Инициализация контроллера
                _controller = new DeltaControllerWrapper(ip, port);
                await _controller.ConnectAsync();

                // Инициализация меню
                InitializeMenu();

                // Главный цикл
                while (_isRunning)
                {
                    _menuManager.Show();
                    var choice = ConsoleHelper.GetChoice();
                    await _menuManager.ExecuteAsync(choice);
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteError($"Ошибка подключения: {ex.Message}");
            }
            finally
            {
                _controller?.Disconnect();
                ConsoleHelper.WriteSuccess("Приложение завершено");
            }
        }

        private void InitializeMenu()
        {
            _menuManager = new MenuManager("ГЛАВНОЕ МЕНЮ");

            _menuManager.AddItem(new MenuItem("1", "Мониторинг данных", async () =>
                await new MonitoringMode(_controller).RunAsync()));

            _menuManager.AddItem(new MenuItem("2", "Тест световой колонны", () =>
                new LightColumnTest(_controller).Run()));

            _menuManager.AddItem(new MenuItem("3", "Тест сигнализации", () =>
                new AlarmTest(_controller).Run()));

            _menuManager.AddItem(new MenuItem("4", "Управление счетчиком", () =>
                new CounterTest(_controller).Run()));

            _menuManager.AddItem(new MenuItem("5", "Тест с координатами", () =>
                new CoordinateTest(_controller).Run()));

            _menuManager.AddItem(new MenuItem("6", "Запись значений", () =>
                new WriteReadTest(_controller).Run()));

            _menuManager.AddItem(new MenuItem("7", "Чтение всех данных", () =>
                new ReadAllDataTest(_controller).Run()));

            _menuManager.AddItem(new MenuItem("8", "Сброс длины", () =>
                new ResetLengthTest(_controller).Run()));

            _menuManager.AddItem(new MenuItem("9", "Выход", () => { _isRunning = false; }));
        }
    }
}