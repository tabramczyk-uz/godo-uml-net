using System.Collections.Generic;
using System.Text;

/// <summary>
/// Outcome of a PlantUML import. Import never fails outright: PlantUML is a far
/// bigger language than GodoUML, so anything the importer does not understand is
/// skipped and reported here instead of stopping the whole file.
/// </summary>
public sealed class PlantUMLImportResult
{
	public PlantUMLImportResult(UMLDiagram diagram, List<string> warnings)
	{
		Diagram = diagram;
		Warnings = warnings;
	}

	public UMLDiagram Diagram { get; }

	/// <summary>One entry per line that was skipped, with its line number.</summary>
	public IReadOnlyList<string> Warnings { get; }

	public bool IsComplete => Warnings.Count == 0;

	/// <summary>True when the source held no nodes the model can represent.</summary>
	public bool IsEmpty => Diagram.Nodes.Count == 0;

	/// <summary>
	/// Tells the user which lines an import would leave out, listing at most
	/// <paramref name="maxListed"/> of them, or <c>null</c> when nothing is.
	/// </summary>
	public string DescribeSkippedLines(int maxListed = 8)
	{
		if (IsComplete)
		{
			return null;
		}

		string lines = Warnings.Count == 1 ? "1 line" : $"{Warnings.Count} lines";
		var description = new StringBuilder($"{lines} could not be imported and will be left out:");

		for (int i = 0; i < Warnings.Count && i < maxListed; i++)
		{
			description.Append('\n').Append(Warnings[i]);
		}

		if (Warnings.Count > maxListed)
		{
			description.Append($"\n...and {Warnings.Count - maxListed} more.");
		}

		return description.ToString();
	}
}
