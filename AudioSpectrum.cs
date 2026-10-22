using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace KatieHoloDeck
{
    /// <summary>
    /// FASE 2 (v9.5.0) — espectro de áudio do sistema via WASAPI loopback + FFT radix-2.
    /// Zero NuGet: P/Invoke/COM puro, thread dedicada e 64 bandas normalizadas 0..1.
    /// Se o loopback falhar (sem device/driver/formato), cai no modo sintético modulado
    /// pelo estado do SMTC — a roda continua viva, só menos precisa.
    /// </summary>
    public sealed class AudioSpectrum : IDisposable
    {
        public event Action<float[]>? BandsChanged;

        private const int FftSize = 2048;
        private const int BandCount = 64;
        private const double SmoothSeconds = 0.200;

        private const uint AUDCLNT_STREAMFLAGS_LOOPBACK = 0x00020000;
        private const uint AUDCLNT_BUFFERFLAGS_SILENT = 0x00000002;
        private const uint COINIT_MULTITHREADED = 0x0;

        private readonly object _lifecycle = new();
        private Thread? _thread;
        private volatile bool _running;
        private volatile bool _disposed;
        private volatile bool _isPlaying;

        private TimeSpan _position;
        private DateTime _positionAt = DateTime.UtcNow;

        private bool _realAudio;
        private int _sampleRate = 48000;
        private int _channels = 2;
        private SampleKind _sampleKind = SampleKind.Float32;

        private readonly float[] _samples = new float[FftSize];
        private int _sampleCount;
        private readonly float[] _fftRe = new float[FftSize];
        private readonly float[] _fftIm = new float[FftSize];
        private readonly float[] _window = new float[FftSize];
        private readonly int[] _reverse = new int[FftSize];
        private readonly float[] _cos = new float[FftSize / 2];
        private readonly float[] _sin = new float[FftSize / 2];

        private readonly float[] _bandTargets = new float[BandCount];
        private readonly float[] _bands = new float[BandCount];
        private float _totalPeak, _leftPeak, _rightPeak;
        private float _totalPeakTarget, _leftPeakTarget, _rightPeakTarget;
        private double _syntheticPhase;

        private float[] _floatBuffer = Array.Empty<float>();
        private short[] _shortBuffer = Array.Empty<short>();

        // COM/WASAPI — criados e liberados na MESMA thread de captura.
        private IMMDeviceEnumerator? _enumerator;
        private IMMDevice? _device;
        private IAudioClient? _client;
        private IAudioCaptureClient? _capture;
        private IntPtr _mixFormatPtr = IntPtr.Zero;
        private bool _wasapiError;

        public AudioSpectrum()
        {
            for (int i = 0; i < FftSize; i++)
                _window[i] = (float)(0.5 - 0.5 * Math.Cos(2.0 * Math.PI * i / (FftSize - 1)));

            for (int i = 0; i < FftSize; i++)
            {
                int rev = 0, v = i;
                for (int bit = 0; bit < 11; bit++) { rev = (rev << 1) | (v & 1); v >>= 1; }
                _reverse[i] = rev;
            }

            for (int k = 0; k < FftSize / 2; k++)
            {
                double a = -2.0 * Math.PI * k / FftSize;
                _cos[k] = (float)Math.Cos(a);
                _sin[k] = (float)Math.Sin(a);
            }
        }

        /// <summary>1..20 (mesma escala do Wallpaper Engine).</summary>
        public double Sensitivity { get; set; } = 1.0;

        /// <summary>"max" | "median" | "mean".</summary>
        public string CalculationType { get; set; } = "max";

        public float TotalPeak => _totalPeak;
        public float LeftPeak => _leftPeak;
        public float RightPeak => _rightPeak;
        public bool IsRealAudio => _realAudio;

        /// <summary>True quando o loopback WASAPI esta ativo E ha energia real agora (nao sintetica).</summary>
        public bool HasAudio => _realAudio && _totalPeak > 0.004f;

        /// <summary>SMTC manda no gating: alimenta o fallback sintético e a posição da faixa.</summary>
        public void FeedPlayback(bool isPlaying, TimeSpan position)
        {
            _isPlaying = isPlaying;
            _position = position;
            _positionAt = DateTime.UtcNow;
        }

        public void Start()
        {
            lock (_lifecycle)
            {
                if (_disposed || _running) return;
                _running = true;
                _wasapiError = false;
                _thread = new Thread(CaptureLoop)
                {
                    IsBackground = true,
                    Name = "KATIE.AudioSpectrum"
                };
                _thread.Start();
            }
        }

        public void Stop()
        {
            Thread? thread;
            lock (_lifecycle)
            {
                if (!_running) return;
                _running = false;
                thread = _thread;
                _thread = null;
            }

            if (thread is not null && thread != Thread.CurrentThread)
                thread.Join(1500);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
        }

        private void CaptureLoop()
        {
            CoInitializeEx(IntPtr.Zero, COINIT_MULTITHREADED);
            try
            {
                bool real = InitWasapi();
                if (!real)
                {
                    _realAudio = false;
                    Debug.WriteLine("[AudioSpectrum] loopback WASAPI indisponível — usando fallback sintético (SMTC).");
                }

                var last = Stopwatch.GetTimestamp();
                while (_running)
                {
                    long now = Stopwatch.GetTimestamp();
                    double dt = Math.Clamp((now - last) / (double)Stopwatch.Frequency, 0.001, 0.100);
                    last = now;

                    if (real)
                    {
                        PumpWasapi();
                        if (_wasapiError)
                        {
                            real = false;
                            _realAudio = false;
                            Debug.WriteLine("[AudioSpectrum] loopback perdeu o fluxo — fallback sintético.");
                        }
                    }

                    if (!real) PumpSynthetic(dt);

                    Smooth(dt);
                    PublishBands();
                    Thread.Sleep(16);
                }
            }
            catch
            {
                _realAudio = false;
                try { RunSyntheticOnly(); } catch { }
            }
            finally
            {
                ReleaseWasapi();
                CoUninitialize();
            }
        }

        private void RunSyntheticOnly()
        {
            var last = Stopwatch.GetTimestamp();
            while (_running)
            {
                long now = Stopwatch.GetTimestamp();
                double dt = Math.Clamp((now - last) / (double)Stopwatch.Frequency, 0.001, 0.100);
                last = now;
                PumpSynthetic(dt);
                Smooth(dt);
                PublishBands();
                Thread.Sleep(16);
            }
        }

        // ================= WASAPI =================

        private bool InitWasapi()
        {
            try
            {
                _enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                if (_enumerator.GetDefaultAudioEndpoint(0 /*eRender*/, 1 /*eMultimedia*/, out var device) != 0 || device is null)
                    return false;
                _device = device;

                var iid = typeof(IAudioClient).GUID;
                if (_device.Activate(ref iid, 1 /*CLSCTX_ALL*/, IntPtr.Zero, out var obj) != 0 || obj is not IAudioClient client)
                    return false;
                _client = client;

                if (_client.GetMixFormat(out _mixFormatPtr) != 0 || _mixFormatPtr == IntPtr.Zero)
                    return false;

                var fmt = Marshal.PtrToStructure<WAVEFORMATEX>(_mixFormatPtr);
                _sampleRate = (int)fmt.nSamplesPerSec;
                _channels = Math.Max(1, (int)fmt.nChannels);

                int subFormat = fmt.wFormatTag == 0xFFFE ? Marshal.ReadInt16(_mixFormatPtr, 24) : fmt.wFormatTag;
                if (fmt.wBitsPerSample == 32 && subFormat == 3) _sampleKind = SampleKind.Float32;
                else if (fmt.wBitsPerSample == 16 && subFormat == 1) _sampleKind = SampleKind.Pcm16;
                else return false;

                if (_client.Initialize(0 /*AUDCLNT_SHAREMODE_SHARED*/, AUDCLNT_STREAMFLAGS_LOOPBACK,
                        10_000_000L, 0, _mixFormatPtr, IntPtr.Zero) != 0)
                    return false;

                var capIid = typeof(IAudioCaptureClient).GUID;
                if (_client.GetService(ref capIid, out var capPtr) != 0 || capPtr == IntPtr.Zero)
                    return false;

                _capture = (IAudioCaptureClient)Marshal.GetObjectForIUnknown(capPtr);
                Marshal.Release(capPtr);

                if (_client.Start() != 0)
                    return false;

                _realAudio = true;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void PumpWasapi()
        {
            if (_capture is null) { _wasapiError = true; return; }

            while (_running)
            {
                if (_capture.GetNextPacketSize(out uint packet) != 0) { _wasapiError = true; return; }
                if (packet == 0) break;

                if (_capture.GetBuffer(out var data, out uint frames, out uint flags,
                        out _, out _) != 0)
                {
                    _wasapiError = true;
                    return;
                }

                try
                {
                    if (frames > 0) ConsumeFrames(data, (int)frames, flags);
                }
                finally
                {
                    _capture.ReleaseBuffer(frames);
                }
            }
        }

        private void ConsumeFrames(IntPtr data, int frames, uint flags)
        {
            int count = frames * _channels;

            if ((flags & AUDCLNT_BUFFERFLAGS_SILENT) != 0)
            {
                for (int i = 0; i < frames; i++) AddSample(0f);
                return;
            }

            if (_sampleKind == SampleKind.Float32)
            {
                EnsureFloat(count);
                Marshal.Copy(data, _floatBuffer, 0, count);
                for (int f = 0; f < frames; f++)
                {
                    int b = f * _channels;
                    float sum = 0f;
                    for (int c = 0; c < _channels; c++) sum += _floatBuffer[b + c];
                    AddSample(sum / _channels);
                }
            }
            else
            {
                EnsureShort(count);
                Marshal.Copy(data, _shortBuffer, 0, count);
                for (int f = 0; f < frames; f++)
                {
                    int b = f * _channels;
                    float sum = 0f;
                    for (int c = 0; c < _channels; c++) sum += _shortBuffer[b + c] / 32768f;
                    AddSample(sum / _channels);
                }
            }
        }

        private void EnsureFloat(int count)
        {
            if (_floatBuffer.Length < count) _floatBuffer = new float[count];
        }

        private void EnsureShort(int count)
        {
            if (_shortBuffer.Length < count) _shortBuffer = new short[count];
        }

        private void AddSample(float sample)
        {
            _samples[_sampleCount++] = sample;
            if (_sampleCount < FftSize) return;

            ComputeFft();
            Array.Copy(_samples, FftSize / 2, _samples, 0, FftSize / 2);
            _sampleCount = FftSize / 2;
        }

        private void ComputeFft()
        {
            for (int i = 0; i < FftSize; i++)
            {
                _fftRe[i] = _samples[i] * _window[i];
                _fftIm[i] = 0f;
            }

            for (int i = 0; i < FftSize; i++)
            {
                int j = _reverse[i];
                if (j <= i) continue;
                (_fftRe[i], _fftRe[j]) = (_fftRe[j], _fftRe[i]);
                (_fftIm[i], _fftIm[j]) = (_fftIm[j], _fftIm[i]);
            }

            for (int len = 2; len <= FftSize; len <<= 1)
            {
                int half = len >> 1;
                int step = FftSize / len;
                for (int i = 0; i < FftSize; i += len)
                {
                    for (int j = 0; j < half; j++)
                    {
                        int k = j * step;
                        float tr = _cos[k] * _fftRe[i + j + half] - _sin[k] * _fftIm[i + j + half];
                        float ti = _cos[k] * _fftIm[i + j + half] + _sin[k] * _fftRe[i + j + half];
                        _fftRe[i + j + half] = _fftRe[i + j] - tr;
                        _fftIm[i + j + half] = _fftIm[i + j] - ti;
                        _fftRe[i + j] += tr;
                        _fftIm[i + j] += ti;
                    }
                }
            }

            MapBands();
        }

        private void MapBands()
        {
            double fMin = 30.0;
            double fMax = Math.Min(16000.0, _sampleRate / 2.0);
            if (fMax <= fMin) fMax = fMin * 2;
            double ratio = fMax / fMin;
            double binScale = (double)FftSize / _sampleRate;

            for (int b = 0; b < BandCount; b++)
            {
                double lo = fMin * Math.Pow(ratio, b / (double)BandCount);
                double hi = fMin * Math.Pow(ratio, (b + 1) / (double)BandCount);

                int k0 = Math.Max(1, (int)Math.Floor(lo * binScale));
                int k1 = Math.Min(FftSize / 2 - 1, Math.Max(k0, (int)Math.Ceiling(hi * binScale) - 1));

                float sum = 0f;
                int n = 0;
                for (int k = k0; k <= k1; k++)
                {
                    float re = _fftRe[k], im = _fftIm[k];
                    sum += MathF.Sqrt(re * re + im * im) * 2f / FftSize;
                    n++;
                }

                float mag = n > 0 ? sum / n : 0f;
                float db = 20f * MathF.Log10(mag + 1e-9f);
                float norm = Math.Clamp((db + 80f) / 80f, 0f, 1f);
                norm = MathF.Pow(norm, 1.25f);
                _bandTargets[b] = Math.Clamp(norm * (float)Sensitivity, 0f, 1f);
            }

            UpdatePeakTargets();
        }

        private void UpdatePeakTargets()
        {
            _totalPeakTarget = Calc(_bandTargets, 0, BandCount);
            _leftPeakTarget = Calc(_bandTargets, 0, BandCount / 2);
            _rightPeakTarget = Calc(_bandTargets, BandCount / 2, BandCount);
        }

        private float Calc(float[] data, int start, int end)
        {
            int n = end - start;
            if (n <= 0) return 0f;

            switch ((CalculationType ?? "max").Trim().ToLowerInvariant())
            {
                case "median":
                {
                    var copy = new float[n];
                    Array.Copy(data, start, copy, 0, n);
                    Array.Sort(copy);
                    return n % 2 == 1 ? copy[n / 2] : (copy[n / 2 - 1] + copy[n / 2]) * 0.5f;
                }
                case "mean":
                {
                    float sum = 0f;
                    for (int i = start; i < end; i++) sum += data[i];
                    return sum / n;
                }
                default:
                {
                    float max = 0f;
                    for (int i = start; i < end; i++) if (data[i] > max) max = data[i];
                    return max;
                }
            }
        }

        // ================= FALLBACK SINTÉTICO =================

        private void PumpSynthetic(double dt)
        {
            if (_isPlaying) _syntheticPhase += dt * Math.PI * 2.0 * (1.1 + 0.12 * Math.Sin(_syntheticPhase * 0.17));
            double t = _position.TotalSeconds + (DateTime.UtcNow - _positionAt).TotalSeconds;

            for (int b = 0; b < BandCount; b++)
            {
                if (!_isPlaying)
                {
                    _bandTargets[b] = 0f;
                    continue;
                }

                double v = 0.45
                           + 0.30 * Math.Sin(_syntheticPhase * 1.7 + b * 0.21 + t * 0.7)
                           + 0.15 * Math.Sin(_syntheticPhase * 0.6 + b * 0.47);
                v *= 0.72 + 0.28 * Math.Sin(_syntheticPhase * 0.31 + b * 0.09);
                v = Math.Clamp(v, 0.0, 1.0) * 0.85;
                _bandTargets[b] = Math.Clamp((float)(v * Sensitivity), 0f, 1f);
            }

            UpdatePeakTargets();
        }

        // ================= SMOOTH / PUBLISH =================

        private void Smooth(double dt)
        {
            float a = (float)(1.0 - Math.Exp(-dt / SmoothSeconds));
            for (int i = 0; i < BandCount; i++)
                _bands[i] += (_bandTargets[i] - _bands[i]) * a;

            _totalPeak += (_totalPeakTarget - _totalPeak) * a;
            _leftPeak += (_leftPeakTarget - _leftPeak) * a;
            _rightPeak += (_rightPeakTarget - _rightPeak) * a;
        }

        private void PublishBands()
        {
            // Idle-zero (Codex P2 v9.5.0): sem playback e bandas já decaídas ao fundo → não publica
            // (zero alocação e zero churn no Dispatcher enquanto nada toca).
            if (!_isPlaying)
            {
                bool any = false;
                for (int i = 0; i < BandCount; i++)
                {
                    if (_bands[i] > 0.001f) { any = true; break; }
                }
                if (!any) return;
            }
            var copy = new float[BandCount];
            Array.Copy(_bands, copy, BandCount);
            BandsChanged?.Invoke(copy);
        }

        private void ReleaseWasapi()
        {
            try { _client?.Stop(); } catch { }

            if (_capture is not null) { try { Marshal.ReleaseComObject(_capture); } catch { } _capture = null; }
            if (_client is not null) { try { Marshal.ReleaseComObject(_client); } catch { } _client = null; }
            if (_device is not null) { try { Marshal.ReleaseComObject(_device); } catch { } _device = null; }
            if (_enumerator is not null) { try { Marshal.ReleaseComObject(_enumerator); } catch { } _enumerator = null; }
            if (_mixFormatPtr != IntPtr.Zero) { CoTaskMemFree(_mixFormatPtr); _mixFormatPtr = IntPtr.Zero; }
        }

        private enum SampleKind { Float32, Pcm16 }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct WAVEFORMATEX
        {
            public ushort wFormatTag;
            public ushort nChannels;
            public uint nSamplesPerSec;
            public uint nAvgBytesPerSec;
            public ushort nBlockAlign;
            public ushort wBitsPerSample;
            public ushort cbSize;
        }

        [DllImport("ole32.dll")]
        private static extern int CoInitializeEx(IntPtr pvReserved, uint dwCoInit);

        [DllImport("ole32.dll")]
        private static extern void CoUninitialize();

        [DllImport("ole32.dll")]
        private static extern void CoTaskMemFree(IntPtr ptr);
    }

    // ===== COM interop mínimo do WASAPI (IMMDevice/IMMDeviceEnumerator vêm do MediaTracker) =====

    [ComImport, Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioClient
    {
        [PreserveSig]
        int Initialize(int shareMode, uint streamFlags, long hnsBufferDuration, long hnsPeriodicity,
            IntPtr format, IntPtr audioSessionGuid);
        [PreserveSig] int GetBufferSize(out uint numBufferFrames);
        [PreserveSig] int GetStreamLatency(out long latency);
        [PreserveSig] int GetCurrentPadding(out uint numPaddingFrames);
        [PreserveSig] int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closestMatch);
        [PreserveSig] int GetMixFormat(out IntPtr deviceFormat);
        [PreserveSig] int GetDevicePeriod(out long defaultPeriod, out long minimumPeriod);
        [PreserveSig] int Start();
        [PreserveSig] int Stop();
        [PreserveSig] int Reset();
        [PreserveSig] int SetEventHandle(IntPtr eventHandle);
        [PreserveSig] int GetService(ref Guid riid, out IntPtr service);
    }

    [ComImport, Guid("C8ADBD64-E71E-48A0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioCaptureClient
    {
        [PreserveSig] int GetBuffer(out IntPtr data, out uint numFramesToRead, out uint flags,
            out long devicePosition, out long qpcPosition);
        [PreserveSig] int ReleaseBuffer(uint numFramesRead);
        [PreserveSig] int GetNextPacketSize(out uint numFramesInNextPacket);
    }
}
