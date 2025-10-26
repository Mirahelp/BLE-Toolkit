using MirahelpBLEToolkit.Core.Enums;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace MirahelpBLEToolkit.Core.Platform
{
    public static class WindowsGattCommon
    {
        public static CharacteristicPropertyOptions MapProperties(GattCharacteristicProperties properties)
        {
            CharacteristicPropertyOptions result = CharacteristicPropertyOptions.None;
            if (properties.HasFlag(GattCharacteristicProperties.Read))
            {
                result |= CharacteristicPropertyOptions.Read;
            }
            if (properties.HasFlag(GattCharacteristicProperties.Write))
            {
                result |= CharacteristicPropertyOptions.Write;
            }
            if (properties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse))
            {
                result |= CharacteristicPropertyOptions.WriteWithoutResponse;
            }
            if (properties.HasFlag(GattCharacteristicProperties.Notify))
            {
                result |= CharacteristicPropertyOptions.Notify;
            }
            if (properties.HasFlag(GattCharacteristicProperties.Indicate))
            {
                result |= CharacteristicPropertyOptions.Indicate;
            }
            return result;
        }

        public static GattCommunicationStatusOptions MapStatus(Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus status)
        {
            if (status == Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success)
            {
                return GattCommunicationStatusOptions.Success;
            }
            if (status == Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.AccessDenied)
            {
                return GattCommunicationStatusOptions.AccessDenied;
            }
            if (status == Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.ProtocolError)
            {
                return GattCommunicationStatusOptions.ProtocolError;
            }
            return GattCommunicationStatusOptions.Unreachable;
        }
    }
}