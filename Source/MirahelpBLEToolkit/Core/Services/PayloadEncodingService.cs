using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace MirahelpBLEToolkit.Core.Services
{
    public static class PayloadEncodingService
    {
        public static Guid? ParseUuid(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return null;
            }
            string trimmed = input.Trim();
            if (trimmed.StartsWith(Constants.AppStrings.HexPrefix, StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed.Substring(Constants.AppStrings.HexPrefix.Length);
            }
            string hexDigitsOnly = new(trimmed.Where(c => Uri.IsHexDigit(c)).ToArray());
            Guid parsedGuid;
            bool isGuid = Guid.TryParse(trimmed, out parsedGuid);
            if (isGuid)
            {
                return parsedGuid;
            }
            if (hexDigitsOnly.Length == 4)
            {
                string full16 = string.Concat(Constants.AppStrings.Uuid16Prefix, hexDigitsOnly, Constants.AppStrings.UuidBleBaseTail);
                Guid parsed16;
                bool isGuid16 = Guid.TryParse(full16, out parsed16);
                if (isGuid16)
                {
                    return parsed16;
                }
            }
            if (hexDigitsOnly.Length == 8)
            {
                string full32 = string.Concat(hexDigitsOnly, Constants.AppStrings.UuidBleBaseTail);
                Guid parsed32;
                bool isGuid32 = Guid.TryParse(full32, out parsed32);
                if (isGuid32)
                {
                    return parsed32;
                }
            }
            return null;
        }

        public static byte[]? Encode(string format, string data)
        {
            if (string.IsNullOrWhiteSpace(format))
            {
                return null;
            }
            string normalizedFormatToken = format.Trim().ToLowerInvariant();
            if (string.Equals(normalizedFormatToken, Constants.AppStrings.PayloadFormatHexToken, StringComparison.OrdinalIgnoreCase))
            {
                string digits = new((data ?? string.Empty).Where(c => Uri.IsHexDigit(c)).ToArray());
                if (digits.Length % 2 != 0)
                {
                    return null;
                }
                byte[] bytes = new byte[digits.Length / 2];
                for (int i = 0; i < digits.Length; i += 2)
                {
                    byte value;
                    bool parsed = byte.TryParse(digits.Substring(i, 2), NumberStyles.HexNumber, null, out value);
                    if (!parsed)
                    {
                        return null;
                    }
                    bytes[i / 2] = value;
                }
                return bytes;
            }
            if (string.Equals(normalizedFormatToken, Constants.AppStrings.PayloadFormatUtf8Token, StringComparison.OrdinalIgnoreCase))
            {
                return Encoding.UTF8.GetBytes(data ?? string.Empty);
            }
            if (string.Equals(normalizedFormatToken, Constants.AppStrings.PayloadFormatBase64Token, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    return Convert.FromBase64String(data ?? string.Empty);
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }
    }
}
