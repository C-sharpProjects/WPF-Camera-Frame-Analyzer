using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Formulatrix.Intern.GrabTheFrame;

public interface IFrameCallback
{
    public void FrameReceived( IntPtr pFrame, int pixelWidth, int pixelHeight );
}

public interface IValueReporter
{
    public void Report( double value );
}

/// <summary>
/// Improved implementation with proper memory management and thread safety
/// </summary>
public class FrameCalculateAndStream : IDisposable
{
    private readonly IValueReporter _reporter;
    private readonly ConcurrentQueue<byte[]> _receivedFrames = new ConcurrentQueue<byte[]>();
    private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    private readonly int _maxQueueSize = 5; // Prevent unbounded growth
    private Task? _processingTask;

    public FrameCalculateAndStream( FrameGrabber fg, IValueReporter vr )
    {
        fg.OnFrameUpdated += HandleFrameUpdated;
        _reporter = vr;
    }

    private void HandleFrameUpdated( byte[] frameData )
    {
        // Drop frames if queue is full (backpressure)
        if( _receivedFrames.Count >= _maxQueueSize )
        {
            _receivedFrames.TryDequeue( out byte[]? _ ); // Drop oldest frame
        }

        _receivedFrames.Enqueue( frameData );
    }

    public void StartStreaming()
    {
        _processingTask = Task.Run( ProcessFrames, _cancellationTokenSource.Token );
    }

    private async Task ProcessFrames()
    {
        while( !_cancellationTokenSource.Token.IsCancellationRequested )
        {
            if( _receivedFrames.TryDequeue( out byte[] frameData ) )
            {
                double average = CalculateAverage( frameData );
                _reporter.Report( average );
            }
            else
            {
                // No frames available, wait a bit
                await Task.Delay( 10, _cancellationTokenSource.Token );
            }
        }
    }

    private double CalculateAverage( byte[] data )
    {
        long sum = 0;
        for( int i = 0; i < data.Length; i++ )
        {
            sum += data[i];
        }
        return (double)sum / data.Length;
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _processingTask?.Wait( 1000 );
        _cancellationTokenSource.Dispose();
    }
}

/// <summary>
/// Improved FrameGrabber with proper memory copying
/// </summary>
public class FrameGrabber : IFrameCallback
{
    public delegate void FrameUpdateHandler( byte[] frameData );
    public event FrameUpdateHandler? OnFrameUpdated;

    public void FrameReceived( IntPtr pFrame, int width, int height )
    {
        // Create a NEW buffer for each frame (critical fix)
        byte[] buffer = new byte[width * height];
        
        // Copy data from native memory immediately
        Marshal.Copy( pFrame, buffer, 0, width * height );

        // Pass the independent copy to listeners
        OnFrameUpdated?.Invoke( buffer );
    }
}
