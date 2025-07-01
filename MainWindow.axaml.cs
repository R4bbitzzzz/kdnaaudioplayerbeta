using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks; // Added this missing namespace
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LibVLCSharp.Shared;
using System.Threading.Tasks;
namespace musicplayC
{
    public partial class MainWindow : Window, IDisposable
    {
        private LibVLC? _libVLC;
        private MediaPlayer? _mediaPlayer;
        private DispatcherTimer? _visualizerTimer;
        private DispatcherTimer? _positionTimer;
        private readonly List<Rectangle> _bars = new();
        private readonly List<double> _barHeights = new();
        private readonly Random _rnd = new();
        private string? _filePath;
        private bool _isUserDraggingSlider;
        private bool _shouldUpdatePosition = true;

        public MainWindow()
        {
            InitializeComponent();
            this.Opened += Window_Opened;
        }

        private void Window_Opened(object? sender, EventArgs e)
        {
            Core.Initialize();
            _libVLC = new LibVLC("--no-video");
            _mediaPlayer = new MediaPlayer(_libVLC);

            SetupTimers();
            SetupVisualizer();
            ResetPlayerState();
        }

        private void SetupTimers()
        {
            _visualizerTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _visualizerTimer.Tick += (_, _) => UpdateVisualizer();

            _positionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _positionTimer.Tick += (_, _) => UpdatePosition();
        }

        private void ResetPlayerState()
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                PositionSlider.Value = 0;
                TimeCounter.Text = "00:00 / --:--";
            });
        }

        private async void OpenFile_Click(object? sender, RoutedEventArgs e)
        {
            var options = new FilePickerOpenOptions
            {
                Title = "Select audio file",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new("Audio files") { Patterns = new[] { "*.mp3", "*.wav", "*.aac", "*.ogg" } }
                }
            };

            var result = await StorageProvider.OpenFilePickerAsync(options);

            if (result.Count > 0 && result[0] is IStorageFile file)
            {
                _filePath = file.Path.LocalPath;
                TitleBlock.Text = System.IO.Path.GetFileName(_filePath);
                await PlayAudio(_filePath);
            }
        }

private async Task PlayAudio(string filePath)
{
    try
    {
        if (_libVLC == null || _mediaPlayer == null)
        {
            Console.WriteLine("LibVLC or MediaPlayer not initialized.");
            return;
        }

        _mediaPlayer.Stop();
        if (_mediaPlayer.Media != null)
        {
            _mediaPlayer.Media.Dispose();
        }

        using var media = new Media(_libVLC, filePath);
        _mediaPlayer.Media = media;
        _mediaPlayer.Play();

        await Task.Delay(100); // Kis késleltetés a média inicializálására
        if (!_mediaPlayer.IsSeekable)
        {
            Console.WriteLine("Warning: This media is not seekable.");
        }

        _positionTimer?.Start();
        _visualizerTimer?.Start();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Playback error: {ex.Message}");
    }
}

        private void PlayButton_Click(object? sender, RoutedEventArgs e) => _mediaPlayer?.Play();
        private void PauseButton_Click(object? sender, RoutedEventArgs e) => _mediaPlayer?.Pause();
        private void StopButton_Click(object? sender, RoutedEventArgs e)
        {
            _mediaPlayer?.Stop();
            ResetPlayerState();
        }

        private void PositionSlider_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            _isUserDraggingSlider = true;
            _shouldUpdatePosition = false;
        }

        private void PositionSlider_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_isUserDraggingSlider && _mediaPlayer != null && _mediaPlayer.Media != null && _mediaPlayer.IsSeekable)
            {
                // Számoljuk ki az időt milliszekundumban
                long totalDuration = _mediaPlayer.Length; // Teljes időtartam ms-ban
                double sliderValue = PositionSlider.Value; // 0-100 között
                long seekTime = (long)((sliderValue / 100.0) * totalDuration);
                _mediaPlayer.Time = seekTime;
                Console.WriteLine($"Seek to time: {seekTime}ms, Actual time: {_mediaPlayer.Time}ms, Position: {_mediaPlayer.Position}");
            }
            _isUserDraggingSlider = false;
            _shouldUpdatePosition = true;
        }

private void PositionSlider_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
{
    if (_isUserDraggingSlider && _mediaPlayer != null && _mediaPlayer.Media != null && _mediaPlayer.IsSeekable)
    {
        float newPosition = (float)(e.NewValue / 100.0);
        _mediaPlayer.Position = newPosition;
        Console.WriteLine($"Seek to position (ValueChanged): {newPosition}, Actual position: {_mediaPlayer.Position}, Time: {_mediaPlayer.Time}ms");
    }
}

private void UpdatePosition()
{
    if (_mediaPlayer == null || !_mediaPlayer.IsPlaying || !_shouldUpdatePosition || _isUserDraggingSlider)
        return;

    Dispatcher.UIThread.InvokeAsync(() =>
    {
        var newValue = _mediaPlayer.Position * 100;
        if (Math.Abs(PositionSlider.Value - newValue) > 1)
        {
            PositionSlider.Value = newValue;
        }

        var current = TimeSpan.FromMilliseconds(_mediaPlayer.Time);
        var total = TimeSpan.FromMilliseconds(_mediaPlayer.Length);
        TimeCounter.Text = $"{current:mm\\:ss} / {total:mm\\:ss}";
    });
}

        private void SetupVisualizer()
        {
            const int barCount = 30;
            VisualizerPanel.Children.Clear();
            _bars.Clear();
            _barHeights.Clear();

            for (int i = 0; i < barCount; i++)
            {
                var bar = new Rectangle
                {
                    Width = 15,
                    Height = 10,
                    Margin = new Thickness(2, 0, 2, 0),
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Fill = new SolidColorBrush(GetRandomColor()),
                    RadiusX = 3,
                    RadiusY = 3
                };

                VisualizerPanel.Children.Add(bar);
                _bars.Add(bar);
                _barHeights.Add(10);
            }
        }

        private void UpdateVisualizer()
        {
            if (_mediaPlayer != null && _mediaPlayer.IsPlaying)
            {
                const int minHeight = 20;
                const int maxHeight = 150;
                const double smoothing = 0.15;

                for (int i = 0; i < _bars.Count; i++)
                {
                    int targetHeight = minHeight + _rnd.Next(maxHeight - minHeight);
                    _barHeights[i] = _barHeights[i] + smoothing * (targetHeight - _barHeights[i]);

                    _bars[i].Height = _barHeights[i];
                    _bars[i].Fill = new SolidColorBrush(GetColorFromHeight((int)_barHeights[i], maxHeight));
                }
            }
            else
            {
                const double smoothing = 0.1;
                const int baseHeight = 10;
                for (int i = 0; i < _bars.Count; i++)
                {
                    _barHeights[i] = _barHeights[i] + smoothing * (baseHeight - _barHeights[i]);
                    _bars[i].Height = _barHeights[i];
                    _bars[i].Fill = Brushes.Gray;
                }
            }
        }

        private Color GetRandomColor() => Color.FromRgb(
            (byte)_rnd.Next(100, 256),
            (byte)_rnd.Next(100, 256),
            (byte)_rnd.Next(100, 256));

        private Color GetColorFromHeight(int height, int maxHeight)
        {
            double ratio = (double)height / maxHeight;
            if (ratio < 0.5)
            {
                byte r = (byte)(ratio * 2 * 255);
                return Color.FromRgb(r, 255, 0);
            }
            else
            {
                byte g = (byte)((1 - (ratio - 0.5) * 2) * 255);
                return Color.FromRgb(255, g, 0);
            }
        }

        public void Dispose()
        {
            _mediaPlayer?.Dispose();
            _libVLC?.Dispose();
            _visualizerTimer?.Stop();
            _positionTimer?.Stop();
        }
    }
}