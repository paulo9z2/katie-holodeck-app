using System;
using System.Runtime.InteropServices;

namespace KatieHoloDeck.LiquidGlass.Interop
{
    /// <summary>
    /// P/Invoke DXGI puro (rota B — zero-NuGet): Output Duplication, a captura da
    /// tela REAL atrás da janela (mesma via do Oyenza/Poni no Windows).
    ///
    /// SLOTS verificados contra as VTABLES C do mingw-w64 em 27/10 (lição da skill:
    /// parse de vtable C, nunca do bloco C++ que omite herança):
    /// IDXGIFactory1: EnumAdapters=7 (IUnknown3+IDXGIObject4+IDXGIFactory5).
    /// IDXGIAdapter: EnumOutputs=7, GetDesc=8.
    /// IDXGIOutput: GetDesc=7 (IUnknown3+IDXGIObject4+Output1:GetDesc...).
    /// IDXGIOutput1: DuplicateOutput=22 (herda IDXGIOutput inteiro: 19+3 slots).
    /// IDXGIOutputDuplication: GetDesc=7, AcquireNextFrame=8, ReleaseFrame=14.
    /// IUnknown: QueryInterface=0, AddRef=1, Release=2.
    ///
    /// DXGI_OUTDUPL_FRAME_INFO real (dxgi1_2.h): LastPresentTime(8), LastMouseUpdateTime(8),
    /// AccumulatedFrames(4), RectsCoalesced(4), ProtectedContentMaskedOut(4),
    /// PointerPosition(POINT 8 + BOOL 4 = 12), TotalMetadataBufferSize(4),
    /// PointerShapeBufferSize(4) = 48 bytes. Passamos um long[6] pinned — só o
    /// AcquireNextFrame escreve, não lemos campos (não precisamos de nada dele).
    /// </summary>
    internal static class DXGI
    {
        public static readonly Guid IID_IDXGIFactory1 = new("770aae78-f26f-4dba-a829-253c83d1b387");
        public static readonly Guid IID_IDXGIOutput1 = new("00cddea8-939b-4b83-a340-a685226666cc");
        // d3d11.h: o ppDesktopResource do AcquireNextFrame é IDXGIResource — QI pra textura real
        public static readonly Guid IID_ID3D11Texture2D = new("6f15aaf2-d208-4e89-9ab4-489535d34f9c");

        // HRESULTs DXGI — valores REAIS do sistema (doc MS usa bit 27 setado, 0x887A002X;
        // a macro MAKE_DXGI_HRESULT clássica geraria 0x87A002X e NÃO é o que volta).
        // WAIT_TIMEOUT confirmado EMPIRICAMENTE no teste headless (acquire em tela
        // estática devolveu 0x887A0027). ACCESS_LOST = 0x887A0006 (idem família).
        public const int S_OK = 0;
        public const int DXGI_ERROR_WAIT_TIMEOUT = unchecked((int)0x887A0027);
        public const int DXGI_ERROR_ACCESS_LOST = unchecked((int)0x887A0006);

        [DllImport("dxgi.dll")]
        public static extern int CreateDXGIFactory1(ref Guid riid, out IntPtr ppFactory);

        // WDA_EXCLUDEFROMCAPTURE: a janela sai de TODA captura (OBS/print/duplication)
        // mas continua visível na tela — sem isso o vidro refrata a si mesmo (loop).
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint dwAffinity);
        public const uint WDA_NONE = 0x0;
        public const uint WDA_EXCLUDEFROMCAPTURE = 0x11;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int L, T, R, B; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct OUTPUT_DESC
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
            public RECT DesktopCoordinates;
            public int AttachedToDesktop;   // BOOL
            public int Rotation;            // DXGI_MODE_ROTATION
        }

        // D3D11_BOX (d3d11.h): left, top, FRONT, right, bottom, back — front no MEIO,
        // pegadinha clássica; Box para 2D: Front=0, Back=1.
        [StructLayout(LayoutKind.Sequential)]
        public struct BOX { public uint Left, Top, Front, Right, Bottom, Back; }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate int DelQueryInterface(IntPtr self, ref Guid riid, out IntPtr ppvObject);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate int DelEnumAdapters(IntPtr self, uint Adapter, out IntPtr ppAdapter);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate int DelEnumOutputs(IntPtr self, uint Output, out IntPtr ppOutput);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate int DelGetOutputDesc(IntPtr self, out OUTPUT_DESC pDesc);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate int DelDuplicateOutput(IntPtr self, IntPtr pDevice, out IntPtr ppOutputDuplication);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate int DelAcquireNextFrame(IntPtr self, uint TimeoutInMilliseconds, IntPtr pFrameInfo, out IntPtr ppDesktopResource);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate int DelReleaseFrame(IntPtr self);
        // ID3D11DeviceContext::CopySubresourceRegion (slot 46): pSrcBox const* → ref
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate void DelCopySubresourceRegion(IntPtr self, IntPtr pDstResource, uint dstSubresource, uint dstX, uint dstY, uint dstZ, IntPtr pSrcResource, uint srcSubresource, ref BOX pSrcBox);

        /// <summary>Delegate de slot de vtable — reusa o helper do D3D11 (mesma assemblia).</summary>
        internal static T Fn<T>(IntPtr obj, int slot) where T : class => D3D11.Fn<T>(obj, slot);
    }
}
