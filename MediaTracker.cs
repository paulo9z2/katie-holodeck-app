using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace KatieHoloDeck
{
    /// <summary>Snapshoot do que a sessão de mídia do sistema está tocando (SMTC).</summary>
    public sealed class MediaUpdate
    {
        public string Title { get; init; } = "";
        public string Artist { get; init; } = "";
        public string AppName { get; init; } = "";
        /// <summary>v9.7 P1: existe sessão SMTC — transportado SEPARADO do AUMID (que pode vir vazio).</summary>
        public bool HasSession { get; init; }
        public byte[]? Thumbnail { get; init; }
        public TimeSpan Position { get; init; }
        public TimeSpan Duration { get; init; }
        public bool IsPlaying { get; init; }
        public bool CanNext { get; init; }
        public bool CanPrevious { get; init; }
        public bool CanPlayPause { get; init; }
        public float Volume { get; init; }
    }

    /// <summary>
    /// Transplante do MediaTracker do Translucid (do próprio sir) — SMTC via WinRT.
    /// Capa, título, posição, play/pause/next/prev. Sem pacote NuGet.
    /// </summary>
    public sealed class MediaTracker : IDisposable
    {
        public event Action<MediaUpdate>? Updated;

        private GlobalSystemMediaTransportControlsSessionManager? _manager;
        private GlobalSystemMediaTransportControlsSession? _session;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private string? _lastArtKey;
        private byte[]? _lastThumb;

        public async Task StartAsync()
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _manager.CurrentSessionChanged += OnCurrentSessionChanged;
            await AttachAsync(_manager.GetCurrentSession());
        }

        private async void OnCurrentSessionChanged(
            GlobalSystemMediaTransportControlsSessionManager sender,
            CurrentSessionChangedEventArgs args) => await AttachAsync(sender.GetCurrentSession());

        private async Task AttachAsync(GlobalSystemMediaTransportControlsSession? session)
        {
            await _gate.WaitAsync();
            try
            {
                if (_session is not null)
                {
                    _session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                    _session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
                    _session.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
                }
                _session = session;
                if (_session is not null)
                {
                    _session.MediaPropertiesChanged += OnMediaPropertiesChanged;
                    _session.PlaybackInfoChanged += OnPlaybackInfoChanged;
                    _session.TimelinePropertiesChanged += OnTimelinePropertiesChanged;
                }
            }
            finally { _gate.Release(); }
            _lastArtKey = null;
            _lastThumb = null;
            await PushAsync();
        }

        private async void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession s, object a) => await PushAsync();
        private async void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession s, object a) => await PushAsync();
        private async void OnTimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession s, object a) => await PushAsync();

        public async Task PushAsync()
        {
            var session = _session;
            if (session is null) { Updated?.Invoke(Idle()); return; }
            try
            {
                var props = await session.TryGetMediaPropertiesAsync();
                var timeline = session.GetTimelineProperties();
                var playback = session.GetPlaybackInfo();
                var controls = playback.Controls;

                byte[]? thumbnail = null;
                var artKey = $"{props?.Title}|{props?.Artist}";
                bool newTrack = _lastArtKey != artKey;

                if (newTrack)
                {
                    // log de diagnóstico (só em troca de faixa): o que o SMTC entregou?
                    try
                    {
                        System.IO.File.AppendAllText(
                            System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "media_debug.jsonl"),
                            $"{DateTime.Now:HH:mm:ss} | {artKey} | thumb={(props?.Thumbnail is null ? "null" : "stream")}\n");
                    }
                    catch { }
                }

                if (props?.Thumbnail is not null)
                {
                    if (newTrack || _lastThumb is null)
                    {
                        try
                        {
                            var stream = await props.Thumbnail.OpenReadAsync();
                            using var buffer = new MemoryStream();
                            using (var input = stream.AsStreamForRead()) await input.CopyToAsync(buffer);
                            thumbnail = buffer.ToArray();
                            if (thumbnail.Length > 0)
                            {
                                _lastArtKey = artKey;   // só cacheia após fetch válido
                                _lastThumb = thumbnail;
                            }
                        }
                        catch { thumbnail = null; }
                    }
                    else thumbnail = _lastThumb;
                }
                else if (newTrack)
                {
                    // faixa nova e a thumb AINDA não chegou: NÃO herdar a capa antiga
                    thumbnail = null;
                    _lastThumb = null;
                }
                else thumbnail = _lastThumb;

                var duration = timeline.EndTime - timeline.StartTime;

                // guard de corrida: a sessão pode ter morrido durante os awaits acima —
                // publicar agora mandaria um snapshot fantasma (HasSession=true tira a roda do Standby)
                if (!ReferenceEquals(session, _session)) return;

                Updated?.Invoke(new MediaUpdate
                {
                    Title = props?.Title ?? "",
                    Artist = props?.Artist ?? "",
                    AppName = session.SourceAppUserModelId,
                    HasSession = true,
                    Thumbnail = thumbnail,
                    Position = timeline.Position,
                    Duration = duration > TimeSpan.Zero ? duration : TimeSpan.Zero,
                    IsPlaying = playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                    CanNext = controls.IsNextEnabled,
                    CanPrevious = controls.IsPreviousEnabled,
                    CanPlayPause = controls.IsPlayEnabled || controls.IsPauseEnabled,
                    Volume = MasterVolume.Get()
                });
            }
            catch { Updated?.Invoke(Idle(_session is not null)); }
        }

        private static MediaUpdate Idle(bool hasSession = false) => new()
        {
            Title = "Nada tocando",
            Artist = "",
            HasSession = hasSession,
            Volume = MasterVolume.Get()
        };

        public async Task TogglePlayPauseAsync()
        {
            if (_session is null) return;
            if (_session.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                await _session.TryPauseAsync();
            else
                await _session.TryPlayAsync();
            await PushAsync();
        }

        public async Task NextAsync() { if (_session is not null) { await _session.TrySkipNextAsync(); await PushAsync(); } }
        public async Task PreviousAsync() { if (_session is not null) { await _session.TrySkipPreviousAsync(); await PushAsync(); } }

        public void Dispose()
        {
            if (_manager is not null) _manager.CurrentSessionChanged -= OnCurrentSessionChanged;
            if (_session is not null)
            {
                _session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                _session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
                _session.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
            }
            _gate.Dispose();
        }
    }

    /// <summary>Volume MASTER via CoreAudio COM (IAudioEndpointVolume) — sem NAudio.</summary>
    public static class MasterVolume
    {
        public static float Get()
        {
            try
            {
                var device = GetDefaultDevice();
                float v = 0.5f;
                device?.GetMasterVolumeLevelScalar(out v);
                return v;
            }
            catch { return 0.5f; }
        }

        public static void Set(float level01)
        {
            try
            {
                var device = GetDefaultDevice();
                device?.SetMasterVolumeLevelScalar(Math.Clamp(level01, 0f, 1f), IntPtr.Zero);
            }
            catch { }
        }

        public static void Adjust(float delta) => Set(Get() + delta);

        private static IAudioEndpointVolume? GetDefaultDevice()
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            enumerator.GetDefaultAudioEndpoint(0 /*eRender*/, 1 /*eMultimedia*/, out var device);
            device.Activate(typeof(IAudioEndpointVolume).GUID, 0, IntPtr.Zero, out var obj);
            return (IAudioEndpointVolume)obj;
        }
    }

    // ===== COM interop mínimo (CoreAudio) =====
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C452949169B8")]
    internal class MMDeviceEnumeratorComObject { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator
    {
        // vtable: enumAudioEndpoints, getDefaultAudioEndpoint, ...
        [PreserveSig]
        int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
        [PreserveSig]
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
        // restante não usado
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E5079096"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(IntPtr notify);
        int UnregisterControlChangeNotify(IntPtr notify);
        int GetChannelCount(out int count);
        int SetMasterVolumeLevel(float level, IntPtr ctx);
        int SetMasterVolumeLevelScalar(float level, IntPtr ctx);
        int GetMasterVolumeLevel(out float level);
        int GetMasterVolumeLevelScalar(out float level);
        int SetChannelVolumeLevel(int channel, float level, IntPtr ctx);
        int SetChannelVolumeLevelScalar(int channel, float level, IntPtr ctx);
        int GetChannelVolumeLevel(int channel, out float level);
        int GetChannelVolumeLevelScalar(int channel, out float level);
        int SetMute(bool mute, IntPtr ctx);
        int GetMute(out bool mute);
    }
}
