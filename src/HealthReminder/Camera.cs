using System.Globalization;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace Strongthium.HealthReminder;

/// <summary>
/// A webcam, through Windows' Media Foundation: a source reader gives its frames as 32-bit RGB, each frame gets the
/// date and time (and what is being taken) drawn on it, and while recording a sink writer encodes them to an H.264 MP4.
/// The frames are also kept for the preview. Everything Media Foundation is done on the camera's own thread.
/// </summary>
internal sealed unsafe partial class Camera : IDisposable
{
    private readonly Thread thread;
    private readonly string name;
    private readonly string caption;
    private readonly Lock frameLock = new();
    private volatile bool stopping;
    private string? recordTo;
    private TaskCompletionSource<string?>? stopRecording;
    private Frame? frame;
    private long recordingStart = -1;

    /// <summary>The camera is running; <see cref="Width"/> and <see cref="Height"/> are known.</summary>
    public bool Ready { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    /// <summary>Counts the frames; the preview copies one when it has changed.</summary>
    public long FrameNumber { get; private set; }
    public bool Recording => recordTo is not null;
    public TimeSpan Recorded { get; private set; }
    /// <summary>Why the camera stopped, if it failed.</summary>
    public string? Error { get; private set; }
    public string CameraName { get; private set; } = "";

    /// <param name="name">The camera to use, by name; the first one if empty or not found.</param>
    /// <param name="caption">What the video is of, drawn under the date and time.</param>
    public Camera(string name, string caption)
    {
        this.name = name;
        this.caption = caption;
        thread = new Thread(Run) { IsBackground = true, Name = "health-reminder camera" };
        thread.SetApartmentState(ApartmentState.MTA);
        thread.Start();
    }

    /// <summary>The names of the webcams.</summary>
    public static Task<List<string>> NamesAsync() => Task.Run(() =>
    {
        CoInitializeEx(IntPtr.Zero, 0);
        Check(MFStartup(MfVersion, MfStartupNoSocket), "MFStartup");
        try
        {
            var (list, count) = EnumerateCameras();
            var activates = (IntPtr*)list;
            var names = new List<string>();
            for (var i = 0; i < count; i++)
            {
                names.Add(FriendlyName(activates[i]));
                Release(activates[i]);
            }
            CoTaskMemFree((IntPtr)activates);
            return names;
        }
        finally
        {
            MFShutdown();
        }
    });

    /// <summary>Starts writing to <paramref name="path"/>, an .mp4.</summary>
    public void StartRecording(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        recordTo = path;
    }

    /// <summary>Stops recording; the result is the file once it is complete, or null if nothing was recorded.</summary>
    public Task<string?> StopRecordingAsync()
    {
        var done = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (recordTo is null || !thread.IsAlive) done.SetResult(null);
        else stopRecording = done;
        return done.Task;
    }

    /// <summary>Copies the latest frame (BGRA, top row first) to <paramref name="target"/>.</summary>
    public void CopyFrame(IntPtr target, int targetStride)
    {
        lock (frameLock)
        {
            if (frame is null) return;
            var rowBytes = Width * 4;
            for (var y = 0; y < Height; y++)
                Buffer.MemoryCopy((byte*)frame.GetPixels() + y * frame.RowBytes, (byte*)target + y * targetStride, targetStride, rowBytes);
        }
    }

    public void Dispose()
    {
        stopping = true;
        thread.Join(TimeSpan.FromSeconds(10));
    }

    private void Run()
    {
        CoInitializeEx(IntPtr.Zero, 0);
        IntPtr source = 0, reader = 0, writer = 0;
        var started = false;
        try
        {
            Check(MFStartup(MfVersion, MfStartupNoSocket), "MFStartup");
            started = true;
            (source, reader) = OpenCamera();
            var stream = 0u;
            var frameDuration = 333_333L;
            using var font = new Font("Segoe UI", Math.Max(14, Height * 0.05f), FontStyle.Bold, GraphicsUnit.Pixel);
            using var small = new Font("Segoe UI", Math.Max(11, Height * 0.035f), FontStyle.Regular, GraphicsUnit.Pixel);
            lock (frameLock) frame = new Frame(Width, Height);
            Ready = true;

            while (!stopping)
            {
                if (writer == 0 && recordTo is { } path)
                {
                    writer = CreateWriter(path, out stream, out frameDuration);
                    recordingStart = -1;
                }
                if (writer != 0 && (stopRecording is not null || stopping))
                {
                    FinishWriter(ref writer);
                    var finished = recordTo;
                    recordTo = null;
                    stopRecording?.TrySetResult(finished);
                    stopRecording = null;
                    continue;
                }

                uint actual, flags;
                long time;
                IntPtr sample;
                Check(Call<uint, uint, IntPtr, IntPtr, IntPtr, IntPtr>(reader, 9, FirstVideoStream, 0, (IntPtr)(&actual), (IntPtr)(&flags), (IntPtr)(&time), (IntPtr)(&sample)), "ReadSample");
                if ((flags & EndOfStream) != 0) throw new IOException("The camera stopped.");
                if (sample == 0) continue;
                try
                {
                    lock (frameLock)
                    {
                        CopyIn(sample, frame!);
                        Overlay(frame!, font, small, writer != 0 ? Recorded : null);
                        FrameNumber++;
                    }
                    if (writer != 0)
                    {
                        if (recordingStart < 0) recordingStart = time;
                        Recorded = TimeSpan.FromTicks(time - recordingStart);
                        WriteFrame(writer, stream, frame!, time - recordingStart, frameDuration);
                    }
                }
                finally
                {
                    Release(sample);
                }
            }
        }
        catch (Exception exception) when (exception is COMException or IOException or InvalidOperationException or DllNotFoundException or EntryPointNotFoundException or UnauthorizedAccessException)
        {
            Error = exception is COMException com ? Describe(com) : exception.Message;
            stopRecording?.TrySetResult(null);
        }
        finally
        {
            if (writer != 0)
            {
                try
                {
                    FinishWriter(ref writer);
                }
                catch (COMException)
                {
                    Release(writer);
                }
            }
            Release(reader);
            if (source != 0)
            {
                Call(source, 12);   // IMFMediaSource::Shutdown
                Release(source);
            }
            if (started) MFShutdown();
            lock (frameLock)
            {
                frame?.Dispose();
                frame = null;
            }
            Ready = false;
        }
    }

    private static string Describe(COMException exception) => exception.HResult switch
    {
        unchecked((int)0x80070005) => "Windows didn't allow the camera. Turn on Settings > Privacy & security > Camera > Let desktop apps access your camera.",
        unchecked((int)0xC00D3704) or unchecked((int)0xC00DABE4) => "The camera is being used by another program.",
        unchecked((int)0xC00D36B3) => "The camera doesn't give a picture Windows can convert.",
        _ => $"{exception.Message} (0x{exception.HResult:X8})",
    };

    // ---- Opening the camera -------------------------------------------------------------------------------------

    private static (IntPtr List, uint Count) EnumerateCameras()
    {
        IntPtr attributes;
        Check(MFCreateAttributes(out attributes, 1), "MFCreateAttributes");
        try
        {
            var key = DevSourceType;
            var value = DevSourceVideoCapture;
            Check(Call<IntPtr, IntPtr>(attributes, 24, (IntPtr)(&key), (IntPtr)(&value)), "SetGUID");
            Check(MFEnumDeviceSources(attributes, out var activates, out var count), "MFEnumDeviceSources");
            return (activates, count);
        }
        finally
        {
            Release(attributes);
        }
    }

    private static string FriendlyName(IntPtr activate)
    {
        var key = DevFriendlyName;
        char* text;
        uint length;
        if (Call<IntPtr, IntPtr, IntPtr>(activate, 13, (IntPtr)(&key), (IntPtr)(&text), (IntPtr)(&length)) < 0) return "Camera";
        var result = new string(text, 0, (int)length);
        CoTaskMemFree((IntPtr)text);
        return result;
    }

    private (IntPtr Source, IntPtr Reader) OpenCamera()
    {
        var (list, count) = EnumerateCameras();
        var activates = (IntPtr*)list;
        if (count == 0)
        {
            CoTaskMemFree((IntPtr)activates);
            throw new InvalidOperationException("No camera was found. Plug in a webcam, or check that Windows lets desktop apps use it.");
        }
        IntPtr source = 0;
        try
        {
            var index = 0;
            for (var i = 0; i < count; i++)
                if (FriendlyName(activates[i]) == name) index = i;
            CameraName = FriendlyName(activates[index]);
            var iid = IidMediaSource;
            Check(Call<IntPtr, IntPtr>(activates[index], 33, (IntPtr)(&iid), (IntPtr)(&source)), "ActivateObject");
        }
        finally
        {
            for (var i = 0; i < count; i++) Release(activates[i]);
            CoTaskMemFree((IntPtr)activates);
        }

        IntPtr attributes, reader;
        Check(MFCreateAttributes(out attributes, 1), "MFCreateAttributes");
        var processing = SourceReaderEnableVideoProcessing;
        Check(Call<IntPtr, uint>(attributes, 21, (IntPtr)(&processing), 1), "SetUINT32");
        var hr = MFCreateSourceReaderFromMediaSource(source, attributes, out reader);
        Release(attributes);
        Check(hr, "MFCreateSourceReaderFromMediaSource");

        // The camera's own format closest to 1280 × 720, at 15 frames a second or more, then the same as RGB32.
        IntPtr best = 0;
        var bestScore = long.MinValue;
        for (uint i = 0; ; i++)
        {
            IntPtr type;
            if (Call<uint, uint, IntPtr>(reader, 5, FirstVideoStream, i, (IntPtr)(&type)) < 0) break;
            var (w, h) = Split(GetUInt64(type, FrameSizeKey));
            var (num, den) = Split(GetUInt64(type, FrameRateKey));
            var fps = den == 0 ? 0 : (double)num / den;
            var score = (w <= 1280 && h <= 720 ? (long)w * h : -(long)w * h) + (fps >= 15 ? 1L << 40 : 0) + (long)Math.Min(fps, 30);
            if (score > bestScore)
            {
                Release(best);
                (best, bestScore) = (type, score);
            }
            else
            {
                Release(type);
            }
        }
        if (best == 0) throw new InvalidOperationException("The camera reports no picture formats.");
        Check(Call<uint, IntPtr, IntPtr>(reader, 7, FirstVideoStream, 0, best), "SetCurrentMediaType (camera)");
        var size = GetUInt64(best, FrameSizeKey);
        var rate = GetUInt64(best, FrameRateKey);
        Release(best);

        Check(MFCreateMediaType(out var rgb), "MFCreateMediaType");
        try
        {
            SetGuid(rgb, MajorTypeKey, MediaTypeVideo);
            SetGuid(rgb, SubtypeKey, VideoFormatRgb32);
            SetUInt64(rgb, FrameSizeKey, size);
            if (rate != 0) SetUInt64(rgb, FrameRateKey, rate);
            Check(Call<uint, IntPtr, IntPtr>(reader, 7, FirstVideoStream, 0, rgb), "SetCurrentMediaType (RGB32)");
        }
        finally
        {
            Release(rgb);
        }
        IntPtr current;
        Check(Call<uint, IntPtr>(reader, 6, FirstVideoStream, (IntPtr)(&current)), "GetCurrentMediaType");
        (var width, var height) = Split(GetUInt64(current, FrameSizeKey));
        frameRate = GetUInt64(current, FrameRateKey);
        uint stride = 0;
        var strideKey = DefaultStrideKey;
        if (Call<IntPtr, IntPtr>(current, 7, (IntPtr)(&strideKey), (IntPtr)(&stride)) >= 0) defaultStride = (int)stride;
        Release(current);
        // H.264 wants even sizes.
        Width = (int)width & ~1;
        Height = (int)height & ~1;
        if (Width <= 0 || Height <= 0) throw new InvalidOperationException("The camera reports no picture size.");
        return (source, reader);
    }

    private ulong frameRate;
    /// <summary>The row order the reader declares: negative is bottom-up; 0 when it doesn't say.</summary>
    private int defaultStride;

    // ---- Each frame ------------------------------------------------------------------------------------------------

    /// <summary>Copies a sample's picture into <paramref name="target"/>, top row first, whichever way up it came.</summary>
    private void CopyIn(IntPtr sample, Frame target)
    {
        IntPtr buffer;
        Check(Call<uint, IntPtr>(sample, 40, 0, (IntPtr)(&buffer)), "GetBufferByIndex");
        try
        {
            var iid = Iid2DBuffer;
            IntPtr buffer2d;
            byte* row0;
            int pitch;
            if (Call<IntPtr, IntPtr>(buffer, 0, (IntPtr)(&iid), (IntPtr)(&buffer2d)) >= 0)
            {
                try
                {
                    Check(Call<IntPtr, IntPtr>(buffer2d, 3, (IntPtr)(&row0), (IntPtr)(&pitch)), "Lock2D");
                    CopyRows(row0, pitch, target);
                    Call(buffer2d, 4);
                }
                finally
                {
                    Release(buffer2d);
                }
                return;
            }
            uint max, length;
            Check(Call<IntPtr, IntPtr, IntPtr>(buffer, 3, (IntPtr)(&row0), (IntPtr)(&max), (IntPtr)(&length)), "Lock");
            // Without IMF2DBuffer the declared stride says which way up it is. The source reader's video processor
            // gives top-down RGB32 when it doesn't say (seen with a Logitech BRIO).
            var rows = (int)(length / 4 / (uint)Math.Max(1, target.Width));
            pitch = (int)(length / (uint)Math.Max(1, rows));
            if (defaultStride < 0) CopyRows(row0 + (long)pitch * (rows - 1), -pitch, target);
            else CopyRows(row0, pitch, target);
            Call(buffer, 4);
        }
        finally
        {
            Release(buffer);
        }
    }

    private static void CopyRows(byte* row0, int pitch, Frame target)
    {
        var pixels = (byte*)target.GetPixels();
        for (var y = 0; y < target.Height; y++)
            Buffer.MemoryCopy(row0 + (long)y * pitch, pixels + (long)y * target.RowBytes, target.RowBytes, target.Width * 4);
    }

    /// <summary>The evidence on the picture: the date and time with the UTC offset, what is being taken, and while
    /// recording a red dot and the time recorded.</summary>
    private void Overlay(Frame bitmap, Font font, Font small, TimeSpan? recorded)
    {
        using var canvas = Graphics.FromImage(bitmap.Bitmap);
        canvas.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        canvas.SmoothingMode = SmoothingMode.AntiAlias;
        var now = DateTimeOffset.Now;
        var offset = now.Offset;
        var stamp = now.ToString("yyyy-MM-dd  HH:mm:ss", CultureInfo.InvariantCulture)
            + $"  UTC{(offset < TimeSpan.Zero ? "−" : "+")}{offset.Duration():hh\\:mm}";
        var band = font.Size * 1.5f + small.Size * 1.6f;
        using var shade = new SolidBrush(Color.FromArgb(150, 0, 0, 0));
        using var red = new SolidBrush(Color.FromArgb(0xE5, 0x39, 0x35));
        canvas.FillRectangle(shade, 0, bitmap.Height - band, bitmap.Width, band);
        var margin = small.Size;
        // DrawString places the top of the text, with a little space above the letters.
        canvas.DrawString(stamp, font, Brushes.White, margin * 0.8f, bitmap.Height - band + font.Size * 0.15f);
        canvas.DrawString(caption, small, Brushes.White, margin * 0.8f, bitmap.Height - small.Size * 1.55f);
        if (recorded is { } elapsed)
        {
            canvas.FillEllipse(red, margin, margin, small.Size, small.Size);
            canvas.DrawString($"REC {elapsed:m\\:ss}", small, Brushes.White, margin + small.Size * 1.3f, margin - small.Size * 0.15f);
        }
    }

    // ---- Writing the MP4 -----------------------------------------------------------------------------------------

    private IntPtr CreateWriter(string path, out uint stream, out long frameDuration)
    {
        var (num, den) = Split(frameRate);
        if (num == 0 || den == 0) (num, den) = (30, 1);
        frameDuration = (long)(10_000_000.0 * den / num);

        IntPtr attributes, writer;
        Check(MFCreateAttributes(out attributes, 1), "MFCreateAttributes");
        var hardware = ReadWriteEnableHardwareTransforms;
        Call<IntPtr, uint>(attributes, 21, (IntPtr)(&hardware), 1);
        var hr = MFCreateSinkWriterFromURL(path, 0, attributes, out writer);
        Release(attributes);
        Check(hr, "MFCreateSinkWriterFromURL");

        IntPtr output = 0, input = 0;
        try
        {
            Check(MFCreateMediaType(out output), "MFCreateMediaType");
            SetGuid(output, MajorTypeKey, MediaTypeVideo);
            SetGuid(output, SubtypeKey, VideoFormatH264);
            SetUInt32(output, AvgBitrateKey, (uint)Math.Clamp(Width * Height * 3, 1_000_000, 8_000_000));
            SetUInt32(output, InterlaceModeKey, 2);   // progressive
            SetUInt64(output, FrameSizeKey, Join((uint)Width, (uint)Height));
            SetUInt64(output, FrameRateKey, Join(num, den));
            SetUInt64(output, PixelAspectRatioKey, Join(1, 1));
            uint index;
            Check(Call<IntPtr, IntPtr>(writer, 3, output, (IntPtr)(&index)), "AddStream");
            stream = index;

            Check(MFCreateMediaType(out input), "MFCreateMediaType");
            SetGuid(input, MajorTypeKey, MediaTypeVideo);
            SetGuid(input, SubtypeKey, VideoFormatRgb32);
            SetUInt32(input, InterlaceModeKey, 2);
            SetUInt32(input, DefaultStrideKey, (uint)(Width * 4));   // top row first
            SetUInt64(input, FrameSizeKey, Join((uint)Width, (uint)Height));
            SetUInt64(input, FrameRateKey, Join(num, den));
            SetUInt64(input, PixelAspectRatioKey, Join(1, 1));
            Check(Call<uint, IntPtr, IntPtr>(writer, 4, stream, input, 0), "SetInputMediaType");
            Check(Call(writer, 5), "BeginWriting");
            return writer;
        }
        catch
        {
            Release(writer);
            throw;
        }
        finally
        {
            Release(output);
            Release(input);
        }
    }

    private static void WriteFrame(IntPtr writer, uint stream, Frame bitmap, long time, long duration)
    {
        var length = (uint)(bitmap.Width * bitmap.Height * 4);
        IntPtr buffer = 0, sample = 0;
        try
        {
            Check(MFCreateMemoryBuffer(length, out buffer), "MFCreateMemoryBuffer");
            byte* data;
            uint max, current;
            Check(Call<IntPtr, IntPtr, IntPtr>(buffer, 3, (IntPtr)(&data), (IntPtr)(&max), (IntPtr)(&current)), "Lock");
            var pixels = (byte*)bitmap.GetPixels();
            for (var y = 0; y < bitmap.Height; y++)
                Buffer.MemoryCopy(pixels + (long)y * bitmap.RowBytes, data + (long)y * bitmap.Width * 4, bitmap.Width * 4, bitmap.Width * 4);
            Call(buffer, 4);
            Check(Call<uint>(buffer, 6, length), "SetCurrentLength");
            Check(MFCreateSample(out sample), "MFCreateSample");
            Check(Call<IntPtr>(sample, 42, buffer), "AddBuffer");
            Check(Call<long>(sample, 36, time), "SetSampleTime");
            Check(Call<long>(sample, 38, duration), "SetSampleDuration");
            Check(Call<uint, IntPtr>(writer, 6, stream, sample), "WriteSample");
        }
        finally
        {
            Release(sample);
            Release(buffer);
        }
    }

    private static void FinishWriter(ref IntPtr writer)
    {
        try
        {
            Check(Call(writer, 11), "Finalize");
        }
        finally
        {
            Release(writer);
            writer = 0;
        }
    }

    /// <summary>A frame: 32-bit BGR pixels, top row first, in memory the GC doesn't move, with a GDI+ bitmap over them
    /// to draw the date and time on.</summary>
    private sealed class Frame : IDisposable
    {
        private readonly IntPtr pixels;

        public Frame(int width, int height)
        {
            (Width, Height, RowBytes) = (width, height, width * 4);
            pixels = Marshal.AllocHGlobal(RowBytes * height);
            Bitmap = new Bitmap(width, height, RowBytes, PixelFormat.Format32bppRgb, pixels);
        }

        public int Width { get; }
        public int Height { get; }
        public int RowBytes { get; }
        public Bitmap Bitmap { get; }
        public IntPtr GetPixels() => pixels;

        public void Dispose()
        {
            Bitmap.Dispose();
            Marshal.FreeHGlobal(pixels);
        }
    }

    // ---- Media Foundation, by its vtables --------------------------------------------------------------------------

    private const uint MfVersion = 0x00020070, MfStartupNoSocket = 1, FirstVideoStream = 0xFFFFFFFC, EndOfStream = 0x2;
    private static readonly Guid DevSourceType = new("c60ac5fe-252a-478f-a0ef-bc8fa5f7cad3");
    private static readonly Guid DevSourceVideoCapture = new("8ac3587a-4ae7-42d8-99e0-0a6013eef90f");
    private static readonly Guid DevFriendlyName = new("60d0e559-52f8-4fa2-bbce-acdb34a8ec01");
    private static readonly Guid SourceReaderEnableVideoProcessing = new("fb394f3d-ccf1-42ee-bbb3-f9b845d5681d");
    private static readonly Guid ReadWriteEnableHardwareTransforms = new("a634a91c-822b-41b9-a494-4de4643612b0");
    private static readonly Guid MajorTypeKey = new("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
    private static readonly Guid SubtypeKey = new("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
    private static readonly Guid FrameSizeKey = new("1652c33d-d6b2-4012-b834-72030849a37d");
    private static readonly Guid FrameRateKey = new("c459a2e8-3d2c-4e44-b132-fee5156c7bb0");
    private static readonly Guid AvgBitrateKey = new("20332624-fb0d-4d9e-bd0d-cbf6786c102e");
    private static readonly Guid InterlaceModeKey = new("e2724bb8-e676-4806-b4b2-a8d6efb44ccd");
    private static readonly Guid PixelAspectRatioKey = new("c6376a1e-8d0a-4027-be45-6d9a0ad39bb6");
    private static readonly Guid DefaultStrideKey = new("644b4e48-1e02-4516-b0eb-c01ca9d49ac6");
    private static readonly Guid MediaTypeVideo = new("73646976-0000-0010-8000-00aa00389b71");
    private static readonly Guid VideoFormatRgb32 = new("00000016-0000-0010-8000-00aa00389b71");
    private static readonly Guid VideoFormatH264 = new("34363248-0000-0010-8000-00aa00389b71");
    private static readonly Guid IidMediaSource = new("279a808d-aec7-40c8-9c6b-a6b492c78a66");
    private static readonly Guid Iid2DBuffer = new("7dc9d5f9-9ed9-44ec-9bbf-0600bb589fbb");

    private static (uint High, uint Low) Split(ulong value) => ((uint)(value >> 32), (uint)value);
    private static ulong Join(uint high, uint low) => ((ulong)high << 32) | low;

    private static void SetGuid(IntPtr attributes, Guid key, Guid value) => Check(Call<IntPtr, IntPtr>(attributes, 24, (IntPtr)(&key), (IntPtr)(&value)), "SetGUID");
    private static void SetUInt32(IntPtr attributes, Guid key, uint value) => Check(Call<IntPtr, uint>(attributes, 21, (IntPtr)(&key), value), "SetUINT32");
    private static void SetUInt64(IntPtr attributes, Guid key, ulong value) => Check(Call<IntPtr, ulong>(attributes, 22, (IntPtr)(&key), value), "SetUINT64");

    private static ulong GetUInt64(IntPtr attributes, Guid key)
    {
        ulong value = 0;
        return Call<IntPtr, IntPtr>(attributes, 8, (IntPtr)(&key), (IntPtr)(&value)) >= 0 ? value : 0;
    }

    private static void* Slot(IntPtr instance, int index) => (*(void***)instance)[index];

    private static int Call(IntPtr instance, int slot) =>
        ((delegate* unmanaged[Stdcall]<IntPtr, int>)Slot(instance, slot))(instance);
    private static int Call<T1>(IntPtr instance, int slot, T1 a) where T1 : unmanaged =>
        ((delegate* unmanaged[Stdcall]<IntPtr, T1, int>)Slot(instance, slot))(instance, a);
    private static int Call<T1, T2>(IntPtr instance, int slot, T1 a, T2 b) where T1 : unmanaged where T2 : unmanaged =>
        ((delegate* unmanaged[Stdcall]<IntPtr, T1, T2, int>)Slot(instance, slot))(instance, a, b);
    private static int Call<T1, T2, T3>(IntPtr instance, int slot, T1 a, T2 b, T3 c) where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged =>
        ((delegate* unmanaged[Stdcall]<IntPtr, T1, T2, T3, int>)Slot(instance, slot))(instance, a, b, c);
    private static int Call<T1, T2, T3, T4, T5, T6>(IntPtr instance, int slot, T1 a, T2 b, T3 c, T4 d, T5 e, T6 f)
        where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged =>
        ((delegate* unmanaged[Stdcall]<IntPtr, T1, T2, T3, T4, T5, T6, int>)Slot(instance, slot))(instance, a, b, c, d, e, f);

    private static void Release(IntPtr instance)
    {
        if (instance != 0) ((delegate* unmanaged[Stdcall]<IntPtr, uint>)Slot(instance, 2))(instance);
    }

    private static void Check(int hr, string what)
    {
        if (hr < 0) throw new COMException(what + " failed", hr);
    }

    [LibraryImport("ole32.dll")] private static partial int CoInitializeEx(IntPtr reserved, uint init);
    [LibraryImport("ole32.dll")] private static partial void CoTaskMemFree(IntPtr memory);
    [LibraryImport("mfplat.dll")] private static partial int MFStartup(uint version, uint flags);
    [LibraryImport("mfplat.dll")] private static partial int MFShutdown();
    [LibraryImport("mfplat.dll")] private static partial int MFCreateAttributes(out IntPtr attributes, uint size);
    [LibraryImport("mfplat.dll")] private static partial int MFCreateMediaType(out IntPtr type);
    [LibraryImport("mfplat.dll")] private static partial int MFCreateSample(out IntPtr sample);
    [LibraryImport("mfplat.dll")] private static partial int MFCreateMemoryBuffer(uint length, out IntPtr buffer);
    [LibraryImport("mf.dll")] private static partial int MFEnumDeviceSources(IntPtr attributes, out IntPtr activates, out uint count);
    [LibraryImport("mfreadwrite.dll")] private static partial int MFCreateSourceReaderFromMediaSource(IntPtr source, IntPtr attributes, out IntPtr reader);
    [LibraryImport("mfreadwrite.dll", StringMarshalling = StringMarshalling.Utf16)] private static partial int MFCreateSinkWriterFromURL(string url, IntPtr byteStream, IntPtr attributes, out IntPtr writer);
}
