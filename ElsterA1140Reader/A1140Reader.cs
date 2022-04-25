using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ElsterA1140Reader
{
    public class A1140Reader
    {
        private readonly SerialPort _serialPort;
        private readonly int _id;
        private readonly string _password;
        private readonly int _waitTimeOut;
        private bool dataReceived = false;
        private readonly ILogger? _logger;

        public A1140Reader(SerialPort serialPort, int id, string password = "00000000", int waitTimeOut = 5000,
            ILoggerFactory? loggerFactory = null)
        {
            _serialPort = serialPort;
            _id = id;
            _password = password;
            _waitTimeOut = waitTimeOut;
            _serialPort.DataReceived += _serialPort_DataReceived;

            if (loggerFactory != null)
            {
                _logger = loggerFactory.CreateLogger<A1140Reader>();
            }
        }

        private void _serialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            dataReceived = true;
        }

        byte[] GetBytes()
        {
            byte[] data = new byte[_serialPort.BytesToRead];
            _serialPort.Read(data, 0, _serialPort.BytesToRead);
            return data;
        }

        bool SendAndGet(byte[] cmd, out byte[]? resp)
        {
            _logger?.LogInformation("Send: " + Encoding.Default.GetString(cmd));

            dataReceived = false;
            _serialPort.Write(cmd, 0, cmd.Length);

            var dt = DateTime.Now;
            while (!dataReceived)
            {
                if (DateTime.Now - dt > TimeSpan.FromMilliseconds(_waitTimeOut)) break;
            }

            if (!dataReceived)
            {
                _logger?.LogWarning("No responce!");
                resp = null;
                return false;
            };

            resp = GetBytes();

            _logger?.LogInformation("Responce: " + Encoding.Default.GetString(resp));
            return true;
        }

        public bool OpenSession()
        {
            _logger?.LogInformation($"{_id:D3}: Begin request ...");

            if (!_serialPort.IsOpen)
            {
                _logger?.LogError($"{_id:D3}: {_serialPort.PortName} is not opened!");
                return false;
            }

            _logger?.LogInformation($"{_id:D3}: Opening session...");

            _serialPort.Write(Utils.WAKE_UP, 0, Utils.WAKE_UP.Length);
            Thread.Sleep(500);
            var cmd = $"/?{_id:D3}\r\n";
            byte[]? resv;
            var hasData = SendAndGet(Encoding.Default.GetBytes(cmd), out resv);
            if (!hasData) return false;
            cmd = (char)0x06 + "056\r\n";
            resv = null;
            hasData = SendAndGet(Encoding.Default.GetBytes(cmd), out resv);
            if (hasData && resv != null)
                _logger?.LogInformation($"CRC is: {resv[^1]}, Calculated: {Utils.CalcBcc(resv[1..])}");

            return true;
        }
    }
}
