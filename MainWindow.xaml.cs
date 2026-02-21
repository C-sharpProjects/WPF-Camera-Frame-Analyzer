using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace Formulatrix.Intern.GrabTheFrame;

public partial class MainWindow : Window
{
    private WebCameraCapture? _cameraCapture;
    private FrameGrabber? _frameGrabber;
    private FrameCalculateAndStream? _frameProcessor;
    private ChartValueReporter? _valueReporter;
    
    private readonly DispatcherTimer _fpsTimer;
    private int _frameCount;
    private readonly Stopwatch _fpsStopwatch = new Stopwatch();
    
    private readonly ObservableCollection<ObservableValue> _observableValues;
    private int _dataPointCount = 0;
    private const int MaxDataPoints = 100;

    public ObservableCollection<ISeries> Series { get; set; }
    public Axis[] XAxes { get; set; }
    public Axis[] YAxes { get; set; }

    public MainWindow()
    {
        InitializeComponent();
        
        // Initialize chart data
        _observableValues = new ObservableCollection<ObservableValue>();
        
        Series = new ObservableCollection<ISeries>
        {
            new LineSeries<ObservableValue>
            {
                Values = _observableValues,
                Fill = null,
                GeometrySize = 0,
                LineSmoothness = 0.5,
                Stroke = new SolidColorPaint(SKColors.DeepSkyBlue) { StrokeThickness = 3 },
                Name = "Pixel Average"
            }
        };

        XAxes = new Axis[]
        {
            new Axis
            {
                Name = "Time",
                NamePaint = new SolidColorPaint(SKColors.Gray),
                LabelsPaint = new SolidColorPaint(SKColors.Gray),
                TextSize = 12,
                MinLimit = 0,
                MaxLimit = MaxDataPoints
            }
        };

        YAxes = new Axis[]
        {
            new Axis
            {
                Name = "Average Value (0-255)",
                NamePaint = new SolidColorPaint(SKColors.Gray),
                LabelsPaint = new SolidColorPaint(SKColors.Gray),
                TextSize = 12,
                MinLimit = 0,
                MaxLimit = 255
            }
        };

        Chart.DataContext = this;

        // FPS Timer
        _fpsTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _fpsTimer.Tick += UpdateFps;
    }

    private void StartButton_Click( object sender, RoutedEventArgs e )
    {
        try
        {
            // Initialize camera capture
            _cameraCapture = new WebCameraCapture( 0 ); // Use default camera
            
            // Initialize frame grabber
            _frameGrabber = new FrameGrabber();
            
            // Initialize value reporter
            _valueReporter = new ChartValueReporter( this );
            
            // Initialize frame processor
            _frameProcessor = new FrameCalculateAndStream( _frameGrabber, _valueReporter );
            
            // Connect camera to frame grabber
            _cameraCapture.OnFrameReady += _frameGrabber.FrameReceived;
            _cameraCapture.OnFrameForDisplay += UpdateCameraImage;
            
            // Start processing
            _cameraCapture.Start();
            _frameProcessor.StartStreaming();
            
            // UI Updates
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            NoCameraOverlay.Visibility = Visibility.Collapsed;
            
            // Start FPS monitoring
            _fpsStopwatch.Restart();
            _fpsTimer.Start();
            
            // Reset stats
            _frameCount = 0;
            _dataPointCount = 0;
            _observableValues.Clear();
        }
        catch( Exception ex )
        {
            MessageBox.Show( $"Failed to start camera: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error );
        }
    }

    private void StopButton_Click( object sender, RoutedEventArgs e )
    {
        StopCamera();
    }

    private void StopCamera()
    {
        _fpsTimer.Stop();
        _fpsStopwatch.Stop();
        
        _cameraCapture?.Stop();
        _frameProcessor?.Dispose();
        _cameraCapture?.Dispose();
        
        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
        NoCameraOverlay.Visibility = Visibility.Visible;
    }

    private void UpdateCameraImage( WriteableBitmap bitmap )
    {
        Dispatcher.Invoke( () =>
        {
            CameraImage.Source = bitmap;
            _frameCount++;
        });
    }

    private void UpdateFps( object? sender, EventArgs e )
    {
        double fps = _frameCount / _fpsStopwatch.Elapsed.TotalSeconds;
        FpsText.Text = $"{fps:F1}";
        _frameCount = 0;
        _fpsStopwatch.Restart();
    }

    public void UpdateChart( double value )
    {
        Dispatcher.Invoke( () =>
        {
            // Update current average display
            CurrentAverageText.Text = $"{value:F2}";
            
            // Update frames processed
            FramesProcessedText.Text = _dataPointCount.ToString();
            
            // Add to chart
            _observableValues.Add( new ObservableValue( value ) );
            
            // Keep only last N points for performance
            if( _observableValues.Count > MaxDataPoints )
            {
                _observableValues.RemoveAt( 0 );
            }
            
            _dataPointCount++;
        });
    }

    protected override void OnClosed( EventArgs e )
    {
        StopCamera();
        base.OnClosed( e );
    }
}

/// <summary>
/// Value reporter that updates the WPF chart
/// </summary>
public class ChartValueReporter : IValueReporter
{
    private readonly MainWindow _window;

    public ChartValueReporter( MainWindow window )
    {
        _window = window;
    }

    public void Report( double value )
    {
        _window.UpdateChart( value );
    }
}
