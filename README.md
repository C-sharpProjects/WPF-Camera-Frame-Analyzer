# WPF Camera Frame Analyzer

A modern WPF application demonstrating proper frame capture, processing, and real-time visualization from a webcam. 
[![Watch the video](https://img.youtube.com/vi/Trd2qrAyW7Q/sddefault.jpg)](https://www.youtube.com/watch?v=Trd2qrAyW7Q)

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=.net)
![WPF](https://img.shields.io/badge/WPF-Windows-0078D4?logo=windows)
![OpenCV](https://img.shields.io/badge/OpenCV-4.9-5C3EE8?logo=opencv)
![LiveCharts](https://img.shields.io/badge/LiveCharts-2.0-FF6384)

---

## 🎯 Overview

This project demonstrates the correct implementation of a real-time camera frame processing pipeline that:

1. **Captures frames** from a webcam at 30 FPS
2. **Calculates average pixel values** for each frame
3. **Streams results** to a real-time chart visualization
4. **Displays live camera feed** in a modern WPF interface

The application was built with best practices in:
- Thread-safe frame processing
- Native interop memory management
- Concurrent data structures
- Real-time data visualization
- Modern WPF UI design

---

## 🐛 The Problem

### Original  Code Issues

It was implemented the frame capture and processing system. Their implementation contained several **critical bugs**:

#### 1. **Data Corruption - Shared Buffer**
```csharp
// ❌ WRONG: All frames share the same buffer
private byte[] _buffer;
Frame bufferedFrame = new Frame(_buffer);  // Same reference for every frame!
```

**Issue**: When the next frame arrives, `Marshal.Copy` overwrites the buffer, corrupting all queued frames. Every frame in the queue ends up containing the most recent frame's data.

#### 2. **Premature Disposal**
```csharp
// ❌ WRONG: Disposed immediately after enqueueing
OnFrameUpdated(bufferedFrame);
bufferedFrame.Dispose();  // Marks as disposed, but still in queue!
```

**Issue**: When the timer thread later tries to process the frame, `GetRawData()` throws `ObjectDisposedException`.

#### 3. **Thread Safety Issues**
```csharp
// ❌ WRONG: No synchronization
private Queue<Frame> _receivedFrames = new Queue<Frame>();
// Accessed from both camera callback thread and timer thread
```

**Issue**: Race conditions, data corruption, and potential exceptions from concurrent access.

#### 4. **Unbounded Queue Growth**
```csharp
// ❌ WRONG: No backpressure mechanism
_receivedFrames.Enqueue(frame);  // Grows infinitely if processing is slow
```

**Issue**: At 30 FPS, the queue can grow indefinitely, causing memory exhaustion.

#### 5. **Misunderstood Frame Lifetime**
The intern didn't realize that `pFrame` pointer is reused by the native library immediately after the callback returns. The data **must be copied** during the callback, not just the pointer stored.

---

## ✅ The Solution

### Key Improvements

#### 1. **Independent Frame Buffers**
```csharp
// ✅ CORRECT: Each frame gets its own copy
public void FrameReceived(IntPtr pFrame, int width, int height)
{
    byte[] buffer = new byte[width * height];  // New buffer each time
    Marshal.Copy(pFrame, buffer, 0, width * height);
    OnFrameUpdated?.Invoke(buffer);
}
```

#### 2. **Thread-Safe Collections**
```csharp
// ✅ CORRECT: Thread-safe concurrent queue
private readonly ConcurrentQueue<byte[]> _receivedFrames = new ConcurrentQueue<byte[]>();
```

#### 3. **Backpressure Management**
```csharp
// ✅ CORRECT: Drop oldest frames if queue is full
if (_receivedFrames.Count >= _maxQueueSize)
{
    _receivedFrames.TryDequeue(out byte[]? _);  // Drop oldest
}
```

#### 4. **Async Processing**
```csharp
// ✅ CORRECT: Dedicated processing task instead of timer
private async Task ProcessFrames()
{
    while (!_cancellationTokenSource.Token.IsCancellationRequested)
    {
        if (_receivedFrames.TryDequeue(out byte[] frameData))
        {
            double average = CalculateAverage(frameData);
            _reporter.Report(average);
        }
        await Task.Delay(10, _cancellationTokenSource.Token);
    }
}
```

#### 5. **Proper Resource Management**
```csharp
// ✅ CORRECT: IDisposable with cancellation tokens
public void Dispose()
{
    _cancellationTokenSource.Cancel();
    _processingTask?.Wait(1000);
    _cancellationTokenSource.Dispose();
}
```

---

## 🚀 Features

### User Interface
- ✨ **Modern Material Design** UI with smooth shadows and animations
- 📹 **Live Camera Feed** with real-time video preview
- 📊 **Animated Real-time Chart** using LiveCharts
- 📈 **Statistics Dashboard** showing:
  - Frames processed count
  - Current average pixel value
  - Real-time FPS counter

### Technical Features
- 🎥 **OpenCV Integration** for cross-platform camera access
- 🔒 **Thread-Safe Processing** with concurrent collections
- 💾 **Proper Memory Management** with no buffer sharing
- ⚡ **High Performance** with configurable frame dropping
- 🎯 **Clean Architecture** following SOLID principles

---

## 📁 Project Structure

```
C:\Users\SESA845051\OneDrive - Schneider Electric\Coding\Formulatrix\C\
│
├── CameraMonitor.csproj          # Project file with dependencies
├── README.md                      # This file
│
├── App.xaml                       # Application resources & styling
├── App.xaml.cs                    # Application entry point
│
├── MainWindow.xaml                # Main UI layout
├── MainWindow.xaml.cs             # UI logic & chart management
│
├── ImprovedFrameGrabber.cs        # Core frame processing classes
│   ├── IFrameCallback             # Native callback interface
│   ├── IValueReporter             # Value reporting interface
│   ├── FrameCalculateAndStream    # Frame processor with thread safety
│   └── FrameGrabber               # Frame receiver from camera
│
└── WebCameraCapture.cs            # OpenCV camera integration
    └── WebCameraCapture           # Camera capture & display
```

### File Descriptions

| File | Purpose |
|------|---------|
| **CameraMonitor.csproj** | .NET 8 WPF project with NuGet dependencies (OpenCvSharp4, LiveCharts) |
| **App.xaml** | Global styles, colors, and resource dictionaries for Material Design look |
| **App.xaml.cs** | Application startup and initialization |
| **MainWindow.xaml** | XAML layout with camera feed, chart, and statistics panels |
| **MainWindow.xaml.cs** | UI event handlers, chart updates, FPS monitoring |
| **ImprovedFrameGrabber.cs** | Fixed implementation of frame processing pipeline with proper memory management |
| **WebCameraCapture.cs** | OpenCV-based camera capture that simulates native callback pattern |

---

## 🏁 Getting Started

### Prerequisites

- **Windows 10/11** (WPF requirement)
- **.NET 8.0 SDK** or later
- **Webcam** (built-in or USB)

### Installation

1. **Clone or navigate to the project directory:**
   ```powershell
   cd "c:\Users\path\Coding\Formulatrix\C"
   ```

2. **Restore NuGet packages:**
   ```powershell
   dotnet restore CameraMonitor.csproj
   ```

3. **Build the project:**
   ```powershell
   dotnet build CameraMonitor.csproj
   ```

4. **Run the application:**
   ```powershell
   dotnet run --project CameraMonitor.csproj
   ```

---

## 🏗️ Architecture

### Component Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                        MainWindow (WPF)                     │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐     │
│  │ Camera Feed  │  │  Statistics  │  │ Live Chart   │     │
│  └──────────────┘  └──────────────┘  └──────────────┘     │
└─────────────────────────────────────────────────────────────┘
         │                    │                    │
         ▼                    ▼                    ▼
┌─────────────────┐  ┌──────────────────────────────────────┐
│ WebCameraCapture│  │    ChartValueReporter (IValueReporter)│
│  (OpenCV)       │  └──────────────────────────────────────┘
└─────────────────┘           ▲
         │                    │
         │ OnFrameReady      │ Report(value)
         ▼                    │
┌─────────────────┐  ┌────────┴──────────────────────────────┐
│  FrameGrabber   │  │  FrameCalculateAndStream              │
│ (IFrameCallback)│──►  - ConcurrentQueue<byte[]>            │
└─────────────────┘  │  - Backpressure management            │
                     │  - Async processing task               │
                     │  - Average calculation                 │
                     └───────────────────────────────────────┘
```

### Data Flow

1. **WebCameraCapture** captures frames from webcam (30 FPS)
2. Converts to grayscale and invokes **OnFrameReady** callback
3. **FrameGrabber** receives pointer, creates **independent byte[] copy**
4. Raises **OnFrameUpdated** event with the copy
5. **FrameCalculateAndStream** enqueues frame (with backpressure)
6. Background task dequeues and calculates average
7. **ChartValueReporter** updates UI on main thread
8. **MainWindow** updates chart and statistics

### Threading Model

| Thread | Responsibility |
|--------|---------------|
| **Camera Thread** | Captures frames, converts to grayscale, copies data |
| **Processing Task** | Dequeues frames, calculates averages, reports values |
| **UI Thread** | Updates chart, statistics, and camera preview |

---

## 🔬 Technical Deep Dive

### Memory Management

#### The Problem: Shared Buffers
```csharp
// Intern's code - BROKEN
private byte[] _buffer;  // ⚠️ Reused for every frame

public void FrameReceived(IntPtr frame, int width, int height)
{
    if (_buffer == null)
        _buffer = new byte[width * height];
    
    Marshal.Copy(frame, _buffer, 0, width * height);
    Frame bufferedFrame = new Frame(_buffer);  // ❌ Same reference!
    OnFrameUpdated(bufferedFrame);
}
```

**Why it fails**: All `Frame` objects hold references to the **same** `_buffer` array. When the next frame arrives, `Marshal.Copy` overwrites this shared buffer, corrupting all previously queued frames.

#### The Solution: Independent Copies
```csharp
// Fixed code - CORRECT
public void FrameReceived(IntPtr pFrame, int width, int height)
{
    byte[] buffer = new byte[width * height];  // ✅ New array each time
    Marshal.Copy(pFrame, buffer, 0, width * height);
    OnFrameUpdated?.Invoke(buffer);  // Each event gets unique copy
}
```

### Thread Safety

#### The Problem: Non-Thread-Safe Collection
```csharp
// Intern's code - BROKEN
private Queue<Frame> _receivedFrames = new Queue<Frame>();

// Camera thread
_receivedFrames.Enqueue(frame);  // ⚠️ Write

// Timer thread
Frame frame = _receivedFrames.Dequeue();  // ⚠️ Read - RACE CONDITION!
```

#### The Solution: ConcurrentQueue
```csharp
// Fixed code - CORRECT
private readonly ConcurrentQueue<byte[]> _receivedFrames = new ConcurrentQueue<byte[]>();

// Multiple threads can safely access
_receivedFrames.Enqueue(frameData);  // ✅ Thread-safe
_receivedFrames.TryDequeue(out byte[] frameData);  // ✅ Thread-safe
```

### Backpressure Management

```csharp
// Prevent unbounded memory growth
private readonly int _maxQueueSize = 5;

private void HandleFrameUpdated(byte[] frameData)
{
    // Drop oldest frame if queue is full
    if (_receivedFrames.Count >= _maxQueueSize)
    {
        _receivedFrames.TryDequeue(out byte[]? _);
    }
    
    _receivedFrames.Enqueue(frameData);
}
```

**Benefits**:
- Prevents memory exhaustion
- Maintains low latency (processes recent frames)
- Graceful degradation under load

### Performance Optimizations

1. **Grayscale Conversion**: Only 1 byte per pixel instead of 3 (BGR)
2. **Frame Dropping**: Processes only what the system can handle
3. **Async Processing**: Non-blocking frame processing
4. **Chart Data Limiting**: Keeps only last 100 points for rendering

---

## 📸 Screenshots

### Main Interface
```
┌──────────────────────────────────────────────────────────────┐
│  Camera Frame Analyzer                    ▶ Start  ⏹ Stop    │
│  Real-time pixel average analysis from webcam                │
├──────────────────────────────────────────────────────────────┤
│  ┌────────────────┐   ┌─────────────────────────────────────┐│
│  │                │   │  Average Pixel Value Over Time      ││
│  │   Camera Feed  │   │                                     ││
│  │                │   │  ╭─────────────────────────────╮   ││
│  │   [Live Video] │   │  │     📈 Animated Chart      │   ││
│  │                │   │  │                             │   ││
│  │                │   │  ╰─────────────────────────────╯   ││
│  └────────────────┘   └─────────────────────────────────────┘│
│  ┌──────────────────────────────────────────────────────────┐│
│  │ FRAMES: 1,234  │  AVERAGE: 127.45  │  FPS: 29.8         ││
│  └──────────────────────────────────────────────────────────┘│
└──────────────────────────────────────────────────────────────┘
```

---

## 🎓 Learning Points

### For Junior Developers

1. **Always copy data from native memory immediately** - pointers can be reused
2. **Use thread-safe collections** when sharing data across threads
3. **Implement backpressure** to prevent unbounded queues
4. **Profile memory usage** in real-time scenarios
5. **Dispose resources properly** with cancellation tokens

### Design Patterns Used

- **Observer Pattern**: Event-based frame updates
- **Producer-Consumer**: Queue-based frame processing
- **Facade Pattern**: Simplified camera interface
- **Strategy Pattern**: Pluggable `IValueReporter`

### Best Practices Demonstrated

✅ Thread-safe concurrent collections  
✅ Async/await for non-blocking operations  
✅ IDisposable with proper cleanup  
✅ Separation of concerns (UI, processing, capture)  
✅ Event-driven architecture  
✅ MVVM-friendly design  
✅ Comprehensive error handling  

---

## 📦 Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| OpenCvSharp4 | 4.9.0 | Cross-platform computer vision library |
| OpenCvSharp4.runtime.win | 4.9.0 | Windows native libraries for OpenCV |
| LiveChartsCore.SkiaSharpView.WPF | 2.0.0-rc2 | Modern charting library with animations |

---
