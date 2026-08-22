using Godot;
using Xunit;

namespace GodoUML.Tests;

public class UMLCodeWriterTests
{
	private static UMLDiagram Parse(string code)
	{
		UMLParseResult result = UMLParser.Parse(code);
		Assert.True(result.IsSuccess, result.ErrorMessage);
		return result.Diagram;
	}

	[Fact]
	public void RenamesTheDeclarationAndEveryRelationship()
	{
		string code = "class Foo\nclass Bar\nFoo --> Bar\nBar --> Foo";
		UMLDiagram diagram = Parse(code);

		string renamed = UMLCodeWriter.RenameNode(code, diagram.Nodes[0], "Baz");

		Assert.Equal("class Baz\nclass Bar\nBaz --> Bar\nBar --> Baz", renamed);
	}

	[Fact]
	public void LeavesNamesThatMerelyContainTheOldNameAlone()
	{
		string code = "class Foo\nclass FooBar\nFoo --> FooBar";
		UMLDiagram diagram = Parse(code);

		string renamed = UMLCodeWriter.RenameNode(code, diagram.Nodes[0], "Qux");

		Assert.Equal("class Qux\nclass FooBar\nQux --> FooBar", renamed);
	}

	[Fact]
	public void RenamingKeepsComentsSpacingAndMultiplicities()
	{
		string code = "class  Foo   // the source\nclass Bar\nFoo \"1\" *-- \"*\" Bar : owns // link";
		UMLDiagram diagram = Parse(code);

		string renamed = UMLCodeWriter.RenameNode(code, diagram.Nodes[0], "Root");

		Assert.Equal(
			"class  Root   // the source\nclass Bar\nRoot \"1\" *-- \"*\" Bar : owns // link",
			renamed
		);
	}

	[Fact]
	public void RewritesAnExistingPosition()
	{
		string code = "class Foo\n\tposition: [1, 2]\nclass Bar";
		UMLDiagram diagram = Parse(code);

		string moved = UMLCodeWriter.SetNodePosition(code, diagram.Nodes[0], new Vector2(30, 40));

		Assert.Equal("class Foo\n\tposition: [30, 40]\nclass Bar", moved);
	}

	[Fact]
	public void InsertsAPositionBelowTheDeclarationWhenThereIsNone()
	{
		string code = "class Foo\n\t+ field: Integer\nclass Bar";
		UMLDiagram diagram = Parse(code);

		string moved = UMLCodeWriter.SetNodePosition(code, diagram.Nodes[0], new Vector2(5, 6));

		Assert.Equal("class Foo\n\tposition: [5, 6]\n\t+ field: Integer\nclass Bar", moved);
	}

	[Fact]
	public void KeepsTheCommentOnAPositionLineItRewrites()
	{
		string code = "class Foo\n\tposition: [1, 2] // placed by hand";
		UMLDiagram diagram = Parse(code);

		string moved = UMLCodeWriter.SetNodePosition(code, diagram.Nodes[0], new Vector2(7, 8));

		Assert.Equal("class Foo\n\tposition: [7, 8] // placed by hand", moved);
	}

	[Fact]
	public void WritesPositionsWithAnInvariantDecimalPoint()
	{
		string code = "class Foo";
		UMLDiagram diagram = Parse(code);

		string moved = UMLCodeWriter.SetNodePosition(code, diagram.Nodes[0], new Vector2(1.5f, -2.25f));

		Assert.Equal("class Foo\n\tposition: [1.5, -2.25]", moved);
	}

	[Fact]
	public void AppendsANewNodeWithItsPosition()
	{
		string added = UMLCodeWriter.AddNode(
			"class Foo\n",
			UMLNodeType.UseCase,
			"Login",
			new Vector2(10, 20)
		);

		Assert.Equal("class Foo\nusecase Login\n\tposition: [10, 20]\n", added);
	}

	[Fact]
	public void AppendsANewNodeToCodeThatDoesNotEndWithANewline()
	{
		string added = UMLCodeWriter.AddNode("class Foo", UMLNodeType.Node, "N", Vector2.Zero);

		Assert.Equal("class Foo\nnode N\n\tposition: [0, 0]\n", added);
	}

	[Fact]
	public void RemovesADeclarationItsBodyAndItsRelationships()
	{
		string code = string.Join(
			"\n",
			"class Foo",
			"\tposition: [1, 2]",
			"\t+ field: Integer",
			"class Bar",
			"\tposition: [3, 4]",
			"Foo --> Bar",
			"Bar --> Bar"
		);
		UMLDiagram diagram = Parse(code);

		string removed = UMLCodeWriter.RemoveNode(code, diagram.Nodes[0]);

		Assert.Equal("class Bar\n\tposition: [3, 4]\nBar --> Bar", removed);
	}

	[Fact]
	public void AppendsARelationshipInTheSpellingTheParserReadsBack()
	{
		UMLDiagram diagram = Parse("class Foo\nclass Bar");

		string added = UMLCodeWriter.AddRelationship(
			"class Foo\nclass Bar\n",
			diagram.Nodes[0],
			diagram.Nodes[1],
			UMLRelationshipType.Generalization,
			UMLRelationshipDirection.Forward
		);

		Assert.Equal("class Foo\nclass Bar\nFoo --|> Bar\n", added);
		UMLRelationship parsed = Assert.Single(Parse(added).Relationships);
		Assert.Equal(UMLRelationshipType.Generalization, parsed.Type);
		Assert.Equal(UMLRelationshipDirection.Forward, parsed.Direction);
	}

	[Fact]
	public void RemovesOnlyTheRelationshipLineItWasAskedFor()
	{
		string code = "class A\nclass B\nA --> B\nA <-- B";
		UMLDiagram diagram = Parse(code);

		string removed = UMLCodeWriter.RemoveRelationship(code, diagram.Relationships[0]);

		Assert.Equal("class A\nclass B\nA <-- B", removed);
	}

	[Fact]
	public void SwapsAnOperatorAndLeavesTheRestOfTheLineAlone()
	{
		string code = "class A\nclass B\nA \"1\" --> \"*\" B : uses // note";
		UMLDiagram diagram = Parse(code);

		string retyped = UMLCodeWriter.SetRelationshipType(
			code,
			diagram.Relationships[0],
			UMLRelationshipType.Composition,
			UMLRelationshipDirection.Backward
		);

		Assert.Equal("class A\nclass B\nA \"1\" *-- \"*\" B : uses // note", retyped);
	}

	[Fact]
	public void ReturnsTheCodeUnchangedWhenTheNodeIsNotInIt()
	{
		string code = "class Foo";
		var stranger = new UMLClass("Ghost");

		Assert.Equal(code, UMLCodeWriter.SetNodePosition(code, stranger, Vector2.One));
		Assert.Equal(code, UMLCodeWriter.RemoveNode(code, stranger));
	}

	[Fact]
	public void FindsTheDeclarationAgainWhenTheRecordedLineNoLongerMatches()
	{
		UMLDiagram diagram = Parse("class Foo");
		diagram.Nodes[0].SourceLine = 42;

		string moved = UMLCodeWriter.SetNodePosition("\n\nclass Foo", diagram.Nodes[0], Vector2.Zero);

		Assert.Equal("\n\nclass Foo\n\tposition: [0, 0]", moved);
	}
}
