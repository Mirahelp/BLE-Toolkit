using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using MirahelpBLEToolkit.Constants;
using MirahelpBLEToolkit.Core.Enums;
using MirahelpBLEToolkit.Core.Interfaces;
using MirahelpBLEToolkit.Core.Results;
using MirahelpBLEToolkit.Core.Services;
using MirahelpBLEToolkit.Presentation;
using System;
using System.Threading;

namespace MirahelpBLEToolkit
{
    public sealed partial class ServicesWidget : UserControl
    {
        private sealed class CharacteristicDisplay
        {
            public Guid Uuid { get; set; }
            public Boolean CanRead { get; set; }
            public Boolean CanWrite { get; set; }
            public Boolean CanWriteWithoutResponse { get; set; }
        }

        private sealed class ServiceDisplay
        {
            public Guid Uuid { get; set; }
            public Boolean HasReadable { get; set; }
            public Boolean HasWritable { get; set; }
            public System.Collections.Generic.List<CharacteristicDisplay> Characteristics { get; set; } = new System.Collections.Generic.List<CharacteristicDisplay>();
        }

        private UInt64 _address;
        private ConnectionService? _connectionService;
        private GattBrowserService? _gattBrowserService;
        private CancellationTokenSource _lifetimeCancellationTokenSource;

        public ServicesWidget()
        {
            InitializeComponent();
            _address = 0;
            _connectionService = null;
            _gattBrowserService = null;
            _lifetimeCancellationTokenSource = new CancellationTokenSource();
            BtnRefresh.Content = UiText(UiCatalogKeys.ButtonRefresh);
            BtnRefresh.Click += OnRefreshClicked;
        }

        public void SetServices(ConnectionService connectionService, GattBrowserService gattBrowserService)
        {
            _connectionService = connectionService;
            _gattBrowserService = gattBrowserService;
        }

        public void SetDevice(UInt64 address)
        {
            _address = address;
            CancellationTokenSource previous = _lifetimeCancellationTokenSource;
            try { previous.Cancel(); } catch { }
            try { previous.Dispose(); } catch { }
            _lifetimeCancellationTokenSource = new CancellationTokenSource();
        }

        private void OnRefreshClicked(Object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            LoadAsync();
        }

        private void LoadAsync()
        {
            UInt64 address = _address;
            ItemsPanel.Children.Clear();
            TxtStatus.Text = String.Empty;
            ItemsPanel.IsVisible = false;
            LoadingOverlay.IsVisible = true;

            CancellationToken token = _lifetimeCancellationTokenSource.Token;

            System.Threading.Tasks.Task.Run(async () =>
            {
                if (_connectionService != null)
                {
                    try { await _connectionService.EnsureConnectedAsync(address, CacheModeOptions.Uncached, token); } catch { }
                }
                if (_gattBrowserService == null)
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        if (token.IsCancellationRequested) return;
                        LoadingOverlay.IsVisible = false;
                        ItemsPanel.IsVisible = true;
                    });
                    return;
                }

                System.Collections.Generic.List<ServiceDisplay> serviceDisplays = new();
                try
                {
                    GattServicesResult servicesResult = await _gattBrowserService.ListServicesAsync(address, CacheModeOptions.Uncached, token);

                    foreach (IGattServiceService service in servicesResult.Services)
                    {
                        ServiceDisplay serviceDisplay = new();
                        serviceDisplay.Uuid = service.Uuid;

                        GattCharacteristicsResult characteristicsResult = await service.GetCharacteristicsAsync(CacheModeOptions.Uncached, token);
                        foreach (IGattCharacteristicService characteristic in characteristicsResult.Characteristics)
                        {
                            CharacteristicDisplay characteristicDisplay = new();
                            characteristicDisplay.Uuid = characteristic.Uuid;

                            try
                            {
                                CharacteristicPropertyOptions properties = characteristic.Properties;
                                Boolean canRead = (properties & CharacteristicPropertyOptions.Read) == CharacteristicPropertyOptions.Read;
                                Boolean canWrite = (properties & CharacteristicPropertyOptions.Write) == CharacteristicPropertyOptions.Write;
                                Boolean canWriteWithoutResponse = (properties & CharacteristicPropertyOptions.WriteWithoutResponse) == CharacteristicPropertyOptions.WriteWithoutResponse;

                                characteristicDisplay.CanRead = canRead;
                                characteristicDisplay.CanWrite = canWrite || canWriteWithoutResponse;
                                characteristicDisplay.CanWriteWithoutResponse = canWriteWithoutResponse;

                                if (canRead) serviceDisplay.HasReadable = true;
                                if (characteristicDisplay.CanWrite) serviceDisplay.HasWritable = true;
                            }
                            catch
                            {
                            }

                            serviceDisplay.Characteristics.Add(characteristicDisplay);

                            try { characteristic.Dispose(); } catch { }
                        }

                        serviceDisplays.Add(serviceDisplay);
                        try { service.Dispose(); } catch { }
                    }
                    try { servicesResult.Device?.Dispose(); } catch { }
                }
                catch
                {
                }

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (token.IsCancellationRequested) return;
                    Build(serviceDisplays);
                    LoadingOverlay.IsVisible = false;
                    ItemsPanel.IsVisible = true;
                });
            }, token);
        }

        private void Build(System.Collections.Generic.List<ServiceDisplay> data)
        {
            ItemsPanel.Children.Clear();

            Object? themeBrushObject = null;
            if (Application.Current != null)
            {
                Application.Current.TryFindResource("ThemeBackgroundBrush", out themeBrushObject);
            }
            IBrush backgroundBrush = themeBrushObject as IBrush ?? Brushes.Transparent;

            foreach (ServiceDisplay entry in data)
            {
                Border serviceBorder = new();
                serviceBorder.Padding = new Thickness(6);
                serviceBorder.Margin = new Thickness(0, 0, 0, 4);
                serviceBorder.BorderBrush = new SolidColorBrush(Color.Parse("#40808080"));
                serviceBorder.BorderThickness = new Thickness(1);
                serviceBorder.CornerRadius = new CornerRadius(6);
                serviceBorder.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
                serviceBorder.Background = backgroundBrush;

                Expander expander = new();
                expander.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;

                TextBlock header = new();
                header.Text = entry.Uuid.ToString();
                header.FontWeight = FontWeight.Bold;
                expander.Header = header;

                StackPanel contentPanel = new();
                contentPanel.Spacing = 6;
                contentPanel.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;

                StackPanel serviceInfoPanel = new();
                serviceInfoPanel.Orientation = Avalonia.Layout.Orientation.Vertical;
                serviceInfoPanel.Spacing = 2;
                serviceInfoPanel.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;

                TextBlock readableLine = new();
                readableLine.Text = UiText(UiCatalogKeys.LabelRead) + ": " + (entry.HasReadable ? UiText(UiCatalogKeys.ValueYes) : UiText(UiCatalogKeys.ValueNo));

                TextBlock writableLine = new();
                writableLine.Text = UiText(UiCatalogKeys.LabelWrite) + ": " + (entry.HasWritable ? UiText(UiCatalogKeys.ValueYes) : UiText(UiCatalogKeys.ValueNo));

                serviceInfoPanel.Children.Add(readableLine);
                serviceInfoPanel.Children.Add(writableLine);

                contentPanel.Children.Add(serviceInfoPanel);

                foreach (CharacteristicDisplay characteristicDisplay in entry.Characteristics)
                {
                    Border characteristicBorder = new();
                    characteristicBorder.Padding = new Thickness(8);
                    characteristicBorder.Margin = new Thickness(8, 2, 0, 2);
                    characteristicBorder.BorderBrush = new SolidColorBrush(Color.Parse("#30808080"));
                    characteristicBorder.BorderThickness = new Thickness(1);
                    characteristicBorder.CornerRadius = new CornerRadius(4);
                    characteristicBorder.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
                    characteristicBorder.Background = backgroundBrush;

                    StackPanel characteristicColumn = new();
                    characteristicColumn.Orientation = Avalonia.Layout.Orientation.Vertical;
                    characteristicColumn.Spacing = 2;

                    TextBlock characteristicUuidText = new();
                    characteristicUuidText.Text = characteristicDisplay.Uuid.ToString();

                    Boolean hasReadable = characteristicDisplay.CanRead;
                    Boolean hasWritableAny = characteristicDisplay.CanWrite;

                    TextBlock characteristicReadableLine = new();
                    characteristicReadableLine.Text = UiText(UiCatalogKeys.LabelRead) + ": " + (hasReadable ? UiText(UiCatalogKeys.ValueYes) : UiText(UiCatalogKeys.ValueNo));

                    TextBlock characteristicWritableLine = new();
                    characteristicWritableLine.Text = UiText(UiCatalogKeys.LabelWrite) + ": " + (hasWritableAny ? UiText(UiCatalogKeys.ValueYes) : UiText(UiCatalogKeys.ValueNo));

                    characteristicColumn.Children.Add(characteristicUuidText);
                    characteristicColumn.Children.Add(characteristicReadableLine);
                    characteristicColumn.Children.Add(characteristicWritableLine);

                    characteristicBorder.Child = characteristicColumn;

                    String capturedCharacteristic = characteristicDisplay.Uuid.ToString();
                    characteristicBorder.AddHandler(InputElement.PointerReleasedEvent, (Object? s, PointerReleasedEventArgs e) =>
                    {
                        if (e != null && e.InitialPressMouseButton == MouseButton.Right)
                        {
                            OpenCopyMenu(characteristicBorder, capturedCharacteristic);
                        }
                    }, handledEventsToo: true);

                    contentPanel.Children.Add(characteristicBorder);
                }

                expander.Content = contentPanel;

                String capturedService = entry.Uuid.ToString();
                serviceBorder.AddHandler(InputElement.PointerReleasedEvent, (Object? s, PointerReleasedEventArgs e) =>
                {
                    if (e != null && e.InitialPressMouseButton == MouseButton.Right)
                    {
                        OpenCopyMenu(serviceBorder, capturedService);
                    }
                }, handledEventsToo: true);

                serviceBorder.Child = expander;

                ItemsPanel.Children.Add(serviceBorder);
            }

            TxtStatus.Text = String.Empty;
        }

        private void OpenCopyMenu(Control target, String value)
        {
            ContextMenu menu = BuildCopyMenu(value);
            ContextMenuManager.Show(menu, target);
        }

        private ContextMenu BuildCopyMenu(String value)
        {
            ContextMenu menu = new();

            MenuItem copyItem = new();
            copyItem.Header = UiText(UiCatalogKeys.MenuCopy);
            copyItem.Click += async (Object? s, Avalonia.Interactivity.RoutedEventArgs e) =>
            {
                TopLevel? topLevel = TopLevel.GetTopLevel(this);
                if (topLevel != null && topLevel.Clipboard != null)
                {
                    try { await topLevel.Clipboard.SetTextAsync(value); } catch { }
                }
            };

            menu.Items.Add(copyItem);
            return menu;
        }

        private static String UiText(String key)
        {
            ILocalizationControllerService localization = AppHost.Localization;
            String text = localization != null ? localization.GetText(key) : key;
            return text ?? String.Empty;
        }
    }
}