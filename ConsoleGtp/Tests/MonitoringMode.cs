using ConsoleGtp.Core.Controllers;
using ConsoleGtp.UI.Display;
using ConsoleGtp.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleGtp.Tests
{
    public class MonitoringMode
    {
        private readonly DeltaControllerWrapper _controller;
        private bool _isRunning;

        public MonitoringMode(DeltaControllerWrapper controller)
        {
            _controller = controller;
        }

        public async Task RunAsync()
        {
            _isRunning = true;
            ConsoleHelper.WriteInfo("Режим мониторинга (нажмите ESC для выхода)");
            ConsoleHelper.WriteInfo("Чтение данных каждые 500 мс...");

            var cts = new CancellationTokenSource();

            var monitorTask = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        _controller.ReadData();
                        ConsoleDisplay.ShowControllerData(_controller.Data);
                    }
                    catch (Exception ex)
                    {
                        ConsoleHelper.WriteError($"Ошибка чтения: {ex.Message}");
                    }

                    await Task.Delay(500);
                }
            }, cts.Token);

            while (_isRunning)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.Escape)
                    {
                        cts.Cancel();
                        _isRunning = false;
                    }
                }
                await Task.Delay(100);
            }

            await monitorTask;
        }
    }
}
