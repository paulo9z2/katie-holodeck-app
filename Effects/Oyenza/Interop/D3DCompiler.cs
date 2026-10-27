using System.Runtime.InteropServices;

namespace KatieHoloDeck.LiquidGlass.Interop;

/// <summary>
/// Compilação de shader em runtime via d3dcompiler_47.dll (System32, presente em
/// todo Windows 11 — verificado 4.669.440 bytes em 27/10). Zero-NuGet: nenhum
/// .cso pré-compilado, nenhum pacote — o HLSL é o artefato e compila no boot
/// (custo único de ~ms; cache futuro se precisar).
/// </summary>
internal static class D3DCompiler
{
    // LPCSTR em toda a assinatura (ANSI — UTF-16 truncava o target no 1º char, X3506)
    [DllImport("d3dcompiler_47.dll", CharSet = CharSet.Ansi, EntryPoint = "D3DCompile")]
    private static extern int D3DCompile(
        IntPtr pSrcData, nuint SrcDataSize, string pSourceName,
        IntPtr pDefines, IntPtr pInclude,
        string pEntrypoint, string pTarget,
        uint Flags1, uint Flags2,
        out IntPtr ppCode, out IntPtr ppErrorMsgs);

    // ID3DBlob: IUnknown(0-2) + GetBufferPointer=3, GetBufferSize=4
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr DelGetBufferPointer(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate nuint DelGetBufferSize(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint DelRelease(IntPtr self);

    /// <summary>Compila e devolve o bytecode (caller dá Release no IntPtr).</summary>
    public static byte[] Compile(string hlslSource, string entryPoint, string target)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(hlslSource);
        int hr;
        var pin = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            hr = D3DCompile(pin.AddrOfPinnedObject(), (nuint)bytes.Length, "GlassGpu.hlsl",
                IntPtr.Zero, IntPtr.Zero, entryPoint, target, 0, 0,
                out IntPtr code, out IntPtr errs);
            if (hr < 0)
            {
                string msg = ReadBlobString(errs);
                D3D11.SafeRelease(ref errs);
                throw new InvalidOperationException($"D3DCompile({entryPoint}/{target}) hr=0x{hr:X8}: {msg}");
            }
            var blob = ReadBlobBytes(code);
            D3D11.SafeRelease(ref code);
            return blob;
        }
        finally { pin.Free(); }
    }

    private static byte[] ReadBlobBytes(IntPtr blob)
    {
        var ptr = Marshal.GetDelegateForFunctionPointer<DelGetBufferPointer>(D3D11.Vtbl(blob, 3))(blob);
        var len = Marshal.GetDelegateForFunctionPointer<DelGetBufferSize>(D3D11.Vtbl(blob, 4))(blob);
        var result = new byte[(int)len];
        Marshal.Copy(ptr, result, 0, (int)len);
        return result;
    }

    private static string ReadBlobString(IntPtr blob)
    {
        if (blob == IntPtr.Zero) return "(sem blob de erro)";
        try { return System.Text.Encoding.UTF8.GetString(ReadBlobBytes(blob)); }
        catch { return "(blob de erro ilegível)"; }
    }
}
