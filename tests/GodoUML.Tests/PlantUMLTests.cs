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

	// [Theory] runs the test once per [InlineData] row, passing the row's values
	// as the arguments.
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

public class PlantUMLSequenceExporterTests
{
	private static SequenceDiagram Parse(string body)
	{
		SequenceParseResult result = SequenceParser.Parse(body);
		Assert.True(result.IsSuccess, result.ErrorMessage);
		return result.Diagram;
	}

	[Fact]
	public void WritesTheParticipantsAndThenTheMessages()
	{
		string plantUml = PlantUMLSequenceExporter.Export(
			Parse(
				string.Join(
					"\n",
					"actor User",
					"participant Shop",
					"User -> Shop : order",
					"Shop ->> Shop : log",
					"Shop --> User : receipt",
					"User -> Shop"
				)
			)
		);

		Assert.Equal(
			string.Join(
				"\n",
				"@startuml",
				"",
				"actor User",
				"participant Shop",
				"",
				"User -> Shop : order",
				"Shop ->> Shop : log",
				"Shop --> User : receipt",
				"User -> Shop",
				"",
				"@enduml",
				""
			),
			plantUml
		);
	}

	[Fact]
	public void LeavesOutTheGapWhenThereAreNoMessages()
	{
		Assert.Equal(
			"@startuml\n\nparticipant Shop\n\n@enduml\n",
			PlantUMLSequenceExporter.Export(Parse("participant Shop"))
		);
	}

	[Fact]
	public void ExportsAnEmptyDiagramAsJustTheMarkers()
	{
		Assert.Equal("@startuml\n\n\n@enduml\n", PlantUMLSequenceExporter.Export(new SequenceDiagram()));
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
		Assert.True(result.Diagram.Nodes[1].Position.Value.Y > result.Diagram.Nodes[0].Position.Value.Y);
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

	[Fact]
	public void HasNothingToSayAboutACompleteImport()
	{
		PlantUMLImportResult result = PlantUMLImporter.Import("@startuml\nclass A\n@enduml");

		Assert.False(result.IsEmpty);
		Assert.Null(result.DescribeSkippedLines());
	}

	[Theory]
	[InlineData("")]
	[InlineData("@startuml\n@enduml")]
	[InlineData("@startuml\nskinparam monochrome true\n@enduml")]
	public void FindsNothingToImportWithoutNodes(string source)
	{
		Assert.True(PlantUMLImporter.Import(source).IsEmpty);
	}

	[Fact]
	public void ListsTheLinesAnImportLeavesOut()
	{
		PlantUMLImportResult result = PlantUMLImporter.Import(
			"@startuml\nclass A\nfoo bar baz\n@enduml"
		);

		Assert.Equal(
			"1 line could not be imported and will be left out:\n" + result.Warnings[0],
			result.DescribeSkippedLines()
		);
	}

	[Fact]
	public void ShortensALongListOfSkippedLines()
	{
		string source = "@startuml\nclass A\n" + string.Concat(Enumerable.Repeat("foo bar baz\n", 5)) + "@enduml";
		PlantUMLImportResult result = PlantUMLImporter.Import(source);

		string description = result.DescribeSkippedLines(maxListed: 2);

		Assert.StartsWith("5 lines could not be imported and will be left out:\n", description);
		Assert.Equal(4, description.Split('\n').Length);
		Assert.EndsWith("\n...and 3 more.", description);
	}
}

public class PlantUMLSequenceImporterTests
{
	private static PlantUMLSequenceImportResult Import(params string[] lines)
	{
		return PlantUMLSequenceImporter.Import(string.Join("\n", ["@startuml", .. lines, "@enduml"]));
	}

	private static string Describe(SequenceMessage message)
	{
		return $"{message.From.Name} {message.Kind} {message.To.Name} : {message.Label}";
	}

	[Fact]
	public void ImportsDeclarationsInOrderWithTheirKinds()
	{
		PlantUMLSequenceImportResult result = Import(
			"participant Shop",
			"actor User",
			"database Store",
			"boundary Gate"
		);

		Assert.True(result.IsComplete, string.Join("; ", result.Warnings));
		Assert.Equal(
			[
				("Shop", SequenceParticipantKind.Participant),
				("User", SequenceParticipantKind.Actor),
				("Store", SequenceParticipantKind.Participant),
				("Gate", SequenceParticipantKind.Participant),
			],
			result.Diagram.Participants.Select(p => (p.Name, p.Kind))
		);
	}

	[Theory]
	[InlineData("A -> B", "A Synchronous B")]
	[InlineData("A->>B", "A Asynchronous B")]
	[InlineData("A --> B", "A Reply B")]
	[InlineData("A -->> B", "A Reply B")]
	[InlineData("A <- B", "B Synchronous A")]
	[InlineData("A <<- B", "B Asynchronous A")]
	[InlineData("A <-- B", "B Reply A")]
	[InlineData("A -[#red]> B", "A Synchronous B")]
	[InlineData("A -[#red]-> B", "A Reply B")]
	public void ImportsEveryArrowItSupports(string line, string expected)
	{
		PlantUMLSequenceImportResult result = Import("participant A", "participant B", line);

		Assert.True(result.IsComplete, string.Join("; ", result.Warnings));
		Assert.Equal($"{expected} : ", Describe(Assert.Single(result.Diagram.Messages)));
	}

	[Theory]
	[InlineData("A <-> B")]
	[InlineData("A ->x B")]
	[InlineData("A - B")]
	[InlineData("[-> A")]
	public void WarnsAboutArrowsThatAreNotOneWayMessages(string line)
	{
		PlantUMLSequenceImportResult result = Import("participant A", "participant B", line);

		Assert.Contains("Line 4", Assert.Single(result.Warnings));
		Assert.Empty(result.Diagram.Messages);
	}

	[Fact]
	public void DeclaresParticipantsThatOnlyAppearInMessages()
	{
		PlantUMLSequenceImportResult result = Import(
			"Alice -> Bob : hello",
			"participant Carol",
			"Bob -> Carol",
			"actor Alice"
		);

		Assert.True(result.IsComplete, string.Join("; ", result.Warnings));
		Assert.Equal(
			[
				("Alice", SequenceParticipantKind.Actor),
				("Bob", SequenceParticipantKind.Participant),
				("Carol", SequenceParticipantKind.Participant),
			],
			result.Diagram.Participants.Select(p => (p.Name, p.Kind))
		);
		Assert.StartsWith(
			"diagram sequence\n\nactor Alice\nparticipant Bob\nparticipant Carol\n\n",
			SequenceCodeGenerator.Generate(result.Diagram)
		);
	}

	[Fact]
	public void TurnsNamesIntoIdentifiersAndFollowsAliases()
	{
		PlantUMLSequenceImportResult result = Import(
			"participant \"Online Shop\" as Shop",
			"actor User as \"The User\"",
			"User -> Shop : buy",
			"\"Online Shop\" --> \"The User\" : ok",
			"\"Bank Account\" -> Bank_Account"
		);

		Assert.True(result.IsComplete, string.Join("; ", result.Warnings));
		Assert.Equal(
			["Shop", "User", "Bank_Account", "Bank_Account2"],
			result.Diagram.Participants.Select(p => p.Name)
		);
		Assert.Equal(
			[
				"User Synchronous Shop : buy",
				"Shop Reply User : ok",
				"Bank_Account Synchronous Bank_Account2 : ",
			],
			result.Diagram.Messages.Select(Describe)
		);
	}

	[Fact]
	public void ReadsSelfMessagesAndKeepsLabelsFreeOfComments()
	{
		PlantUMLSequenceImportResult result = Import(
			"participant Shop",
			"Shop ->> Shop :  fetch http://example.com  "
		);

		SequenceMessage message = Assert.Single(result.Diagram.Messages);
		Assert.True(message.IsSelfMessage);
		Assert.Equal("fetch http:/example.com", message.Label);
	}

	[Fact]
	public void DerivesActivationsAndSkipsTheOnesWrittenOut()
	{
		PlantUMLSequenceImportResult result = Import(
			"' a comment",
			"/' a block",
			"   comment '/",
			"title Checkout",
			"skinparam monochrome true",
			"autonumber",
			"hide footbox",
			"box \"Backend\"",
			"participant Shop",
			"participant Bank",
			"end box",
			"Shop -> Bank : charge",
			"activate Bank",
			"note right of Bank",
			"  checks the card",
			"end note",
			"note left of Shop : waits",
			"Bank --> Shop : ok",
			"deactivate Bank"
		);

		Assert.True(result.IsComplete, string.Join("; ", result.Warnings));
		Assert.Equal(2, result.Diagram.Messages.Count);
		Assert.Single(result.Diagram.Activations);
	}

	[Fact]
	public void WarnsAboutFragmentsCreationAndDividersButKeepsTheMessagesInside()
	{
		PlantUMLSequenceImportResult result = Import(
			"participant A",
			"participant B",
			"== Start ==",
			"alt ok",
			"A -> B : pay",
			"else failed",
			"A -> B : retry",
			"end",
			"...",
			"create C",
			"A -> C",
			"destroy C",
			"return done"
		);

		Assert.Equal(8, result.Warnings.Count);
		Assert.All(result.Warnings, warning => Assert.Contains("unsupported", warning));
		Assert.Equal(["A", "B", "C"], result.Diagram.Participants.Select(p => p.Name));
		Assert.Equal(3, result.Diagram.Messages.Count);
	}

	[Theory]
	[InlineData("A -> B")]
	[InlineData("participant A")]
	[InlineData("actor User", "actor Admin", "User --> Admin")]
	[InlineData("actor User", "activate User")]
	[InlineData("participant A", "alt ok", "end")]
	public void TakesForASequenceDiagramWhatPlantUmlWould(params string[] lines)
	{
		Assert.True(Import(lines).IsSequenceDiagram);
	}

	[Theory]
	[InlineData]
	[InlineData("actor User")]
	[InlineData("class A", "A -> B")]
	[InlineData("A -> B", "A <|-- C")]
	[InlineData("actor User", "User --> (Log in)")]
	[InlineData("participant A", "A -up-> B")]
	[InlineData("A -> B", "package Shop {", "}")]
	public void LeavesClassAndUseCaseDiagramsToTheClassImporter(params string[] lines)
	{
		Assert.False(Import(lines).IsSequenceDiagram);
	}

	[Theory]
	[InlineData("")]
	[InlineData("@startuml\n@enduml")]
	[InlineData("@startuml\nskinparam monochrome true\n@enduml")]
	public void FindsNothingToImportWithoutParticipants(string source)
	{
		Assert.True(PlantUMLSequenceImporter.Import(source).IsEmpty);
	}

	[Fact]
	public void ListsTheLinesAnImportLeavesOut()
	{
		PlantUMLSequenceImportResult result = Import("A -> B", "loop 3 times");

		Assert.False(result.IsEmpty);
		Assert.Equal(
			"1 line could not be imported and will be left out:\n"
				+ "Line 3: unsupported sequence diagram statement (loop 3 times)",
			result.DescribeSkippedLines()
		);
	}
}
