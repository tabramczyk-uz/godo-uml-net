/// <summary>
/// Picks the PlantUML exporter or importer for a diagram's kind, so the File
/// menu needs no knowledge of the kinds. This is the one place that does: a
/// new diagram kind adds one branch to <see cref="Export"/> and one to
/// <see cref="Import"/>.
/// </summary>
public static class PlantUMLDiagrams
{
	/// <summary>
	/// Exports the diagram <paramref name="code"/> describes, choosing the
	/// exporter by its diagram header. An error in the header or the code
	/// stops the export, so an older diagram is never exported in its place.
	/// </summary>
	public static PlantUMLExportResult Export(string code)
	{
		UMLDiagramHeader header = UMLDiagramHeader.Read(code);
		if (!header.IsSuccess)
		{
			return PlantUMLExportResult.Failure(header.ErrorMessage, header.ErrorLineNumber);
		}

		string body = header.RemoveFrom(code);
		switch (header.Kind)
		{
			case UMLDiagramKind.Sequence:
				SequenceParseResult sequence = SequenceParser.Parse(body);
				return sequence.IsSuccess
					? PlantUMLExportResult.Success(PlantUMLSequenceExporter.Export(sequence.Diagram))
					: PlantUMLExportResult.Failure(sequence.ErrorMessage, sequence.ErrorLineNumber);
			default:
				UMLParseResult result = UMLParser.Parse(body);
				return result.IsSuccess
					? PlantUMLExportResult.Success(PlantUMLExporter.Export(result.Diagram))
					: PlantUMLExportResult.Failure(result.ErrorMessage, result.ErrorLineNumber);
		}
	}

	/// <summary>
	/// Imports PlantUML <paramref name="source"/> as GodoUML code. A PlantUML
	/// sequence diagram becomes a sequence diagram; anything else is read as a
	/// class diagram, as PlantUML itself falls back to when a file is not a
	/// sequence diagram.
	/// </summary>
	public static PlantUMLDiagramImport Import(string source)
	{
		PlantUMLSequenceImportResult sequence = PlantUMLSequenceImporter.Import(source);
		if (sequence.IsSequenceDiagram)
		{
			return new PlantUMLDiagramImport(
				UMLDiagramKind.Sequence,
				SequenceCodeGenerator.Generate(sequence.Diagram),
				sequence.IsEmpty,
				sequence.Warnings
			);
		}

		PlantUMLImportResult result = PlantUMLImporter.Import(source);
		return new PlantUMLDiagramImport(
			UMLDiagramKind.Class,
			UMLCodeGenerator.Generate(result.Diagram),
			result.IsEmpty,
			result.Warnings
		);
	}
}
