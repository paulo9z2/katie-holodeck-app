using System.Runtime.InteropServices;

namespace KatieHoloDeck.LiquidGlass.Interop;

/// <summary>
/// P/Invoke D3D11 puro (rota B do BRIEF — zero-NuGet, fiel à doutrina do app).
/// SLOTS VERIFICADOS contra a VTABLE C do d3d11.h mingw-w64 (parse 27/10):
/// ID3D11Device herda IUnknown DIRETO → CreateBuffer=3, CreateTexture2D=5,
/// CreateSRV=7, CreateRTV=9, CreateVS=12, CreatePS=15, CreateSampler=23,
/// GetImmediateContext=40.
/// ID3D11DeviceContext herda ID3D11DeviceChild (+4 slots de GetDevice/GetPrivateData/
/// SetPrivateData/SetPrivateDataInterface) → PSSetSR=8, PSSetShader=9, PSSetSamplers=10,
/// VSSetShader=11, Draw=13, Map=14, Unmap=15, PSSetCB=16, IASetTopology=24,
/// OMSetRT=33, RSSetViewports=44, CopyResource=47, UpdateSubresource=48,
/// GenerateMips=54, Flush=111. IUnknown: QueryInterface=0, AddRef=1, Release=2.
/// LIÇÃO: parse do bloco C++ do header NÃO inclui métodos herdados — usar a
/// vtable C (struct ...Vtbl) ou somar a herança; slot errado = AV incapturável.
/// </summary>
internal static class D3D11
{
    // ---- Constantes ----
    public const int FORMAT_B8G8R8A8_UNORM = 87;
    // D3D11_BIND_FLAG — verificado contra d3d11.h mingw-w64 27/10 (meu 1º chute era
    // D3D10: RT=0x20 e não 0x2; CB=0x4 e não 0x10. SRV=0x8 estava certo).
    public const uint BIND_VERTEX_BUFFER = 0x1;
    public const uint BIND_INDEX_BUFFER = 0x2;
    public const uint BIND_CONSTANT_BUFFER = 0x4;
    public const uint BIND_SHADER_RESOURCE = 0x8;
    public const uint BIND_RENDER_TARGET = 0x20;
    public const uint BIND_DEPTH_STENCIL = 0x40;
    public const uint MISC_GENERATE_MIPS = 0x1;
    public const uint USAGE_DEFAULT = 0;
    public const uint USAGE_STAGING = 3;
    public const uint CPU_ACCESS_READ = 0x20000;
    public const uint MAP_READ = 1;
    public const uint TOPOLOGY_TRIANGLELIST = 4;
    public const int FILTER_MIN_MAG_MIP_LINEAR = 0x15;
    public const int ADDRESS_CLAMP = 3;
    public const int COMPARISON_NEVER = 1;
    public const uint D3D11_SDK_VERSION = 7;
    public const int DRIVER_TYPE_HARDWARE = 1;
    public const int FEATURE_LEVEL_11_0 = 0xB000;

    // ---- Structs ----
    [StructLayout(LayoutKind.Sequential)]
    public struct TEXTURE2D_DESC
    {
        public uint Width, Height, MipLevels, ArraySize;
        public int Format;
        public uint SampleDescCount, SampleDescQuality;
        public uint Usage, BindFlags, CPUAccessFlags, MiscFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BUFFER_DESC
    {
        public uint ByteWidth, Usage, BindFlags, CPUAccessFlags, MiscFlags, StructureByteStride;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SUBRESOURCE_DATA
    {
        public IntPtr pSysMem;
        public uint SysMemPitch, SysMemSlicePitch;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MAPPED_SUBRESOURCE
    {
        public IntPtr pData;
        public uint RowLength, DepthPitch;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VIEWPORT
    {
        public float TopLeftX, TopLeftY, Width, Height, MinDepth, MaxDepth;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SAMPLER_DESC
    {
        public int Filter;
        public int AddressU, AddressV, AddressW;
        public float MipLODBias;
        public uint MaxAnisotropy;
        public int ComparisonFunc;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] BorderColor;
        public float MinLOD, MaxLOD;
    }

    // ---- COM plumbing ----
    internal static IntPtr Vtbl(IntPtr obj, int slot)
        => Marshal.ReadIntPtr(Marshal.ReadIntPtr(obj, 0), slot * IntPtr.Size);

    internal static T Fn<T>(IntPtr obj, int slot) where T : class
        => Marshal.GetDelegateForFunctionPointer<T>(Vtbl(obj, slot));

    internal static void Release(IntPtr obj) { if (obj != IntPtr.Zero) _ = Fn<DelRelease>(obj, 2)(obj); }

    /// <summary>Release null-tolerante (blobs do compiler, handles opcionais).</summary>
    internal static void SafeRelease(ref IntPtr obj) { Release(obj); obj = IntPtr.Zero; }

    // ---- Delegates (slots verificados) ----
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate uint DelRelease(IntPtr self);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int DelCreateTexture2D(IntPtr self, ref TEXTURE2D_DESC desc, IntPtr pInitialData, out IntPtr ppTex);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int DelCreateShaderResourceView(IntPtr self, IntPtr pResource, IntPtr pDesc, out IntPtr ppSRV);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int DelCreateRenderTargetView(IntPtr self, IntPtr pResource, IntPtr pDesc, out IntPtr ppRTV);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int DelCreateVertexShader(IntPtr self, IntPtr pBytecode, nuint bytecodeLength, IntPtr pClassLinkage, out IntPtr ppVS);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int DelCreatePixelShader(IntPtr self, IntPtr pBytecode, nuint bytecodeLength, IntPtr pClassLinkage, out IntPtr ppPS);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int DelCreateBuffer(IntPtr self, ref BUFFER_DESC desc, ref SUBRESOURCE_DATA initData, out IntPtr ppBuffer);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int DelCreateSamplerState(IntPtr self, ref SAMPLER_DESC desc, out IntPtr ppSampler);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate void DelGetImmediateContext(IntPtr self, out IntPtr ppContext);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate void DelPSSetShaderResources(IntPtr self, uint startSlot, uint numViews, IntPtr[] ppSRVs);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate void DelPSSetShader(IntPtr self, IntPtr pPS, IntPtr ppClassInstances, uint numClassInstances);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate void DelVSSetShader(IntPtr self, IntPtr pVS, IntPtr ppClassInstances, uint numClassInstances);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate void DelPSSetSamplers(IntPtr self, uint startSlot, uint numSamplers, IntPtr[] ppSamplers);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate void DelPSSetConstantBuffers(IntPtr self, uint startSlot, uint numBuffers, IntPtr[] ppCBs);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate void DelDraw(IntPtr self, uint vertexCount, uint startVertexLocation);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int DelMap(IntPtr self, IntPtr pResource, uint subresource, uint mapType, uint mapFlags, out MAPPED_SUBRESOURCE pMapped);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate void DelUnmap(IntPtr self, IntPtr pResource, uint subresource);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate void DelIASetPrimitiveTopology(IntPtr self, uint topology);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate void DelOMSetRenderTargets(IntPtr self, uint numViews, IntPtr[] ppRTVs, IntPtr pDSV);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate void DelRSSetViewports(IntPtr self, uint numViewports, ref VIEWPORT pViewports);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate void DelUpdateSubresource(IntPtr self, IntPtr pDstResource, uint dstSubresource, IntPtr pDstBox, IntPtr pSrcData, uint srcRowPitch, uint srcDepthPitch);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate void DelCopyResource(IntPtr self, IntPtr pDst, IntPtr pSrc);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate void DelGenerateMips(IntPtr self, IntPtr pSRV);

    // ---- Entry points (função C exportada — sem vtable) ----
    [DllImport("d3d11.dll")]
    internal static extern int D3D11CreateDevice(
        IntPtr pAdapter, int driverType, IntPtr software, uint flags,
        [In] int[]? pFeatureLevels, uint featureLevels, uint sdkVersion,
        out IntPtr ppDevice, out int pFeatureLevel, out IntPtr ppImmediateContext);
}
