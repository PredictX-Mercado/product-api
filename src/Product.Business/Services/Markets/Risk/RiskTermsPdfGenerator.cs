using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Product.Business.Interfaces.Market;
using Product.Business.Options;
using Product.Contracts.Markets;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Product.Business.Services.Markets;

public class RiskTermsPdfGenerator : IRiskTermsPdfGenerator
{
    private readonly RiskTermsCompanyOptions _companyOptions;
    private static readonly CultureInfo PtBr = new("pt-BR");
    private const string DateFormat = "dd/MM/yyyy HH:mm:ss";
    private const string SansFont = "Segoe UI"; // matches Tailwind ui-sans on Windows
    private const string SerifFont = "Times New Roman"; // matches Tailwind serif on Windows

    public RiskTermsPdfGenerator(IOptions<RiskTermsCompanyOptions> companyOptions)
    {
        _companyOptions = companyOptions?.Value ?? new RiskTermsCompanyOptions();
    }

    public byte[] BuildPdf(
        string body,
        RiskTermsResponse acceptance,
        string? marketTitle,
        string? username,
        string? userEmail,
        string? maskedUserCpf
    )
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var issueDate = acceptance.AcceptedAt.ToOffset(TimeSpan.FromHours(-3));
        var issueDateText = issueDate.ToString(DateFormat, PtBr);
        var companyLine =
            $"{_companyOptions.Name} | CNPJ {_companyOptions.Cnpj} | {_companyOptions.Address}, {_companyOptions.City}/{_companyOptions.State}";

        var cleanBody = SanitizeContractText(body);

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontFamily(SerifFont).FontSize(11));

                page.Header()
                    .Column(col =>
                    {
                        col.Spacing(2);
                        col.Item().Text(companyLine).SemiBold().FontSize(10).FontFamily(SansFont);
                        col.Item()
                            .Text("Mercado onde foi feito o aceite: " + (marketTitle ?? "Mercado"))
                            .SemiBold()
                            .FontFamily(SansFont)
                            .FontSize(10);
                        col.Item()
                            .Text("Data do aceite: " + issueDateText)
                            .SemiBold()
                            .FontFamily(SansFont)
                            .FontSize(10);
                    });

                page.Content()
                    .PaddingTop(10)
                    .Column(col =>
                    {
                        col.Spacing(6);

                        RenderRiskTerms(col, cleanBody);

                        col.Item().PaddingTop(10).LineHorizontal(0.5f);

                        RenderReceipt(col, acceptance, username, userEmail, maskedUserCpf);
                    });

                page.Footer()
                    .AlignRight()
                    .Text(txt =>
                    {
                        txt.Span("Gerado por Product • ").FontSize(9);
                        txt.Span(
                                DateTimeOffset
                                    .UtcNow.ToOffset(TimeSpan.FromHours(-3))
                                    .ToString("dd/MM/yyyy HH:mm:ss")
                            )
                            .FontSize(9);
                    });
            });
        });

        return doc.GeneratePdf();
    }

    private static void RenderRiskTerms(ColumnDescriptor col, string body)
    {
        var lines = body.Replace("\r\n", "\n")
            .Replace("\r", "\n")
            .Split('\n')
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        var reTitle = new Regex(@"^CONTRATO\b", RegexOptions.IgnoreCase);
        var reSection = new Regex(@"^(?<n>\d+)\)\s*(?<t>.+)$");
        var reSub = new Regex(@"^(?<n>\d+\.\d+)\)\s*(?<t>.+)$");
        var reItem = new Regex(@"^(?<n>\d+\.\d+\.\d+)\)\s*(?<t>.+)$");

        foreach (var line in lines)
        {
            if (reTitle.IsMatch(line))
            {
                col.Item()
                    .PaddingBottom(4)
                    .AlignCenter()
                    .Text(line)
                    .Bold()
                    .FontFamily(SansFont)
                    .FontSize(14);
                continue;
            }

            var mSec = reSection.Match(line);
            if (mSec.Success)
            {
                col.Item()
                    .PaddingTop(6)
                    .Row(row =>
                    {
                        row.Spacing(4);
                        row.ConstantItem(24)
                            .AlignRight()
                            .Text(mSec.Groups["n"].Value + ")")
                            .Bold()
                            .FontFamily(SansFont)
                            .FontSize(12);
                        row.RelativeItem()
                            .Text(mSec.Groups["t"].Value)
                            .Bold()
                            .FontFamily(SansFont)
                            .FontSize(12);
                    });
                continue;
            }

            var mItem3 = reItem.Match(line);
            if (mItem3.Success)
            {
                col.Item()
                    .PaddingLeft(14)
                    .Row(row =>
                    {
                        row.Spacing(4);
                        row.ConstantItem(48)
                            .AlignRight()
                            .Text(mItem3.Groups["n"].Value + ")")
                            .SemiBold()
                            .FontFamily(SansFont);
                        row.RelativeItem()
                            .Text(mItem3.Groups["t"].Value)
                            .Justify()
                            .FontFamily(SerifFont);
                    });
                continue;
            }

            var mSub = reSub.Match(line);
            if (mSub.Success)
            {
                col.Item()
                    .PaddingLeft(10)
                    .Row(row =>
                    {
                        row.Spacing(4);
                        row.ConstantItem(40)
                            .AlignRight()
                            .Text(mSub.Groups["n"].Value + ")")
                            .SemiBold()
                            .FontFamily(SansFont);
                        row.RelativeItem()
                            .Text(mSub.Groups["t"].Value)
                            .Justify()
                            .FontFamily(SerifFont);
                    });
                continue;
            }

            col.Item()
                .Text(line)
                .FontFamily(SerifFont)
                .Justify()
                .LineHeight(1.25f);
        }
    }

    private static void RenderReceipt(
        ColumnDescriptor col,
        RiskTermsResponse acceptance,
        string? username,
        string? userEmail,
        string? maskedUserCpf
    )
    {
        var issueDateLocal = acceptance.AcceptedAt.ToOffset(TimeSpan.FromHours(-3));
        var issueDateLocalText = issueDateLocal.ToString(DateFormat, PtBr);

        col.Item().Text("Comprovante de aceite").Bold().FontSize(12);

        col.Item()
            .Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(110);
                    columns.RelativeColumn();
                });

                void Row(string label, string value)
                {
                    table.Cell()
                        .PaddingVertical((float)1.5)
                        .Text(label)
                        .SemiBold()
                        .FontFamily(SansFont);
                    table.Cell()
                        .PaddingVertical((float)1.5)
                        .Text(value)
                        .FontFamily(SerifFont);
                }

                Row("Usuario:", username ?? "-");
                Row("CPF:", maskedUserCpf ?? "-");
                Row("E-mail:", userEmail ?? "-");
                Row("Aceito em:", issueDateLocalText);
                Row("Documento:", acceptance.Id.ToString());

                if (!string.IsNullOrWhiteSpace(acceptance.TermHash))
                    Row("Hash do termo:", acceptance.TermHash);
            });
    }

    private static string SanitizeContractText(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var s = input;

        s = s.Replace("\u00AD", "");
        s = s.Replace("\u200B", "");
        s = s.Replace("\u200C", "");
        s = s.Replace("\u200D", "");
        s = s.Replace("\uFEFF", "");
        s = s.Replace("\u00A0", " ");

        s = Regex.Replace(s, @"(?<=\d\))(?=\S)", " ");
        s = Regex.Replace(s, @"[ \t]+", " ");

        return s.Trim();
    }
}
