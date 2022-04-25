using System.Text;

namespace ElsterA1140Reader
{
    public static class Utils
    {
        public const byte SOH = 0x01;
        public const byte STX = 0x02;
        public const byte ETX = 0x03;
        public const byte BBRACKET = 0x28;
        public const byte EBRACKET = 0x29;
        public static readonly byte[] WAKE_UP = { SOH, (byte)'B', (byte)'0', ETX, (byte)'q' };

        public static byte CalcBcc(byte[] msg, byte res = 0)
        {
            byte result = res;
            foreach (byte b in msg)
                result ^= b;
            return result;
        }

        public static byte[] GetCommand(string cmd, string func, string param)
        {
            List<byte> result = new List<byte>();
            result.Add(SOH);
            result.AddRange(Encoding.Default.GetBytes(cmd));
            result.Add(STX);
            result.AddRange(Encoding.Default.GetBytes(func));
            result.Add(0x28);
            result.AddRange(Encoding.Default.GetBytes(param));
            result.Add(0x29);
            result.Add(ETX);
            result.Add(CalcBcc(result.ToArray()[1..]));
            return result.ToArray();
        }

        public static string ElsterEncrypt(string password, string seed)
        {
            //Convert hex string to byte array.

            byte[] s = HexStringToByteArray(seed);
            byte[] crypted = new byte[8];
            for (int pos = 0; pos != 8; ++pos)
            {
                crypted[pos] = (byte)(password[pos] ^ s[pos]);
            }
            int last = crypted[7];
            for (int pos = 0; pos != 8; ++pos)
            {
                crypted[pos] = (byte)((crypted[pos] + last) & 0xFF);
                last = crypted[pos];
            }

            return BitConverter.ToString(crypted);
        }

        public static byte[] HexStringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }
    }
}