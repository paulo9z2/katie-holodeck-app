using System;
using System.Runtime.InteropServices;
using KatieHoloDeck.LiquidGlass.Interop;

namespace KatieHoloDeck.LiquidGlass
{
    /// <summary>
    /// Captura da TELA REAL atrás da janela via DXGI Output Duplication — a fonte do
    /// liquid verdadeiro (o proxy RTB mostra só o conteúdo do app; isto mostra o jogo/
    /// wallpaper/qualquer coisa que esteja EMBAIXO do overlay).
    ///
    /// Desenho por frame:
    /// - AcquireNextFrame(timeout curto): S_OK → CopyResource(desktop→_last) +
    ///   ReleaseFrame (a textura do frame é efêmera; _last é a nossa cópia local da
    ///   tela inteira). WAIT_TIMEOUT (tela estática) → reusa _last. Outros hr → fail
    ///   (jogo fullscreen exclusivo mata a duplicação — ACCESS_LOST; caller decide).
    /// - Recorte: CopySubresourceRegion(_last → _staging pequena) + Map → bytes.
    ///   O _last full-screen só é reescrito quando a tela muda; o readback é só da
    ///   região da pill (~0,2MB) — leve o bastante pra 30Hz.
    /// - Janela marcada WDA_EXCLUDEFROMCAPTURE pelo caller → a captura não contém o
    ///   app → sem feedback loop (mesma técnica do Poni/Oyenza).
    ///
    /// Device: compartilha o do motor GPU quando existe; no caminho CPU cria um
    /// device D3D11 próprio (duplicação e readback são independentes do motor).
    /// Slots de vtable verificados contra os headers C (ver DXGI.cs / D3D11.cs).
    /// </summary>
    internal sealed class BackdropDuplication : IDisposable
    {
        private IntPtr _dev, _ctx;
        private readonly bool _ownDevice;
        private IntPtr _factory, _adapter, _output, _output1, _dup;
        private IntPtr _last;       // DEFAULT: cópia local do desktop (tela do output)
        private IntPtr _staging;    // STAGING: região pequena pro readback CPU
        private int _stW, _stH;
        private DXGI.RECT _desk;    // DesktopCoordinates do output (tela virtual)
        private readonly long[] _frameInfo = new long[6];   // OUTDUPL_FRAME_INFO (48B)
        private readonly GCHandle _fiPin;

        public string FailReason { get; private set; } = "";
        public bool Ready => _dup != IntPtr.Zero;
        /// <summary>True quando _last já tem conteúdo real (1º frame chegou).
        /// Falso = tela ainda não mudou desde o mount; caller usa o proxy.</summary>
        public bool HasFrame => _hasFrame;
        private bool _hasFrame;
        public int ScreenW => _desk.R - _desk.L;
        public int ScreenH => _desk.B - _desk.T;

        /// <param name="dev">Device D3D11 do motor (IntPtr.Zero = cria um próprio).</param>
        /// <param name="ctx">Immediate context do mesmo device.</param>
        /// <param name="probeX">X físico de um ponto da janela (pra escolher o monitor).</param>
        /// <param name="probeY">Y físico idem.</param>
        public BackdropDuplication(IntPtr dev, IntPtr ctx, int probeX, int probeY)
        {
            _fiPin = GCHandle.Alloc(_frameInfo, GCHandleType.Pinned);
            try
            {
                if (dev == IntPtr.Zero || ctx == IntPtr.Zero)
                {
                    int hr = D3D11.D3D11CreateDevice(IntPtr.Zero, D3D11.DRIVER_TYPE_HARDWARE, IntPtr.Zero, 0,
                        new[] { D3D11.FEATURE_LEVEL_11_0 }, 1, D3D11.D3D11_SDK_VERSION,
                        out dev, out _, out ctx);
                    if (hr < 0) { Fail($"CreateDevice hr=0x{hr:X8}"); return; }
                    _ownDevice = true;
                }
                _dev = dev; _ctx = ctx;
                Init(probeX, probeY);
            }
            catch (Exception ex)
            {
                Fail($"init: {ex.GetType().Name} {ex.Message}");
            }
        }

        private void Fail(string why)
        {
            FailReason = why;
            Dispose();
        }

        private void Init(int probeX, int probeY)
        {
            var iid = DXGI.IID_IDXGIFactory1;
            int hr = DXGI.CreateDXGIFactory1(ref iid, out _factory);
            if (hr < 0) { Fail($"CreateDXGIFactory1 hr=0x{hr:X8}"); return; }
            var enumAdapters = DXGI.Fn<DXGI.DelEnumAdapters>(_factory, 7);

            IntPtr foundAdapter = IntPtr.Zero, foundOutput = IntPtr.Zero, foundOutput1 = IntPtr.Zero, foundDup = IntPtr.Zero;
            DXGI.RECT foundDesk = default;

            for (uint a = 0; a < 8 && foundDup == IntPtr.Zero; a++)
            {
                hr = enumAdapters(_factory, a, out IntPtr adapter);
                if (hr < 0) break;   // DXGI_ERROR_NOT_FOUND: fim da lista
                bool adapterTaken = false;
                var enumOutputs = DXGI.Fn<DXGI.DelEnumOutputs>(adapter, 7);
                for (uint o = 0; o < 8; o++)
                {
                    hr = enumOutputs(adapter, o, out IntPtr output);
                    if (hr < 0) break;
                    bool outputTaken = false;
                    do
                    {
                        var getDesc = DXGI.Fn<DXGI.DelGetOutputDesc>(output, 7);
                        hr = getDesc(output, out DXGI.OUTPUT_DESC desc);
                        if (hr < 0 || desc.AttachedToDesktop == 0) break;
                        var r = desc.DesktopCoordinates;
                        bool contains = probeX >= r.L && probeX < r.R && probeY >= r.T && probeY < r.B;

                        var qi = DXGI.Fn<DXGI.DelQueryInterface>(output, 0);
                        var iidOut1 = DXGI.IID_IDXGIOutput1;
                        hr = qi(output, ref iidOut1, out IntPtr output1);
                        if (hr < 0) break;
                        var dupOut = DXGI.Fn<DXGI.DelDuplicateOutput>(output1, 22);
                        hr = dupOut(output1, _dev, out IntPtr dup);
                        if (hr < 0) { D3D11.SafeRelease(ref output1); break; }

                        if (foundDup == IntPtr.Zero)
                        {
                            // primeiro vivo (fallback) ou o que contém o probe —
                            // só guardamos UM por vez; extras são liberados
                            foundAdapter = adapter; foundOutput = output; foundOutput1 = output1;
                            foundDup = dup; foundDesk = r;
                            adapterTaken = true; outputTaken = true;
                        }
                        else
                        {
                            D3D11.SafeRelease(ref dup);
                            D3D11.SafeRelease(ref output1);
                        }
                    } while (false);
                    if (!outputTaken) D3D11.SafeRelease(ref output);
                }
                if (!adapterTaken) D3D11.SafeRelease(ref adapter);
            }

            if (foundDup == IntPtr.Zero) { Fail("nenhum output com DuplicateOutput ok"); return; }
            _dup = foundDup; _output1 = foundOutput1; _output = foundOutput; _adapter = foundAdapter; _desk = foundDesk;

            // _last: cópia local da tela inteira (DEFAULT, sem bind — só alvo de cópia)
            var td = new D3D11.TEXTURE2D_DESC
            {
                Width = (uint)ScreenW, Height = (uint)ScreenH, MipLevels = 1, ArraySize = 1,
                Format = D3D11.FORMAT_B8G8R8A8_UNORM, SampleDescCount = 1, SampleDescQuality = 0,
                Usage = D3D11.USAGE_DEFAULT, BindFlags = 0, CPUAccessFlags = 0, MiscFlags = 0
            };
            var createTex = D3D11.Fn<D3D11.DelCreateTexture2D>(_dev, 5);
            hr = createTex(_dev, ref td, IntPtr.Zero, out _last);
            if (hr < 0 || _last == IntPtr.Zero) { Fail($"_last CreateTexture2D hr=0x{hr:X8}"); return; }

            // frame base: 150ms no mount (na maioria dos drivers o 1º acquire traz o
            // desktop na hora; tela 100% parada → PollFirstFrame dos ticks resolve)
            hr = Acquire(150);
            if (hr < 0 && hr != DXGI.DXGI_ERROR_WAIT_TIMEOUT) { Fail($"acquire inicial hr=0x{hr:X8}"); return; }
        }

        /// <summary>Tick sem 1º frame: acquire(0) não-bloqueante. True = já tem frame.</summary>
        public bool PollFirstFrame()
        {
            if (!Ready || HasFrame) return HasFrame;
            Acquire(0);
            return HasFrame;
        }

        /// <summary>Acquire → copia p/ _last → Release. Timeout 0 = poll não-bloqueante.
        /// O ppDesktopResource do acquire é um IDXGIResource — QI pra ID3D11Texture2D
        /// antes do CopyResource (o ponteiro wrapper NÃO é a textura).</summary>
        private int Acquire(uint timeoutMs)
        {
            if (_dup == IntPtr.Zero) return DXGI.DXGI_ERROR_ACCESS_LOST;
            var acquire = DXGI.Fn<DXGI.DelAcquireNextFrame>(_dup, 8);
            int hr = acquire(_dup, timeoutMs, _fiPin.AddrOfPinnedObject(), out IntPtr dxgiRes);
            if (hr >= 0 && dxgiRes != IntPtr.Zero)
            {
                var qiRes = DXGI.Fn<DXGI.DelQueryInterface>(dxgiRes, 0);
                var iidTex = DXGI.IID_ID3D11Texture2D;
                if (qiRes(dxgiRes, ref iidTex, out IntPtr desktopTex) >= 0 && desktopTex != IntPtr.Zero)
                {
                    var copyRes = D3D11.Fn<D3D11.DelCopyResource>(_ctx, 47);
                    copyRes(_ctx, _last, desktopTex);
                    D3D11.SafeRelease(ref desktopTex);   // o QI deu AddRef a nós
                    _hasFrame = true;
                }
                var release = DXGI.Fn<DXGI.DelReleaseFrame>(_dup, 14);
                _ = release(_dup);
            }
            return hr;
        }

        /// <summary>
        /// Região FÍSICA da tela (px, coords da tela virtual) → BGRA opaco em dst (w*h*4).
        /// O conteúdo refletido é o que está ATRÁS da janela (caller marca WDA_EXCLUDEFROMCAPTURE).
        /// Bordas fora do monitor: zero-fill (regra de sanidade: área útil < 50% = fail).
        /// </summary>
        public bool TryGetRegion(int x, int y, int w, int h, byte[] dst, out string fail)
        {
            fail = "";
            if (!Ready || _last == IntPtr.Zero || w <= 0 || h <= 0 || dst.Length < w * h * 4)
            { fail = "not ready"; return false; }
            if (!HasFrame)
            {
                // 1º frame ainda não chegou (tela parada desde o mount): caller segue
                // no proxy — NÃO é erro do pipeline, não desmonta nada
                _ = Acquire(0);
                if (!HasFrame) { fail = "primeiro frame pendente"; return false; }
            }

            int hr = Acquire(0);
            if (hr < 0 && hr != DXGI.DXGI_ERROR_WAIT_TIMEOUT)
            {
                fail = hr == DXGI.DXGI_ERROR_ACCESS_LOST ? "access_lost" : $"acquire hr=0x{hr:X8}";
                return false;
            }

            // coords relativas ao output (DesktopCoordinates pode ter origin != 0)
            int rx = x - _desk.L, ry = y - _desk.T;
            int sw = ScreenW, sh = ScreenH;
            int cx0 = Math.Max(0, rx), cy0 = Math.Max(0, ry);
            int cx1 = Math.Min(sw, rx + w), cy1 = Math.Min(sh, ry + h);
            int validW = cx1 - cx0, validH = cy1 - cy0;
            if (validW < w / 2 || validH < h / 2) { fail = "regiao majoritariamente fora do monitor"; return false; }

            // staging (recriada se a região mudou de tamanho)
            if (_staging == IntPtr.Zero || _stW != w || _stH != h)
            {
                D3D11.SafeRelease(ref _staging);
                var sd = new D3D11.TEXTURE2D_DESC
                {
                    Width = (uint)w, Height = (uint)h, MipLevels = 1, ArraySize = 1,
                    Format = D3D11.FORMAT_B8G8R8A8_UNORM, SampleDescCount = 1, SampleDescQuality = 0,
                    Usage = D3D11.USAGE_STAGING, BindFlags = 0,
                    CPUAccessFlags = D3D11.CPU_ACCESS_READ, MiscFlags = 0
                };
                var createTex = D3D11.Fn<D3D11.DelCreateTexture2D>(_dev, 5);
                int chr = createTex(_dev, ref sd, IntPtr.Zero, out _staging);
                if (chr < 0 || _staging == IntPtr.Zero) { fail = $"staging hr=0x{chr:X8}"; return false; }
                _stW = w; _stH = h;
            }

            // recorte GPU: D3D11_BOX = (left, top, FRONT, right, bottom, BACK) — front no meio!
            var box = new DXGI.BOX
            {
                Left = (uint)cx0, Top = (uint)cy0, Front = 0,
                Right = (uint)cx1, Bottom = (uint)cy1, Back = 1
            };
            var copyRegion = DXGI.Fn<DXGI.DelCopySubresourceRegion>(_ctx, 46);
            copyRegion(_ctx, _staging, 0, 0, 0, 0, _last, 0, ref box);

            // readback da região pequena (zero unsafe: Marshal.Copy por linha)
            var map = D3D11.Fn<D3D11.DelMap>(_ctx, 14);
            int mhr = map(_ctx, _staging, 0, D3D11.MAP_READ, 0, out D3D11.MAPPED_SUBRESOURCE mapped);
            if (mhr < 0) { fail = $"map hr=0x{mhr:X8}"; return false; }
            try
            {
                int srcStride = (int)mapped.RowLength;
                int validBytes = validW * 4;
                for (int row = 0; row < h; row++)
                {
                    int sy = row + cy0 - ry;   // linha da staging correspondente
                    int dstOff = row * w * 4;
                    if (sy >= 0 && sy < validH)
                    {
                        IntPtr sp = mapped.pData + (nint)((long)sy * srcStride);
                        Marshal.Copy(sp, dst, dstOff, validBytes);
                    }
                    // fora da área válida (região atravessando borda): permanece zero
                    for (int z = dstOff + validBytes; z < dstOff + w * 4; z++) dst[z] = 0;
                }
            }
            finally
            {
                var unmap = D3D11.Fn<D3D11.DelUnmap>(_ctx, 15);
                unmap(_ctx, _staging, 0);
            }
            return true;
        }

        public void Dispose()
        {
            if (_fiPin.IsAllocated) _fiPin.Free();
            D3D11.SafeRelease(ref _staging);
            D3D11.SafeRelease(ref _last);
            D3D11.SafeRelease(ref _dup);
            D3D11.SafeRelease(ref _output1);
            D3D11.SafeRelease(ref _output);
            D3D11.SafeRelease(ref _adapter);
            D3D11.SafeRelease(ref _factory);
            if (_ownDevice)
            {
                D3D11.SafeRelease(ref _ctx);
                D3D11.SafeRelease(ref _dev);
            }
        }
    }
}
