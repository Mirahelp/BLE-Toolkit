using System;

namespace MirahelpBLEToolkit.Core.Models
{
    public static class DeviceColorGenerator
    {
        public static String GenerateHex(UInt64 address)
        {
            UInt32 rgb = (UInt32)(address & 0x00FFFFFFUL);
            Char[] hexDigits = new Char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };
            Char[] chars = new Char[7];
            chars[0] = '#';
            chars[1] = hexDigits[(Int32)((rgb >> 20) & 0x0F)];
            chars[2] = hexDigits[(Int32)((rgb >> 16) & 0x0F)];
            chars[3] = hexDigits[(Int32)((rgb >> 12) & 0x0F)];
            chars[4] = hexDigits[(Int32)((rgb >> 8) & 0x0F)];
            chars[5] = hexDigits[(Int32)((rgb >> 4) & 0x0F)];
            chars[6] = hexDigits[(Int32)(rgb & 0x0F)];
            String result = new(chars);
            return result;
        }
    }
}