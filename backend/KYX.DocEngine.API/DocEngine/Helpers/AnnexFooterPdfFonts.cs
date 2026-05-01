using System.Runtime.InteropServices;
using PdfSharp.Fonts;

namespace KYX.DocEngine.API.Helpers;

/// <summary>
/// PdfSharp 6.2 Core: sem <see cref="GlobalFontSettings.FontResolver"/> ou
/// <see cref="GlobalFontSettings.UseWindowsFontsUnderWindows"/>, <c>new XFont(...)</c> falha fora do Windows.
/// Registamos uma vez um resolver com um TTF do sistema (ou Arial no Windows com a flag oficial).
/// </summary>
internal static class AnnexFooterPdfFonts
{
    internal const string EmbeddedFamilyName = "DocEngineAnnexFooter";

    private static readonly object Gate = new();
    private static bool _attempted;
    private static string? _stampFamily;

    /// <summary>
    /// Família a passar a <see cref="PdfSharp.Drawing.XFont"/> (ex.: <c>DocEngineAnnexFooter</c> ou <c>Arial</c>).
    /// </summary>
    internal static string? StampFamily => _stampFamily;

    /// <summary>
    /// Garante que existe estratégia de fontes para o carimbo; thread-safe.
    /// </summary>
    internal static bool TryEnsureStampFont()
    {
        lock (Gate)
        {
            if (_attempted)
            {
                return _stampFamily != null;
            }

            _attempted = true;

            var bytes = TryLoadSystemSansTtf();
            if (bytes is { Length: > 0 })
            {
                GlobalFontSettings.FontResolver = new SingleTtfFontResolver(EmbeddedFamilyName, bytes);
                _stampFamily = EmbeddedFamilyName;
                return true;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                GlobalFontSettings.UseWindowsFontsUnderWindows = true;
                _stampFamily = "Arial";
                return true;
            }

            return false;
        }
    }

    private static byte[]? TryLoadSystemSansTtf()
    {
        var windir = Environment.GetEnvironmentVariable("WINDIR");
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // 1. Caminhos exactos por prioridade (TTF e OTF — PdfSharp lê ambos)
        var exactPaths = new[]
        {
            // Alpine Linux — ttf-freefont (Dockerfile.publish): pode ser .ttf ou .otf
            "/usr/share/fonts/freefont/FreeSans.ttf",
            "/usr/share/fonts/freefont/FreeSans.otf",
            "/usr/share/fonts/freefont/FreeSerif.ttf",
            "/usr/share/fonts/freefont/FreeSerif.otf",
            "/usr/share/fonts/freefont/FreeMono.ttf",
            "/usr/share/fonts/freefont/FreeMono.otf",
            // Alpine Linux — font-dejavu
            "/usr/share/fonts/dejavu/DejaVuSans.ttf",
            "/usr/share/fonts/TTF/DejaVuSans.ttf",
            // Debian/Ubuntu
            "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
            "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
            "/usr/share/fonts/truetype/noto/NotoSans-Regular.ttf",
            // macOS
            "/Library/Fonts/Arial.ttf",
            Path.Combine(profile, "Library/Fonts/Arial.ttf"),
            "/System/Library/Fonts/Supplemental/Arial.ttf",
            // Windows
            string.IsNullOrEmpty(windir) ? null : Path.Combine(windir, "Fonts", "arial.ttf"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf")
        };

        foreach (var p in exactPaths)
        {
            var b = TryReadFont(p);
            if (b != null) return b;
        }

        // 2. Fallback: varrer directórios comuns procurando qualquer TTF/OTF
        var searchDirs = new[]
        {
            "/usr/share/fonts/freefont",
            "/usr/share/fonts/dejavu",
            "/usr/share/fonts/TTF",
            "/usr/share/fonts/truetype",
            "/usr/share/fonts",
            Path.Combine(profile, "Library/Fonts"),
            "/Library/Fonts",
            "/System/Library/Fonts"
        };

        foreach (var dir in searchDirs)
        {
            if (!Directory.Exists(dir)) continue;
            try
            {
                foreach (var ext in new[] { "*.ttf", "*.otf" })
                {
                    foreach (var f in Directory.GetFiles(dir, ext, SearchOption.AllDirectories))
                    {
                        var b = TryReadFont(f);
                        if (b != null) return b;
                    }
                }
            }
            catch
            {
                // directório sem permissão — tentar o próximo
            }
        }

        return null;
    }

    private static byte[]? TryReadFont(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        try
        {
            var b = File.ReadAllBytes(path);
            return b.Length > 1000 ? b : null;
        }
        catch
        {
            return null;
        }
    }

    private sealed class SingleTtfFontResolver : IFontResolver
    {
        private readonly string _family;
        private readonly byte[] _fontBytes;
        private const string FaceKey = "DocEngineAnnexFooterFace";

        public SingleTtfFontResolver(string family, byte[] fontBytes)
        {
            _family = family;
            _fontBytes = fontBytes;
        }

        public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            if (!familyName.Equals(_family, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // Simulação de bold no PdfSharp 6.2: "Not implemented and must be false" — usamos só o TTF regular.
            return new FontResolverInfo(FaceKey, mustSimulateBold: false, mustSimulateItalic: isItalic);
        }

        public byte[] GetFont(string faceName) =>
            faceName.Equals(FaceKey, StringComparison.Ordinal)
                ? _fontBytes
                : throw new InvalidOperationException($"Face de fonte desconhecida para o carimbo: {faceName}");
    }
}
