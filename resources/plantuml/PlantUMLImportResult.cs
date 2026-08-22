using System.Collections.Generic;

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
}
