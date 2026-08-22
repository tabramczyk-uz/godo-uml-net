using System.Linq;
using Godot;
using Xunit;

namespace GodoUML.Tests;

public class UMLParserTests
{
	private static UMLDiagram ParseOrFail(string code)
	{
		UMLParseResult result = UMLParser.Parse(code);
		Assert.True(
			result.IsSuccess,
			$"Expected a successful parse but got: {result.ErrorMessage} on line {result.ErrorLineNumber}"
		);
		return result.Diagram;
	}

	private static UMLParseResult ParseExpectingFailure(string code)
	{
		UMLParseResult result = UMLParser.Parse(code);
		Assert.False(result.IsSuccess, "Expected the parse to fail");
		return result;
	}

	[Fact]
	public void ParsesAnEmptyDocument()
	{
		UMLDiagram diagram = ParseOrFail("");

		Assert.Empty(diagram.Nodes);
		Assert.Empty(diagram.Relationships);
	}

	[Theory]
	[InlineData("node Plain", UMLNodeType.Node)]
	[InlineData("class Account", UMLNodeType.Class)]
	[InlineData("interface Payable", UMLNodeType.Interface)]
	[InlineData("abstract Shape", UMLNodeType.AbstractClass)]
	[InlineData("enum Currency", UMLNodeType.Enum)]
	[InlineData("usecase Withdraw", UMLNodeType.UseCase)]
	[InlineData("actor Client", UMLNodeType.Actor)]
	public void ParsesEveryNodeType(string code, UMLNodeType expected)
	{
		UMLNode node = Assert.Single(ParseOrFail(code).Nodes);

		Assert.Equal(expected, node.Type);
		Assert.Equal(0, node.SourceLine);
	}

	[Fact]
	public void ClassifiersAreBackedByUmlClass()
	{
		UMLDiagram diagram = ParseOrFail("class A\ninterface B\nabstract C\nenum D\nnode E");

		Assert.All(diagram.Nodes.Take(4), node => Assert.IsType<UMLClass>(node));
		Assert.IsType<UMLNode>(diagram.Nodes[4]);
	}

	[Fact]
	public void ParsesPosition()
	{
		UMLNode node = Assert.Single(ParseOrFail("class Foo\n\tposition: [10, -20.5]").Nodes);

		Assert.Equal(new Vector2(10.0f, -20.5f), node.Position);
	}

	[Fact]
	public void RecordsTheDeclarationLineOfEveryNode()
	{
		UMLDiagram diagram = ParseOrFail("// a comment\nclass A\n\tposition: [1, 2]\n\nclass B");

		Assert.Equal(1, diagram.Nodes[0].SourceLine);
		Assert.Equal(4, diagram.Nodes[1].SourceLine);
	}

	[Fact]
	public void ParsesAttributesWithAndWithoutTypes()
	{
		var account = (UMLClass)
			Assert.Single(ParseOrFail("class Account\n\t- balance: Decimal\n\t+ owner\n").Nodes);

		Assert.Equal(2, account.Attributes.Count);
		Assert.Equal("balance", account.Attributes[0].Name);
		Assert.Equal("Decimal", account.Attributes[0].Type);
		Assert.Equal(UMLVisibility.Private, account.Attributes[0].Visibility);
		Assert.Equal("owner", account.Attributes[1].Name);
		Assert.Equal("", account.Attributes[1].Type);
		Assert.Equal(UMLVisibility.Public, account.Attributes[1].Visibility);
	}

	[Theory]
	[InlineData("+", UMLVisibility.Public)]
	[InlineData("-", UMLVisibility.Private)]
	[InlineData("#", UMLVisibility.Protected)]
	[InlineData("~", UMLVisibility.Package)]
	public void ParsesEveryVisibility(string symbol, UMLVisibility expected)
	{
		var node = (UMLClass)Assert.Single(ParseOrFail($"class A\n\t{symbol} field: Integer").Nodes);

		Assert.Equal(expected, Assert.Single(node.Attributes).Visibility);
	}

	[Fact]
	public void ParsesEnumerationLiteralsAsUntypedAttributes()
	{
		var currency = (UMLClass)Assert.Single(ParseOrFail("enum Currency\n\tPLN\n\tEUR").Nodes);

		Assert.Equal(["PLN", "EUR"], currency.Attributes.Select(literal => literal.Name));
		Assert.All(currency.Attributes, literal => Assert.Equal(UMLVisibility.Unknown, literal.Visibility));
	}

	[Fact]
	public void ParsesMethodsWithArgumentsAndReturnTypes()
	{
		var node = (UMLClass)
			Assert.Single(ParseOrFail("class A\n\t+ transfer(amount: Decimal, to: Account): Boolean").Nodes);

		UMLMethod method = Assert.Single(node.Methods);
		Assert.Equal("transfer", method.Name);
		Assert.Equal("Boolean", method.ReturnType);
		Assert.Equal(UMLVisibility.Public, method.Visibility);
		Assert.Equal(["amount", "to"], method.Arguments.Select(argument => argument.Name));
		Assert.Equal(["Decimal", "Account"], method.Arguments.Select(argument => argument.Type));
	}

	[Fact]
	public void ParsesAMethodWithoutArgumentsOrVisibility()
	{
		var node = (UMLClass)Assert.Single(ParseOrFail("class A\n\trun()").Nodes);

		UMLMethod method = Assert.Single(node.Methods);
		Assert.Empty(method.Arguments);
		Assert.Equal("", method.ReturnType);
		Assert.Equal(UMLVisibility.Unknown, method.Visibility);
	}

	[Fact]
	public void DoesNotSplitAGenericArgumentTypeOnItsComma()
	{
		var node = (UMLClass)
			Assert.Single(ParseOrFail("class A\n\t+ store(entries: Map<String, Integer>)").Nodes);

		UMLMethodArgument argument = Assert.Single(Assert.Single(node.Methods).Arguments);
		Assert.Equal("entries", argument.Name);
		Assert.Equal("Map<String, Integer>", argument.Type);
	}

	[Fact]
	public void KeepsPositionAPropertyRatherThanAnAttribute()
	{
		var node = (UMLClass)Assert.Single(ParseOrFail("class A\n\tposition: [4, 5]").Nodes);

		Assert.Empty(node.Attributes);
		Assert.Equal(new Vector2(4.0f, 5.0f), node.Position);
	}

	[Fact]
	public void AnUnknownKeyValueLineOnAClassIsAnAttribute()
	{
		var node = (UMLClass)Assert.Single(ParseOrFail("class A\n\tcolour: Red").Nodes);

		UMLAttribute attribute = Assert.Single(node.Attributes);
		Assert.Equal("colour", attribute.Name);
		Assert.Equal("Red", attribute.Type);
		Assert.Equal(UMLVisibility.Unknown, attribute.Visibility);
	}

	[Fact]
	public void APropertyKeywordWithAVisibilityIsStillAnAttribute()
	{
		var node = (UMLClass)Assert.Single(ParseOrFail("class A\n\t+ position: Vector2").Nodes);

		Assert.Equal("position", Assert.Single(node.Attributes).Name);
		Assert.Equal(Vector2.Zero, node.Position);
	}

	[Theory]
	[InlineData("A - B", UMLRelationshipType.Association, UMLRelationshipDirection.None)]
	[InlineData("A -- B", UMLRelationshipType.Association, UMLRelationshipDirection.None)]
	[InlineData("A -> B", UMLRelationshipType.Association, UMLRelationshipDirection.Forward)]
	[InlineData("A --> B", UMLRelationshipType.Association, UMLRelationshipDirection.Forward)]
	[InlineData("A <- B", UMLRelationshipType.Association, UMLRelationshipDirection.Backward)]
	[InlineData("A <-> B", UMLRelationshipType.Association, UMLRelationshipDirection.Both)]
	[InlineData("A .. B", UMLRelationshipType.Dependency, UMLRelationshipDirection.None)]
	[InlineData("A ..> B", UMLRelationshipType.Dependency, UMLRelationshipDirection.Forward)]
	[InlineData("A <.. B", UMLRelationshipType.Dependency, UMLRelationshipDirection.Backward)]
	[InlineData("A --|> B", UMLRelationshipType.Generalization, UMLRelationshipDirection.Forward)]
	[InlineData("A <|-- B", UMLRelationshipType.Generalization, UMLRelationshipDirection.Backward)]
	[InlineData("A ..|> B", UMLRelationshipType.Realization, UMLRelationshipDirection.Forward)]
	[InlineData("A <|.. B", UMLRelationshipType.Realization, UMLRelationshipDirection.Backward)]
	[InlineData("A o-- B", UMLRelationshipType.Aggregation, UMLRelationshipDirection.Backward)]
	[InlineData("A --o B", UMLRelationshipType.Aggregation, UMLRelationshipDirection.Forward)]
	[InlineData("A *-- B", UMLRelationshipType.Composition, UMLRelationshipDirection.Backward)]
	[InlineData("A --* B", UMLRelationshipType.Composition, UMLRelationshipDirection.Forward)]
	public void ParsesEveryRelationshipOperator(
		string line,
		UMLRelationshipType expectedType,
		UMLRelationshipDirection expectedDirection
	)
	{
		UMLDiagram diagram = ParseOrFail($"class A\nclass B\n{line}");

		UMLRelationship relationship = Assert.Single(diagram.Relationships);
		Assert.Equal(expectedType, relationship.Type);
		Assert.Equal(expectedDirection, relationship.Direction);
		Assert.Same(diagram.Nodes[0], relationship.From);
		Assert.Same(diagram.Nodes[1], relationship.To);
		Assert.Equal(2, relationship.SourceLine);
	}

	[Theory]
	[InlineData("A > B")]
	[InlineData("A < B")]
	[InlineData("A >> B")]
	[InlineData("A << B")]
	[InlineData("A . B")]
	public void StillAcceptsTheOperatorsTheLanguageStartedWith(string line)
	{
		UMLDiagram diagram = ParseOrFail($"class A\nclass B\n{line}");

		Assert.Single(diagram.Relationships);
	}

	[Fact]
	public void ParsesMultiplicitiesAndLabels()
	{
		UMLDiagram diagram = ParseOrFail(
			"class Order\nclass Item\nOrder \"1\" *-- \"0..*\" Item : contains"
		);

		UMLRelationship relationship = Assert.Single(diagram.Relationships);
		Assert.Equal("1", relationship.FromMultiplicity);
		Assert.Equal("0..*", relationship.ToMultiplicity);
		Assert.Equal("contains", relationship.Label);
		Assert.Equal(UMLRelationshipType.Composition, relationship.Type);
	}

	[Fact]
	public void StripsCommentsBeforeParsing()
	{
		UMLDiagram diagram = ParseOrFail(
			"class A // the first one\n\tposition: [1, 2] // moved\n// stand-alone\nclass B"
		);

		Assert.Equal(2, diagram.Nodes.Count);
		Assert.Equal(new Vector2(1.0f, 2.0f), diagram.Nodes[0].Position);
	}

	[Theory]
	[InlineData("class A\nclass A", "Duplicate node: A")]
	[InlineData("thing A", "Unknown node type: thing")]
	[InlineData("class A\nA -> B", "Unknown node: B")]
	[InlineData("class A\n\tposition: [1, 2]\n\tposition: [3, 4]", "Duplicate property: position")]
	[InlineData("node N\n\tcolour: red", "Unknown property: colour")]
	[InlineData("class A\n\tposition: 1, 2", "Invalid position format")]
	[InlineData("class A\n\t\tposition: [1, 2]", "Unexpected indentation")]
	[InlineData("\tposition: [1, 2]", "Unexpected indentation")]
	[InlineData("node N\n\t+ field: Integer", "A 'node' cannot have attributes")]
	[InlineData("actor A\n\trun()", "A 'actor' cannot have methods")]
	[InlineData("class A\n\t???", "Invalid property or member syntax")]
	[InlineData("!!!", "Syntax error")]
	public void ReportsTheFirstError(string code, string expectedMessage)
	{
		Assert.Equal(expectedMessage, ParseExpectingFailure(code).ErrorMessage);
	}

	[Fact]
	public void ReportsTheLineTheErrorIsOn()
	{
		UMLParseResult result = ParseExpectingFailure("class A\n\nclass B\n\nclass A");

		Assert.Equal(4, result.ErrorLineNumber);
		Assert.Null(result.Diagram);
	}

	[Fact]
	public void ParsesACompleteClassDiagram()
	{
		UMLDiagram diagram = ParseOrFail(
			string.Join(
				"\n",
				"abstract Shape",
				"\tposition: [10, 10]",
				"\t# name: String",
				"\t+ area(): Decimal",
				"",
				"class Circle",
				"\tposition: [10, 200]",
				"\t- radius: Decimal",
				"",
				"interface Drawable",
				"\t+ draw(canvas: Canvas): void",
				"",
				"Circle --|> Shape",
				"Circle ..|> Drawable",
				"Shape \"1\" o-- \"*\" Circle : groups"
			)
		);

		Assert.Equal(3, diagram.Nodes.Count);
		Assert.Equal(3, diagram.Relationships.Count);
		Assert.Equal(UMLNodeType.AbstractClass, diagram.Nodes[0].Type);
		Assert.Equal(UMLRelationshipType.Generalization, diagram.Relationships[0].Type);
		Assert.Equal(UMLRelationshipType.Realization, diagram.Relationships[1].Type);
		Assert.Equal(UMLRelationshipType.Aggregation, diagram.Relationships[2].Type);
	}
}
