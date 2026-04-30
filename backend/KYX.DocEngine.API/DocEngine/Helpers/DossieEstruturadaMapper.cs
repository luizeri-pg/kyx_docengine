using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text.Json;
using KYX.DocEngine.API.Models.DTOs.Documents;
using Microsoft.Extensions.Logging;

namespace KYX.DocEngine.API.Helpers;

/// <summary>
/// Converte o payload aninhado do dossiê Simplix (cliente, captura, trilhaEventos, …) no dicionário
/// de chaves <c>{{MAIÚSCULAS}}</c> esperado pelo motor HTML — equivalente a
/// <c>docs/scripts/estrutura-dossie-to-flat-dados.mjs</c> + merge com <c>dossie-simplix-template-dados.sem-imagens.json</c>.
/// </summary>
public static class DossieEstruturadaMapper
{
    private const string DefaultLogoDataUri =
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    /// <summary>Título do cabeçalho no HTML do dossiê (<c>{{DOSSIE_HEADER_TITULO}}</c>) quando o pedido não envia um.</summary>
    public const string DefaultHeaderTitulo = "Dossiê probatório – Contratação Digital Simplix";

    /// <summary>
    /// Caminho relativo ao ContentRoot (repo: <c>docs/templates/...</c>).
    /// </summary>
    public static readonly string SemImagensRelativePath =
        "../../docs/templates/dossie-simplix-template-dados.sem-imagens.json";

    public static bool IsDossieStructuredPayload(JsonElement dados) =>
        dados.ValueKind == JsonValueKind.Object
        && TryGetPropertyIgnoreCase(dados, "cliente", out var cliente)
        && cliente.ValueKind == JsonValueKind.Object;

    /// <summary>
    /// Tenta converter <paramref name="dados"/> aninhado em chaves planas + PDFs de <c>anexosPdf</c>.
    /// </summary>
    public static bool TryResolve(
        JsonElement dados,
        string contentRootPath,
        ILogger? logger,
        out Dictionary<string, string> flat,
        out List<PdfAnexoPayload>? pdfsFromAnexos)
    {
        pdfsFromAnexos = null;
        flat = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!IsDossieStructuredPayload(dados))
        {
            return false;
        }

        try
        {
            var fromEstrutura = BuildTemplateDadosFromEstrutura(dados);
            MergeSemImagensStrings(contentRootPath, logger, flat);
            foreach (var kv in fromEstrutura)
            {
                flat[kv.Key] = kv.Value;
            }

            StripCcbCredEmitCorrespIfNoStructuredCcb(dados, flat);

            if (string.IsNullOrWhiteSpace(flat.GetValueOrDefault("LOGO")))
            {
                flat["LOGO"] = DefaultLogoDataUri;
            }

            if (string.IsNullOrWhiteSpace(flat.GetValueOrDefault("DOSSIE_HEADER_TITULO")))
            {
                flat["DOSSIE_HEADER_TITULO"] = DefaultHeaderTitulo;
            }

            pdfsFromAnexos = BuildPdfsAnexosForApi(dados);
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Falha ao mapear payload estruturado do dossiê; usa-se achatamento JSON.");
            flat = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            pdfsFromAnexos = null;
            return false;
        }
    }

    private static void MergeSemImagensStrings(string contentRootPath, ILogger? logger, Dictionary<string, string> target)
    {
        var path = Path.GetFullPath(Path.Combine(contentRootPath, SemImagensRelativePath));
        if (!File.Exists(path))
        {
            logger?.LogWarning("Ficheiro sem-imagens não encontrado em {Path}; merge só com dados do payload.", path);
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("dados", out var dadosSem)
                || dadosSem.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            foreach (var p in dadosSem.EnumerateObject())
            {
                if (p.Name.StartsWith("_", StringComparison.Ordinal))
                {
                    continue;
                }

                if (p.Value.ValueKind == JsonValueKind.String)
                {
                    target[p.Name] = p.Value.GetString() ?? "";
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Erro ao ler merge sem-imagens.");
        }
    }

    private static Dictionary<string, string> BuildTemplateDadosFromEstrutura(JsonElement e)
    {
        _ = TryGetPropertyIgnoreCase(e, "cliente", out var cliente);
        _ = TryGetPropertyIgnoreCase(e, "captura", out var captura);

        var ctx = new CapturaCtx(
            GetString(captura, "ip"),
            GetString(captura, "latitude"),
            GetString(captura, "longitude"),
            GetString(captura, "userAgent"));

        var trilhaRaw = NormalizeTrilhaEventosRows(e);
        var eventosHtml = string.Join(
            "\n",
            trilhaRaw.Select(row => EvtRow(
                GetString(row, "evento", "tipo", "nome"),
                row.TryGetProperty("dataHoraHtml", out var dh) && dh.ValueKind == JsonValueKind.String
                    ? dh.GetString() ?? ""
                    : EscapeHtml(GetString(row, "dataHora")),
                BuildIdentificacaoCell(row, ctx))));

        var logsRaw = NormalizeLogsInteracaoRows(e);
        var interacoesHtml = string.Join(
            "\n",
            logsRaw.Select(row =>
            {
                var colData = row.TryGetProperty("dataHoraHtml", out var dh) && dh.ValueKind == JsonValueKind.String
                    ? dh.GetString() ?? ""
                    : EscapeHtml(GetString(row, "dataHora"));
                var colTexto = row.TryGetProperty("interacaoHtml", out var ih) && ih.ValueKind == JsonValueKind.String
                    ? ih.GetString() ?? ""
                    : EscapeHtml(GetString(row, "texto", "interacao"));
                return InteracaoRow(colData, GetString(row, "tipo", "origem"), colTexto);
            }));

        TryGetPropertyIgnoreCase(e, "logsCabecalho", out var logsCabecalho);
        TryGetPropertyIgnoreCase(e, "logsInteracao", out var logsInteracaoRoot);

        var nomeCliente = SafeGetString(logsCabecalho, "nome", "nomeCliente");
        if (string.IsNullOrEmpty(nomeCliente))
        {
            nomeCliente = GetString(cliente, "nome");
        }

        var proto = FirstNonEmpty(
            SafeGetString(logsCabecalho, "protocoloAtendimento", "protocolo"),
            GetString(e, "protocoloAtendimento"),
            logsInteracaoRoot.ValueKind == JsonValueKind.Object
                ? GetString(logsInteracaoRoot, "protocolo")
                : "");
        var inicio = FirstNonEmpty(
            SafeGetString(logsCabecalho, "dataHoraInicioAtendimento", "inicioAtendimento"),
            GetString(e, "atendimentoInicio"),
            logsInteracaoRoot.ValueKind == JsonValueKind.Object
                ? GetString(logsInteracaoRoot, "dataHoraInicio")
                : "");

        var outDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CLIENTE_NOME"] = nomeCliente,
            ["CLIENTE_CPF"] = GetString(cliente, "cpf"),
            ["CLIENTE_NASCIMENTO"] = GetString(cliente, "nascimento"),
            ["CAPTURA_DATA_HORA"] = GetString(captura, "dataHora"),
            ["CAPTURA_LATITUDE"] = GetString(captura, "latitude"),
            ["CAPTURA_LONGITUDE"] = GetString(captura, "longitude"),
            ["CAPTURA_IP"] = GetString(captura, "ip"),
            ["CAPTURA_PORTA"] = GetString(captura, "porta"),
            ["CAPTURA_MODELO_OS"] = GetString(captura, "modeloOs", "modeloOS"),
            ["PROTOCOLO_ATENDIMENTO"] = proto,
            ["ATENDIMENTO_INICIO"] = inicio,
            ["EVENTOS_HTML"] = eventosHtml,
            ["INTERACOES_HTML"] = interacoesHtml,
            ["IMG_CLIENTE_FOTO"] = NormalizeImageDataUri(GetString(cliente, "fotoBase64", "fotoEmBase64"))
        };

        if (TryGetPropertyIgnoreCase(e, "provaDeVida", out var prova) && prova.ValueKind == JsonValueKind.Object)
        {
            var pn = GetString(prova, "nome");
            outDict["PROVA_VIDA_NOME"] = string.IsNullOrEmpty(pn) ? GetString(cliente, "nome") : pn;
            outDict["IMG_SELFIE"] = NormalizeImageDataUri(
                GetString(prova, "base64", "imagemBase64"));
        }
        else
        {
            outDict["PROVA_VIDA_NOME"] = GetString(cliente, "nome");
            outDict["IMG_SELFIE"] = NormalizeImageDataUri(
                GetString(e, "provaDeVidaBase64", "imgSelfieBase64"));
        }

        if (TryGetPropertyIgnoreCase(e, "documento", out var documento) &&
            documento.ValueKind == JsonValueKind.Object &&
            TryGetPropertyIgnoreCase(documento, "base64", out var docB64))
        {
            var img = NormalizeImageDataUri(docB64.GetString());
            outDict["IMG_DOCUMENTO_FRENTE"] = img;
            outDict["IMG_DOCUMENTO_VERSO"] = img;
            var rt = GetString(documento, "rotulo", "tipo");
            if (!string.IsNullOrEmpty(rt))
            {
                outDict["DOCUMENTO_TIPO"] = rt;
            }
        }

        if (TryGetPropertyIgnoreCase(e, "documentos", out var documentos) &&
            documentos.ValueKind == JsonValueKind.Object)
        {
            ApplyDocumentoIdentificacao(documentos, outDict);
        }
        else if (TryGetPropertyIgnoreCase(e, "documentoIdentificacao", out var docId) &&
                 docId.ValueKind == JsonValueKind.Object)
        {
            ApplyDocumentoIdentificacao(docId, outDict);
        }

        if (TryGetPropertyIgnoreCase(e, "ccbCampos", out var ccbCampos) && ccbCampos.ValueKind == JsonValueKind.Object)
        {
            FlattenPrefix(ccbCampos, "CCB", outDict);
        }
        else if (TryGetPropertyIgnoreCase(e, "ccb", out var ccb) && ccb.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in ccb.EnumerateObject())
            {
                if (string.Equals(p.Name, "base64", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var key = $"CCB_{p.Name.ToUpperInvariant()}";
                outDict[key] = JsonValueToString(p.Value);
            }
        }

        if (TryGetPropertyIgnoreCase(e, "hashDossie", out var hash))
        {
            outDict["HASH_DOSSIE"] = JsonValueToString(hash);
        }

        if (TryGetPropertyIgnoreCase(e, "docengineUseChromePageFooter", out var foot))
        {
            outDict["DOCENGINE_USE_CHROME_PAGE_FOOTER"] = JsonValueToString(foot);
        }

        FillFromAnexosPdfDeclarative(e, outDict);
        FillRasterImagesFromAnexosByOrder(e, outDict);

        ApplyBrandingFromStructuredPayload(e, outDict);

        if (TryGetPropertyIgnoreCase(e, "templateExtras", out var tex) && tex.ValueKind == JsonValueKind.Object)
        {
            MergeExtras(tex, outDict);
        }
        else if (TryGetPropertyIgnoreCase(e, "camposExtrasTemplate", out var cet) && cet.ValueKind == JsonValueKind.Object)
        {
            MergeExtras(cet, outDict);
        }

        outDict["DOSSIE_BLOCO_INTERCALADO_HTML"] = FirstNonEmpty(
            GetStringDirect(e, "dossieBlocoIntercaladoHtml", "blocoIntercaladoHtml", "dossie_bloco_intercalado_html", "bloco_intercalado_html"));
        outDict["TERMOS_POLITICA_HTML"] = FirstNonEmpty(
            GetStringDirect(e, "termosPoliticaHtml", "termosPoliticaHTML", "termos_politica_html", "termoPoliticaHtml"));

        return outDict;
    }

    private static void ApplyBrandingFromStructuredPayload(JsonElement e, Dictionary<string, string> outDict)
    {
        var logo = FirstNonEmpty(
            GetStringDirect(e, "logoBase64", "logo", "logoSimplixBase64"),
            GetMarcaString(e, "logoBase64", "logo"));
        if (!string.IsNullOrWhiteSpace(logo))
        {
            outDict["LOGO"] = NormalizeImageDataUri(logo);
        }

        var titulo = FirstNonEmpty(
            GetStringDirect(e, "titulo", "tituloDossie", "tituloCabecalho", "headerTitulo"),
            GetMarcaString(e, "titulo", "tituloDossie", "tituloCabecalho"));
        if (!string.IsNullOrWhiteSpace(titulo))
        {
            outDict["DOSSIE_HEADER_TITULO"] = titulo;
        }
    }

    private static string GetMarcaString(JsonElement e, params string[] names)
    {
        if (!TryGetPropertyIgnoreCase(e, "marca", out var marca) || marca.ValueKind != JsonValueKind.Object)
        {
            return "";
        }

        return GetString(marca, names);
    }

    /// <summary>
    /// Reconhece alias de logo/título no dicionário já achatado e garante cabeçalho quando o título está vazio.
    /// </summary>
    public static void NormalizeBrandingForGenerate(Dictionary<string, string> flat)
    {
        static string Get(Dictionary<string, string> d, string key) =>
            d.TryGetValue(key, out var v) ? v : "";

        if (!string.IsNullOrWhiteSpace(Get(flat, "titulo")) &&
            string.IsNullOrWhiteSpace(Get(flat, "DOSSIE_HEADER_TITULO")))
        {
            flat["DOSSIE_HEADER_TITULO"] = flat["titulo"];
        }

        foreach (var alias in new[] { "logoBase64", "logo", "logoSimplixBase64" })
        {
            var v = Get(flat, alias);
            if (!string.IsNullOrWhiteSpace(v) && string.IsNullOrWhiteSpace(Get(flat, "LOGO")))
            {
                flat["LOGO"] = NormalizeImageDataUri(v);
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(Get(flat, "DOSSIE_HEADER_TITULO")))
        {
            var nestedTitulo = FirstNonEmpty(
                Get(flat, "marca.titulo"),
                Get(flat, "marca.tituloDossie"),
                Get(flat, "marca.tituloCabecalho"));
            if (!string.IsNullOrWhiteSpace(nestedTitulo))
            {
                flat["DOSSIE_HEADER_TITULO"] = nestedTitulo;
            }
        }

        if (string.IsNullOrWhiteSpace(Get(flat, "LOGO")))
        {
            var nestedLogo = FirstNonEmpty(
                Get(flat, "marca.logoBase64"),
                Get(flat, "marca.logo"));
            if (!string.IsNullOrWhiteSpace(nestedLogo))
            {
                flat["LOGO"] = NormalizeImageDataUri(nestedLogo);
            }
        }

        if (string.IsNullOrWhiteSpace(Get(flat, "DOSSIE_HEADER_TITULO")))
        {
            flat["DOSSIE_HEADER_TITULO"] = DefaultHeaderTitulo;
        }

        SyncHashDossieAndChromeFooterAliases(flat);

        SyncLegacyLogoSimplixAlias(flat);
    }

    /// <summary>
    /// Payloads achatados ou parceiros enviam <c>hashDossie</c> / <c>docengineUseChromePageFooter</c>;
    /// o motor e o carimbo de anexos usam <c>HASH_DOSSIE</c> e <c>DOCENGINE_USE_CHROME_PAGE_FOOTER</c>.
    /// </summary>
    private static void SyncHashDossieAndChromeFooterAliases(Dictionary<string, string> flat)
    {
        static string Get(Dictionary<string, string> d, string key) =>
            d.TryGetValue(key, out var v) ? v : "";

        if (string.IsNullOrWhiteSpace(Get(flat, "HASH_DOSSIE")))
        {
            var h = FirstNonEmpty(
                Get(flat, "hashDossie"),
                Get(flat, "hash_dossie"),
                Get(flat, "dados.hashDossie"),
                Get(flat, "dados.hash_dossie"));
            if (!string.IsNullOrWhiteSpace(h))
            {
                flat["HASH_DOSSIE"] = h;
            }
        }

        if (string.IsNullOrWhiteSpace(Get(flat, "DOCENGINE_USE_CHROME_PAGE_FOOTER")))
        {
            var f = FirstNonEmpty(
                Get(flat, "docengineUseChromePageFooter"),
                Get(flat, "docengine_use_chrome_page_footer"),
                Get(flat, "dados.docengineUseChromePageFooter"),
                Get(flat, "dados.docengine_use_chrome_page_footer"));
            if (!string.IsNullOrWhiteSpace(f))
            {
                flat["DOCENGINE_USE_CHROME_PAGE_FOOTER"] = f;
            }
        }
    }

    /// <summary>
    /// Templates antigos em <c>tb_template</c> ainda declaram <c>LOGO_SIMPLIX_BASE64</c> em <c>requiredFields</c>
    /// enquanto o HTML novo usa <c>{{LOGO}}</c>. Espelha o valor para satisfazer validação e placeholders legados.
    /// </summary>
    private static void SyncLegacyLogoSimplixAlias(Dictionary<string, string> flat)
    {
        static string Get(Dictionary<string, string> d, string key) =>
            d.TryGetValue(key, out var v) ? v : "";

        var logo = Get(flat, "LOGO");
        var legacy = Get(flat, "LOGO_SIMPLIX_BASE64");

        if (!string.IsNullOrWhiteSpace(logo) && string.IsNullOrWhiteSpace(legacy))
        {
            flat["LOGO_SIMPLIX_BASE64"] = logo;
        }
        else if (!string.IsNullOrWhiteSpace(legacy) && string.IsNullOrWhiteSpace(logo))
        {
            flat["LOGO"] = NormalizeImageDataUri(legacy);
            flat["LOGO_SIMPLIX_BASE64"] = flat["LOGO"];
        }
    }

    private static void ApplyDocumentoIdentificacao(JsonElement doc, Dictionary<string, string> outDict)
    {
        if (TryGetPropertyIgnoreCase(doc, "frenteBase64", out var f) ||
            TryGetPropertyIgnoreCase(doc, "frenteEmBase64", out f))
        {
            outDict["IMG_DOCUMENTO_FRENTE"] = NormalizeImageDataUri(f.GetString());
        }

        if (TryGetPropertyIgnoreCase(doc, "versoBase64", out var v) ||
            TryGetPropertyIgnoreCase(doc, "versoEmBase64", out v))
        {
            outDict["IMG_DOCUMENTO_VERSO"] = NormalizeImageDataUri(v.GetString());
        }

        var r = GetString(doc, "rotulo", "tipo");
        if (!string.IsNullOrEmpty(r) && !outDict.ContainsKey("DOCUMENTO_TIPO"))
        {
            outDict["DOCUMENTO_TIPO"] = r;
        }
    }

    private static void MergeExtras(JsonElement extras, Dictionary<string, string> outDict)
    {
        foreach (var p in extras.EnumerateObject())
        {
            if (p.Value.ValueKind == JsonValueKind.String)
            {
                var v = p.Value.GetString() ?? "";
                if (p.Name.StartsWith("IMG_", StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(v) &&
                    LooksLikeRasterImageBase64(v))
                {
                    outDict[p.Name] = NormalizeImageDataUri(v);
                }
                else
                {
                    outDict[p.Name] = v;
                }
            }
            else
            {
                outDict[p.Name] = p.Value.GetRawText();
            }
        }
    }

    private static void FlattenPrefix(JsonElement obj, string prefix, Dictionary<string, string> target)
    {
        foreach (var p in obj.EnumerateObject())
        {
            target[$"{prefix}_{p.Name.ToUpperInvariant()}"] = JsonValueToString(p.Value);
        }
    }

    private static string JsonValueToString(JsonElement v) =>
        v.ValueKind switch
        {
            JsonValueKind.String => v.GetString() ?? "",
            JsonValueKind.Number => v.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => v.GetRawText()
        };

    private static List<JsonElement> NormalizeTrilhaEventosRows(JsonElement e)
    {
        if (!TryGetPropertyIgnoreCase(e, "trilhaEventos", out var te))
        {
            return new List<JsonElement>();
        }

        if (te.ValueKind == JsonValueKind.Array)
        {
            return te.EnumerateArray().ToList();
        }

        if (te.ValueKind == JsonValueKind.Object)
        {
            if (TryGetPropertyIgnoreCase(te, "eventos", out var ev) && ev.ValueKind == JsonValueKind.Array)
            {
                return ev.EnumerateArray().ToList();
            }

            if (TryGetPropertyIgnoreCase(te, "itens", out var it) && it.ValueKind == JsonValueKind.Array)
            {
                return it.EnumerateArray().ToList();
            }
        }

        return new List<JsonElement>();
    }

    private static List<JsonElement> NormalizeLogsInteracaoRows(JsonElement e)
    {
        if (!TryGetPropertyIgnoreCase(e, "logsInteracao", out var li))
        {
            return new List<JsonElement>();
        }

        if (li.ValueKind == JsonValueKind.Array)
        {
            return li.EnumerateArray().ToList();
        }

        if (li.ValueKind == JsonValueKind.Object &&
            TryGetPropertyIgnoreCase(li, "registros", out var reg) &&
            reg.ValueKind == JsonValueKind.Array)
        {
            return reg.EnumerateArray().ToList();
        }

        return new List<JsonElement>();
    }

    private sealed record CapturaCtx(string Ip, string Lat, string Lon, string Ua);

    private static string IdLine(CapturaCtx ctx)
    {
        return $"""<span class="id-line">IP: {EscapeHtml(ctx.Ip)}</span><span class="id-line">Latitude: {EscapeHtml(ctx.Lat)}</span><span class="id-line">Longitude: {EscapeHtml(ctx.Lon)}</span><span class="id-line">Celular e Navegador: {EscapeHtml(ctx.Ua)}</span>""";
    }

    private static string BuildIdentificacaoCell(JsonElement row, CapturaCtx ctx)
    {
        if (TryGetPropertyIgnoreCase(row, "identificacaoHtml", out var ih) &&
            ih.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(ih.GetString()))
        {
            return ih.GetString() ?? "";
        }

        if (TryGetPropertyIgnoreCase(row, "identificacao", out var id) &&
            id.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(id.GetString()))
        {
            return EscapeHtml(id.GetString() ?? "");
        }

        return IdLine(ctx);
    }

    private static string EvtRow(string evento, string dataHoraHtml, string identCell) =>
        $"<tr><td>{EscapeHtml(evento)}</td><td>{dataHoraHtml}</td><td>{identCell}</td></tr>";

    private static string InteracaoRow(string dataHora, string tipo, string texto) =>
        $"<tr><td>{dataHora}</td><td>{EscapeHtml(tipo)}</td><td>{texto}</td></tr>";

    private static string EscapeHtml(string? s) => WebUtility.HtmlEncode(s ?? "");

    private static string SafeGetString(JsonElement obj, params string[] names) =>
        obj.ValueKind == JsonValueKind.Object ? GetString(obj, names) : "";

    private static string GetString(JsonElement obj, params string[] names)
    {
        if (obj.ValueKind != JsonValueKind.Object)
        {
            return "";
        }

        foreach (var n in names)
        {
            if (TryGetPropertyIgnoreCase(obj, n, out var p))
            {
                if (p.ValueKind == JsonValueKind.String)
                {
                    return p.GetString() ?? "";
                }

                if (p.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                {
                    return p.GetRawText();
                }
            }
        }

        return "";
    }

    private static string GetStringDirect(JsonElement e, params string[] names)
    {
        foreach (var n in names)
        {
            if (TryGetPropertyIgnoreCase(e, n, out var p) && p.ValueKind == JsonValueKind.String)
            {
                var s = p.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                {
                    return s!;
                }
            }
        }

        return "";
    }

    private static string FirstNonEmpty(params string?[] xs)
    {
        foreach (var x in xs)
        {
            if (!string.IsNullOrWhiteSpace(x))
            {
                return x!;
            }
        }

        return "";
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement obj, string name, out JsonElement value)
    {
        value = default;
        if (obj.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var p in obj.EnumerateObject())
        {
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = p.Value;
                return true;
            }
        }

        return false;
    }

    private static string NormalizeImageDataUri(string? base64OrDataUri, string mimeFallback = "image/jpeg")
    {
        if (string.IsNullOrWhiteSpace(base64OrDataUri))
        {
            return "";
        }

        var s = base64OrDataUri.Trim();
        if (s.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return s;
        }

        var compact = s.Replace(" ", "", StringComparison.Ordinal).Replace("\n", "", StringComparison.Ordinal)
            .Replace("\r", "", StringComparison.Ordinal);
        var mime = mimeFallback;
        if (compact.StartsWith("iVBOR", StringComparison.Ordinal))
        {
            mime = "image/png";
        }
        else if (compact.StartsWith("/9j/", StringComparison.Ordinal))
        {
            mime = "image/jpeg";
        }
        else if (compact.StartsWith("R0lGOD", StringComparison.Ordinal))
        {
            mime = "image/gif";
        }
        else if (compact.StartsWith("UklGR", StringComparison.Ordinal))
        {
            mime = "image/webp";
        }

        return $"data:{mime};base64,{compact}";
    }

    private static bool LooksLikePdfBase64(string s)
    {
        var c = s.Trim().Replace(" ", "").Replace("\n", "").Replace("\r", "");
        if (c.Length == 0)
        {
            return false;
        }

        if (c.StartsWith("data:application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return c.StartsWith("JVBERi", StringComparison.Ordinal);
    }

    private static bool LooksLikeRasterImageBase64(string s)
    {
        var c = s.Trim().Replace(" ", "").Replace("\n", "").Replace("\r", "");
        if (c.Length == 0)
        {
            return false;
        }

        if (c.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return c.StartsWith("iVBOR", StringComparison.Ordinal) ||
               c.StartsWith("/9j/", StringComparison.Ordinal) ||
               c.StartsWith("R0lGOD", StringComparison.Ordinal) ||
               c.StartsWith("UklGR", StringComparison.Ordinal);
    }

    private static string StripPdfDataUriToBase64(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            return "";
        }

        var t = s.Trim();
        const string prefix = "data:application/pdf";
        if (t.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            var idx = t.IndexOf("base64,", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                t = t[(idx + "base64,".Length)..];
            }
        }

        return t.Replace(" ", "").Replace("\n", "").Replace("\r", "");
    }

    private static void FillFromAnexosPdfDeclarative(JsonElement e, Dictionary<string, string> outDict)
    {
        if (!TryGetPropertyIgnoreCase(e, "anexosPdf", out var lista) || lista.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var sorted = lista.EnumerateArray()
            .Select((item, idx) => (item, ordem: GetAnexoOrdem(item, idx)))
            .OrderBy(x => x.ordem)
            .Select(x => x.item)
            .ToList();

        var docKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "IMG_DOCUMENTO_FRENTE", "IMG_DOCUMENTO_VERSO" };

        foreach (var item in sorted)
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var chavesImg = CoalesceChavesTemplateLista(item, "imagemParaChaves", "mapearBase64Para");
            if (TryGetPropertyIgnoreCase(item, "base64", out var b64El))
            {
                var b64 = b64El.ValueKind == JsonValueKind.String ? b64El.GetString() : null;
                if (!string.IsNullOrEmpty(b64) && LooksLikeRasterImageBase64(b64) && !LooksLikePdfBase64(b64))
                {
                    var img = NormalizeImageDataUri(b64);
                    foreach (var k in chavesImg)
                    {
                        if (string.IsNullOrEmpty(k))
                        {
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(outDict.GetValueOrDefault(k)))
                        {
                            outDict[k] = img;
                        }
                    }

                    if (chavesImg.Any(k => docKeys.Contains(k)) &&
                        string.IsNullOrWhiteSpace(outDict.GetValueOrDefault("DOCUMENTO_TIPO")))
                    {
                        var t = GetString(item, "tipo", "rotulo");
                        if (!string.IsNullOrEmpty(t))
                        {
                            outDict["DOCUMENTO_TIPO"] = t;
                        }
                    }

                    if (chavesImg.Any(k => string.Equals(k, "IMG_SELFIE", StringComparison.OrdinalIgnoreCase)) &&
                        string.IsNullOrWhiteSpace(outDict.GetValueOrDefault("PROVA_VIDA_NOME")))
                    {
                        if (TryGetPropertyIgnoreCase(item, "dadosCliente", out var dc) && dc.ValueKind == JsonValueKind.Object)
                        {
                            var n = GetString(dc, "nome");
                            if (!string.IsNullOrEmpty(n))
                            {
                                outDict["PROVA_VIDA_NOME"] = n;
                            }
                        }
                        else
                        {
                            var n = GetString(item, "nome");
                            if (!string.IsNullOrEmpty(n))
                            {
                                outDict["PROVA_VIDA_NOME"] = n;
                            }
                        }
                    }
                }
            }

            if (TryGetPropertyIgnoreCase(item, "textoParaChaves", out var txtMap) &&
                txtMap.ValueKind == JsonValueKind.Object)
            {
                ApplyTextMap(txtMap, outDict);
            }
            else if (TryGetPropertyIgnoreCase(item, "camposTemplate", out var cm) && cm.ValueKind == JsonValueKind.Object)
            {
                ApplyTextMap(cm, outDict);
            }
        }
    }

    private static void ApplyTextMap(JsonElement txtMap, Dictionary<string, string> outDict)
    {
        foreach (var p in txtMap.EnumerateObject())
        {
            if (string.IsNullOrWhiteSpace(outDict.GetValueOrDefault(p.Name)))
            {
                outDict[p.Name] = p.Value.ValueKind == JsonValueKind.String
                    ? p.Value.GetString() ?? ""
                    : p.Value.GetRawText();
            }
        }
    }

    private static List<string> CoalesceChavesTemplateLista(JsonElement item, params string[] propNames)
    {
        foreach (var n in propNames)
        {
            if (!TryGetPropertyIgnoreCase(item, n, out var v))
            {
                continue;
            }

            if (v.ValueKind == JsonValueKind.Array)
            {
                return v.EnumerateArray()
                    .Where(x => x.ValueKind == JsonValueKind.String)
                    .Select(x => x.GetString()!)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();
            }

            if (v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString()))
            {
                return new List<string> { v.GetString()! };
            }
        }

        return new List<string>();
    }

    private static int GetAnexoOrdem(JsonElement item, int idx)
    {
        if (TryGetPropertyIgnoreCase(item, "ordem", out var o) && o.TryGetInt32(out var i))
        {
            return i;
        }

        return idx + 1;
    }

    private static void FillRasterImagesFromAnexosByOrder(JsonElement e, Dictionary<string, string> outDict)
    {
        if (!TryGetPropertyIgnoreCase(e, "anexosPdf", out var lista) || lista.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var sorted = lista.EnumerateArray()
            .Select((item, idx) => (item, ordem: GetAnexoOrdem(item, idx)))
            .OrderBy(x => x.ordem)
            .Select(x => x.item)
            .ToList();

        var rasters = sorted
            .Where(item =>
            {
                if (item.ValueKind != JsonValueKind.Object ||
                    !TryGetPropertyIgnoreCase(item, "base64", out var b))
                {
                    return false;
                }

                var bs = b.ValueKind == JsonValueKind.String ? b.GetString() : null;
                return !string.IsNullOrEmpty(bs) && LooksLikeRasterImageBase64(bs!) && !LooksLikePdfBase64(bs!);
            })
            .ToList();

        if (rasters.Count == 0)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(outDict.GetValueOrDefault("IMG_SELFIE")))
        {
            var b64 = GetString(rasters[0], "base64");
            outDict["IMG_SELFIE"] = NormalizeImageDataUri(b64);
            if (string.IsNullOrWhiteSpace(outDict.GetValueOrDefault("PROVA_VIDA_NOME")))
            {
                if (TryGetPropertyIgnoreCase(rasters[0], "dadosCliente", out var dc) && dc.ValueKind == JsonValueKind.Object)
                {
                    var n = GetString(dc, "nome");
                    if (!string.IsNullOrEmpty(n))
                    {
                        outDict["PROVA_VIDA_NOME"] = n;
                    }
                }
                else
                {
                    var n = GetString(rasters[0], "nome");
                    if (!string.IsNullOrEmpty(n))
                    {
                        outDict["PROVA_VIDA_NOME"] = n;
                    }
                }
            }
        }

        if (rasters.Count > 1 && string.IsNullOrWhiteSpace(outDict.GetValueOrDefault("IMG_DOCUMENTO_FRENTE")))
        {
            var img = NormalizeImageDataUri(GetString(rasters[1], "base64"));
            outDict["IMG_DOCUMENTO_FRENTE"] = img;
            outDict["IMG_DOCUMENTO_VERSO"] = img;
            if (string.IsNullOrWhiteSpace(outDict.GetValueOrDefault("DOCUMENTO_TIPO")))
            {
                var t = GetString(rasters[1], "tipo", "rotulo");
                if (!string.IsNullOrEmpty(t))
                {
                    outDict["DOCUMENTO_TIPO"] = t;
                }
            }
        }
    }

    private static List<PdfAnexoPayload> BuildPdfsAnexosForApi(JsonElement e)
    {
        var result = new List<PdfAnexoPayload>();
        if (!TryGetPropertyIgnoreCase(e, "anexosPdf", out var lista) || lista.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        var items = lista.EnumerateArray().Select((item, idx) => (item, idx)).ToList();
        var withOrd = new List<(PdfAnexoPayload payload, int idx)>();
        foreach (var (item, idx) in items)
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !TryGetPropertyIgnoreCase(item, "base64", out var b64El))
            {
                continue;
            }

            var raw = b64El.ValueKind == JsonValueKind.String ? b64El.GetString() : null;
            var b64 = StripPdfDataUriToBase64(raw);
            if (string.IsNullOrEmpty(b64) || !LooksLikePdfBase64(b64))
            {
                continue;
            }

            int ordem;
            if (TryGetPropertyIgnoreCase(item, "ordem", out var o) && o.ValueKind == JsonValueKind.Number && o.TryGetInt32(out var oi))
            {
                ordem = oi;
            }
            else
            {
                ordem = idx + 1;
            }

            withOrd.Add((new PdfAnexoPayload { Ordem = ordem, Base64 = b64 }, idx));
        }

        foreach (var x in withOrd.OrderBy(x => x.payload.Ordem).ThenBy(x => x.idx))
        {
            result.Add(x.payload);
        }

        return result;
    }

    private static bool HasStructuredCcbCamposPayload(JsonElement e)
    {
        if (TryGetPropertyIgnoreCase(e, "ccbCampos", out var cc) && cc.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in cc.EnumerateObject())
            {
                if (!CcbSoloAnexoKeys.Contains(NormCcbKey(p.Name)))
                {
                    return true;
                }
            }
        }

        if (TryGetPropertyIgnoreCase(e, "ccb", out var ccb) && ccb.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in ccb.EnumerateObject())
            {
                if (string.Equals(p.Name, "base64", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!CcbSoloAnexoKeys.Contains(NormCcbKey(p.Name)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string NormCcbKey(string k) =>
        k.ToLowerInvariant().Replace("_", "", StringComparison.Ordinal);

    private static readonly HashSet<string> CcbSoloAnexoKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "base64", "documentobase64", "contratobase64", "ccbcontratobase64", "ordem", "rotulo"
    };

    private static void StripCcbCredEmitCorrespIfNoStructuredCcb(JsonElement estrutura, Dictionary<string, string> dados)
    {
        if (HasStructuredCcbCamposPayload(estrutura))
        {
            return;
        }

        var prefixes = new[] { "CCB_", "CREDOR_", "EMITENTE_", "CORRESP_" };
        var keys = dados.Keys.ToList();
        foreach (var key in keys)
        {
            if (prefixes.Any(p => key.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            {
                dados.Remove(key);
            }
        }
    }
}
