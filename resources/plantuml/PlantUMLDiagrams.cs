/// <summary>
/// Picks the PlantUML exporter for a diagram's kind, so the File menu needs
/// no knowledge of the kinds. This is the one place that does: a new diagram
/// kind adds one branch to <see cref="Export"/>.
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
}
