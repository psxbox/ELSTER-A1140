// See https://aka.ms/new-console-template for more information
Console.WriteLine(ElsterEncrypt("00000000", "AEE6FA3263C33AA7"));
Console.WriteLine(ElsterEncrypt("00000000", "7A82F0BD2D953F2C"));

static string ElsterEncrypt(string password, string seed)
{
    //Convert hex string to byte array.
    
    byte[] s = StringToByteArray(seed);
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

static byte[] StringToByteArray(string hex) {
    return Enumerable.Range(0, hex.Length)
                     .Where(x => x % 2 == 0)
                     .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                     .ToArray();
}