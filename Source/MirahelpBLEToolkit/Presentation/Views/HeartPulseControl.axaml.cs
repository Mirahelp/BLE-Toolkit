using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using System;

namespace MirahelpBLEToolkit
{
    public sealed partial class HeartPulseControl : UserControl
    {
        private readonly DispatcherTimer _timer;
        private DateTime _lastTickUtc;
        private Double _phaseBase;
        private Double _phasePulse;
        private Double _basePeriodSeconds;
        private Double _pulsePeriodSeconds;
        private Double _baseAmpTarget;
        private Double _pulseAmpTarget;
        private Double _baseAmpCurrent;
        private Double _pulseAmpCurrent;
        private Boolean _pulseEnabled;
        private ScaleTransform _scaleTransform = null!;

        public HeartPulseControl()
        {
            InitializeComponent();
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(16);
            _timer.Tick += OnTick;
            _lastTickUtc = DateTime.UtcNow;
            _phaseBase = 0.0;
            _phasePulse = 0.0;
            _basePeriodSeconds = 3.2;
            _pulsePeriodSeconds = 2.2;
            _baseAmpTarget = 0.03;
            _pulseAmpTarget = 0.0;
            _baseAmpCurrent = 0.0;
            _pulseAmpCurrent = 0.0;
            _pulseEnabled = false;
            EnsureTransform();
            SetScale(1.0);
            if (!_timer.IsEnabled) _timer.Start();
        }

        public void SetStyle(Color color, Double basePeriodSeconds, Double baseAmplitude)
        {
            SolidColorBrush brush = new(color);
            HeartShape.Fill = brush;
            _basePeriodSeconds = Math.Max(0.2, basePeriodSeconds);
            _baseAmpTarget = Math.Max(0.0, Math.Min(0.49, baseAmplitude));
        }

        public void SetPulse(Boolean enabled, Double pulsePeriodSeconds, Double pulseAmplitude)
        {
            _pulseEnabled = enabled;
            _pulsePeriodSeconds = Math.Max(0.2, pulsePeriodSeconds);
            _pulseAmpTarget = enabled ? Math.Max(0.0, Math.Min(0.49, pulseAmplitude)) : 0.0;
            if (!_timer.IsEnabled) _timer.Start();
        }

        private void EnsureTransform()
        {
            ScaleTransform? tx = HeartShape.RenderTransform as ScaleTransform;
            if (tx == null)
            {
                tx = new ScaleTransform();
                tx.ScaleX = 1.0;
                tx.ScaleY = 1.0;
                HeartShape.RenderTransform = tx;
            }
            _scaleTransform = tx;
        }

        private void OnTick(Object? sender, EventArgs e)
        {
            DateTime now = DateTime.UtcNow;
            Double dt = (now - _lastTickUtc).TotalSeconds;
            if (dt > 0.05) dt = 0.05;
            _lastTickUtc = now;

            Double smooth = Math.Min(1.0, Math.Max(0.0, dt * 10.0));
            _baseAmpCurrent += (_baseAmpTarget - _baseAmpCurrent) * smooth;
            _pulseAmpCurrent += (_pulseAmpTarget - _pulseAmpCurrent) * smooth;

            Double sBase = 0.0;
            if (_basePeriodSeconds > 0.0001 && _baseAmpCurrent > 0.0)
            {
                Double omegaBase = (2.0 * Math.PI) / _basePeriodSeconds;
                _phaseBase += omegaBase * dt;
                if (_phaseBase > 2.0 * Math.PI) _phaseBase -= 2.0 * Math.PI;
                sBase = _baseAmpCurrent * Math.Sin(_phaseBase);
            }

            Double sPulse = 0.0;
            if (_pulseEnabled && _pulsePeriodSeconds > 0.0001 && _pulseAmpCurrent > 0.0)
            {
                Double omegaPulse = (2.0 * Math.PI) / _pulsePeriodSeconds;
                _phasePulse += omegaPulse * dt;
                if (_phasePulse > 2.0 * Math.PI) _phasePulse -= 2.0 * Math.PI;
                sPulse = _pulseAmpCurrent * Math.Sin(_phasePulse);
            }

            Double s = 1.0 + sBase + sPulse;
            SetScale(s);
        }

        private void SetScale(Double s)
        {
            EnsureTransform();
            _scaleTransform.ScaleX = s;
            _scaleTransform.ScaleY = s;
        }
    }
}