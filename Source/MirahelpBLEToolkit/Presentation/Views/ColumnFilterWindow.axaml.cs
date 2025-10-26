using Avalonia.Controls;
using MirahelpBLEToolkit.Constants;
using MirahelpBLEToolkit.Core.Interfaces;
using System;
using System.Collections.Generic;

namespace MirahelpBLEToolkit
{
    public sealed partial class ColumnFilterWindow : Window
    {
        private readonly Dictionary<String, CheckBox> _boxes;
        private String _column;
        private ILocalizationControllerService? _localizationControllerService;

        public ColumnFilterWindow()
        {
            InitializeComponent();
            _boxes = new Dictionary<String, CheckBox>(StringComparer.OrdinalIgnoreCase);
            _column = String.Empty;

            BtnAll.Click += OnAll;
            BtnNone.Click += OnNone;
            BtnApply.Click += OnApply;
        }

        public void SetLocalization(ILocalizationControllerService localizationControllerService)
        {
            _localizationControllerService = localizationControllerService;

            String selectAll = UiText(UiCatalogKeys.LabelSelectAll);
            String clearAll = UiText(UiCatalogKeys.LabelClear);
            String apply = UiText(UiCatalogKeys.LabelApply);

            BtnAll.Content = selectAll ?? String.Empty;
            BtnNone.Content = clearAll ?? String.Empty;
            BtnApply.Content = apply ?? String.Empty;

            Caption.Text = (UiText(UiCatalogKeys.LabelFilter) ?? String.Empty) + ": " + (_column ?? String.Empty);
        }

        public void LoadValues(String column, IReadOnlyList<String> values, IReadOnlySet<String>? selected)
        {
            _column = column ?? String.Empty;
            Caption.Text = (UiText(UiCatalogKeys.LabelFilter) ?? String.Empty) + ": " + (_column ?? String.Empty);
            _boxes.Clear();
            ListPanel.Children.Clear();
            foreach (String v in values)
            {
                CheckBox cb = new();
                cb.Content = v;
                cb.IsChecked = selected == null || selected.Contains(v);
                _boxes[v] = cb;
                ListPanel.Children.Add(cb);
            }
        }

        public IReadOnlySet<String> GetSelection()
        {
            HashSet<String> set = new(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<String, CheckBox> kv in _boxes)
            {
                Boolean include = kv.Value.IsChecked.HasValue && kv.Value.IsChecked.Value;
                if (include)
                {
                    set.Add(kv.Key);
                }
            }
            return set;
        }

        private void OnAll(Object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            foreach (CheckBox cb in _boxes.Values)
            {
                cb.IsChecked = true;
            }
        }

        private void OnNone(Object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            foreach (CheckBox cb in _boxes.Values)
            {
                cb.IsChecked = false;
            }
        }

        private void OnApply(Object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close(true);
        }

        private String UiText(String key)
        {
            if (_localizationControllerService == null)
            {
                return key ?? String.Empty;
            }
            return _localizationControllerService.GetText(key) ?? String.Empty;
        }
    }
}