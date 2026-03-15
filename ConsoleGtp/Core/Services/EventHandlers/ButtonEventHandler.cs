using ConsoleGtp.Core.Controllers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleGtp.Core.Services.EventHandlers
{
    public class ButtonEventHandler
    {
        private readonly DeltaControllerWrapper _controller;

        public ButtonEventHandler(DeltaControllerWrapper controller)
        {
            _controller = controller;
            SubscribeToEvents();
        }

        private void SubscribeToEvents()
        {
            // Здесь нужно подключиться к событиям оригинального cntDelta
            // Это заглушка - в реальности нужно получить доступ к событиям из cntDelta
        }

        private void OnSelectPress(string message)
        {
            _controller.RaiseButtonSelectPress(message);
        }

        private void OnSelectOn(string message)
        {
            _controller.RaiseButtonSelectOn(message);
        }

        private void OnSelectOff(string message)
        {
            _controller.RaiseButtonSelectOff(message);
        }

        private void OnCancelPress(string message)
        {
            _controller.RaiseButtonCancelPress(message);
        }

        private void OnCancelOn(string message)
        {
            _controller.RaiseButtonCancelOn(message);
        }

        private void OnCancelOff(string message)
        {
            _controller.RaiseButtonCancelOff(message);
        }

        private void OnOnOffOn(string message)
        {
            _controller.RaiseButtonOnOffOn(message);
        }

        private void OnOnOffOff(string message)
        {
            _controller.RaiseButtonOnOffOff(message);
        }

        private void OnKKOpen(string message)
        {
            _controller.RaiseButtonKKOpen(message);
        }

        private void OnKKClose(string message)
        {
            _controller.RaiseButtonKKClose(message);
        }
    }
}
