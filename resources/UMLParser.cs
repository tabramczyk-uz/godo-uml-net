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
			return ParseProperty(content);
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
			return AddRelationship(relationshipMatch.Groups[1].Value, relationshipMatch.Groups[3].Value);
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

		currentNode = nodeType == UMLNodeType.Class ? new UMLClass(nodeName) : new UMLNode(nodeName);
		diagram.Nodes.Add(currentNode);
		nodesByName.Add(nodeName, currentNode);
		return true;
	}

	private bool AddRelationship(string fromNodeName, string toNodeName)
	{
		if (!nodesByName.TryGetValue(fromNodeName, out UMLNode fromNode))
		{
			return Fail($"Unknown node: {fromNodeName}");
		}

		if (!nodesByName.TryGetValue(toNodeName, out UMLNode toNode))
		{
			return Fail($"Unknown node: {toNodeName}");
		}

		diagram.Relationships.Add(new UMLRelationship(fromNode, toNode));
		return true;
	}

	private bool ParseProperty(string content)
	{
		Match propertyMatch = UMLSyntax.PropertyRegex().Match(content);
		if (!propertyMatch.Success)
		{
			return Fail("Invalid property syntax");
		}

		string propertyName = propertyMatch.Groups[1].Value;
		if (!UMLSyntax.TryGetNodeProperty(propertyName, out UMLNodeProperty property))
		{
			return Fail($"Unknown property: {propertyName}");
		}

		if (!currentNodeProperties.Add(property))
		{
			return Fail($"Duplicate property: {propertyName}");
		}

		string propertyValue = propertyMatch.Groups[2].Value;
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

	private bool Fail(string message)
	{
		errorMessage = message;
		return false;
	}
}
