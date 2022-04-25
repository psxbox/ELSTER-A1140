using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

        public A1140Reader(SerialPort serialPort, int id, string password = "00000000", int waitTimeOut = 6000,
            ILoggerFactory? loggerFactory = null)
        {
            _serialPort = serialPort;
            _id = id;
            _password = password;
            _waitTimeOut = waitTimeOut;
            _serialPort.DataReceived += SerialPort_DataReceived;

            if (loggerFactory != null)
            {
                _logger = loggerFactory.CreateLogger<A1140Reader>();
            }
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            dataReceived = true;
        }

        byte[] GetBytes()
        {
            List<byte> data = new();

            while (_serialPort.BytesToRead > 0)
            {
                byte[] buf = new byte[_serialPort.BytesToRead];
                _serialPort.Read(buf, 0, _serialPort.BytesToRead);
                data.AddRange(buf);
                Thread.Sleep(200);
            }
            return data.ToArray();
        }

        bool SendAndGet(byte[] cmd, out byte[]? resp)
        {
            _logger?.LogInformation("Send: {cmd}", Encoding.Default.GetString(cmd));

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

            _logger?.LogInformation("Responce: {resp}", Encoding.Default.GetString(resp));
            return true;
        }

        public bool OpenSession()
        {
            _logger?.LogInformation("{id}: Begin request ...", _id);

            if (!_serialPort.IsOpen)
            {
                _logger?.LogError("{id}: {port} is not opened!", _id, _serialPort.PortName);
                return false;
            }

            _logger?.LogInformation("{id}: Opening session...", _id);

            _serialPort.Write(Utils.WAKE_UP, 0, Utils.WAKE_UP.Length);
            Thread.Sleep(500);
            var cmd = $"/?{_id:D3}!\r\n";
            var hasData = SendAndGet(Encoding.Default.GetBytes(cmd), out byte[]? resv);
            if (!hasData) return false;
            cmd = (char)0x06 + "056\r\n";
            resv = null;
            hasData = SendAndGet(Encoding.Default.GetBytes(cmd), out resv);
            if (hasData && resv != null)
            {
                var crc = Utils.CalcBcc(resv[1..^1]);
                _logger?.LogInformation("CRC is: {crc1}, Calculated: {crc2}", resv[^1], crc);
                var res = Encoding.Default.GetString(resv[1..^1]);
                if (res.IndexOf("P0") != -1 && resv[^1] == crc)
                {
                    var match = Regex.Match(res, @"\([^)]+\)").Value.Trim('(', ')');
                    _logger?.LogInformation("Password seed: {match}", match);
                }
                else
                {
                    return false;
                }
            }
            return true;
        }
    }
}
