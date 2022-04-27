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
        public bool SessionOpened { get; set; } = false;

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
            _logger?.LogInformation(">>>: {cmd}", Encoding.Default.GetString(cmd));

            dataReceived = false;
            _serialPort.Write(cmd, 0, cmd.Length);

            var dt = DateTime.Now;
            while (!dataReceived)
            {
                if (DateTime.Now - dt > TimeSpan.FromMilliseconds(_waitTimeOut)) break;
            }

            if (!dataReceived)
            {
                _logger?.LogWarning("Javob kelmadi!");
                resp = null;
                return false;
            };

            resp = GetBytes();

            _logger?.LogInformation("<<<: {resp}", Encoding.Default.GetString(resp));
            return true;
        }

        bool Authorize(string seed)
        {
            var pass = Utils.ElsterEncrypt(_password, seed).Replace("-", "");
            List<byte> buf = new()
            {
                Utils.SOH,
                (byte)'P',
                (byte)'2',
                Utils.STX
            };
            buf.AddRange(Encoding.ASCII.GetBytes('(' + pass + ')'));
            buf.Add(Utils.ETX);
            var crc = Utils.CalcBcc(buf.ToArray()[1..]);
            buf.Add(crc);

            return SendAndGet(buf.ToArray(), out byte[]? recv) && recv?[0] == 0x06;
        }

        public bool OpenSession()
        {
            _logger?.LogInformation("{id}: So'rov jo'natish ...", _id);

            if (!_serialPort.IsOpen)
            {
                _logger?.LogError("{id}: {port} port ochilmagan!", _id, _serialPort.PortName);
                return false;
            }

            _logger?.LogInformation("{id}: Sessiya chilmoqda...", _id);

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
                _logger?.LogInformation("Kelgan CRC: {crc1}, Hisoblangan: {crc2}", resv[^1], crc);
                var res = Encoding.Default.GetString(resv[1..^1]);
                if (res.IndexOf("P0") != -1 && resv[^1] == crc)
                {
                    var match = Regex.Match(res, @"\([^)]+\)").Value.Trim('(', ')');
                    _logger?.LogInformation("Password seed: {match}", match);
                    if (Authorize(match))
                    {
                        _logger?.LogInformation("Parol mos keldi, sessiya o'rnatildi");
                        SessionOpened = true;
                        return true;
                    }
                    else
                    {
                        _logger?.LogWarning("Password mos kelmadi, sessiya o'rnatilmadi!");
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            return false;
        }

        public Dictionary<string, double> ReadCurrent()
        {
            var cmd = Utils.GetCommand("RD", "507000", "00");
            SendAndGet(cmd, out byte[]? resv);
            Dictionary<string, double> result = new();



            if (resv is not null && resv.Length > 87)
            {
                _logger?.LogInformation("{cur}", BitConverter.ToString(resv));

                var crc = BitConverter.ToUInt16(resv.AsSpan(^2..));
                var calc_crc = NullFX.CRC.Crc16.ComputeChecksum(NullFX.CRC.Crc16Algorithm.Standard, resv[0..^2]);

                _logger?.LogInformation("CRC: {crc}, Calc: {calc}", crc, calc_crc);

                for (int i = 0; i < 10; i++)
                {
                    var val = string.Join("", BitConverter.ToString(resv, 4 + i * 8, 8).Split("-").Reverse());
                    if (ulong.TryParse(val, out ulong res))
                    {
                        var r = res * 0.000001;
                        _logger?.LogInformation("Cum{i}: {res}", i + 1, r);
                        result.Add($"cumulative{i + 1}", r);
                    }
                }

            }

            return result;
        }
    }
}
