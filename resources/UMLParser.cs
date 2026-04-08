using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;

public class UMLParser
{
	public event Action<string, int> ErrorOccurred;

	public enum Visibility
	{
		Unknown,
		Public,
		Private,
		Package,
		Protected
	}

	public enum NodeType
	{
		Unknown,
		Node,
		Class
	}

	public enum NodeProperty
	{
		Unknown,
		Position
	}

	private const string CommentPrefix = "//";

	private static readonly Regex NodeRegex = new Regex(@"([a-zA-Z_][a-zA-Z0-9_]*)\s+([a-zA-Z_][a-zA-Z0-9_]*)", RegexOptions.Compiled);
	private static readonly Dictionary<NodeType, string> NodeTypeNames = new Dictionary<NodeType, string>
	{
		{ NodeType.Node, "node" },
		{ NodeType.Class, "class" }
	};

	private static readonly Regex PropertyRegex = new Regex(@"\t([a-zA-Z_][a-zA-Z0-9_]*):\s*(.+)", RegexOptions.Compiled);
	private static readonly Dictionary<NodeProperty, string> NodePropertyNames = new Dictionary<NodeProperty, string>
	{
		{ NodeProperty.Position, "position" }
	};

	private static readonly Regex RelationshipRegex = new Regex(@"([a-zA-Z_][a-zA-Z0-9_]*)\s*(->|<-|[\-\.]{1,2}|[<>]{1,2}|[oO]{1,2})\s*([a-zA-Z_][a-zA-Z0-9_]*)", RegexOptions.Compiled);
	private static readonly Regex PositionRegex = new Regex(@"\[\s*([\-+]?\d*\.?\d+)\s*,\s*([\-+]?\d*\.?\d+)\s*\]", RegexOptions.Compiled);

	private void RaiseError(string message, int lineNumber)
	{
		ErrorOccurred?.Invoke(message, lineNumber);
	}

	public UMLDiagram ParseCode(string code)
	{
		var diagram = new UMLDiagram();

		string[] lines = code.Split('\n');
		int lineNumber = -1;
		UMLNode currentNode = null;
		var currentNodeSetProperties = new HashSet<NodeProperty>();
		var takenNodeNames = new HashSet<string>();

		foreach (string rawLine in lines)
		{
			lineNumber += 1;
			string line = StripEndEdgesAndComments(rawLine);

			if (string.IsNullOrWhiteSpace(line))
				continue;

			if (currentNode != null)
			{
				int indentLevel = GetLineIndentation(line);
				if (indentLevel == 0)
				{
					currentNode = null;
					currentNodeSetProperties.Clear();
				}
				else if (indentLevel == 1)
				{
					Match propertyMatch = PropertyRegex.Match(line);
					if (propertyMatch.Success)
					{
						string propertyName = propertyMatch.Groups[1].Value;
						string propertyValue = propertyMatch.Groups[2].Value;

						NodeProperty property = GetPropertyTypeFromName(propertyName);
						if (property == NodeProperty.Unknown)
						{
							RaiseError($"Unknown property: {propertyName}", lineNumber);
							return null;
						}

						if (currentNodeSetProperties.Contains(property))
						{
							RaiseError($"Duplicate property: {propertyName}", lineNumber);
							return null;
						}

						currentNodeSetProperties.Add(property);

						switch (property)
						{
							case NodeProperty.Position:
								Match positionMatch = PositionRegex.Match(propertyValue);
								if (positionMatch.Success)
								{
									float x = float.Parse(positionMatch.Groups[1].Value, CultureInfo.InvariantCulture);
									float y = float.Parse(positionMatch.Groups[2].Value, CultureInfo.InvariantCulture);
									currentNode.Position = new Vector2(x, y);
									continue;
								}

								RaiseError("Invalid position format", lineNumber);
								return null;
						}
					}

					RaiseError("Invalid property syntax", lineNumber);
					return null;
				}
				else
				{
					RaiseError("Unexpected indentation", lineNumber);
					return null;
				}
			}

			Match nodeRegexMatch = NodeRegex.Match(line);
			if (nodeRegexMatch.Success)
			{
				string nodeTypeName = nodeRegexMatch.Groups[1].Value;
				string nodeName = nodeRegexMatch.Groups[2].Value;

				if (takenNodeNames.Contains(nodeName))
				{
					RaiseError($"Duplicate node: {nodeName}", lineNumber);
					return null;
				}

				NodeType nodeType = GetNodeTypeFromName(nodeTypeName);
				switch (nodeType)
				{
					case NodeType.Node:
						currentNode = new UMLNode(nodeName);
						break;
					case NodeType.Class:
						currentNode = new UMLClass(nodeName);
						break;
					default:
						RaiseError($"Unknown node type: {nodeTypeName}", lineNumber);
						return null;
				}

				diagram.Nodes.Add(currentNode);
				takenNodeNames.Add(nodeName);
				continue;
			}

			Match relationshipRegexMatch = RelationshipRegex.Match(line);
			if (relationshipRegexMatch.Success)
			{
				string fromNodeName = relationshipRegexMatch.Groups[1].Value;
				string toNodeName = relationshipRegexMatch.Groups[3].Value;

				UMLNode fromNode = GetNodeByName(diagram, fromNodeName);
				UMLNode toNode = GetNodeByName(diagram, toNodeName);

				if (fromNode == null)
				{
					RaiseError($"Unknown node: {fromNodeName}", lineNumber);
					return null;
				}

				if (toNode == null)
				{
					RaiseError($"Unknown node: {toNodeName}", lineNumber);
					return null;
				}

				diagram.Relationships.Add(new UMLRelationship(fromNode, toNode));
				continue;
			}

			RaiseError("Syntax error", lineNumber);
			return null;
		}

		return diagram;
	}

	public string ChangeNodeName(string code, UMLNode node, string newName)
	{
		string[] lines = code.Split('\n');

		for (int i = 0; i < lines.Length; i++)
		{
			string line = lines[i];
			int commentIndex = line.IndexOf(CommentPrefix, StringComparison.Ordinal);

			if (commentIndex >= 0)
			{
				string codePart = line.Substring(0, commentIndex).Replace(node.Name, newName);
				string commentPart = line.Substring(commentIndex);
				lines[i] = codePart + commentPart;
			}
			else
			{
				lines[i] = line.Replace(node.Name, newName);
			}
		}

		return string.Join("\n", lines);
	}

	public string ChangeNodePosition(string code, UMLNode node, Vector2 newPosition)
	{
		string propertyName = NodePropertyNames[NodeProperty.Position];
		string xPosition = ToStringWithoutTrailingZeroes(newPosition.X);
		string yPosition = ToStringWithoutTrailingZeroes(newPosition.Y);

		int declarationLineNumber = FindNodeDeclaration(code, node);
		if (declarationLineNumber == -1)
		{
			RaiseError($"Node declaration not found for node: {node.Name}", -1);
			return code;
		}

		var lines = new List<string>(code.Split('\n'));
		for (int i = declarationLineNumber + 1; i < lines.Count; i++)
		{
			string line = lines[i];
			string strippedLine = StripEndEdgesAndComments(line);
			if (string.IsNullOrWhiteSpace(strippedLine))
			{
				continue;
			}

			if (GetLineIndentation(strippedLine) != 1)
			{
				break;
			}

			Match positionRegexMatch = PositionRegex.Match(strippedLine);
			if (positionRegexMatch.Success)
			{
				lines[i] = $"\t{propertyName}: [{xPosition}, {yPosition}]";
				return string.Join("\n", lines);
			}
		}

		lines.Insert(declarationLineNumber + 1, $"\t{propertyName}: [{xPosition}, {yPosition}]");
		return string.Join("\n", lines);
	}

	public int FindNodeDeclaration(string code, UMLNode node)
	{
		NodeType nodeType = GetNodeType(node);
		string nodeTypeName = NodeTypeNames[nodeType];
		var regex = new Regex($"{Regex.Escape(nodeTypeName)}\\s+{Regex.Escape(node.Name)}");

		string[] lines = code.Split('\n');
		for (int i = 0; i < lines.Length; i++)
		{
			string strippedLine = StripEndEdgesAndComments(lines[i]);
			if (string.IsNullOrWhiteSpace(strippedLine))
			{
				continue;
			}

			if (regex.IsMatch(strippedLine))
			{
				return i;
			}
		}

		return -1;
	}

	public int GetLineIndentation(string line)
	{
		int indent = 0;
		foreach (char character in line)
		{
			if (character == '\t')
			{
				indent += 1;
			}
			else
			{
				break;
			}
		}

		return indent;
	}

	public UMLNode GetNodeByName(UMLDiagram diagram, string nodeName)
	{
		foreach (UMLNode node in diagram.Nodes)
		{
			if (node.Name == nodeName)
			{
				return node;
			}
		}

		return null;
	}

	public NodeType GetNodeType(UMLNode node)
	{
		return node is UMLClass ? NodeType.Class : NodeType.Node;
	}

	public NodeType GetNodeTypeFromName(string typeName)
	{
		foreach (KeyValuePair<NodeType, string> pair in NodeTypeNames)
		{
			if (pair.Value == typeName)
			{
				return pair.Key;
			}
		}

		return NodeType.Unknown;
	}

	public NodeProperty GetPropertyTypeFromName(string propertyName)
	{
		foreach (KeyValuePair<NodeProperty, string> pair in NodePropertyNames)
		{
			if (pair.Value == propertyName)
			{
				return pair.Key;
			}
		}

		return NodeProperty.Unknown;
	}

	public string StripEndEdgesAndComments(string line)
	{
		int commentIndex = line.IndexOf(CommentPrefix, StringComparison.Ordinal);
		if (commentIndex >= 0)
		{
			line = line.Substring(0, commentIndex);
		}

		return line.TrimEnd();
	}

	public string ToStringWithoutTrailingZeroes(float value)
	{
		string strValue = value.ToString(CultureInfo.InvariantCulture);
		if (strValue.Contains("."))
		{
			strValue = strValue.TrimEnd('0').TrimEnd('.');
		}

		return strValue;
	}
}
