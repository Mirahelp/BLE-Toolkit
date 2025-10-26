using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using MirahelpBLEToolkit.Constants;
using MirahelpBLEToolkit.Core.Enums;
using MirahelpBLEToolkit.Core.Interfaces;
using MirahelpBLEToolkit.Core.Results;
using MirahelpBLEToolkit.Core.Services;
using MirahelpBLEToolkit.Presentation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace MirahelpBLEToolkit
{
    public sealed partial class CommunicationWidget : UserControl
    {
        private sealed class UuidItem
        {
            public Guid Uuid { get; set; }
            public String Label { get; set; } = String.Empty;
            public override String ToString() { return Label; }
        }

        private enum WizardStep
        {
            WriteTargetMode = 0,
            WriteListService = 1,
            WriteListCharacteristic = 2,
            WriteManualService = 3,
            WriteManualCharacteristic = 4,
            WriteType = 5,
            PayloadFormat = 6,
            PayloadText = 7,
            ResponseMode = 8,
            SameAsWrite = 9,
            ResponseTargetMode = 10,
            ResponseListService = 11,
            ResponseListCharacteristic = 12,
            ResponseManualService = 13,
            ResponseManualCharacteristic = 14,
            Timeout = 15,
            SummaryRun = 16
        }

        private const Int32 UiFailSafeExtraMs = 4000;

        private UInt64 _address;

        private ReadWriteService? _readWriteService;
        private GattBrowserService? _gattBrowserService;
        private IConnectionService? _connectionService;
        private ILocalizationControllerService? _localizationControllerService;

        private readonly List<(Guid Service, List<Guid> Characteristics)> _serviceData;

        private WizardStep _step;

        private Boolean _writePickManual;
        private Guid _writeServiceUuid;
        private Guid _writeCharacteristicUuid;
        private WriteTypeOptions _writeType;
        private String _payloadFormatDisplay;
        private String _payloadText;

        private String _responseModeDisplay;
        private Boolean _sameAsWrite;
        private Boolean _responsePickManual;
        private Guid _responseServiceUuid;
        private Guid _responseCharacteristicUuid;
        private Int32 _timeoutMs;

        private ComboBox? _cmbWriteService;
        private ComboBox? _cmbWriteCharacteristic;
        private TextBox? _txtWriteServiceUuid;
        private TextBox? _txtWriteCharacteristicUuid;

        private RadioButton? _rbWriteFromList;
        private RadioButton? _rbWriteManual;

        private RadioButton? _rbWriteWithResponse;
        private RadioButton? _rbWriteWithoutResponse;

        private ComboBox? _cmbPayloadFormat;
        private TextBox? _txtPayload;

        private RadioButton? _rbRespNone;
        private RadioButton? _rbRespRead;
        private RadioButton? _rbRespNotify;

        private CheckBox? _chkSameAsWrite;

        private RadioButton? _rbRespFromList;
        private RadioButton? _rbRespManual;

        private ComboBox? _cmbRespService;
        private ComboBox? _cmbRespCharacteristic;
        private TextBox? _txtRespServiceUuid;
        private TextBox? _txtRespCharacteristicUuid;

        private TextBox? _txtTimeout;

        private TextBlock? _txtSummary;
        private TextBlock? _txtResultStatus;
        private TextBlock? _txtResultLen;
        private TextBlock? _txtResultHex;
        private TextBlock? _txtResultAscii;

        private CancellationTokenSource _lifetimeCancellationTokenSource;

        public CommunicationWidget()
        {
            InitializeComponent();

            _address = 0;

            _readWriteService = null;
            _gattBrowserService = null;
            _connectionService = null;
            _localizationControllerService = null;

            _serviceData = new List<(Guid Service, List<Guid> Characteristics)>();

            _step = WizardStep.WriteTargetMode;

            _writePickManual = false;
            _writeServiceUuid = Guid.Empty;
            _writeCharacteristicUuid = Guid.Empty;
            _writeType = WriteTypeOptions.WithResponse;
            _payloadFormatDisplay = UiText(UiCatalogKeys.HintFormatHex);
            _payloadText = String.Empty;

            _responseModeDisplay = UiText(UiCatalogKeys.HintResponseNone);
            _sameAsWrite = true;
            _responsePickManual = false;
            _responseServiceUuid = Guid.Empty;
            _responseCharacteristicUuid = Guid.Empty;
            _timeoutMs = 3000;

            _lifetimeCancellationTokenSource = new CancellationTokenSource();

            BtnBack.Click += OnBackClicked;
            BtnNext.Click += OnNextClicked;

            BtnBack.Content = UiText(UiCatalogKeys.MenuBack);
            BtnNext.Content = UiText(UiCatalogKeys.MenuContinue);

            RootHost.AddHandler(InputElement.PointerReleasedEvent, OnRootContextMenuRequested, handledEventsToo: true);

            UpdateStep();
        }

        public void SetServices(ReadWriteService readWriteService, GattBrowserService gattBrowserService, IConnectionService connectionService, ILocalizationControllerService localizationControllerService)
        {
            _readWriteService = readWriteService;
            _gattBrowserService = gattBrowserService;
            _connectionService = connectionService;
            _localizationControllerService = localizationControllerService;

            BtnBack.Content = UiText(UiCatalogKeys.MenuBack);
            BtnNext.Content = UiText(UiCatalogKeys.MenuContinue);
            UpdateStep();
        }

        public void SetDevice(UInt64 address)
        {
            _address = address;

            try { _lifetimeCancellationTokenSource.Cancel(); } catch { }
            try { _lifetimeCancellationTokenSource.Dispose(); } catch { }
            _lifetimeCancellationTokenSource = new CancellationTokenSource();

            LoadingOverlay.IsVisible = false;
            ContentPanel.IsVisible = true;
            _step = WizardStep.WriteTargetMode;
            UpdateStep();
        }

        private String UiText(String key)
        {
            ILocalizationControllerService? svc = _localizationControllerService;
            if (svc == null)
            {
                return key ?? String.Empty;
            }
            return svc.GetText(key);
        }

        private void RefreshServicesAsync()
        {
            UInt64 address = _address;
            if (address == 0 || _gattBrowserService == null)
            {
                return;
            }

            ContentPanel.IsVisible = false;
            LoadingOverlay.IsVisible = true;

            CancellationToken token = _lifetimeCancellationTokenSource.Token;

            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    if (_connectionService != null)
                    {
                        try { await _connectionService.EnsureConnectedAsync(address, CacheModeOptions.Uncached, token); } catch { }
                    }
                }
                catch
                {
                }

                List<(Guid, List<Guid>)> data = new();
                try
                {
                    GattServicesResult servicesResult = await _gattBrowserService.ListServicesAsync(address, CacheModeOptions.Uncached, token);
                    foreach (IGattServiceService service in servicesResult.Services)
                    {
                        List<Guid> characteristics = new();
                        try
                        {
                            GattCharacteristicsResult charsResult = await service.GetCharacteristicsAsync(CacheModeOptions.Uncached, token);
                            foreach (IGattCharacteristicService ch in charsResult.Characteristics)
                            {
                                characteristics.Add(ch.Uuid);
                                try { ch.Dispose(); } catch { }
                            }
                        }
                        catch
                        {
                        }
                        data.Add((service.Uuid, characteristics));
                        try { service.Dispose(); } catch { }
                    }
                    try { servicesResult.Device?.Dispose(); } catch { }
                }
                catch
                {
                }

                Dispatcher.UIThread.Post(() =>
                {
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }
                    _serviceData.Clear();
                    foreach ((Guid Service, List<Guid> Characteristics) pair in data)
                    {
                        _serviceData.Add((pair.Service, pair.Characteristics));
                    }
                    LoadingOverlay.IsVisible = false;
                    ContentPanel.IsVisible = true;
                    RebindCombosIfAny();
                    _step = WizardStep.WriteTargetMode;
                    UpdateStep();
                });
            }, token);
        }

        private void OnBackClicked(Object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            WizardStep previous = ComputePrevious(_step);
            _step = previous;
            UpdateStep();
        }

        private void OnNextClicked(Object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (_step == WizardStep.SummaryRun)
            {
                _ = ExecuteAsync();
                return;
            }
            if (!TryCommitCurrentStep())
            {
                return;
            }
            WizardStep next = ComputeNext(_step);
            _step = next;
            UpdateStep();
        }

        private WizardStep ComputePrevious(WizardStep current)
        {
            if (current == WizardStep.WriteTargetMode) return WizardStep.WriteTargetMode;
            if (current == WizardStep.WriteListService) return WizardStep.WriteTargetMode;
            if (current == WizardStep.WriteListCharacteristic) return WizardStep.WriteListService;
            if (current == WizardStep.WriteManualService) return WizardStep.WriteTargetMode;
            if (current == WizardStep.WriteManualCharacteristic) return WizardStep.WriteManualService;
            if (current == WizardStep.WriteType) return _writePickManual ? WizardStep.WriteManualCharacteristic : WizardStep.WriteListCharacteristic;
            if (current == WizardStep.PayloadFormat) return WizardStep.WriteType;
            if (current == WizardStep.PayloadText) return WizardStep.PayloadFormat;
            if (current == WizardStep.ResponseMode) return WizardStep.PayloadText;
            if (current == WizardStep.SameAsWrite) return WizardStep.ResponseMode;
            if (current == WizardStep.ResponseTargetMode) return WizardStep.SameAsWrite;
            if (current == WizardStep.ResponseListService) return WizardStep.ResponseTargetMode;
            if (current == WizardStep.ResponseListCharacteristic) return WizardStep.ResponseListService;
            if (current == WizardStep.ResponseManualService) return WizardStep.ResponseTargetMode;
            if (current == WizardStep.ResponseManualCharacteristic) return WizardStep.ResponseManualService;
            if (current == WizardStep.Timeout) return _sameAsWrite ? WizardStep.SameAsWrite : (_responsePickManual ? WizardStep.ResponseManualCharacteristic : WizardStep.ResponseListCharacteristic);
            if (current == WizardStep.SummaryRun) return WizardStep.Timeout;
            return WizardStep.WriteTargetMode;
        }

        private WizardStep ComputeNext(WizardStep current)
        {
            if (current == WizardStep.WriteTargetMode) return _writePickManual ? WizardStep.WriteManualService : WizardStep.WriteListService;
            if (current == WizardStep.WriteListService) return WizardStep.WriteListCharacteristic;
            if (current == WizardStep.WriteListCharacteristic) return WizardStep.WriteType;
            if (current == WizardStep.WriteManualService) return WizardStep.WriteManualCharacteristic;
            if (current == WizardStep.WriteManualCharacteristic) return WizardStep.WriteType;
            if (current == WizardStep.WriteType) return WizardStep.PayloadFormat;
            if (current == WizardStep.PayloadFormat) return WizardStep.PayloadText;
            if (current == WizardStep.PayloadText) return WizardStep.ResponseMode;
            if (current == WizardStep.ResponseMode)
            {
                String respNoneLabel = UiText(UiCatalogKeys.HintResponseNone);
                if (String.Equals(_responseModeDisplay, respNoneLabel, StringComparison.OrdinalIgnoreCase)) return WizardStep.SummaryRun;
                return WizardStep.SameAsWrite;
            }
            if (current == WizardStep.SameAsWrite) return _sameAsWrite ? WizardStep.Timeout : WizardStep.ResponseTargetMode;
            if (current == WizardStep.ResponseTargetMode) return _responsePickManual ? WizardStep.ResponseManualService : WizardStep.ResponseListService;
            if (current == WizardStep.ResponseListService) return WizardStep.ResponseListCharacteristic;
            if (current == WizardStep.ResponseListCharacteristic) return WizardStep.Timeout;
            if (current == WizardStep.ResponseManualService) return WizardStep.ResponseManualCharacteristic;
            if (current == WizardStep.ResponseManualCharacteristic) return WizardStep.Timeout;
            if (current == WizardStep.Timeout) return WizardStep.SummaryRun;
            return WizardStep.SummaryRun;
        }

        private void UpdateStep()
        {
            ContentPanel.Children.Clear();
            _cmbWriteService = null;
            _cmbWriteCharacteristic = null;
            _txtWriteServiceUuid = null;
            _txtWriteCharacteristicUuid = null;
            _rbWriteFromList = null;
            _rbWriteManual = null;
            _rbWriteWithResponse = null;
            _rbWriteWithoutResponse = null;
            _cmbPayloadFormat = null;
            _txtPayload = null;
            _rbRespNone = null;
            _rbRespRead = null;
            _rbRespNotify = null;
            _chkSameAsWrite = null;
            _rbRespFromList = null;
            _rbRespManual = null;
            _cmbRespService = null;
            _cmbRespCharacteristic = null;
            _txtRespServiceUuid = null;
            _txtRespCharacteristicUuid = null;
            _txtTimeout = null;
            _txtSummary = null;
            _txtResultStatus = null;
            _txtResultLen = null;
            _txtResultHex = null;
            _txtResultAscii = null;

            if (_step == WizardStep.WriteTargetMode)
            {
                BuildSectionLabel(UiText(UiCatalogKeys.HintServiceCaption));
                BuildRadiosVertical(
                    out _rbWriteFromList,
                    out _rbWriteManual,
                    new String[] { UiText(UiCatalogKeys.HintChooseFromList), UiText(UiCatalogKeys.HintEnterUuid) },
                    new Boolean[] { !_writePickManual, _writePickManual },
                    "writeMode",
                    (String choice) =>
                    {
                        _writePickManual = String.Equals(choice, UiText(UiCatalogKeys.HintEnterUuid), StringComparison.OrdinalIgnoreCase);
                        UpdateButtons();
                    });
                if (_rbWriteFromList != null) _rbWriteFromList.IsEnabled = _serviceData.Count > 0;
            }
            else if (_step == WizardStep.WriteListService)
            {
                BuildLabeledCombo(UiText(UiCatalogKeys.LabelService), out _cmbWriteService, ServiceItems(), OnWriteServiceChanged);
            }
            else if (_step == WizardStep.WriteListCharacteristic)
            {
                BuildLabeledCombo(UiText(UiCatalogKeys.LabelCharacteristic), out _cmbWriteCharacteristic, CharacterItemsFor(_writeServiceUuid), (_) => UpdateButtons());
            }
            else if (_step == WizardStep.WriteManualService)
            {
                BuildLabeledText(UiText(UiCatalogKeys.LabelService), _writeServiceUuid != Guid.Empty ? _writeServiceUuid.ToString() : String.Empty, out _txtWriteServiceUuid, (_) => UpdateButtons());
            }
            else if (_step == WizardStep.WriteManualCharacteristic)
            {
                BuildLabeledText(UiText(UiCatalogKeys.LabelCharacteristic), _writeCharacteristicUuid != Guid.Empty ? _writeCharacteristicUuid.ToString() : String.Empty, out _txtWriteCharacteristicUuid, (_) => UpdateButtons());
            }
            else if (_step == WizardStep.WriteType)
            {
                BuildSectionLabel(UiText(UiCatalogKeys.HintWriteTypeCaption));
                BuildRadiosVertical(
                    out _rbWriteWithResponse,
                    out _rbWriteWithoutResponse,
                    new String[] { UiText(UiCatalogKeys.HintWriteWithResponse), UiText(UiCatalogKeys.HintWriteWithoutResponse) },
                    new Boolean[] { _writeType != WriteTypeOptions.WithoutResponse, _writeType == WriteTypeOptions.WithoutResponse },
                    "wt",
                    (String _) =>
                    {
                        _writeType = _rbWriteWithoutResponse != null && _rbWriteWithoutResponse.IsChecked.GetValueOrDefault() ? WriteTypeOptions.WithoutResponse : WriteTypeOptions.WithResponse;
                        UpdateButtons();
                    });
            }
            else if (_step == WizardStep.PayloadFormat)
            {
                BuildSectionLabel(UiText(UiCatalogKeys.HintPayloadFormatCaption));
                BuildComboForFormats(out _cmbPayloadFormat, _payloadFormatDisplay, (_) => UpdateButtons());
            }
            else if (_step == WizardStep.PayloadText)
            {
                BuildLabeledMultiline(UiText(UiCatalogKeys.LabelPayload), _payloadText ?? String.Empty, out _txtPayload, (_) => UpdateButtons());
            }
            else if (_step == WizardStep.ResponseMode)
            {
                BuildSectionLabel(UiText(UiCatalogKeys.HintResponseCaption));
                BuildRadiosVertical(
                    out _rbRespNone,
                    out _rbRespRead,
                    new String[] { UiText(UiCatalogKeys.HintResponseNone), UiText(UiCatalogKeys.HintResponseRead) },
                    new Boolean[]
                    {
                        String.Equals(_responseModeDisplay, UiText(UiCatalogKeys.HintResponseNone), StringComparison.OrdinalIgnoreCase),
                        String.Equals(_responseModeDisplay, UiText(UiCatalogKeys.HintResponseRead), StringComparison.OrdinalIgnoreCase)
                    },
                    "rm",
                    (String s) =>
                    {
                        _responseModeDisplay = s;
                        UpdateButtons();
                    });

                RadioButton rbThird = new()
                {
                    GroupName = "rm",
                    Content = UiText(UiCatalogKeys.HintResponseNotify),
                    IsChecked = String.Equals(_responseModeDisplay, UiText(UiCatalogKeys.HintResponseNotify), StringComparison.OrdinalIgnoreCase),
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left
                };
                rbThird.Checked += (Object? s, Avalonia.Interactivity.RoutedEventArgs e) =>
                {
                    _responseModeDisplay = UiText(UiCatalogKeys.HintResponseNotify);
                    UpdateButtons();
                };
                ContentPanel.Children.Add(rbThird);
                _rbRespNotify = rbThird;
            }
            else if (_step == WizardStep.SameAsWrite)
            {
                BuildSectionLabel(UiText(UiCatalogKeys.HintResponseCharacteristicCaption));
                BuildCheckbox(UiText(UiCatalogKeys.HintSameAsWrite), _sameAsWrite, out _chkSameAsWrite, (_) =>
                {
                    _sameAsWrite = _chkSameAsWrite != null && _chkSameAsWrite.IsChecked.GetValueOrDefault();
                    UpdateButtons();
                });
            }
            else if (_step == WizardStep.ResponseTargetMode)
            {
                BuildSectionLabel(UiText(UiCatalogKeys.HintCharacteristicCaption));
                BuildRadiosVertical(
                    out _rbRespFromList,
                    out _rbRespManual,
                    new String[] { UiText(UiCatalogKeys.HintChooseFromList), UiText(UiCatalogKeys.HintEnterUuid) },
                    new Boolean[] { !_responsePickManual, _responsePickManual },
                    "respmode",
                    (String s) =>
                    {
                        _responsePickManual = String.Equals(s, UiText(UiCatalogKeys.HintEnterUuid), StringComparison.OrdinalIgnoreCase);
                        UpdateButtons();
                    });
            }
            else if (_step == WizardStep.ResponseListService)
            {
                BuildLabeledCombo(UiText(UiCatalogKeys.LabelService), out _cmbRespService, ServiceItems(), OnRespServiceChanged);
            }
            else if (_step == WizardStep.ResponseListCharacteristic)
            {
                BuildLabeledCombo(UiText(UiCatalogKeys.LabelCharacteristic), out _cmbRespCharacteristic, CharacterItemsFor(_responseServiceUuid), (_) => UpdateButtons());
            }
            else if (_step == WizardStep.ResponseManualService)
            {
                BuildLabeledText(UiText(UiCatalogKeys.LabelService), _responseServiceUuid != Guid.Empty ? _responseServiceUuid.ToString() : String.Empty, out _txtRespServiceUuid, (_) => UpdateButtons());
            }
            else if (_step == WizardStep.ResponseManualCharacteristic)
            {
                BuildLabeledText(UiText(UiCatalogKeys.LabelCharacteristic), _responseCharacteristicUuid != Guid.Empty ? _responseCharacteristicUuid.ToString() : String.Empty, out _txtRespCharacteristicUuid, (_) => UpdateButtons());
            }
            else if (_step == WizardStep.Timeout)
            {
                BuildLabeledNumber(UiText(UiCatalogKeys.LabelTimeout), _timeoutMs.ToString(), out _txtTimeout, (_) => UpdateButtons(), UiText(UiCatalogKeys.LabelMillisecondsSuffix));
            }
            else
            {
                BuildSectionLabel(UiText(UiCatalogKeys.TitleWriteReadOperation));
                BuildSummaryBlock();
            }

            UpdateButtons();
        }

        private void UpdateButtons()
        {
            Boolean canBack = _step != WizardStep.WriteTargetMode;
            Boolean canNext = ValidateCurrentStep();
            BtnBack.IsEnabled = canBack;
            BtnNext.IsEnabled = canNext;

            BtnBack.Content = UiText(UiCatalogKeys.MenuBack);
            BtnNext.Content = _step == WizardStep.SummaryRun ? UiText(UiCatalogKeys.MenuRun) : UiText(UiCatalogKeys.MenuContinue);
        }

        private Boolean TryCommitCurrentStep()
        {
            if (_step == WizardStep.WriteTargetMode)
            {
                _writePickManual = _rbWriteManual != null && _rbWriteManual.IsChecked.GetValueOrDefault();
                return true;
            }
            if (_step == WizardStep.WriteListService)
            {
                UuidItem? si = _cmbWriteService?.SelectedItem as UuidItem;
                if (si == null) return false;
                _writeServiceUuid = si.Uuid;
                return true;
            }
            if (_step == WizardStep.WriteListCharacteristic)
            {
                UuidItem? ci = _cmbWriteCharacteristic?.SelectedItem as UuidItem;
                if (ci == null) return false;
                _writeCharacteristicUuid = ci.Uuid;
                return true;
            }
            if (_step == WizardStep.WriteManualService)
            {
                Guid? s = _txtWriteServiceUuid != null ? PayloadEncodingService.ParseUuid(_txtWriteServiceUuid.Text ?? String.Empty) : null;
                if (!s.HasValue) return false;
                _writeServiceUuid = s.Value;
                return true;
            }
            if (_step == WizardStep.WriteManualCharacteristic)
            {
                Guid? c = _txtWriteCharacteristicUuid != null ? PayloadEncodingService.ParseUuid(_txtWriteCharacteristicUuid.Text ?? String.Empty) : null;
                if (!c.HasValue) return false;
                _writeCharacteristicUuid = c.Value;
                return true;
            }
            if (_step == WizardStep.WriteType)
            {
                _writeType = _rbWriteWithoutResponse != null && _rbWriteWithoutResponse.IsChecked.GetValueOrDefault() ? WriteTypeOptions.WithoutResponse : WriteTypeOptions.WithResponse;
                return true;
            }
            if (_step == WizardStep.PayloadFormat)
            {
                String selected = _cmbPayloadFormat != null && _cmbPayloadFormat.SelectedItem is String s ? s : UiText(UiCatalogKeys.HintFormatHex);
                _payloadFormatDisplay = selected;
                return true;
            }
            if (_step == WizardStep.PayloadText)
            {
                String data = _txtPayload != null ? (_txtPayload.Text ?? String.Empty) : String.Empty;
                Byte[]? enc = EncodePayloadToBytes(_payloadFormatDisplay, data);
                if (enc == null) return false;
                _payloadText = data;
                return true;
            }
            if (_step == WizardStep.ResponseMode)
            {
                Boolean isNone = _rbRespNone != null && _rbRespNone.IsChecked.GetValueOrDefault();
                Boolean isRead = _rbRespRead != null && _rbRespRead.IsChecked.GetValueOrDefault();
                Boolean isNotify = _rbRespNotify != null && _rbRespNotify.IsChecked.GetValueOrDefault();
                if (isNone) _responseModeDisplay = UiText(UiCatalogKeys.HintResponseNone);
                else if (isRead) _responseModeDisplay = UiText(UiCatalogKeys.HintResponseRead);
                else if (isNotify) _responseModeDisplay = UiText(UiCatalogKeys.HintResponseNotify);
                else return false;
                return true;
            }
            if (_step == WizardStep.SameAsWrite)
            {
                _sameAsWrite = _chkSameAsWrite != null && _chkSameAsWrite.IsChecked.GetValueOrDefault();
                return true;
            }
            if (_step == WizardStep.ResponseTargetMode)
            {
                _responsePickManual = _rbRespManual != null && _rbRespManual.IsChecked.GetValueOrDefault();
                return true;
            }
            if (_step == WizardStep.ResponseListService)
            {
                UuidItem? si = _cmbRespService?.SelectedItem as UuidItem;
                if (si == null) return false;
                _responseServiceUuid = si.Uuid;
                return true;
            }
            if (_step == WizardStep.ResponseListCharacteristic)
            {
                UuidItem? ci = _cmbRespCharacteristic?.SelectedItem as UuidItem;
                if (ci == null) return false;
                _responseCharacteristicUuid = ci.Uuid;
                return true;
            }
            if (_step == WizardStep.ResponseManualService)
            {
                Guid? s = _txtRespServiceUuid != null ? PayloadEncodingService.ParseUuid(_txtRespServiceUuid.Text ?? String.Empty) : null;
                if (!s.HasValue) return false;
                _responseServiceUuid = s.Value;
                return true;
            }
            if (_step == WizardStep.ResponseManualCharacteristic)
            {
                Guid? c = _txtRespCharacteristicUuid != null ? PayloadEncodingService.ParseUuid(_txtRespCharacteristicUuid.Text ?? String.Empty) : null;
                if (!c.HasValue) return false;
                _responseCharacteristicUuid = c.Value;
                return true;
            }
            if (_step == WizardStep.Timeout)
            {
                Int32 msParsed;
                Boolean ok = Int32.TryParse(_txtTimeout != null ? (_txtTimeout.Text ?? "3000") : "3000", out msParsed);
                if (!ok) msParsed = 3000;
                _timeoutMs = Math.Max(100, Math.Min(60000, msParsed));
                return true;
            }
            return true;
        }

        private Boolean ValidateCurrentStep()
        {
            if (_step == WizardStep.WriteTargetMode)
            {
                if (_rbWriteFromList != null && _rbWriteFromList.IsChecked.GetValueOrDefault() && _serviceData.Count == 0) return false;
                return (_rbWriteFromList?.IsChecked ?? false) || (_rbWriteManual?.IsChecked ?? false);
            }
            if (_step == WizardStep.WriteListService) return _cmbWriteService?.SelectedItem is UuidItem;
            if (_step == WizardStep.WriteListCharacteristic) return _cmbWriteCharacteristic?.SelectedItem is UuidItem;
            if (_step == WizardStep.WriteManualService) return PayloadEncodingService.ParseUuid(_txtWriteServiceUuid?.Text ?? String.Empty).HasValue;
            if (_step == WizardStep.WriteManualCharacteristic) return PayloadEncodingService.ParseUuid(_txtWriteCharacteristicUuid?.Text ?? String.Empty).HasValue;
            if (_step == WizardStep.WriteType)
            {
                Boolean hasSelection = (_rbWriteWithResponse?.IsChecked ?? false) || (_rbWriteWithoutResponse?.IsChecked ?? false);
                return hasSelection;
            }
            if (_step == WizardStep.PayloadFormat) return _cmbPayloadFormat?.SelectedItem is String;
            if (_step == WizardStep.PayloadText)
            {
                String data = _txtPayload?.Text ?? String.Empty;
                return EncodePayloadToBytes(_payloadFormatDisplay, data) != null;
            }
            if (_step == WizardStep.ResponseMode)
            {
                Boolean b1 = _rbRespNone?.IsChecked ?? false;
                Boolean b2 = _rbRespRead?.IsChecked ?? false;
                Boolean b3 = _rbRespNotify?.IsChecked ?? false;
                return b1 || b2 || b3;
            }
            if (_step == WizardStep.SameAsWrite) return true;
            if (_step == WizardStep.ResponseTargetMode) return (_rbRespFromList?.IsChecked ?? false) || (_rbRespManual?.IsChecked ?? false);
            if (_step == WizardStep.ResponseListService) return _cmbRespService?.SelectedItem is UuidItem;
            if (_step == WizardStep.ResponseListCharacteristic) return _cmbRespCharacteristic?.SelectedItem is UuidItem;
            if (_step == WizardStep.ResponseManualService) return PayloadEncodingService.ParseUuid(_txtRespServiceUuid?.Text ?? String.Empty).HasValue;
            if (_step == WizardStep.ResponseManualCharacteristic) return PayloadEncodingService.ParseUuid(_txtRespCharacteristicUuid?.Text ?? String.Empty).HasValue;
            if (_step == WizardStep.Timeout)
            {
                Int32 ms;
                return Int32.TryParse(_txtTimeout?.Text ?? "3000", out ms) && ms >= 100 && ms <= 60000;
            }
            if (_step == WizardStep.SummaryRun) return true;
            return false;
        }

        private void BuildSectionLabel(String text)
        {
            TextBlock label = new();
            label.Text = text ?? String.Empty;
            label.FontWeight = Avalonia.Media.FontWeight.SemiBold;
            label.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
            label.TextAlignment = Avalonia.Media.TextAlignment.Left;
            ContentPanel.Children.Add(label);
        }

        private void BuildLabeledCombo(String labelText, out ComboBox combo, IReadOnlyList<UuidItem> items, Action<SelectionChangedEventArgs> onChanged)
        {
            TextBlock label = new();
            label.Text = labelText ?? String.Empty;
            label.FontWeight = Avalonia.Media.FontWeight.SemiBold;
            label.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
            label.TextAlignment = Avalonia.Media.TextAlignment.Left;
            ContentPanel.Children.Add(label);

            ComboBox cb = new();
            cb.ItemsSource = items;
            cb.MinWidth = 420;
            cb.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            cb.SelectionChanged += (Object? s, SelectionChangedEventArgs e) => onChanged(e);
            ContentPanel.Children.Add(cb);

            combo = cb;
            if (items.Count > 0 && cb.SelectedItem == null) cb.SelectedIndex = 0;
        }

        private void BuildLabeledText(String labelText, String initial, out TextBox textBox, Action<TextChangedEventArgs> onChanged)
        {
            TextBlock label = new();
            label.Text = labelText ?? String.Empty;
            label.FontWeight = Avalonia.Media.FontWeight.SemiBold;
            label.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
            label.TextAlignment = Avalonia.Media.TextAlignment.Left;
            ContentPanel.Children.Add(label);

            TextBox tb = new();
            tb.Text = initial ?? String.Empty;
            tb.MinWidth = 420;
            tb.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            tb.TextChanged += (Object? s, TextChangedEventArgs e) => onChanged(e);
            ContentPanel.Children.Add(tb);

            textBox = tb;
        }

        private void BuildLabeledMultiline(String labelText, String initial, out TextBox textBox, Action<TextChangedEventArgs> onChanged)
        {
            TextBlock label = new();
            label.Text = labelText ?? String.Empty;
            label.FontWeight = Avalonia.Media.FontWeight.SemiBold;
            label.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
            label.TextAlignment = Avalonia.Media.TextAlignment.Left;
            ContentPanel.Children.Add(label);

            TextBox tb = new();
            tb.AcceptsReturn = true;
            tb.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
            tb.Height = 120;
            tb.Text = initial ?? String.Empty;
            tb.MinWidth = 420;
            tb.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            tb.TextChanged += (Object? s, TextChangedEventArgs e) => onChanged(e);
            ContentPanel.Children.Add(tb);

            textBox = tb;
        }

        private void BuildLabeledNumber(String labelText, String initial, out TextBox textBox, Action<TextChangedEventArgs> onChanged, String suffix)
        {
            TextBlock label = new();
            label.Text = labelText ?? String.Empty;
            label.FontWeight = Avalonia.Media.FontWeight.SemiBold;
            label.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
            label.TextAlignment = Avalonia.Media.TextAlignment.Left;
            ContentPanel.Children.Add(label);

            StackPanel row = new() { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };

            TextBox tb = new();
            tb.Text = initial ?? String.Empty;
            tb.Width = 120;
            tb.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
            tb.TextChanged += (Object? s, TextChangedEventArgs e) => onChanged(e);

            TextBlock tail = new();
            tail.Text = suffix ?? String.Empty;
            tail.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
            tail.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;

            row.Children.Add(tb);
            row.Children.Add(tail);
            ContentPanel.Children.Add(row);

            textBox = tb;
        }

        private void BuildCheckbox(String labelText, Boolean initial, out CheckBox checkBox, Action<Avalonia.Interactivity.RoutedEventArgs> onChanged)
        {
            CheckBox cb = new();
            cb.Content = labelText ?? String.Empty;
            cb.IsChecked = initial;
            cb.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
            cb.Checked += (Object? s, Avalonia.Interactivity.RoutedEventArgs e) => onChanged(e);
            cb.Unchecked += (Object? s, Avalonia.Interactivity.RoutedEventArgs e) => onChanged(e);
            ContentPanel.Children.Add(cb);

            checkBox = cb;
        }

        private void BuildRadiosVertical(out RadioButton first, out RadioButton second, String[] captions, Boolean[] checks, String group, Action<String> onPicked)
        {
            StackPanel column = new() { Orientation = Avalonia.Layout.Orientation.Vertical, Spacing = 8, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };

            RadioButton r1 = new() { GroupName = group, Content = captions[0], IsChecked = checks[0], HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
            r1.Checked += (Object? s, Avalonia.Interactivity.RoutedEventArgs e) => onPicked(captions[0]);

            RadioButton r2 = new() { GroupName = group, Content = captions[1], IsChecked = checks[1], HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
            r2.Checked += (Object? s, Avalonia.Interactivity.RoutedEventArgs e) => onPicked(captions[1]);

            column.Children.Add(r1);
            column.Children.Add(r2);
            ContentPanel.Children.Add(column);

            first = r1;
            second = r2;
        }

        private void BuildComboForFormats(out ComboBox combo, String currentDisplay, Action<SelectionChangedEventArgs> onChanged)
        {
            String hexDisplay = UiText(UiCatalogKeys.HintFormatHex);
            String utf8Display = UiText(UiCatalogKeys.HintFormatUtf8);
            String base64Display = UiText(UiCatalogKeys.HintFormatBase64);

            ComboBox cb = new();
            cb.ItemsSource = new Object[] { hexDisplay, utf8Display, base64Display };
            if (String.Equals(currentDisplay, utf8Display, StringComparison.OrdinalIgnoreCase)) cb.SelectedIndex = 1;
            else if (String.Equals(currentDisplay, base64Display, StringComparison.OrdinalIgnoreCase)) cb.SelectedIndex = 2;
            else cb.SelectedIndex = 0;
            cb.MinWidth = 420;
            cb.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            cb.SelectionChanged += (Object? s, SelectionChangedEventArgs e) => onChanged(e);
            ContentPanel.Children.Add(cb);
            combo = cb;
        }

        private IReadOnlyList<UuidItem> ServiceItems()
        {
            List<UuidItem> items = _serviceData.Select(s => new UuidItem { Uuid = s.Service, Label = s.Service.ToString() }).ToList();
            List<UuidItem> ordered = items.OrderBy(x => x.Label, StringComparer.Ordinal).ToList();
            return ordered;
        }

        private IReadOnlyList<UuidItem> CharacterItemsFor(Guid serviceUuid)
        {
            List<UuidItem> items = new();
            foreach ((Guid Service, List<Guid> Characteristics) entry in _serviceData)
            {
                if (entry.Service == serviceUuid)
                {
                    foreach (Guid c in entry.Characteristics)
                    {
                        items.Add(new UuidItem { Uuid = c, Label = c.ToString() });
                    }
                }
            }
            List<UuidItem> ordered = items.OrderBy(x => x.Label, StringComparer.Ordinal).ToList();
            return ordered;
        }

        private void OnWriteServiceChanged(SelectionChangedEventArgs e)
        {
            UpdateButtons();
        }

        private void OnRespServiceChanged(SelectionChangedEventArgs e)
        {
            UpdateButtons();
        }

        private void BuildSummaryBlock()
        {
            TextBlock summary = new()
            {
                Text = BuildSummaryText(),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                TextAlignment = Avalonia.Media.TextAlignment.Left
            };
            _txtSummary = summary;

            TextBlock resultStatus = new()
            {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                TextAlignment = Avalonia.Media.TextAlignment.Left
            };
            _txtResultStatus = resultStatus;

            TextBlock resultLen = new()
            {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                TextAlignment = Avalonia.Media.TextAlignment.Left
            };
            _txtResultLen = resultLen;

            TextBlock resultHex = new()
            {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                TextAlignment = Avalonia.Media.TextAlignment.Left
            };
            _txtResultHex = resultHex;
            resultHex.AddHandler(InputElement.PointerReleasedEvent, OnResultCopyContext, handledEventsToo: true);

            TextBlock resultAscii = new()
            {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                TextAlignment = Avalonia.Media.TextAlignment.Left
            };
            _txtResultAscii = resultAscii;
            resultAscii.AddHandler(InputElement.PointerReleasedEvent, OnResultCopyContext, handledEventsToo: true);

            ContentPanel.Children.Add(summary);
            ContentPanel.Children.Add(resultStatus);
            ContentPanel.Children.Add(resultLen);
            ContentPanel.Children.Add(resultHex);
            ContentPanel.Children.Add(resultAscii);
        }

        private String BuildSummaryText()
        {
            String writeTypeLabel = _writeType == WriteTypeOptions.WithoutResponse ? UiText(UiCatalogKeys.ValueWithoutResponseShort) : UiText(UiCatalogKeys.ValueWithResponseShort);
            Int32 payloadLength = 0;
            if (_payloadText != null)
            {
                String pfHex = UiText(UiCatalogKeys.HintFormatHex);
                if (String.Equals(_payloadFormatDisplay, pfHex, StringComparison.OrdinalIgnoreCase))
                {
                    Int32 digits = _payloadText.Count(Uri.IsHexDigit);
                    payloadLength = Math.Max(0, digits / 2);
                }
                else
                {
                    payloadLength = _payloadText.Length;
                }
            }
            List<String> parts = new();
            parts.Add(UiText(UiCatalogKeys.LabelService) + ": " + _writeServiceUuid.ToString());
            parts.Add(UiText(UiCatalogKeys.LabelCharacteristic) + ": " + _writeCharacteristicUuid.ToString());
            parts.Add(UiText(UiCatalogKeys.LabelWriteType) + ": " + writeTypeLabel);
            parts.Add(UiText(UiCatalogKeys.LabelFormat) + ": " + _payloadFormatDisplay);
            parts.Add(UiText(UiCatalogKeys.LabelLength) + ": " + payloadLength.ToString());
            parts.Add(UiText(UiCatalogKeys.HintResponseCaption) + ": " + _responseModeDisplay);
            if (!String.Equals(_responseModeDisplay, UiText(UiCatalogKeys.HintResponseNone), StringComparison.OrdinalIgnoreCase))
            {
                if (_sameAsWrite)
                {
                    parts.Add(UiText(UiCatalogKeys.HintSameAsWrite));
                }
                else
                {
                    parts.Add(UiText(UiCatalogKeys.LabelService) + ": " + _responseServiceUuid.ToString());
                    parts.Add(UiText(UiCatalogKeys.LabelCharacteristic) + ": " + _responseCharacteristicUuid.ToString());
                }
                parts.Add(UiText(UiCatalogKeys.LabelTimeout) + ": " + _timeoutMs.ToString() + UiText(UiCatalogKeys.LabelMillisecondsSuffix));
            }
            return String.Join(Environment.NewLine, parts);
        }

        private void RebindCombosIfAny()
        {
            if (_cmbWriteService != null && _step == WizardStep.WriteListService)
            {
                _cmbWriteService.ItemsSource = ServiceItems();
                if (_cmbWriteService.SelectedItem == null && _cmbWriteService.ItemCount > 0) _cmbWriteService.SelectedIndex = 0;
            }
            if (_cmbWriteCharacteristic != null && _step == WizardStep.WriteListCharacteristic)
            {
                _cmbWriteCharacteristic.ItemsSource = CharacterItemsFor(_writeServiceUuid);
                if (_cmbWriteCharacteristic.SelectedItem == null && _cmbWriteCharacteristic.ItemCount > 0) _cmbWriteCharacteristic.SelectedIndex = 0;
            }
            if (_cmbRespService != null && _step == WizardStep.ResponseListService)
            {
                _cmbRespService.ItemsSource = ServiceItems();
                if (_cmbRespService.SelectedItem == null && _cmbRespService.ItemCount > 0) _cmbRespService.SelectedIndex = 0;
            }
            if (_cmbRespCharacteristic != null && _step == WizardStep.ResponseListCharacteristic)
            {
                _cmbRespCharacteristic.ItemsSource = CharacterItemsFor(_responseServiceUuid);
                if (_cmbRespCharacteristic.SelectedItem == null && _cmbRespCharacteristic.ItemCount > 0) _cmbRespCharacteristic.SelectedIndex = 0;
            }
        }

        private static String BytesToAscii(Byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return String.Empty;
            Char[] chars = new Char[bytes.Length];
            for (Int32 i = 0; i < bytes.Length; i++)
            {
                Byte b = bytes[i];
                chars[i] = b >= 32 && b <= 126 ? (Char)b : '.';
            }
            return new String(chars);
        }

        private void OnResultCopyContext(Object? sender, PointerReleasedEventArgs e)
        {
            if (e == null || e.InitialPressMouseButton != MouseButton.Right)
            {
                return;
            }
            TextBlock? textBlock = sender as TextBlock;
            if (textBlock == null)
            {
                return;
            }
            String text = textBlock.Text ?? String.Empty;
            ContextMenu menu = new();
            MenuItem copyItem = new();
            copyItem.Header = UiText(UiCatalogKeys.MenuCopy);
            copyItem.Click += async (Object? s, Avalonia.Interactivity.RoutedEventArgs a) =>
            {
                TopLevel? topLevel = TopLevel.GetTopLevel(textBlock);
                if (topLevel != null && topLevel.Clipboard != null)
                {
                    try { await topLevel.Clipboard.SetTextAsync(text); } catch { }
                }
            };
            menu.Items.Add(copyItem);
            ContextMenuManager.Show(menu, textBlock);
        }

        private void OnRootContextMenuRequested(Object? sender, PointerReleasedEventArgs e)
        {
            if (e == null || e.InitialPressMouseButton != MouseButton.Right) return;
            if (e.Source is TextBlock) return;

            ContextMenu menu = new();
            MenuItem refreshItem = new() { Header = UiText(UiCatalogKeys.MenuFetch) };
            refreshItem.Click += (Object? s, Avalonia.Interactivity.RoutedEventArgs a) => RefreshServicesAsync();
            menu.Items.Add(refreshItem);
            ContextMenuManager.Show(menu, RootHost);
        }

        private async System.Threading.Tasks.Task ExecuteAsync()
        {
            if (_readWriteService == null) return;

            CancellationToken token = _lifetimeCancellationTokenSource.Token;

            UInt64 address = _address;
            Guid writeService = _writeServiceUuid;
            Guid writeCharacteristic = _writeCharacteristicUuid;
            WriteTypeOptions writeType = _writeType;

            Byte[]? payload = EncodePayloadToBytes(_payloadFormatDisplay, _payloadText ?? String.Empty);
            if (payload == null) return;

            String responseModeDisplay = _responseModeDisplay;
            Boolean isSame = _sameAsWrite;
            Guid responseService = isSame ? writeService : _responseServiceUuid;
            Guid responseCharacteristic = isSame ? writeCharacteristic : _responseCharacteristicUuid;
            Int32 timeoutMs = _timeoutMs;

            BtnNext.IsEnabled = false;
            ContentPanel.IsVisible = false;
            LoadingOverlay.IsVisible = true;
            if (_txtResultStatus != null) _txtResultStatus.Text = String.Empty;
            if (_txtResultLen != null) _txtResultLen.Text = String.Empty;
            if (_txtResultHex != null) _txtResultHex.Text = String.Empty;
            if (_txtResultAscii != null) _txtResultAscii.Text = String.Empty;

            await System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    Int32 uiTimeout = Math.Max(1000, timeoutMs + UiFailSafeExtraMs);
                    using CancellationTokenSource uiCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                    System.Threading.Tasks.Task coreTask;

                    String respNone = UiText(UiCatalogKeys.HintResponseNone);
                    String respRead = UiText(UiCatalogKeys.HintResponseRead);
                    String respNotify = UiText(UiCatalogKeys.HintResponseNotify);

                    if (String.Equals(responseModeDisplay, respNone, StringComparison.OrdinalIgnoreCase))
                    {
                        coreTask = CoreWriteOnly(address, writeService, writeCharacteristic, writeType, payload, token);
                    }
                    else if (String.Equals(responseModeDisplay, respRead, StringComparison.OrdinalIgnoreCase))
                    {
                        coreTask = CoreWriteThenRead(address, writeService, writeCharacteristic, responseService, responseCharacteristic, writeType, payload, token);
                    }
                    else
                    {
                        coreTask = CoreWriteThenNotify(address, writeService, writeCharacteristic, responseService, responseCharacteristic, writeType, payload, timeoutMs, token);
                    }

                    uiCts.CancelAfter(uiTimeout);
                    System.Threading.Tasks.Task finished = await System.Threading.Tasks.Task.WhenAny(coreTask, System.Threading.Tasks.Task.Delay(uiTimeout, uiCts.Token));

                    if (finished != coreTask)
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            if (token.IsCancellationRequested) return;
                            LoadingOverlay.IsVisible = false;
                            ContentPanel.IsVisible = true;
                            if (_txtResultStatus != null) _txtResultStatus.Text = UiText(UiCatalogKeys.TextNotifyTimeout);
                            BtnNext.IsEnabled = true;
                        });
                    }
                }
                catch
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (token.IsCancellationRequested) return;
                        LoadingOverlay.IsVisible = false;
                        ContentPanel.IsVisible = true;
                        if (_txtResultStatus != null) _txtResultStatus.Text = UiText(UiCatalogKeys.TextConnectFailed);
                        BtnNext.IsEnabled = true;
                    });
                }
            }, token);
        }

        private async System.Threading.Tasks.Task CoreWriteOnly(UInt64 address, Guid serviceUuid, Guid characteristicUuid, WriteTypeOptions writeType, Byte[] payload, CancellationToken token)
        {
            try
            {
                GattWriteResult writeRes = await _readWriteService!.WriteAsync(address, serviceUuid, characteristicUuid, writeType, payload, token);
                Dispatcher.UIThread.Post(() =>
                {
                    if (token.IsCancellationRequested) return;
                    LoadingOverlay.IsVisible = false;
                    ContentPanel.IsVisible = true;
                    String writePrefix = UiText(UiCatalogKeys.TextWritePrefix);
                    if (_txtResultStatus != null) _txtResultStatus.Text = writePrefix + ": " + writeRes.Status.ToString();
                    BtnNext.IsEnabled = true;
                });
            }
            catch
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (token.IsCancellationRequested) return;
                    LoadingOverlay.IsVisible = false;
                    ContentPanel.IsVisible = true;
                    if (_txtResultStatus != null) _txtResultStatus.Text = UiText(UiCatalogKeys.TextConnectFailed);
                    BtnNext.IsEnabled = true;
                });
            }
        }

        private async System.Threading.Tasks.Task CoreWriteThenRead(UInt64 address, Guid writeServiceUuid, Guid writeCharacteristicUuid, Guid responseServiceUuid, Guid responseCharacteristicUuid, WriteTypeOptions writeType, Byte[] payload, CancellationToken token)
        {
            try
            {
                GattWriteResult writeRes = await _readWriteService!.WriteAsync(address, writeServiceUuid, writeCharacteristicUuid, writeType, payload, token);
                if (writeRes.Status != GattCommunicationStatusOptions.Success)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (token.IsCancellationRequested) return;
                        LoadingOverlay.IsVisible = false;
                        ContentPanel.IsVisible = true;
                        String writePrefix = UiText(UiCatalogKeys.TextWritePrefix);
                        if (_txtResultStatus != null) _txtResultStatus.Text = writePrefix + ": " + writeRes.Status.ToString();
                        BtnNext.IsEnabled = true;
                    });
                    return;
                }

                GattReadResult readRes = await _readWriteService!.ReadAsync(address, responseServiceUuid, responseCharacteristicUuid, CacheModeOptions.Uncached, token);
                Byte[] data = readRes.Data ?? Array.Empty<Byte>();
                String hex = data.Length > 0 ? Convert.ToHexString(data) : String.Empty;
                String ascii = BytesToAscii(data);

                Dispatcher.UIThread.Post(() =>
                {
                    if (token.IsCancellationRequested) return;
                    LoadingOverlay.IsVisible = false;
                    ContentPanel.IsVisible = true;

                    String readPrefix = UiText(UiCatalogKeys.TextReadPrefix);
                    if (_txtResultStatus != null) _txtResultStatus.Text = readPrefix + ": " + readRes.Status.ToString();
                    if (_txtResultLen != null) _txtResultLen.Text = UiText(UiCatalogKeys.LabelLength) + ": " + data.Length.ToString();
                    if (_txtResultHex != null) _txtResultHex.Text = UiText(UiCatalogKeys.TextHexPrefix) + ": " + hex;
                    if (_txtResultAscii != null) _txtResultAscii.Text = UiText(UiCatalogKeys.TextAsciiPrefix) + ": " + ascii;

                    BtnNext.IsEnabled = true;
                });
            }
            catch
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (token.IsCancellationRequested) return;
                    LoadingOverlay.IsVisible = false;
                    ContentPanel.IsVisible = true;
                    if (_txtResultStatus != null) _txtResultStatus.Text = UiText(UiCatalogKeys.TextConnectFailed);
                    BtnNext.IsEnabled = true;
                });
            }
        }

        private async System.Threading.Tasks.Task CoreWriteThenNotify(UInt64 address, Guid writeServiceUuid, Guid writeCharacteristicUuid, Guid responseServiceUuid, Guid responseCharacteristicUuid, WriteTypeOptions writeType, Byte[] payload, Int32 timeoutMs, CancellationToken token)
        {
            try
            {
                Byte[]? notifyBytes = null;
                GattWriteResult writeRes;

                if (responseServiceUuid == writeServiceUuid)
                {
                    (GattWriteResult Write, GattReadResult? Read, Byte[]? Notify) result = await _readWriteService!.WriteAndWaitNotifyAsync(
                        address,
                        writeServiceUuid,
                        writeCharacteristicUuid,
                        responseCharacteristicUuid,
                        writeType,
                        payload,
                        TimeSpan.FromMilliseconds(Math.Max(0, timeoutMs)),
                        token);
                    writeRes = result.Write;
                    notifyBytes = result.Notify;
                }
                else
                {
                    (GattWriteResult Write, GattReadResult? Read, Byte[]? Notify) result = await _readWriteService!.WriteAndWaitNotifyAcrossServicesAsync(
                        address,
                        writeServiceUuid,
                        writeCharacteristicUuid,
                        responseServiceUuid,
                        responseCharacteristicUuid,
                        writeType,
                        payload,
                        TimeSpan.FromMilliseconds(Math.Max(0, timeoutMs)),
                        token);
                    writeRes = result.Write;
                    notifyBytes = result.Notify;
                }

                Byte[] data = notifyBytes ?? Array.Empty<Byte>();
                String hex = data.Length > 0 ? Convert.ToHexString(data) : String.Empty;
                String ascii = BytesToAscii(data);

                Dispatcher.UIThread.Post(() =>
                {
                    if (token.IsCancellationRequested) return;
                    LoadingOverlay.IsVisible = false;
                    ContentPanel.IsVisible = true;

                    String writePrefix = UiText(UiCatalogKeys.TextWritePrefix);
                    if (_txtResultStatus != null) _txtResultStatus.Text = writePrefix + ": " + writeRes.Status.ToString();

                    if (notifyBytes != null)
                    {
                        if (_txtResultLen != null) _txtResultLen.Text = UiText(UiCatalogKeys.TextNotifyLenPrefix) + ": " + data.Length.ToString();
                        if (_txtResultHex != null) _txtResultHex.Text = UiText(UiCatalogKeys.TextHexPrefix) + ": " + hex;
                        if (_txtResultAscii != null) _txtResultAscii.Text = UiText(UiCatalogKeys.TextAsciiPrefix) + ": " + ascii;
                    }
                    else
                    {
                        if (_txtResultLen != null) _txtResultLen.Text = UiText(UiCatalogKeys.TextNotifyTimeout);
                        if (_txtResultHex != null) _txtResultHex.Text = String.Empty;
                        if (_txtResultAscii != null) _txtResultAscii.Text = String.Empty;
                    }

                    BtnNext.IsEnabled = true;
                });
            }
            catch
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (token.IsCancellationRequested) return;
                    LoadingOverlay.IsVisible = false;
                    ContentPanel.IsVisible = true;
                    if (_txtResultStatus != null) _txtResultStatus.Text = UiText(UiCatalogKeys.TextConnectFailed);
                    BtnNext.IsEnabled = true;
                });
            }
        }

        private static String MapFormatDisplayToToken(String display, Func<String, String> ui)
        {
            if (String.Equals(display, ui(UiCatalogKeys.HintFormatHex), StringComparison.OrdinalIgnoreCase)) return Constants.AppStrings.PayloadFormatHexToken;
            if (String.Equals(display, ui(UiCatalogKeys.HintFormatUtf8), StringComparison.OrdinalIgnoreCase)) return Constants.AppStrings.PayloadFormatUtf8Token;
            if (String.Equals(display, ui(UiCatalogKeys.HintFormatBase64), StringComparison.OrdinalIgnoreCase)) return Constants.AppStrings.PayloadFormatBase64Token;
            return Constants.AppStrings.PayloadFormatHexToken;
        }

        private Byte[]? EncodePayloadToBytes(String formatDisplay, String data)
        {
            String token = MapFormatDisplayToToken(formatDisplay, UiText);
            Byte[]? bytes = PayloadEncodingService.Encode(token, data);
            return bytes;
        }
    }
}