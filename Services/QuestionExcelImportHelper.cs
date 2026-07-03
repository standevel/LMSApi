using ClosedXML.Excel;
using ErrorOr;
using LMS.Api.Contracts;
using Microsoft.AspNetCore.Http;

namespace LMS.Api.Services;

internal static class QuestionExcelImportHelper
{
    private const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private static readonly string[] OptionLetters = ["A", "B", "C", "D", "E", "F"];

    public static QuestionImportTemplateDto GenerateTemplate(string fileName)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Questions");
        var headers = new[]
        {
            "Question Text",
            "Question Type",
            "Points",
            "Difficulty",
            "Category",
            "Tags",
            "Explanation",
            "Feedback",
            "Option A",
            "Option B",
            "Option C",
            "Option D",
            "Option E",
            "Option F",
            "Correct Options"
        };

        for (var index = 0; index < headers.Length; index++)
        {
            var cell = sheet.Cell(1, index + 1);
            cell.Value = headers[index];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E6F4F1");
        }

        sheet.Cell(2, 1).Value = "What is 2 + 2?";
        sheet.Cell(2, 2).Value = "MultipleChoice";
        sheet.Cell(2, 3).Value = 1;
        sheet.Cell(2, 4).Value = "Easy";
        sheet.Cell(2, 5).Value = "Arithmetic";
        sheet.Cell(2, 6).Value = "addition,basic";
        sheet.Cell(2, 7).Value = "2 + 2 equals 4.";
        sheet.Cell(2, 8).Value = "Review basic addition.";
        sheet.Cell(2, 9).Value = "3";
        sheet.Cell(2, 10).Value = "4";
        sheet.Cell(2, 11).Value = "5";
        sheet.Cell(2, 15).Value = "B";

        sheet.Cell(3, 1).Value = "Explain photosynthesis briefly.";
        sheet.Cell(3, 2).Value = "Essay";
        sheet.Cell(3, 3).Value = 5;
        sheet.Cell(3, 4).Value = "Medium";
        sheet.Cell(3, 5).Value = "Biology";

        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();

        var instructions = workbook.Worksheets.Add("Instructions");
        instructions.Cell(1, 1).Value = "Question Import Template";
        instructions.Cell(1, 1).Style.Font.Bold = true;
        instructions.Cell(1, 1).Style.Font.FontSize = 14;
        instructions.Cell(3, 1).Value = "1. Keep the Questions sheet name and header row unchanged.";
        instructions.Cell(4, 1).Value = "2. Question Text is required. Question Type defaults to MultipleChoice.";
        instructions.Cell(5, 1).Value = "3. Supported types: MultipleChoice, SingleChoice, TrueFalse, Essay, ShortAnswer, FileUpload.";
        instructions.Cell(6, 1).Value = "4. Enter answer choices in Option A through Option F.";
        instructions.Cell(7, 1).Value = "5. Correct Options accepts letters separated by commas, e.g. B or A,C.";
        instructions.Cell(8, 1).Value = "6. Leave option columns blank for essay, short answer, or file upload questions.";
        instructions.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return new QuestionImportTemplateDto(stream.ToArray(), fileName, ContentType);
    }

    public static async Task<ErrorOr<List<QuestionImportRow>>> ParseAsync(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return Error.Validation("File.Required", "Please upload an Excel file.");
        }

        var extension = Path.GetExtension(file.FileName);
        if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(extension, ".xls", StringComparison.OrdinalIgnoreCase))
        {
            return Error.Validation("File.InvalidType", "Please upload an .xlsx or .xls file.");
        }

        try
        {
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream, ct);
            stream.Position = 0;

            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheets.FirstOrDefault(ws =>
                string.Equals(ws.Name, "Questions", StringComparison.OrdinalIgnoreCase)) ?? workbook.Worksheets.First();

            var rows = new List<QuestionImportRow>();
            foreach (var row in worksheet.RowsUsed().Skip(1))
            {
                var questionText = Read(row, 1);
                var hasAnyValue = Enumerable.Range(1, 15).Any(column => !string.IsNullOrWhiteSpace(Read(row, column)));
                if (!hasAnyValue)
                {
                    continue;
                }

                var options = Enumerable.Range(0, OptionLetters.Length)
                    .Select(index => new QuestionImportOption(OptionLetters[index], Read(row, 9 + index)))
                    .Where(option => !string.IsNullOrWhiteSpace(option.Text))
                    .ToList();
                var correctTokens = Read(row, 15)
                    .Split([',', ';', '|', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(token => token.ToUpperInvariant())
                    .ToHashSet();

                rows.Add(new QuestionImportRow(
                    row.RowNumber(),
                    questionText,
                    NormalizeType(Read(row, 2)),
                    ParsePoints(Read(row, 3)),
                    EmptyToNull(Read(row, 4)),
                    EmptyToNull(Read(row, 5)),
                    EmptyToNull(Read(row, 6)),
                    EmptyToNull(Read(row, 7)),
                    EmptyToNull(Read(row, 8)),
                    options.Select((option, index) => new QuestionImportOption(
                        option.Letter,
                        option.Text,
                        correctTokens.Contains(option.Letter) || correctTokens.Contains(option.Text.ToUpperInvariant()),
                        index + 1)).ToList()));
            }

            return rows;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Error.Validation("File.InvalidExcel", "The Excel file could not be read. Please use the downloaded template.");
        }
    }

    private static string Read(IXLRow row, int column) => row.Cell(column).GetValue<string>().Trim();

    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static int ParsePoints(string value) => int.TryParse(value, out var points) && points > 0 ? points : 1;

    private static string NormalizeType(string value) => string.IsNullOrWhiteSpace(value) ? "MultipleChoice" : value.Trim();

    public static QuestionImportPreviewItemDto ToPreview(QuestionImportRow row) => new()
    {
        RowNumber = row.RowNumber,
        QuestionText = row.QuestionText,
        QuestionType = row.QuestionType,
        Points = row.Points,
        Difficulty = row.Difficulty,
        Category = row.Category,
        Tags = row.Tags,
        Explanation = row.Explanation,
        Feedback = row.Feedback,
        Options = row.Options.Select(option => new QuestionImportPreviewOptionDto
        {
            OptionText = option.Text,
            DisplayOrder = option.DisplayOrder,
            IsCorrectAnswer = option.IsCorrect
        }).ToList()
    };
}

internal record QuestionImportRow(
    int RowNumber,
    string QuestionText,
    string QuestionType,
    int Points,
    string? Difficulty,
    string? Category,
    string? Tags,
    string? Explanation,
    string? Feedback,
    List<QuestionImportOption> Options);

internal record QuestionImportOption(
    string Letter,
    string Text,
    bool IsCorrect = false,
    int DisplayOrder = 0);
