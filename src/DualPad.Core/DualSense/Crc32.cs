namespace DualPad.Core.DualSense;

/// <summary>CRC-32 (IEEE 802.3) as used by DualSense Bluetooth reports.</summary>
public static class Crc32
{
    private static readonly uint[] Table = BuildTable();

    /// <summary>Bluetooth output reports are checksummed with this byte prepended.</summary>
    public const byte OutputSeed = 0xA2;

    /// <summary>Bluetooth input reports are checksummed with this byte prepended.</summary>
    public const byte InputSeed = 0xA1;

    public static uint Compute(byte seed, ReadOnlySpan<byte> data)
    {
        var crc = Update(0xFFFFFFFF, seed);
        foreach (var b in data) crc = Update(crc, b);
        return ~crc;
    }

    private static uint Update(uint crc, byte b) => Table[(crc ^ b) & 0xFF] ^ (crc >> 8);

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            var c = i;
            for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            table[i] = c;
        }
        return table;
    }
}
