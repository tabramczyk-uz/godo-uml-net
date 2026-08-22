using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Godot;

/// <summary>
/// Turns UML source code into a <see cref="UMLDiagram"/>. Parsing stops at the
/// first error. One instance holds the state of one parse; use the static <see
/// cref="Parse"/> entry point.
/// </summary>
public sealed class UMLParser
{
	private readonly UMLDiagram diagram = new();
	private readonly Dictionary<string, UMLNode> nodesByName = [];
	private readonly HashSet<UMLNodeProperty> currentNodeProperties = [];

	private UMLNode currentNode;
	private int lineNumber;
	private string errorMessage;

	private UMLParser() { }

	public static UMLParseResult Parse(string code)
	{
		return new UMLParser().Run(code);
	}

	private UMLParseResult Run(string code)
	{
		string[] lines = code.Split('\n');
		for (lineNumber = 0; lineNumber < lines.Length; lineNumber++)
		{
			if (!ParseLine(UMLSyntax.StripComment(lines[lineNumber])))
			{
				return UMLParseResult.Failure(errorMessage, lineNumber);
			}
		}

		return UMLParseResult.Success(diagram);
	}

	private bool ParseLine(string line)
	{
		if (string.IsNullOrWhiteSpace(line))
		{
			return true;
		}

		int indentation = UMLSyntax.GetIndentation(line);
		string content = line[indentation..];

		if (indentation == 0)
		{
			currentNode = null;
			currentNodeProperties.Clear();
			return ParseDeclaration(content);
		}

		if (indentation == 1 && currentNode != null)
		{
			return ParseNodeBody(content);
		}

		return Fail("Unexpected indentation");
	}

	private bool ParseDeclaration(string content)
	{
		Match nodeMatch = UMLSyntax.NodeRegex().Match(content);
		if (nodeMatch.Success)
		{
			return AddNode(nodeMatch.Groups[1].Value, nodeMatch.Groups[2].Value);
		}

		Match relationshipMatch = UMLSyntax.RelationshipRegex().Match(content);
		if (relationshipMatch.Success)
		{
			return AddRelationship(relationshipMatch);
		}

		return Fail("Syntax error");
	}

	private bool AddNode(string typeKeyword, string nodeName)
	{
		if (nodesByName.ContainsKey(nodeName))
		{
			return Fail($"Duplicate node: {nodeName}");
		}

		if (!UMLSyntax.TryGetNodeType(typeKeyword, out UMLNodeType nodeType))
		{
			return Fail($"Unknown node type: {typeKeyword}");
		}

		currentNode = UMLNode.Create(nodeType, nodeName);
		currentNode.SourceLine = lineNumber;
		diagram.Nodes.Add(currentNode);
		nodesByName.Add(nodeName, currentNode);
		return true;
	}

	private bool AddRelationship(Match match)
	{
		string fromNodeName = match.Groups[1].Value;
		string toNodeName = match.Groups[5].Value;

		if (!nodesByName.TryGetValue(fromNodeName, out UMLNode fromNode))
		{
			return Fail($"Unknown node: {fromNodeName}");
		}

		if (!nodesByName.TryGetValue(toNodeName, out UMLNode toNode))
		{
			return Fail($"Unknown node: {toNodeName}");
		}

		if (
			!UMLSyntax.TryGetRelationship(
				match.Groups[3].Value,
				out UMLRelationshipType type,
				out UMLRelationshipDirection direction
			)
		)
		{
			return Fail($"Unknown relationship operator: {match.Groups[3].Value}");
		}

		diagram.Relationships.Add(
			new UMLRelationship(fromNode, toNode, type, direction)
			{
				FromMultiplicity = match.Groups[2].Value,
				ToMultiplicity = match.Groups[4].Value,
				Label = match.Groups[6].Value,
				SourceLine = lineNumber,
			}
		);
		return true;
	}

	/// <summary>
	/// Parses one indented line below a declaration. An operation is recognised by
	/// its parentheses and an attribute by its visibility, which keeps both apart
	/// from a property such as <c>position:</c>.
	/// </summary>
	private bool ParseNodeBody(string content)
	{
		Match methodMatch = UMLSyntax.MethodRegex().Match(content);
		if (methodMatch.Success)
		{
			return AddMethod(methodMatch);
		}

		Match attributeMatch = UMLSyntax.AttributeRegex().Match(content);
		Match propertyMatch = UMLSyntax.PropertyRegex().Match(content);

		if (attributeMatch.Success && IsAttributeLine(attributeMatch, propertyMatch))
		{
			return AddAttribute(attributeMatch);
		}

		if (propertyMatch.Success)
		{
			return ParseProperty(propertyMatch.Groups[1].Value, propertyMatch.Groups[2].Value);
		}

		return Fail("Invalid property or member syntax");
	}

	/// <summary>
	/// An attribute written as <c>name: Type</c>, without a visibility symbol, is
	/// spelled exactly like a property. A visibility symbol settles it, and so
	/// does a bare name, since a property always carries a value. What is left is
	/// decided by the node: a property keyword always wins, and a node with no
	/// compartments to put an attribute in has nothing else the line could be.
	/// </summary>
	private bool IsAttributeLine(Match attributeMatch, Match propertyMatch)
	{
		if (attributeMatch.Groups[1].Value.Length > 0 || !propertyMatch.Success)
		{
			return true;
		}

		return currentNode is UMLClass
			&& !UMLSyntax.TryGetNodeProperty(attributeMatch.Groups[2].Value, out _);
	}

	private bool AddAttribute(Match match)
	{
		if (currentNode is not UMLClass currentClass)
		{
			return Fail($"{DescribeCurrentNodeType()} cannot have attributes");
		}

		currentClass.Attributes.Add(
			new UMLAttribute(
				match.Groups[2].Value,
				match.Groups[3].Value,
				UMLNotation.GetVisibility(match.Groups[1].Value)
			)
		);
		return true;
	}

	private bool AddMethod(Match match)
	{
		if (currentNode is not UMLClass currentClass)
		{
			return Fail($"{DescribeCurrentNodeType()} cannot have methods");
		}

		if (!TryParseArguments(match.Groups[3].Value, out List<UMLMethodArgument> arguments))
		{
			return false;
		}

		currentClass.Methods.Add(
			new UMLMethod(
				match.Groups[2].Value,
				match.Groups[4].Value,
				UMLNotation.GetVisibility(match.Groups[1].Value),
				arguments
			)
		);
		return true;
	}

	private bool TryParseArguments(string argumentList, out List<UMLMethodArgument> arguments)
	{
		arguments = [];
		if (string.IsNullOrWhiteSpace(argumentList))
		{
			return true;
		}

		foreach (string argument in SplitArguments(argumentList))
		{
			Match argumentMatch = UMLSyntax.ArgumentRegex().Match(argument.Trim());
			if (!argumentMatch.Success)
			{
				return Fail($"Invalid argument: {argument.Trim()}");
			}

			arguments.Add(
				new UMLMethodArgument(argumentMatch.Groups[1].Value, argumentMatch.Groups[2].Value)
			);
		}

		return true;
	}

	/// <summary>
	/// Splits an argument list on its commas, skipping the ones nested inside a
	/// generic or array type such as <c>Dictionary&lt;String, Integer&gt;</c>.
	/// </summary>
	private static List<string> SplitArguments(string argumentList)
	{
		List<string> arguments = [];
		int depth = 0;
		int start = 0;

		for (int i = 0; i < argumentList.Length; i++)
		{
			switch (argumentList[i])
			{
				case '<' or '[':
					depth += 1;
					break;
				case '>' or ']':
					depth -= 1;
					break;
				case ',' when depth == 0:
					arguments.Add(argumentList[start..i]);
					start = i + 1;
					break;
			}
		}

		arguments.Add(argumentList[start..]);
		return arguments;
	}

	private bool ParseProperty(string propertyName, string propertyValue)
	{
		if (!UMLSyntax.TryGetNodeProperty(propertyName, out UMLNodeProperty property))
		{
			return Fail($"Unknown property: {propertyName}");
		}

		if (!currentNodeProperties.Add(property))
		{
			return Fail($"Duplicate property: {propertyName}");
		}

		return property switch
		{
			UMLNodeProperty.Position => ParsePosition(propertyValue),
			_ => Fail($"Unhandled property: {propertyName}"),
		};
	}

	private bool ParsePosition(string propertyValue)
	{
		Match positionMatch = UMLSyntax.PositionRegex().Match(propertyValue);
		if (
			!positionMatch.Success
			|| !float.TryParse(
				positionMatch.Groups[1].Value,
				NumberStyles.Float,
				CultureInfo.InvariantCulture,
				out float x
			)
			|| !float.TryParse(
				positionMatch.Groups[2].Value,
				NumberStyles.Float,
				CultureInfo.InvariantCulture,
				out float y
			)
		)
		{
			return Fail("Invalid position format");
		}

		currentNode.Position = new Vector2(x, y);
		return true;
	}

	private string DescribeCurrentNodeType()
	{
		return $"A '{UMLSyntax.GetKeyword(currentNode.Type)}'";
	}

	private bool Fail(string message)
	{
		errorMessage = message;
		return false;
	}
}
