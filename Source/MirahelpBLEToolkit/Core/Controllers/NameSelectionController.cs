using System;
using System.Linq;
using System.Text;

namespace MirahelpBLEToolkit.Core.Controllers
{
    public static class NameSelectionController
    {
        public static String ChooseBestName(UInt64 address, String operatingSystemName, String advertisementName, String genericAccessProfileName)
        {
            String chosenGenericAccessProfileName = genericAccessProfileName ?? String.Empty;
            String chosenAdvertisementName = advertisementName ?? String.Empty;
            String chosenOperatingSystemName = operatingSystemName ?? String.Empty;
            if (!String.IsNullOrWhiteSpace(chosenGenericAccessProfileName) && !IsAddressLike(chosenGenericAccessProfileName, address))
            {
                return chosenGenericAccessProfileName;
            }
            if (!String.IsNullOrWhiteSpace(chosenAdvertisementName) && !IsAddressLike(chosenAdvertisementName, address))
            {
                return chosenAdvertisementName;
            }
            if (!String.IsNullOrWhiteSpace(chosenOperatingSystemName) && !IsAddressLike(chosenOperatingSystemName, address))
            {
                return chosenOperatingSystemName;
            }
            return String.Empty;
        }

        public static Boolean IsAddressLike(String text, UInt64 address)
        {
            if (String.IsNullOrWhiteSpace(text))
            {
                return false;
            }
            String trimmed = text.Trim();
            if (trimmed.Equals(FormatAddress(address), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            String hexOnly = new(trimmed.Where(c => Uri.IsHexDigit(c)).ToArray());
            if (hexOnly.Length == 12)
            {
                Boolean isAllHex = hexOnly.All(c =>
                {
                    Char upper = Char.ToUpperInvariant(c);
                    return (upper >= '0' && upper <= '9') || (upper >= 'A' && upper <= 'F');
                });
                if (isAllHex)
                {
                    return true;
                }
            }
            Int32 colonCount = trimmed.Count(c => c == ':');
            if (colonCount >= 5)
            {
                return true;
            }
            return false;
        }

        public static String FormatAddress(UInt64 address)
        {
            Byte[] raw = BitConverter.GetBytes(address);
            Byte[] mac = new Byte[6];
            Array.Copy(raw, 0, mac, 0, 6);
            Array.Reverse(mac);

            Char[] hexDigits = new Char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };
            StringBuilder builder = new(17);
            for (Int32 i = 0; i < 6; i++)
            {
                Byte b = mac[i];
                builder.Append(hexDigits[(b >> 4) & 0x0F]);
                builder.Append(hexDigits[b & 0x0F]);
                if (i < 5)
                {
                    builder.Append(':');
                }
            }
            return builder.ToString();
        }
    }
}