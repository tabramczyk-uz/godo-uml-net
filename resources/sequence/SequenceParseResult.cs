/// <summary>
/// Outcome of a single <see cref="SequenceParser.Parse"/> call: either a diagram,
/// or the first error encountered.
/// </summary>
public sealed class SequenceParseResult
{
	private SequenceParseResult(SequenceDiagram diagram, string errorMessage, int errorLineNumber)
	{
		Diagram = diagram;
		ErrorMessage = errorMessage;
		ErrorLineNumber = errorLineNumber;
	}

	public SequenceDiagram Diagram { get; }
	public string ErrorMessage { get; }
	public int ErrorLineNumber { get; }

	public bool IsSuccess => Diagram != null;

	public static SequenceParseResult Success(SequenceDiagram diagram)
	{
		return new SequenceParseResult(diagram, null, -1);
	}

	public static SequenceParseResult Failure(string errorMessage, int errorLineNumber)
	{
		return new SequenceParseResult(null, errorMessage, errorLineNumber);
	}
}
