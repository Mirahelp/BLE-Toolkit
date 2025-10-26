using System;

namespace MirahelpBLEToolkit.Constants
{
    public static class AppStrings
    {
        public const String LocalizationFolderRoot = "Locales";
        public const String LocalizationLocaleEnUs = "en_US";
        public const String LocalizationMessagesFolder = "LC_MESSAGES";
        public const String LocalizationDefaultDomain = "ui";

        public const String ErrorCodeUnreachable = "error.unreachable";
        public const String ErrorCodeTimeout = "error.timeout";
        public const String ErrorCodeCanceled = "error.canceled";

        public const String FormatHexUpper = "X";

        public const String PayloadFormatHexToken = "hex";
        public const String PayloadFormatUtf8Token = "utf8";
        public const String PayloadFormatBase64Token = "base64";

        public const String HexPrefix = "0x";
        public const String Uuid16Prefix = "0000";
        public const String UuidBleBaseTail = "-0000-1000-8000-00805F9B34FB";
    }
}