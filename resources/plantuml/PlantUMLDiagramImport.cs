using System.Collections.Generic;

/// <summary>
/// Outcome of <see cref="PlantUMLDiagrams.Import"/>: the generated GodoUML code
/// of whichever diagram kind the PlantUML turned out to be. Like the
/// importers it wraps, it never fails outright; skipped lines are listed in
/// <see cref="Warnings"/>.
/// </summary>
public sealed class PlantUMLDiagramImport
{
	public PlantUMLDiagramImport(
		UMLDiagramKind kind,
		string code,
		bool isEmpty,
		IReadOnlyList<string> warnings
	)
	{
		Kind = kind;
		Code = code;
		IsEmpty = isEmpty;
		Warnings = warnings;
	}

	public UMLDiagramKind Kind { get; }

	/// <summary>The generated code, with a diagram header where the kind needs one.</summary>
	public string Code { get; }

	/// <summary>True when the source held nothing the diagram kind can represent.</summary>
	public bool IsEmpty { get; }

	/// <summary>One entry per line that was skipped, with its line number.</summary>
	public IReadOnlyList<string> Warnings { get; }

	/// <summary>
	/// Tells the user which lines an import would leave out, listing at most
	/// <paramref name="maxListed"/> of them, or <c>null</c> when nothing is.
	/// </summary>
	public string DescribeSkippedLines(int maxListed = 8)
	{
		return PlantUMLSkippedLines.Describe(Warnings, maxListed);
	}
}
