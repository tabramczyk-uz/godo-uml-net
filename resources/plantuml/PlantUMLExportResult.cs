/// <summary>
/// Outcome of <see cref="PlantUMLDiagrams.Export"/>: either the PlantUML text,
/// or the first error in the code, which stops the export.
/// </summary>
public sealed class PlantUMLExportResult
{
	private PlantUMLExportResult(string text, string errorMessage, int errorLineNumber)
	{
		Text = text;
		ErrorMessage = errorMessage;
		ErrorLineNumber = errorLineNumber;
	}

	public string Text { get; }
	public string ErrorMessage { get; }
	public int ErrorLineNumber { get; }

	public bool IsSuccess => Text != null;

	public static PlantUMLExportResult Success(string text)
	{
		return new PlantUMLExportResult(text, null, -1);
	}

	public static PlantUMLExportResult Failure(string errorMessage, int errorLineNumber)
	{
		return new PlantUMLExportResult(null, errorMessage, errorLineNumber);
	}
}
