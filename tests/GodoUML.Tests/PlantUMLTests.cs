using System.Linq;
using Godot;
using Xunit;

namespace GodoUML.Tests;

public class PlantUMLExporterTests
{
	private static UMLDiagram Parse(string code)
	{
		UMLParseResult result = UMLParser.Parse(code);
		Assert.True(result.IsSuccess, result.ErrorMessage);
		return result.Diagram;
	}

	[Fact]
	public void WrapsTheDiagramInStartAndEndMarkers()
	{
		string plantUml = PlantUMLExporter.Export(Parse("class Foo"), includePositions: false);

		Assert.StartsWith("@startuml", plantUml);
		Assert.EndsWith("@enduml\n", plantUml);
	}

	[Fact]
	public void WritesEveryNodeTypeWithItsPlantUmlKeyword()
	{
		string plantUml = PlantUMLExporter.Export(
			Parse("node A\nclass B\ninterface C\nabstract D\nenum E\nusecase F\nactor G"),
			includePositions: false
		);

		Assert.Contains("rectangle A\n", plantUml);
		Assert.Contains("class B\n", plantUml);
		Assert.Contains("interface C\n", plantUml);
		Assert.Contains("abstract class D\n", plantUml);
		Assert.Contains("enum E\n", plantUml);
		Assert.Contains("usecase F\n", plantUml);
		Assert.Contains("actor G\n", plantUml);
	}

	[Fact]
	public void WritesMembersInsideABlock()
	{
		string plantUml = PlantUMLExporter.Export(
			Parse("class Account\n\t- balance: Decimal\n\t+ deposit(amount: Decimal): void"),
			includePositions: false
		);

		Assert.Contains(
			"class Account {\n  - balance : Decimal\n  + deposit(amount : Decimal) : void\n}\n",
			plantUml
		);
	}

	[Fact]
	public void LeavesOutTheBlockWhenThereAreNoMembers()
	{
		string plantUml = PlantUMLExporter.Export(Parse("class Empty"), includePositions: false);

		Assert.DoesNotContain("{", plantUml);
	}

	[Theory]
	[InlineData("A --> B", "A --> B")]
	[InlineData("A - B", "A -- B")]
	[InlineData("A <|-- B", "A <|-- B")]
	[InlineData("A ..|> B", "A ..|> B")]
	[InlineData("A *-- B", "A *-- B")]
	[InlineData("A --o B", "A --o B")]
	[InlineData("A <.. B", "A <.. B")]
	public void WritesTheArrowThatMatchesTheRelationship(string line, string expected)
	{
		string plantUml = PlantUMLExporter.Export(
			Parse($"class A\nclass B\n{line}"),
			includePositions: false
		);

		Assert.Contains(expected + "\n", plantUml);
	}

	[Fact]
	public void WritesMultiplicitiesAndLabels()
	{
		string plantUml = PlantUMLExporter.Export(
			Parse("class A\nclass B\nA \"1\" *-- \"0..*\" B : contains"),
			includePositions: false
		);

		Assert.Contains("A \"1\" *-- \"0..*\" B : contains\n", plantUml);
	}

	[Fact]
	public void WritesPositionsAsPlantUmlComments()
	{
		string plantUml = PlantUMLExporter.Export(Parse("class Foo\n\tposition: [12, 34]"));

		Assert.Contains("'@position Foo [12, 34]\n", plantUml);
	}
}

public class PlantUMLImporterTests
{
	[Fact]
	public void ImportsDeclarationsAndTheirTypes()
	{
		PlantUMLImportResult result = PlantUMLImporter.Import(
			string.Join(
				"\n",
				"@startuml",
				"class Account",
				"interface Payable",
				"abstract class Shape",
				"enum Currency",
				"usecase Withdraw",
				"actor Client",
				"rectangle Box",
				"@enduml"
			)
		);

		Assert.True(result.IsComplete);
		Assert.Equal(
			[
				UMLNodeType.Class,
				UMLNodeType.Interface,
				UMLNodeType.AbstractClass,
				UMLNodeType.Enum,
				UMLNodeType.UseCase,
				UMLNodeType.Actor,
				UMLNodeType.Node,
			],
			result.Diagram.Nodes.Select(node => node.Type)
		);
	}

	[Fact]
	public void ImportsMembersInBothSpellings()
	{
		PlantUMLImportResult result = PlantUMLImporter.Import(
			string.Join(
				"\n",
				"@startuml",
				"class Account {",
				"  - balance : Decimal",
				"  {static} + int count",
				"  .. Operations ..",
				"  + deposit(amount : Decimal) : void",
				"  + void close()",
				"}",
				"@enduml"
			)
		);

		var account = (UMLClass)Assert.Single(result.Diagram.Nodes);
		Assert.Equal(["balance", "count"], account.Attributes.Select(attribute => attribute.Name));
		Assert.Equal(["Decimal", "int"], account.Attributes.Select(attribute => attribute.Type));
		Assert.Equal(["deposit", "close"], account.Methods.Select(method => method.Name));
		Assert.Equal("void", account.Methods[1].ReturnType);
		Assert.Equal("amount", Assert.Single(account.Methods[0].Arguments).Name);
	}

	[Theory]
	[InlineData("A <|-- B", UMLRelationshipType.Generalization, UMLRelationshipDirection.Backward)]
	[InlineData("A ..|> B", UMLRelationshipType.Realization, UMLRelationshipDirection.Forward)]
	[InlineData("A *-- B", UMLRelationshipType.Composition, UMLRelationshipDirection.Backward)]
	[InlineData("A o--> B", UMLRelationshipType.Aggregation, UMLRelationshipDirection.Backward)]
	[InlineData("A --> B", UMLRelationshipType.Association, UMLRelationshipDirection.Forward)]
	[InlineData("A ----> B", UMLRelationshipType.Association, UMLRelationshipDirection.Forward)]
	[InlineData("A -up-> B", UMLRelationshipType.Association, UMLRelationshipDirection.Forward)]
	[InlineData("A .down.> B", UMLRelationshipType.Dependency, UMLRelationshipDirection.Forward)]
	[InlineData("A -- B", UMLRelationshipType.Association, UMLRelationshipDirection.None)]
	public void ImportsEveryArrowItSupports(
		string line,
		UMLRelationshipType expectedType,
		UMLRelationshipDirection expectedDirection
	)
	{
		PlantUMLImportResult result = PlantUMLImporter.Import($"@startuml\n{line}\n@enduml");

		UMLRelationship relationship = Assert.Single(result.Diagram.Relationships);
		Assert.True(result.IsComplete, string.Join("; ", result.Warnings));
		Assert.Equal(expectedType, relationship.Type);
		Assert.Equal(expectedDirection, relationship.Direction);
	}

	[Fact]
	public void DeclaresNodesThatOnlyAppearInRelationships()
	{
		PlantUMLImportResult result = PlantUMLImporter.Import("@startuml\nClass01 <|-- Class02\n@enduml");

		Assert.Equal(["Class01", "Class02"], result.Diagram.Nodes.Select(node => node.Name));
		Assert.All(result.Diagram.Nodes, node => Assert.Equal(UMLNodeType.Class, node.Type));
	}

	[Fact]
	public void ImportsMultiplicitiesAndLabels()
	{
		PlantUMLImportResult result = PlantUMLImporter.Import(
			"@startuml\nOrder \"1\" *-- \"0..*\" Item : contains\n@enduml"
		);

		UMLRelationship relationship = Assert.Single(result.Diagram.Relationships);
		Assert.Equal("1", relationship.FromMultiplicity);
		Assert.Equal("0..*", relationship.ToMultiplicity);
		Assert.Equal("contains", relationship.Label);
	}

	[Fact]
	public void TurnsNamesWithSpacesIntoIdentifiersAndKeepsTheAlias()
	{
		PlantUMLImportResult result = PlantUMLImporter.Import(
			string.Join(
				"\n",
				"@startuml",
				"class \"Bank Account\" as BA",
				"class \"Plain Label\"",
				"BA --> \"Plain Label\"",
				"@enduml"
			)
		);

		Assert.Equal(["BA", "Plain_Label"], result.Diagram.Nodes.Select(node => node.Name));
		UMLRelationship relationship = Assert.Single(result.Diagram.Relationships);
		Assert.Same(result.Diagram.Nodes[0], relationship.From);
		Assert.Same(result.Diagram.Nodes[1], relationship.To);
	}

	[Fact]
	public void ReadsTheUseCaseAndActorShorthand()
	{
		PlantUMLImportResult result = PlantUMLImporter.Import("@startuml\n:Client: --> (Withdraw)\n@enduml");

		Assert.Equal(UMLNodeType.Actor, result.Diagram.Nodes[0].Type);
		Assert.Equal(UMLNodeType.UseCase, result.Diagram.Nodes[1].Type);
		Assert.Equal(["Client", "Withdraw"], result.Diagram.Nodes.Select(node => node.Name));
	}

	[Fact]
	public void SkipsDirectivesCommentsNotesAndGroups()
	{
		PlantUMLImportResult result = PlantUMLImporter.Import(
			string.Join(
				"\n",
				"@startuml",
				"' a comment",
				"/' a block",
				"   comment '/",
				"skinparam classAttributeIconSize 0",
				"hide empty members",
				"left to right direction",
				"title A title",
				"package Domain {",
				"  class Account",
				"}",
				"note left of Account",
				"  something",
				"end note",
				"note right of Account : inline",
				"@enduml"
			)
		);

		Assert.True(result.IsComplete, string.Join("; ", result.Warnings));
		Assert.Equal("Account", Assert.Single(result.Diagram.Nodes).Name);
	}

	[Fact]
	public void ReportsWhatItCouldNotRead()
	{
		PlantUMLImportResult result = PlantUMLImporter.Import("@startuml\nclass A\n?? nonsense ??\n@enduml");

		Assert.False(result.IsComplete);
		Assert.Contains("Line 3", Assert.Single(result.Warnings));
		Assert.Single(result.Diagram.Nodes);
	}

	[Fact]
	public void LaysOutADiagramThatArrivesWithoutPositions()
	{
		PlantUMLImportResult result = PlantUMLImporter.Import("@startuml\nA <|-- B\nA <|-- C\n@enduml");

		Assert.All(result.Diagram.Nodes, node => Assert.NotEqual(Vector2.Zero, node.Position));
		Assert.True(result.Diagram.Nodes[1].Position.Y > result.Diagram.Nodes[0].Position.Y);
	}

	[Fact]
	public void ReadsBackThePositionsItsOwnExporterWrote()
	{
		PlantUMLImportResult result = PlantUMLImporter.Import(
			"@startuml\n'@position Foo [120, 340]\nclass Foo\n@enduml"
		);

		Assert.Equal(new Vector2(120.0f, 340.0f), Assert.Single(result.Diagram.Nodes).Position);
	}

	[Fact]
	public void DropsATypeTheGodoUmlLanguageCannotSpell()
	{
		PlantUMLImportResult result = PlantUMLImporter.Import(
			"@startuml\nclass A {\n  + weird : <<what>>\n}\n@enduml"
		);

		var node = (UMLClass)Assert.Single(result.Diagram.Nodes);
		Assert.Equal("", Assert.Single(node.Attributes).Type);
	}
}
