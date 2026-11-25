using Xunit;

namespace GodoUML.Tests;

/// <summary>
/// The File menu's PlantUML export goes through <see cref="PlantUMLDiagrams"/>,
/// which picks the exporter by diagram kind.
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
}
