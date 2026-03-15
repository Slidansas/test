using ConsoleGtp.Core.Services.EventHandlers;
using EasyModbus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleGtp.Core.Controllers
{
    public class DeltaControllerWrapper : IDisposable
    {
        private readonly cntDelta _controller;
        private readonly cntDeltaData _data;
        private readonly ButtonEventHandler _buttonEventHandler;
        private bool _isConnected;

        public cntDeltaData Data => _data;
        public bool IsConnected => _isConnected;
        public string IpAddress { get; }
        public int Port { get; }

        public event Action<string> OnButtonSelectPress;
        public event Action<string> OnButtonSelectOn;
        public event Action<string> OnButtonSelectOff;
        public event Action<string> OnButtonCancelPress;
        public event Action<string> OnButtonCancelOn;
        public event Action<string> OnButtonCancelOff;
        public event Action<string> OnButtonOnOffOn;
        public event Action<string> OnButtonOnOffOff;
        public event Action<string> OnButtonKKOpen;
        public event Action<string> OnButtonKKClose;

        public DeltaControllerWrapper(string ip, int port)
        {
            IpAddress = ip;
            Port = port;
            _controller = new cntDelta(ip, port);
            _data = new cntDeltaData();
            _buttonEventHandler = new ButtonEventHandler(this);
        }

        public async Task ConnectAsync()
        {
            await Task.Run(() =>
            {
                _controller.open();
                _isConnected = true;
            });
        }

        public void Disconnect()
        {
            if (_isConnected)
            {
                _controller.close();
                _isConnected = false;
            }
        }

        public void ReadData()
        {
            if (!_isConnected) throw new InvalidOperationException("Контроллер не подключен");
            _controller.read(_data);
        }

        public void WriteValue(int address, int value, string owner = "test")
        {
            if (!_isConnected) throw new InvalidOperationException("Контроллер не подключен");
            _controller.writeInt(address, value, owner);
        }

        public void WriteMultipleValues(int startAddress, int count, int value)
        {
            if (!_isConnected) throw new InvalidOperationException("Контроллер не подключен");
            _controller.writeAllInt(startAddress, count, value);
        }

        public int ReadHoldingRegister(int address)
        {
            if (!_isConnected) throw new InvalidOperationException("Контроллер не подключен");

            var tempClient = new ModbusClient();
            try
            {
                tempClient.Connect(IpAddress, Port);
                var result = tempClient.ReadHoldingRegisters(address, 1);
                return result[0];
            }
            finally
            {
                tempClient.Disconnect();
            }
        }

        public void Dispose()
        {
            Disconnect();
        }

        // Внутренние методы для событий
        internal void RaiseButtonSelectPress(string message) => OnButtonSelectPress?.Invoke(message);
        internal void RaiseButtonSelectOn(string message) => OnButtonSelectOn?.Invoke(message);
        internal void RaiseButtonSelectOff(string message) => OnButtonSelectOff?.Invoke(message);
        internal void RaiseButtonCancelPress(string message) => OnButtonCancelPress?.Invoke(message);
        internal void RaiseButtonCancelOn(string message) => OnButtonCancelOn?.Invoke(message);
        internal void RaiseButtonCancelOff(string message) => OnButtonCancelOff?.Invoke(message);
        internal void RaiseButtonOnOffOn(string message) => OnButtonOnOffOn?.Invoke(message);
        internal void RaiseButtonOnOffOff(string message) => OnButtonOnOffOff?.Invoke(message);
        internal void RaiseButtonKKOpen(string message) => OnButtonKKOpen?.Invoke(message);
        internal void RaiseButtonKKClose(string message) => OnButtonKKClose?.Invoke(message);
    }
}
