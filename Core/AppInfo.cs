using System;
using System.Reflection;

namespace KatieHoloDeck.Core
{
    /// <summary>
    /// F2.0 — fonte única da versão exibida. O número mora no <c>.csproj</c>
    /// (<c>&lt;Version&gt;</c> → AssemblyVersion); aqui só formatamos o que o assembly
    /// carrega. Zero constante manual de versão em C#/XAML: mudar o csproj muda
    /// Settings, barra e Propriedades do .exe de uma vez.
    /// </summary>
    public static class AppInfo
    {
        /// <summary>Versão pronta para exibição: <c>"v1.2.3"</c> (major.minor.build).</summary>
        public static readonly string VersionString = BuildVersionString();

        private static string BuildVersionString()
        {
            Version? v = Assembly.GetExecutingAssembly().GetName().Version;
            return v == null ? "v0.0.0" : "v" + v.ToString(3);
        }
    }
}
