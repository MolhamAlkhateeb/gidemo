using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using System.Text;

namespace Chatbot.Api.Services;

/// <summary>
/// Extracts plain text from uploaded Office documents so it can be injected into a text model's
/// context. Vision-capable models receive images directly; this handles docx/xlsx.
/// </summary>
public interface IDocumentParser
{
    bool CanParse(string contentType, string fileName);
    Task<string> ExtractTextAsync(Stream input, string fileName, CancellationToken ct);
}

public class OpenXmlDocumentParser : IDocumentParser
{
    public bool CanParse(string contentType, string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext is ".docx" or ".xlsx";
    }

    public Task<string> ExtractTextAsync(Stream input, string fileName, CancellationToken ct)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        // OpenXml needs a seekable stream.
        var ms = new MemoryStream();
        input.CopyTo(ms);
        ms.Position = 0;

        var text = ext switch
        {
            ".docx" => ExtractDocx(ms),
            ".xlsx" => ExtractXlsx(ms),
            _ => string.Empty
        };
        return Task.FromResult(text);
    }

    private static string ExtractDocx(Stream stream)
    {
        using var doc = WordprocessingDocument.Open(stream, false);
        return doc.MainDocumentPart?.Document.Body?.InnerText ?? string.Empty;
    }

    private static string ExtractXlsx(Stream stream)
    {
        using var doc = SpreadsheetDocument.Open(stream, false);
        var sb = new StringBuilder();
        var workbookPart = doc.WorkbookPart;
        if (workbookPart is null) return string.Empty;

        foreach (var worksheetPart in workbookPart.WorksheetParts)
        {
            var sheetData = worksheetPart.Worksheet.Elements<SheetData>().FirstOrDefault();
            if (sheetData is null) continue;

            foreach (var row in sheetData.Elements<Row>())
            {
                var cells = row.Elements<Cell>()
                    .Select(c => GetCellValue(workbookPart, c));
                sb.AppendLine(string.Join("\t", cells));
            }
        }
        return sb.ToString();
    }

    private static string GetCellValue(WorkbookPart workbookPart, Cell cell)
    {
        var value = cell.CellValue?.InnerText ?? string.Empty;
        if (cell.DataType?.Value == CellValues.SharedString &&
            int.TryParse(value, out var idx))
        {
            var table = workbookPart.SharedStringTablePart?.SharedStringTable;
            return table?.ElementAt(idx).InnerText ?? value;
        }
        return value;
    }
}
