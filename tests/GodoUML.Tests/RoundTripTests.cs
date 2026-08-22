using System.Linq;
using Godot;
using Xunit;

namespace GodoUML.Tests;

/// <summary>
/// The brief the editor is built to asks the visual and the textual
/// representation to be equivalent. These tests hold every path between the two
/// to that promise: code that survives a rewrite, a diagram that survives being
/// written back out, and a diagram that survives a trip through PlantUML.
/// </summary>
public class RoundTripTests
{
	private const string ClassDiagram = """
		abstract Shape
			position: [40, 40]
			# name: String
			+ area(): Decimal
		class Circle
			position: [40, 210]
			- radius: Decimal
			+ area(): Decimal
		interface Drawable
			position: [280, 40]
			+ draw(canvas: Canvas, layer: Integer): void
		enum Palette
			position: [280, 210]
			RED
			GREEN
		node Legend
			position: [520, 40]
		usecase Render
			position: [520, 210]
		actor Designer
			position: [520, 380]

		Circle --|> Shape
		Circle ..|> Drawable
		Shape "1" o-- "0..*" Circle : groups
		Designer --> Render : starts
		Render ..> Circle
		Legend -- Palette
		""";

	private static UMLDiagram Parse(string code)
	{
		UMLParseResult result = UMLParser.Parse(code);
		Assert.True(
			result.IsSuccess,
			$"{result.ErrorMessage} on line {result.ErrorLineNumber}:\n{code}"
		);
		return result.Diagram;
	}

	private static void AssertSameDiagram(UMLDiagram expected, UMLDiagram actual)
	{
		Assert.Equal(
			expected.Nodes.Select(node => (node.Name, node.Type, node.Position)),
			actual.Nodes.Select(node => (node.Name, node.Type, node.Position))
		);

		foreach ((UMLNode left, UMLNode right) in expected.Nodes.Zip(actual.Nodes))
		{
			if (left is not UMLClass leftClass)
			{
				continue;
			}

			var rightClass = (UMLClass)right;
			Assert.Equal(
				leftClass.Attributes.Select(UMLNotation.Format),
				rightClass.Attributes.Select(UMLNotation.Format)
			);
			Assert.Equal(
				leftClass.Methods.Select(UMLNotation.Format),
				rightClass.Methods.Select(UMLNotation.Format)
			);
		}

		Assert.Equal(
			expected.Relationships.Select(Describe),
			actual.Relationships.Select(Describe)
		);
	}

	private static string Describe(UMLRelationship relationship)
	{
		return string.Join(
			"|",
			relationship.From.Name,
			relationship.To.Name,
			relationship.Type,
			relationship.Direction,
			relationship.FromMultiplicity,
			relationship.ToMultiplicity,
			relationship.Label
		);
	}

	[Fact]
	public void GeneratedCodeParsesBackIntoTheSameDiagram()
	{
		UMLDiagram original = Parse(ClassDiagram);

		UMLDiagram reparsed = Parse(UMLCodeGenerator.Generate(original));

		AssertSameDiagram(original, reparsed);
	}

	[Fact]
	public void GeneratingIsStableAfterTheFirstPass()
	{
		string generated = UMLCodeGenerator.Generate(Parse(ClassDiagram));

		Assert.Equal(generated, UMLCodeGenerator.Generate(Parse(generated)));
	}

	[Fact]
	public void ADiagramSurvivesATripThroughPlantUml()
	{
		UMLDiagram original = Parse(ClassDiagram);

		PlantUMLImportResult imported = PlantUMLImporter.Import(PlantUMLExporter.Export(original));

		Assert.True(imported.IsComplete, string.Join("; ", imported.Warnings));
		AssertSameDiagram(original, imported.Diagram);
	}

	[Fact]
	public void ExportingIsStableAfterTheFirstPass()
	{
		string plantUml = PlantUMLExporter.Export(Parse(ClassDiagram));

		string second = PlantUMLExporter.Export(PlantUMLImporter.Import(plantUml).Diagram);

		Assert.Equal(plantUml, second);
	}

	[Fact]
	public void AnImportedDiagramBecomesCodeThatParses()
	{
		PlantUMLImportResult imported = PlantUMLImporter.Import(
			string.Join(
				"\n",
				"@startuml",
				"skinparam monochrome true",
				"abstract class \"Base Shape\" as Base {",
				"  # {abstract} String name",
				"  + area() : Decimal",
				"}",
				"class Circle {",
				"  - radius : Decimal",
				"}",
				"enum Palette {",
				"  RED",
				"  GREEN",
				"}",
				"Circle -up-|> Base",
				"Base \"1\" o-- \"0..*\" Circle : groups",
				"(Render) <-- :Designer:",
				"@enduml"
			)
		);

		UMLDiagram reparsed = Parse(UMLCodeGenerator.Generate(imported.Diagram));

		Assert.True(imported.IsComplete, string.Join("; ", imported.Warnings));
		AssertSameDiagram(imported.Diagram, reparsed);
	}

	[Fact]
	public void AVisualEditKeepsEverythingElseByteForByte()
	{
		string code = string.Join(
			"\n",
			"// A hand written diagram.",
			"",
			"class Account   // the aggregate root",
			"\tposition: [10, 20]",
			"\t- balance: Decimal",
			"",
			"class Ledger",
			"",
			"Account \"1\" *-- \"*\" Ledger : records"
		);
		UMLDiagram diagram = Parse(code);

		string moved = UMLCodeWriter.SetNodePosition(code, diagram.Nodes[0], new Vector2(99, 88));
		string renamed = UMLCodeWriter.RenameNode(moved, diagram.Nodes[1], "Journal");

		Assert.Equal(
			string.Join(
				"\n",
				"// A hand written diagram.",
				"",
				"class Account   // the aggregate root",
				"\tposition: [99, 88]",
				"\t- balance: Decimal",
				"",
				"class Journal",
				"",
				"Account \"1\" *-- \"*\" Journal : records"
			),
			renamed
		);
		Assert.Equal(2, Parse(renamed).Nodes.Count);
	}
}
