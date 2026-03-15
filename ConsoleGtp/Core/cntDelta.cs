using ConsoleGtp.Core;
using EasyModbus;
using System;
using System.Collections;
using System.Threading;

namespace ConsoleGtp.Core
{
    public class cntDelta
    {
        public ModbusClient modbusClient = new ModbusClient();
        public ModbusServer server = new ModbusServer();
        string _ip = "";
        int _port = 0;
        int _activeAdr = 0;

        //data
        int _counter = 0;

        cntDeltaData oldModbus;

        //SELECT
        public delegate void pressSelect(string message);
        public event pressSelect? NotifyPressSelect;

        public delegate void KeySelectOn(string message);
        public event KeySelectOn? NotifyKeySelectOn;
        public delegate void KeySelectOff(string message);
        public event KeySelectOff? NotifyKeySelectOff;
        //CANCEL
        public delegate void pressCancel(string message);
        public event pressCancel? NotifyPressCancel;

        public delegate void KeyCancelOn(string message);
        public event KeyCancelOn? NotifyKeyCancelOn;
        public delegate void KeyCancelOff(string message);
        public event KeyCancelOff? NotifyKeyCancelOff;
        //ONOFF
        public delegate void KeyOnOffOn(string message);
        public event KeyOnOffOn? NotifyKeyOnOffOn;
        public delegate void KeyOnOffOff(string message);
        public event KeyOnOffOff? NotifyKeyOnOffOff;
        //KK
        public delegate void KeyKKOpen(string message);
        public event KeyKKOpen? NotifyKeyKKOpen;
        public delegate void KeyKKClose(string message);
        public event KeyKKClose? NotifyKeyKKClose;

        public cntDelta(string ip, int port)
        {
            _ip = ip;
            _port = port;

            oldModbus = new cntDeltaData();
            oldModbus.Counter = -1;
        }

        /// <summary>
        /// Инициализация контролера
        /// </summary>
        public void open()
        {
            string metod = System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "open";
            try
            {
                modbusClient = new ModbusClient();
                modbusClient.ReceiveDataChanged += ModbusClient_ReceiveDataChanged;
                modbusClient.Connect(_ip, _port);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в {metod}: {ex.Message}");
            }
        }

        /// <summary>
        /// Сервер - изменение данных в контролере
        /// </summary>
        /// <param name="sender"></param>
        private void ModbusClient_ReceiveDataChanged(object sender)
        {
            string metod = System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "ModbusClient_ReceiveDataChanged";
            try
            {
                ModbusClient modbusClient = (ModbusClient)sender;
                byte[] b = modbusClient.receiveData;
                if (_activeAdr == 100 || _activeAdr == 101)
                {
                    int[] array4 = new int[1];
                    for (int i = 0; i < 1; i++)
                    {
                        byte b2 = b[9 + i * 2];
                        byte b3 = b[9 + i * 2 + 1];
                        b[9 + i * 2] = b3;
                        b[9 + i * 2 + 1] = b2;
                        array4[i] = BitConverter.ToInt16(b, 9 + i * 2);
                    }

                    if (_activeAdr == 100) _counter = array4[0];
                    if (_activeAdr == 101)
                    {
                        bool[] bits = intBits(array4[0]);
                        bool butONOFF = bits[0];
                        bool butSelect = bits[1];
                        bool butCancel = bits[2];
                        bool butKK = bits[3];
                        cntDeltaData newModbus = new cntDeltaData { Counter = _counter, bitsButton = bits };

                        if (oldModbus.Counter != -1) ButtonCompareModbus(newModbus, oldModbus);
                        oldModbus = new cntDeltaData { Counter = _counter, bitsButton = bits };
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в {metod}: {ex.Message}");
            }
        }

        /// <summary>
        /// Генерация событий
        /// </summary>
        /// <param name="newData"></param>
        /// <param name="oldData"></param>
        private void ButtonCompareModbus(cntDeltaData newData, cntDeltaData oldData)
        {
            //ONOFF
            if (oldData.bitsButton?[0] == false && newData.bitsButton?[0] == true)
            {
                NotifyKeyOnOffOn?.Invoke("");
            }
            if (oldData.bitsButton?[0] == true && newData.bitsButton?[0] == false)
            {
                NotifyKeyOnOffOff?.Invoke("");
            }

            //SELECT
            if (oldData.bitsButton?[1] == false && newData.bitsButton?[1] == true)
            {
                NotifyKeySelectOn?.Invoke("");
                NotifyPressSelect?.Invoke("");
            }
            if (oldData.bitsButton?[1] == true && newData.bitsButton?[1] == false)
            {
                NotifyKeySelectOff?.Invoke("");
            }

            //CANCEL
            if (oldData.bitsButton?[2] == false && newData.bitsButton?[2] == true)
            {
                NotifyKeyCancelOn?.Invoke("");
                NotifyPressCancel?.Invoke("");
            }
            if (oldData.bitsButton?[2] == true && newData.bitsButton?[2] == false)
            {
                NotifyKeyCancelOff?.Invoke("");
            }
            //KK
            if (oldData.bitsButton?[3] == false && newData.bitsButton?[3] == true)
            {
                NotifyKeyKKOpen?.Invoke("");
            }
            if (oldData.bitsButton?[3] == true && newData.bitsButton?[3] == false)
            {
                NotifyKeyKKClose?.Invoke("");
            }
        }

        /// <summary>
        /// Отключение от контроллера
        /// </summary>
        public void close()
        {
            try
            {
                modbusClient.Disconnect();
            }
            catch { }
            ;
        }

        /// <summary>
        /// Метод для чтения данных из контроллера.
        /// Запись полученных данных идет в 
        /// <c><see cref="cntDeltaData"/></c>.
        /// Для добавления переменной для считывания необходимо указать адрес, значение переменной
        /// После чего считать полученный ответ. Пример:
        /// <code>
        /// // Адрес переменной в памяти контроллера
        /// _activeAdr = СntDeltaModbus.modbusAdrHoldingCount;
        /// 
        /// // Получаемый ответ
        /// int[] Response1 = modbusClient.ReadHoldingRegisters(_activeAdr, СntDeltaModbus.modbusCountHoldingCount);
        /// 
        /// // Запись ответа
        /// DeltaModbusData.Counter = Response1[0];
        /// </code>
        /// </summary>
        /// <returns>Описание возвращаемого значения</returns>
        public void read(cntDeltaData DeltaModbusData)
        {
            string metod = System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "read";

            try
            {
                int[] Response;

                //Адрес переменной в памяти контроллера
                _activeAdr = СntDeltaModbus.modbusAdrHoldingCount;
                //Получаемый ответ
                int[] Response1 = modbusClient.ReadHoldingRegisters(_activeAdr, СntDeltaModbus.modbusCountHoldingCount);
                //Запись ответа
                DeltaModbusData.Counter = Response1[0];

                _activeAdr = 44;
                int[] Response2 = modbusClient.ReadHoldingRegisters(_activeAdr, СntDeltaModbus.modbusSystemStatusCount);
                DeltaModbusData.Reset_Alarm_Success = Response2[0];

                _activeAdr = 260;
                int[] Response4 = modbusClient.ReadHoldingRegisters(_activeAdr, 1);
                DeltaModbusData.CoilNumbers = Response4[0];

                _activeAdr = 250;
                int[] Response3 = modbusClient.ReadHoldingRegisters(_activeAdr, 1);
                DeltaModbusData.Meters = Response3[0];
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в {metod}: {ex.Message}");
            }
            ;
        }

        private bool[] loadLasers(int modbusAdrHoldingLaser)
        {
            string metod = System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "loadLasers";
            bool[] bits = { false, false, false, false, false, false, false, false };
            try
            {
                _activeAdr = modbusAdrHoldingLaser;
                int[] l1 = modbusClient.ReadHoldingRegisters(modbusAdrHoldingLaser, 1);
                Thread.Sleep(5);
                int[] l2 = modbusClient.ReadHoldingRegisters(modbusAdrHoldingLaser + 1, 1);
                Thread.Sleep(5);

                bits[0] = intBits(l1[0])[0];
                bits[1] = intBits(l2[0])[0];
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в {metod}: {ex.Message}");
            }
            return bits;
        }

        #region Запись в контроллер
        /// <summary>
        /// Запись байта
        /// </summary>
        /// <param name="Adr"></param>
        /// <param name="Value"></param>
        public void writeInt(int Adr, int Value, string owner)
        {
            string metod = System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "writeInt";
            try
            {
                _activeAdr = Adr;
                modbusClient.WriteSingleRegister(Adr, Value);
            }
            catch
            {
                try
                {
                    Thread.Sleep(5);
                    _activeAdr = Adr;
                    modbusClient.WriteSingleRegister(Adr, Value);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка в {metod}: {ex.Message}");
                }
            }
        }
        /// <summary>
        /// /// Запись байтов
        /// </summary>
        /// <param name="Adr"></param>
        /// <param name="Count"></param>
        /// <param name="Value"></param>
        public void writeAllInt(int Adr, int Count, int Value)
        {
            string metod = System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "writeAllInt";
            try
            {
                _activeAdr = Adr;
                for (int i = 0; i < Count; i++)
                {
                    modbusClient.WriteSingleRegister(Adr + i, Value);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в {metod}: {ex.Message}");
            }
        }
        #endregion

        #region Преобразования
        /// <summary>
        /// Преобразование Int to Bit
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        public bool[] intBits(int i)
        {
            BitArray ba = new BitArray(new int[] { i });
            bool[] bits = new bool[ba.Count];
            ba.CopyTo(bits, 0);
            return bits;
        }
        #endregion

        // Добавленные свойства для доступа
        public string IPAddress => _ip;
        public int Port => _port;
    }
}