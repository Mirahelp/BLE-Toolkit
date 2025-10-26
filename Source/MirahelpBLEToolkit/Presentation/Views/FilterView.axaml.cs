using Avalonia.Controls;
using MirahelpBLEToolkit.Core.Controllers;
using MirahelpBLEToolkit.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MirahelpBLEToolkit
{
    public sealed partial class FilterView : UserControl
    {
        public event Action<String, Boolean>? StringOptionToggled;
        public event Action<UInt64, Boolean>? DeviceOptionToggled;

        public FilterView()
        {
            InitializeComponent();
        }

        public void LoadStringOptions(IReadOnlyList<String> options, IReadOnlySet<String>? selected)
        {
            ItemsPanel.Children.Clear();

            IReadOnlySet<String> chosen;
            if (selected != null)
            {
                chosen = selected;
            }
            else
            {
                HashSet<String> all = new(options, StringComparer.OrdinalIgnoreCase);
                chosen = all;
            }

            foreach (String option in options)
            {
                StackPanel row = new();
                row.Orientation = Avalonia.Layout.Orientation.Horizontal;
                row.Spacing = 8;

                CheckBox checkBox = new();
                checkBox.IsChecked = chosen.Contains(option);

                TextBlock label = new();
                label.Text = option ?? String.Empty;
                label.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;

                String captured = option ?? String.Empty;
                checkBox.Checked += (Object? s, Avalonia.Interactivity.RoutedEventArgs e) => OnStringToggle(captured, true);
                checkBox.Unchecked += (Object? s, Avalonia.Interactivity.RoutedEventArgs e) => OnStringToggle(captured, false);

                row.Children.Add(checkBox);
                row.Children.Add(label);
                ItemsPanel.Children.Add(row);
            }
        }

        public void LoadDeviceOptions(IReadOnlyList<DeviceState> devices, IReadOnlySet<UInt64>? selected)
        {
            ItemsPanel.Children.Clear();

            HashSet<UInt64> chosen;
            if (selected != null)
            {
                chosen = new HashSet<UInt64>(selected);
            }
            else
            {
                chosen = new HashSet<UInt64>(devices.Select(d => d.Address));
            }

            foreach (DeviceState device in devices)
            {
                StackPanel row = new();
                row.Orientation = Avalonia.Layout.Orientation.Horizontal;
                row.Spacing = 8;

                CheckBox checkBox = new();
                checkBox.IsChecked = chosen.Contains(device.Address);

                TextBlock label = new();
                String addressText = NameSelectionController.FormatAddress(device.Address);
                label.Text = addressText;
                label.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;

                UInt64 captured = device.Address;
                checkBox.Checked += (Object? s, Avalonia.Interactivity.RoutedEventArgs e) => OnDeviceToggle(captured, true);
                checkBox.Unchecked += (Object? s, Avalonia.Interactivity.RoutedEventArgs e) => OnDeviceToggle(captured, false);

                row.Children.Add(checkBox);
                row.Children.Add(label);
                ItemsPanel.Children.Add(row);
            }
        }

        private void OnStringToggle(String value, Boolean include)
        {
            Action<String, Boolean>? handler = StringOptionToggled;
            if (handler != null)
            {
                handler(value, include);
            }
        }

        private void OnDeviceToggle(UInt64 address, Boolean include)
        {
            Action<UInt64, Boolean>? handler = DeviceOptionToggled;
            if (handler != null)
            {
                handler(address, include);
            }
        }
    }
}