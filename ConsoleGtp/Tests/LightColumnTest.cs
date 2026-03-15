using ConsoleGtp.Core;
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
    public class LightColumnTest
    {
        private readonly DeltaControllerWrapper _controller;

        public LightColumnTest(DeltaControllerWrapper controller)
        {
            _controller = controller;
        }

        public void Run()
        {
            var running = true;

            while (running)
            {
                Console.Clear();
                ConsoleHelper.WriteHeader("ТЕСТ СВЕТОВОЙ КОЛОННЫ");

                var state = new LightColumnState
                {
                    RedLight = _controller.ReadHoldingRegister(СntDeltaModbus.modbusAdrLightColumnRedLight),
                    YellowLight = _controller.ReadHoldingRegister(СntDeltaModbus.modbusAdrLightColumnYellowLight),
                    GreenLight = _controller.ReadHoldingRegister(СntDeltaModbus.modbusAdrLightColumnGreenLight)
                };

                ConsoleDisplay.ShowLightColumnState(state);

                Console.WriteLine("\n--- УПРАВЛЕНИЕ ---");
                Console.WriteLine("1. Красный - Горит");
                Console.WriteLine("2. Красный - Мигает");
                Console.WriteLine("3. Желтый - Горит");
                Console.WriteLine("4. Желтый - Мигает");
                Console.WriteLine("5. Зеленый - Горит");
                Console.WriteLine("6. Зеленый - Мигает");
                Console.WriteLine("7. Выключить все");
                Console.WriteLine("0. Назад");
                Console.Write("\nВыберите режим: ");

                var key = Console.ReadKey(true);
                Console.WriteLine();

                try
                {
                    switch (key.KeyChar)
                    {
                        case '1':
                            _controller.WriteValue(СntDeltaModbus.modbusAdrLightColumnRedLight, 1);
                            ConsoleHelper.WriteSuccess("Красный свет - горит");
                            break;
                        case '2':
                            _controller.WriteValue(СntDeltaModbus.modbusAdrLightColumnRedLight, 2);
                            ConsoleHelper.WriteSuccess("Красный свет - мигает");
                            break;
                        case '3':
                            _controller.WriteValue(СntDeltaModbus.modbusAdrLightColumnYellowLight, 1);
                            ConsoleHelper.WriteSuccess("Желтый свет - горит");
                            break;
                        case '4':
                            _controller.WriteValue(СntDeltaModbus.modbusAdrLightColumnYellowLight, 2);
                            ConsoleHelper.WriteSuccess("Желтый свет - мигает");
                            break;
                        case '5':
                            _controller.WriteValue(СntDeltaModbus.modbusAdrLightColumnGreenLight, 1);
                            ConsoleHelper.WriteSuccess("Зеленый свет - горит");
                            break;
                        case '6':
                            _controller.WriteValue(СntDeltaModbus.modbusAdrLightColumnGreenLight, 2);
                            ConsoleHelper.WriteSuccess("Зеленый свет - мигает");
                            break;
                        case '7':
                            _controller.WriteMultipleValues(СntDeltaModbus.modbusAdrLightColumnRedLight, 3, 0);
                            ConsoleHelper.WriteSuccess("Все световые сигналы выключены");
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
                    Thread.Sleep(500);
                }
            }
        }
    }
}
