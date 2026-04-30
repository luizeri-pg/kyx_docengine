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
        var candidates = new[]
        {
            "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
            "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
            "/usr/share/fonts/truetype/noto/NotoSans-Regular.ttf",
            "/Library/Fonts/Arial.ttf",
            Path.Combine(profile, "Library/Fonts/Arial.ttf"),
            "/System/Library/Fonts/Supplemental/Arial.ttf",
            string.IsNullOrEmpty(windir) ? null : Path.Combine(windir, "Fonts", "arial.ttf"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf")
        };

        foreach (var p in candidates)
        {
            if (string.IsNullOrEmpty(p) || !File.Exists(p))
            {
                continue;
            }

            try
            {
                var b = File.ReadAllBytes(p);
                if (b.Length > 1000)
                {
                    return b;
                }
            }
            catch
            {
                // tentar próximo caminho
            }
        }

        return null;
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
