using System;

namespace MirahelpBLEToolkit.Core.Enums
{
    [Flags]
    public enum CharacteristicPropertyOptions
    {
        None = 0,
        Read = 1,
        Write = 2,
        WriteWithoutResponse = 4,
        Notify = 8,
        Indicate = 16
    }
}
