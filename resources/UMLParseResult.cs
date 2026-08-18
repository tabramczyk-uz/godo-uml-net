/// <summary>
/// Outcome of a single <see cref="UMLParser.Parse"/> call: either a diagram, or
/// the first error encountered.
/// </summary>
public sealed class UMLParseResult
{
	private UMLParseResult(UMLDiagram diagram, string errorMessage, int errorLineNumber)
	{
		Diagram = diagram;
		ErrorMessage = errorMessage;
		ErrorLineNumber = errorLineNumber;
	}

	public UMLDiagram Diagram { get; }
	public string ErrorMessage { get; }
	public int ErrorLineNumber { get; }

	public bool IsSuccess => Diagram != null;

	public static UMLParseResult Success(UMLDiagram diagram)
	{
		return new UMLParseResult(diagram, null, -1);
	}

	public static UMLParseResult Failure(string errorMessage, int errorLineNumber)
	{
		return new UMLParseResult(null, errorMessage, errorLineNumber);
	}
}
