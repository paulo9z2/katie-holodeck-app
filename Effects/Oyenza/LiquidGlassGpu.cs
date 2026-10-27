using System;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using KatieHoloDeck.LiquidGlass.Interop;

namespace KatieHoloDeck.LiquidGlass;

/// <summary>
/// Motor GPU do Liquid Glass Oyenza — twin do LiquidGlassCpu (mesmo contrato:
/// ctor → Width/Height/Output → Distort(src) → Dispose) para plugar no mount
/// existente com fallback transparente pro CPU.
/// Rota B do BRIEF: P/Invoke puro (zero-NuGet), HLSL compilado em runtime
/// (d3dcompiler_47), frost via GenerateMips — sem Direct2D, sem passes extras.
/// Um draw por frame: upload src → mips → Draw(3) → CopyResource → Map staging.
/// </summary>
public sealed class LiquidGlassGpu : IDisposable
{
    public int Width { get; }
    public int Height { get; }
    public byte[] Output { get; } = Array.Empty<byte>();
    /// <summary>false se qualquer passo do init falhou — caller usa o CPU.</summary>
    public bool Ready { get; private set; }
    public string? FailReason { get; private set; }

    // D3D
    private IntPtr _device;
    private IntPtr _ctx;
    private IntPtr _srcTex, _srcSrv;      // backdrop + mip chain (frost)
    private IntPtr _outTex, _outRtv;      // render target do glass
    private IntPtr _stagingTex;           // readback CPU
    private IntPtr _cbuf;                 // GlassParams 176 B
    private IntPtr _vs, _ps;
    private IntPtr _sampler;

    // Parâmetros (guardados p/ rebuild do cbuffer no flip)
    private readonly float _innerW, _innerH;      // px
    private readonly float _radiusPx, _shoulderPx;
    private readonly float _bevelPx, _magPx;
    private readonly float _frostMix, _shadowReachPx, _shadowDrop;
    private readonly float _saturation, _dispersionPx, _padPx;
    private readonly uint _w, _h;

    private FlipState _flip;
    private long _tickNo;
    private bool _disposed;

    private const string ShaderRelativePath = "Effects\\Oyenza\\GlassGpu.hlsl";

    public LiquidGlassGpu(double innerW, double innerH,
        double radiusDip, double shoulderDip, double bevelDip, double magDip,
        double padDip, double dpi, double chromatic)
    {
        try
        {
            float dpiF = (float)(double.IsFinite(dpi) && dpi > 0 ? Math.Max(0.5, dpi) : 1.0);
            // PARIDADE COM O CPU: W = round((inner + 2*pad) * dpi), clamp 4..8192
            _w = (uint)Math.Clamp((int)Math.Round((innerW + 2 * padDip) * dpiF), 4, 8192);
            _h = (uint)Math.Clamp((int)Math.Round((innerH + 2 * padDip) * dpiF), 4, 8192);
            Width = (int)_w; Height = (int)_h;
            Output = new byte[_w * _h * 4];

            _innerW = (float)(innerW * dpiF);
            _innerH = (float)(innerH * dpiF);
            _radiusPx = (float)(radiusDip * dpiF);
            _shoulderPx = (float)(shoulderDip * dpiF);
            _bevelPx = (float)(bevelDip * dpiF);
            _magPx = (float)(magDip * dpiF);
            _padPx = (float)(padDip * dpiF);
            _saturation = GlassParams.SATURATION;
            _dispersionPx = _magPx * (float)chromatic * GlassConsts.ChromaticK;

            // Espessura Oyenza pela altura da face (mesma curva do GlassMath)
            var th = GlassMath.Thickness((float)innerH);
            _frostMix = Math.Clamp(th.frostMix, 0f, 1f);
            _shadowReachPx = (float)(th.shadowReach * dpiF);
            _shadowDrop = th.shadowDrop;

            InitDevice();
            InitShaders();
            InitResources();
            UploadParams(0f);
            Ready = true;
        }
        catch (Exception ex)
        {
            FailReason = ex.Message;
            Ready = false;
            DisposeCore();
        }
    }

    // ------------------------------------------------------------------
    // Init
    // ------------------------------------------------------------------
    private void InitDevice()
    {
        int hr = D3D11.D3D11CreateDevice(IntPtr.Zero, D3D11.DRIVER_TYPE_HARDWARE,
            IntPtr.Zero, 0, new[] { D3D11.FEATURE_LEVEL_11_0 }, 1, D3D11.D3D11_SDK_VERSION,
            out _device, out _, out _ctx);
        if (hr < 0 || _device == IntPtr.Zero || _ctx == IntPtr.Zero)
            throw new InvalidOperationException($"D3D11CreateDevice hr=0x{hr:X8}");
    }

    private void InitShaders()
    {
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ShaderRelativePath);
        if (!File.Exists(path))
            throw new FileNotFoundException($"shader não encontrado ao lado do exe: {path}");
        string hlsl = File.ReadAllText(path);

        var vs = D3DCompiler.Compile(hlsl, "VS_Main", "vs_5_0");
        var ps = D3DCompiler.Compile(hlsl, "PS_Main", "ps_5_0");

        var pinVs = GCHandle.Alloc(vs, GCHandleType.Pinned);
        var pinPs = GCHandle.Alloc(ps, GCHandleType.Pinned);
        try
        {
            // CreateVertexShader/CreatePixelShader levam (bytecode, LENGTH, classLinkage, out)
            int hr = D3D11.Fn<D3D11.DelCreateVertexShader>(_device, 12)(_device, pinVs.AddrOfPinnedObject(), (nuint)vs.Length, IntPtr.Zero, out _vs);
            if (hr < 0) throw new InvalidOperationException($"CreateVertexShader hr=0x{hr:X8}");
            hr = D3D11.Fn<D3D11.DelCreatePixelShader>(_device, 15)(_device, pinPs.AddrOfPinnedObject(), (nuint)ps.Length, IntPtr.Zero, out _ps);
            if (hr < 0) throw new InvalidOperationException($"CreatePixelShader hr=0x{hr:X8}");
        }
        finally { pinVs.Free(); pinPs.Free(); }
    }

    private void InitResources()
    {
        var dev = _device;
        int hr;

        // src: backdrop com mip chain completa (frost via GenerateMips).
        // MISC_GENERATE_MIPS EXIGE Bind = SRV|RTV (doc: blits internos por RT) — só SRV = E_INVALIDARG.
        var srcDesc = new D3D11.TEXTURE2D_DESC
        {
            Width = _w, Height = _h, MipLevels = 0, ArraySize = 1,
            Format = D3D11.FORMAT_B8G8R8A8_UNORM,
            SampleDescCount = 1, SampleDescQuality = 0,
            Usage = D3D11.USAGE_DEFAULT,
            BindFlags = D3D11.BIND_SHADER_RESOURCE | D3D11.BIND_RENDER_TARGET,
            CPUAccessFlags = 0,
            MiscFlags = D3D11.MISC_GENERATE_MIPS
        };
        hr = D3D11.Fn<D3D11.DelCreateTexture2D>(dev, 5)(dev, ref srcDesc, IntPtr.Zero, out _srcTex);
        if (hr < 0) throw new InvalidOperationException($"CreateTexture2D(src) hr=0x{hr:X8}");
        hr = D3D11.Fn<D3D11.DelCreateShaderResourceView>(dev, 7)(dev, _srcTex, IntPtr.Zero, out _srcSrv);
        if (hr < 0) throw new InvalidOperationException($"CreateSRV(src) hr=0x{hr:X8}");

        // out: render target do glass (1 mip)
        var outDesc = new D3D11.TEXTURE2D_DESC
        {
            Width = _w, Height = _h, MipLevels = 1, ArraySize = 1,
            Format = D3D11.FORMAT_B8G8R8A8_UNORM,
            SampleDescCount = 1, SampleDescQuality = 0,
            Usage = D3D11.USAGE_DEFAULT,
            BindFlags = D3D11.BIND_RENDER_TARGET,
            CPUAccessFlags = 0, MiscFlags = 0
        };
        hr = D3D11.Fn<D3D11.DelCreateTexture2D>(dev, 5)(dev, ref outDesc, IntPtr.Zero, out _outTex);
        if (hr < 0) throw new InvalidOperationException($"CreateTexture2D(out) hr=0x{hr:X8}");
        hr = D3D11.Fn<D3D11.DelCreateRenderTargetView>(dev, 9)(dev, _outTex, IntPtr.Zero, out _outRtv);
        if (hr < 0) throw new InvalidOperationException($"CreateRTV(out) hr=0x{hr:X8}");

        // staging: readback p/ WriteableBitmap
        var stgDesc = new D3D11.TEXTURE2D_DESC
        {
            Width = _w, Height = _h, MipLevels = 1, ArraySize = 1,
            Format = D3D11.FORMAT_B8G8R8A8_UNORM,
            SampleDescCount = 1, SampleDescQuality = 0,
            Usage = D3D11.USAGE_STAGING,
            BindFlags = 0,
            CPUAccessFlags = D3D11.CPU_ACCESS_READ,
            MiscFlags = 0
        };
        hr = D3D11.Fn<D3D11.DelCreateTexture2D>(dev, 5)(dev, ref stgDesc, IntPtr.Zero, out _stagingTex);
        if (hr < 0) throw new InvalidOperationException($"CreateTexture2D(staging) hr=0x{hr:X8}");

        // cbuffer (recriado a cada flip — ver UploadParams)
        // sampler linear/clamp
        var smpDesc = new D3D11.SAMPLER_DESC
        {
            Filter = D3D11.FILTER_MIN_MAG_MIP_LINEAR,
            AddressU = D3D11.ADDRESS_CLAMP,
            AddressV = D3D11.ADDRESS_CLAMP,
            AddressW = D3D11.ADDRESS_CLAMP,
            MipLODBias = 0f,
            MaxAnisotropy = 1,
            ComparisonFunc = D3D11.COMPARISON_NEVER,
            BorderColor = new float[4],
            MinLOD = 0f,
            MaxLOD = float.MaxValue
        };
        hr = D3D11.Fn<D3D11.DelCreateSamplerState>(dev, 23)(dev, ref smpDesc, out _sampler);
        if (hr < 0) throw new InvalidOperationException($"CreateSamplerState hr=0x{hr:X8}");
    }

    private void UploadParams(float lightMix)
    {
        var p = new GlassParams(
            body: new Vector2(_innerW, _innerH),
            shape: new Vector2(_radiusPx, _shoulderPx),
            pxPerDip: 1f,                       // tudo já em px aqui
            useCopy: 1,
            lens: new Vector4(_bevelPx, _magPx, GlassParams.RIM_POWER, _frostMix),
            tintDark: GlassConsts.TintDark,
            tintLight: GlassConsts.TintLight,
            lightDir: GlassConsts.LightDir,
            lightRim: 0.32f,
            lightGlow: 0.18f,
            shadow: new Vector4(0.28f, _shadowReachPx, _shadowDrop, lightMix),
            copy: new Vector4(_w, _h, _saturation, _dispersionPx),
            probe: Vector4.Zero,
            origin: new Vector2(_padPx, _padPx));

        D3D11.SafeRelease(ref _cbuf);
        var desc = new D3D11.BUFFER_DESC
        {
            ByteWidth = 176, Usage = D3D11.USAGE_DEFAULT,
            BindFlags = D3D11.BIND_CONSTANT_BUFFER,
            CPUAccessFlags = 0, MiscFlags = 0, StructureByteStride = 0
        };
        var pin = GCHandle.Alloc(p, GCHandleType.Pinned);
        try
        {
            var data = new D3D11.SUBRESOURCE_DATA { pSysMem = pin.AddrOfPinnedObject() };
            int hr = D3D11.Fn<D3D11.DelCreateBuffer>(_device, 3)(_device, ref desc, ref data, out _cbuf);
            if (hr < 0) throw new InvalidOperationException($"CreateBuffer(cbuf) hr=0x{hr:X8}");
        }
        finally { pin.Free(); }
    }

    // ------------------------------------------------------------------
    // Frame
    // ------------------------------------------------------------------
    /// <summary>Um frame: src (RTB proxy) → glass GPU → Output.
    /// false = falha de GPU/runtime → caller degrada pro CPU e descarta o twin.</summary>
    public bool TryDistort(byte[] src)
    {
        if (!Ready || _disposed) return false;
        if (src == null || src.Length < (int)(_w * _h * 4)) return false;

        try
        {
            // SLOTS DE CONTEXTO: ID3D11DeviceContext herda ID3D11DeviceChild (+4 slots:
            // GetDevice/GetPrivateData/SetPrivateData/SetPrivateDataInterface) antes dos
            // métodos próprios — vtable C completa do mingw-w64 (parse 27/10, 115 entradas).
            var updateSub = D3D11.Fn<D3D11.DelUpdateSubresource>(_ctx, 48);
            var genMips = D3D11.Fn<D3D11.DelGenerateMips>(_ctx, 54);
            var setRT = D3D11.Fn<D3D11.DelOMSetRenderTargets>(_ctx, 33);
            var setVP = D3D11.Fn<D3D11.DelRSSetViewports>(_ctx, 44);
            var setSR = D3D11.Fn<D3D11.DelPSSetShaderResources>(_ctx, 8);
            var setPS = D3D11.Fn<D3D11.DelPSSetShader>(_ctx, 9);
            var setVS = D3D11.Fn<D3D11.DelVSSetShader>(_ctx, 11);
            var setSmp = D3D11.Fn<D3D11.DelPSSetSamplers>(_ctx, 10);
            var setCB = D3D11.Fn<D3D11.DelPSSetConstantBuffers>(_ctx, 16);
            var setTopo = D3D11.Fn<D3D11.DelIASetPrimitiveTopology>(_ctx, 24);
            var draw = D3D11.Fn<D3D11.DelDraw>(_ctx, 13);
            var copyRes = D3D11.Fn<D3D11.DelCopyResource>(_ctx, 47);
            var map = D3D11.Fn<D3D11.DelMap>(_ctx, 14);
            var unmap = D3D11.Fn<D3D11.DelUnmap>(_ctx, 15);

            // 1. upload do backdrop (mip 0) + regenera mips pro frost
            var pin = GCHandle.Alloc(src, GCHandleType.Pinned);
            try { updateSub(_ctx, _srcTex, 0, IntPtr.Zero, pin.AddrOfPinnedObject(), _w * 4, 0); }
            finally { pin.Free(); }
            genMips(_ctx, _srcSrv);

            // 2. flip claro/escuro: luma médio (amostrado) com dead band a cada 3 ticks
            _tickNo++;
            if (_tickNo % 3 == 0)
            {
                float lm = SampleLuma(src);
                float was = _flip.IsLight ? 1f : 0f;
                float now = _flip.Update(lm, dt: 0.033f * 3f);
                if (now != was) UploadParams(now);
            }

            // 3. draw
            var vp = new D3D11.VIEWPORT
            { TopLeftX = 0f, TopLeftY = 0f, Width = _w, Height = _h, MinDepth = 0f, MaxDepth = 1f };
            setRT(_ctx, 1, new[] { _outRtv }, IntPtr.Zero);
            setVP(_ctx, 1, ref vp);
            setSR(_ctx, 0, 1, new[] { _srcSrv });
            setPS(_ctx, _ps, IntPtr.Zero, 0);
            setVS(_ctx, _vs, IntPtr.Zero, 0);
            setSmp(_ctx, 0, 1, new[] { _sampler });
            setCB(_ctx, 0, 1, new[] { _cbuf });
            setTopo(_ctx, D3D11.TOPOLOGY_TRIANGLELIST);
            draw(_ctx, 3, 0);

            // 4. readback
            copyRes(_ctx, _stagingTex, _outTex);
            int hr = map(_ctx, _stagingTex, 0, D3D11.MAP_READ, 0, out D3D11.MAPPED_SUBRESOURCE mapped);
            if (hr < 0) throw new InvalidOperationException($"Map hr=0x{hr:X8}");
            try
            {
                uint row = mapped.RowLength == 0 ? _w * 4 : mapped.RowLength;
                int dst = 0;
                IntPtr srcPtr = mapped.pData;
                int w4 = (int)(_w * 4);
                for (uint y = 0; y < _h; y++)
                {
                    Marshal.Copy(srcPtr, Output, dst, w4);
                    srcPtr += (int)row;
                    dst += w4;
                }
            }
            finally { unmap(_ctx, _stagingTex, 0); }

            return true;
        }
        catch (Exception ex)
        {
            Ready = false;
            FailReason = ex.Message;
            return false;
        }
    }

    private D3D11.VIEWPORT VP => new()
    { TopLeftX = 0f, TopLeftY = 0f, Width = _w, Height = _h, MinDepth = 0f, MaxDepth = 1f };
    private void Unused() { _ = VP; } // mantém o shape num só lugar; VP usado via local no TryDistort

    /// <summary>Luma sRGB médio (amostras espaçadas, só pixels opacos).</summary>
    private static float SampleLuma(byte[] src)
    {
        const int samples = 256;
        int len = src.Length;
        int stride = Math.Max(4, len / 4 / samples * 4);
        double sum = 0; int count = 0;
        for (int i = 0; i + 3 < len; i += stride)
        {
            if (src[i + 3] < 250) continue;   // só área visível
            double r = src[i + 2] / 255.0, g = src[i + 1] / 255.0, b = src[i] / 255.0; // BGRA
            sum += 0.2126 * r + 0.7152 * g + 0.0722 * b;
            count++;
        }
        return count == 0 ? 0f : (float)(sum / count);
    }

    // ------------------------------------------------------------------
    // Dispose
    // ------------------------------------------------------------------
    private void DisposeCore()
    {
        D3D11.SafeRelease(ref _sampler);
        D3D11.SafeRelease(ref _ps);
        D3D11.SafeRelease(ref _vs);
        D3D11.SafeRelease(ref _cbuf);
        D3D11.SafeRelease(ref _stagingTex);
        D3D11.SafeRelease(ref _outRtv);
        D3D11.SafeRelease(ref _outTex);
        D3D11.SafeRelease(ref _srcSrv);
        D3D11.SafeRelease(ref _srcTex);
        D3D11.SafeRelease(ref _ctx);
        D3D11.SafeRelease(ref _device);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DisposeCore();
        GC.SuppressFinalize(this);
    }
}
