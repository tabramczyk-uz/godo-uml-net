using System.Collections.Generic;

/// <summary>
/// Outcome of a PlantUML sequence diagram import. Like
/// <see cref="PlantUMLImportResult"/>, it never fails outright: what the
/// importer cannot read is skipped and listed in <see cref="Warnings"/>.
/// </summary>
public sealed class PlantUMLSequenceImportResult
{
	public PlantUMLSequenceImportResult(
		SequenceDiagram diagram,
		List<string> warnings,
		bool isSequenceDiagram
	)
	{
		Diagram = diagram;
		Warnings = warnings;
		IsSequenceDiagram = isSequenceDiagram;
	}

	public SequenceDiagram Diagram { get; }

	/// <summary>One entry per line that was skipped, with its line number.</summary>
	public IReadOnlyList<string> Warnings { get; }

	/// <summary>
	/// Whether the source is a PlantUML sequence diagram rather than a class
	/// or use case diagram, and so should be imported as this result rather
	/// than through <see cref="PlantUMLImporter"/>. See
	/// <see cref="PlantUMLSequenceImporter"/> for how that is decided.
	/// </summary>
	public bool IsSequenceDiagram { get; }

	public bool IsComplete => Warnings.Count == 0;

	/// <summary>True when the source held no participants.</summary>
	public bool IsEmpty => Diagram.Participants.Count == 0;

	/// <summary>
	/// Tells the user which lines an import would leave out, listing at most
	/// <paramref name="maxListed"/> of them, or <c>null</c> when nothing is.
	/// </summary>
	public string DescribeSkippedLines(int maxListed = 8)
	{
		return PlantUMLSkippedLines.Describe(Warnings, maxListed);
	}
}
