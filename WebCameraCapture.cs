using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenCvSharp;

namespace Formulatrix.Intern.GrabTheFrame;

/// <summary>
/// Webcam capture using OpenCV that implements the native callback pattern
/// </summary>
public class WebCameraCapture : IDisposable
{
    private VideoCapture? _capture;
    private Thread? _captureThread;
    private bool _isRunning;
    private readonly int _cameraIndex;
    
    public delegate void FrameReadyHandler( IntPtr pFrame, int width, int height );
    public event FrameReadyHandler? OnFrameReady;
    
    public delegate void FrameDisplayHandler( WriteableBitmap bitmap );
    public event FrameDisplayHandler? OnFrameForDisplay;

    private WriteableBitmap? _writeableBitmap;
    private byte[]? _frameBuffer;

    public WebCameraCapture( int cameraIndex = 0 )
    {
        _cameraIndex = cameraIndex;
    }

    public void Start()
    {
        if( _isRunning )
            return;

        _capture = new VideoCapture( _cameraIndex );
        
        if( !_capture.IsOpened() )
        {
            throw new Exception( "Failed to open camera. Make sure a camera is connected." );
        }

        // Set camera properties for better performance
        _capture.Set( VideoCaptureProperties.FrameWidth, 640 );
        _capture.Set( VideoCaptureProperties.FrameHeight, 480 );
        _capture.Set( VideoCaptureProperties.Fps, 30 );

        _isRunning = true;
        _captureThread = new Thread( CaptureLoop )
        {
            IsBackground = true,
            Name = "Camera Capture Thread"
        };
        _captureThread.Start();
    }

    public void Stop()
    {
        _isRunning = false;
        _captureThread?.Join( 1000 );
        _capture?.Release();
    }

    private void CaptureLoop()
    {
        using Mat frame = new Mat();
        using Mat grayFrame = new Mat();
        
        while( _isRunning )
        {
            try
            {
                // Capture frame
                if( _capture == null || !_capture.Read( frame ) || frame.Empty() )
                {
                    Thread.Sleep( 10 );
                    continue;
                }

                // Convert to grayscale for processing
                Cv2.CvtColor( frame, grayFrame, ColorConversionCodes.BGR2GRAY );

                int width = grayFrame.Width;
                int height = grayFrame.Height;
                int size = width * height;

                // Ensure buffer is allocated
                if( _frameBuffer == null || _frameBuffer.Length != size )
                {
                    _frameBuffer = new byte[size];
                }

                // Copy grayscale data to buffer
                Marshal.Copy( grayFrame.Data, _frameBuffer, 0, size );

                // Pin the buffer and get pointer for callback
                GCHandle handle = GCHandle.Alloc( _frameBuffer, GCHandleType.Pinned );
                try
                {
                    IntPtr ptr = handle.AddrOfPinnedObject();
                    
                    // Invoke the callback (simulating native callback pattern)
                    OnFrameReady?.Invoke( ptr, width, height );
                }
                finally
                {
                    handle.Free();
                }

                // Update display (convert color frame for display)
                UpdateDisplay( frame );

                // Control frame rate
                Thread.Sleep( 33 ); // ~30 FPS
            }
            catch( Exception ex )
            {
                Console.WriteLine( $"Camera capture error: {ex.Message}" );
                Thread.Sleep( 100 );
            }
        }
    }

    private void UpdateDisplay( Mat frame )
    {
        int width = frame.Width;
        int height = frame.Height;

        // Create or reuse WritableBitmap
        if( _writeableBitmap == null || 
            _writeableBitmap.PixelWidth != width || 
            _writeableBitmap.PixelHeight != height )
        {
            Application.Current.Dispatcher.Invoke( () =>
            {
                _writeableBitmap = new WriteableBitmap( 
                    width, 
                    height, 
                    96, 
                    96, 
                    PixelFormats.Bgr24, 
                    null 
                );
            });
        }

        if( _writeableBitmap == null )
            return;

        Application.Current.Dispatcher.Invoke( () =>
        {
            _writeableBitmap.Lock();
            try
            {
                // Copy frame data to bitmap
                int stride = width * 3; // BGR24 format
                
                // Get bytes from Mat and copy to bitmap
                byte[] buffer = new byte[stride * height];
                Marshal.Copy( frame.Data, buffer, 0, buffer.Length );
                Marshal.Copy( buffer, 0, _writeableBitmap.BackBuffer, buffer.Length );

                _writeableBitmap.AddDirtyRect( 
                    new Int32Rect( 0, 0, width, height ) 
                );
            }
            finally
            {
                _writeableBitmap.Unlock();
            }

            OnFrameForDisplay?.Invoke( _writeableBitmap );
        });
    }

    public void Dispose()
    {
        Stop();
        _capture?.Dispose();
    }
}
