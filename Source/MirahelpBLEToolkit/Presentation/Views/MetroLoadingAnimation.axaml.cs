using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;
using System;

namespace MirahelpBLEToolkit
{

    public sealed partial class MetroLoadingAnimation : UserControl
    {
        private readonly DispatcherTimer _timer;
        private DateTime _startedUtc;

        private const Double PeriodSeconds = 1.2;
        private static readonly Double[] _delays = new Double[] { 0.00, 0.15, 0.30, 0.45, 0.60 };

        public MetroLoadingAnimation()
        {
            InitializeComponent();

            SolidColorBrush accentBrush = BuildAccentBrush();
            InitializeDot(Dot1, accentBrush);
            InitializeDot(Dot2, accentBrush);
            InitializeDot(Dot3, accentBrush);
            InitializeDot(Dot4, accentBrush);
            InitializeDot(Dot5, accentBrush);

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(33);
            _timer.Tick += OnTick;

            this.AttachedToVisualTree += OnAttachedToVisualTree;
            this.DetachedFromVisualTree += OnDetachedFromVisualTree;
        }

        private void OnAttachedToVisualTree(Object? sender, VisualTreeAttachmentEventArgs e)
        {
            _startedUtc = DateTime.UtcNow;
            if (!_timer.IsEnabled) _timer.Start();
        }

        private void OnDetachedFromVisualTree(Object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (_timer.IsEnabled) _timer.Stop();
        }

        private static void InitializeDot(Ellipse ellipse, SolidColorBrush brush)
        {
            ellipse.Fill = brush;
            ellipse.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
            ScaleTransform transform = new();
            transform.ScaleX = 0.6;
            transform.ScaleY = 0.6;
            ellipse.RenderTransform = transform;
            ellipse.Opacity = 0.3;
        }

        private static SolidColorBrush BuildAccentBrush()
        {
            Color baseAccent = Color.FromArgb(0xFF, 0x00, 0x78, 0xD4);
            if (Application.Current != null)
            {
                Object? value;
                Boolean ok = Application.Current.TryFindResource("SystemAccentColor", out value);
                if (ok && value is Color c)
                {
                    baseAccent = c;
                }
            }
            SolidColorBrush brush = new(baseAccent);
            return brush;
        }

        private void OnTick(Object? sender, EventArgs e)
        {
            DateTime now = DateTime.UtcNow;
            Double t = (now - _startedUtc).TotalSeconds;

            UpdateDot(Dot1, t, _delays[0]);
            UpdateDot(Dot2, t, _delays[1]);
            UpdateDot(Dot3, t, _delays[2]);
            UpdateDot(Dot4, t, _delays[3]);
            UpdateDot(Dot5, t, _delays[4]);
        }

        private static void UpdateDot(Ellipse ellipse, Double timeSeconds, Double delaySeconds)
        {
            Double phase = timeSeconds - delaySeconds;
            Double wrapped = phase - Math.Floor(phase / PeriodSeconds) * PeriodSeconds;
            Double normalized = wrapped / PeriodSeconds;

            Double pulse = 0.5 - 0.5 * Math.Cos(normalized * 2.0 * Math.PI);

            Double scale = 0.6 + 0.4 * pulse;
            Double opacity = 0.3 + 0.7 * pulse;

            ScaleTransform transform = ellipse.RenderTransform as ScaleTransform ?? new ScaleTransform();
            transform.ScaleX = scale;
            transform.ScaleY = scale;
            if (!Object.ReferenceEquals(ellipse.RenderTransform, transform))
            {
                ellipse.RenderTransform = transform;
            }

            ellipse.Opacity = opacity;
        }
    }
}