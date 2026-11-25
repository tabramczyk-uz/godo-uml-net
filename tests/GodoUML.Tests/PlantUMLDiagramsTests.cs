using Xunit;

namespace GodoUML.Tests;

/// <summary>
/// The File menu's PlantUML import and export go through
/// <see cref="PlantUMLDiagrams"/>, which picks the importer or exporter by
/// diagram kind.
/// </summary>
public class PlantUMLDiagramsTests
{
	[Fact]
	public void ExportsAClassDiagramWithoutAHeader()
	{
		PlantUMLExportResult result = PlantUMLDiagrams.Export("class A\nclass B\nA --|> B");

		Assert.True(result.IsSuccess);
		Assert.Contains("class A", result.Text);
		Assert.Contains("A --|> B", result.Text);
	}

	[Fact]
	public void ExportsAClassDiagramWithAHeaderAndLeavesTheHeaderOut()
	{
		PlantUMLExportResult result = PlantUMLDiagrams.Export("diagram class\nclass A");

		Assert.True(result.IsSuccess);
		Assert.Contains("class A", result.Text);
		Assert.DoesNotContain("diagram", result.Text);
	}

	[Fact]
	public void ExportsASequenceDiagramAsAPlantUMLSequenceDiagram()
	{
		PlantUMLExportResult result = PlantUMLDiagrams.Export(
			"diagram sequence\nactor User\nparticipant Shop\nUser -> Shop : order"
		);

		Assert.True(result.IsSuccess);
		Assert.Equal(
			"@startuml\n\nactor User\nparticipant Shop\n\nUser -> Shop : order\n\n@enduml\n",
			result.Text
		);
	}

	[Fact]
	public void StopsTheExportOnAHeaderError()
	{
		PlantUMLExportResult result = PlantUMLDiagrams.Export("// kind\ndiagram nope\nclass A");

		Assert.False(result.IsSuccess);
		Assert.Null(result.Text);
		Assert.Equal("Unknown diagram kind: nope", result.ErrorMessage);
		Assert.Equal(1, result.ErrorLineNumber);
	}

	[Fact]
	public void StopsTheExportOnAClassDiagramError()
	{
		PlantUMLExportResult result = PlantUMLDiagrams.Export("class A\nclass A");

		Assert.False(result.IsSuccess);
		Assert.Equal(1, result.ErrorLineNumber);
	}

	[Fact]
	public void StopsTheExportOnASequenceDiagramError()
	{
		PlantUMLExportResult result = PlantUMLDiagrams.Export(
			"diagram sequence\nparticipant Shop\nShop -> Bank"
		);

		Assert.False(result.IsSuccess);
		Assert.Equal(2, result.ErrorLineNumber);
	}

	[Fact]
	public void ImportsAPlantUMLSequenceDiagramAsASequenceDiagram()
	{
		PlantUMLDiagramImport result = PlantUMLDiagrams.Import(
			"@startuml\nactor User\nUser -> Shop : order\n@enduml"
		);

		Assert.Equal(UMLDiagramKind.Sequence, result.Kind);
		Assert.False(result.IsEmpty);
		Assert.Null(result.DescribeSkippedLines());
		Assert.Equal(
			"diagram sequence\n\nactor User\nparticipant Shop\n\nUser -> Shop : order\n",
			result.Code
		);
		Assert.Equal(UMLDiagramKind.Sequence, UMLDiagramHeader.Read(result.Code).Kind);
	}

	[Fact]
	public void ImportsAPlantUMLClassDiagramAsAClassDiagram()
	{
		PlantUMLDiagramImport result = PlantUMLDiagrams.Import(
			"@startuml\nclass A\nclass B\nA <|-- B\n@enduml"
		);

		Assert.Equal(UMLDiagramKind.Class, result.Kind);
		Assert.False(result.IsEmpty);
		Assert.False(UMLDiagramHeader.Read(result.Code).IsPresent);
		UMLParseResult parsed = UMLParser.Parse(result.Code);
		Assert.True(parsed.IsSuccess);
		Assert.Equal(2, parsed.Diagram.Nodes.Count);
		Assert.Equal(
			UMLRelationshipType.Generalization,
			Assert.Single(parsed.Diagram.Relationships).Type
		);
	}

	[Fact]
	public void ListsTheLinesASequenceDiagramImportSkips()
	{
		PlantUMLDiagramImport result = PlantUMLDiagrams.Import(
			"@startuml\nparticipant A\nalt ok\nA -> A\nend\n@enduml"
		);

		Assert.Equal(UMLDiagramKind.Sequence, result.Kind);
		Assert.NotEmpty(result.Warnings);
		Assert.NotNull(result.DescribeSkippedLines());
	}

	[Fact]
	public void ReportsAnEmptyImportWhenNothingCanBeImported()
	{
		PlantUMLDiagramImport result = PlantUMLDiagrams.Import("@startuml\n@enduml");

		Assert.True(result.IsEmpty);
	}

	[Fact]
	public void ExportThenImportGivesBackTheSequenceDiagramCode()
	{
		const string code =
			"diagram sequence\n\nactor User\nparticipant Shop\n\n"
			+ "User -> Shop : order\nShop ->> Shop : log\nShop --> User : receipt\n";

		PlantUMLExportResult exported = PlantUMLDiagrams.Export(code);
		PlantUMLDiagramImport imported = PlantUMLDiagrams.Import(exported.Text);

		Assert.Equal(code, imported.Code);
	}
}
