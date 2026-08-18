using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Godot;

/// <summary>
/// Applies edits made in the visual editor back to the UML source code,
/// touching as little of it as possible.
/// </summary>
public static class UMLCodeWriter
{
	private const string CoordinateFormat = "0.###";

	/// <summary>
	/// Renames a node in its declaration and in every relationship that
	/// references it. Comments, indentation and spacing are left untouched, as
	/// are identifiers that merely contain the old name.
	/// </summary>
	public static string RenameNode(string code, UMLNode node, string newName)
	{
		string[] lines = code.Split('\n');

		for (int i = 0; i < lines.Length; i++)
		{
			UMLSyntax.SplitComment(lines[i], out string codePart, out string comment);
			string content = codePart.TrimEnd();

			if (content.Length == 0 || UMLSyntax.GetIndentation(content) != 0)
			{
				continue;
			}

			Match declarationMatch = UMLSyntax.NodeRegex().Match(content);
			if (declarationMatch.Success)
			{
				if (declarationMatch.Groups[2].Value == node.Name)
				{
					lines[i] = ReplaceGroup(codePart, declarationMatch.Groups[2], newName) + comment;
				}

				continue;
			}

			Match relationshipMatch = UMLSyntax.RelationshipRegex().Match(content);
			if (!relationshipMatch.Success)
			{
				continue;
			}

			// The right-hand side is replaced first so the left-hand side's index
			// stays valid.
			string renamedLine = codePart;
			if (relationshipMatch.Groups[3].Value == node.Name)
			{
				renamedLine = ReplaceGroup(renamedLine, relationshipMatch.Groups[3], newName);
			}

			if (relationshipMatch.Groups[1].Value == node.Name)
			{
				renamedLine = ReplaceGroup(renamedLine, relationshipMatch.Groups[1], newName);
			}

			lines[i] = renamedLine + comment;
		}

		return string.Join("\n", lines);
	}

	/// <summary>
	/// Rewrites the node's position property, inserting it right below the
	/// declaration if it is not there yet.
	/// </summary>
	public static string SetNodePosition(string code, UMLNode node, Vector2 newPosition)
	{
		string[] lines = code.Split('\n');

		int declarationLineNumber = FindDeclarationLine(lines, node);
		if (declarationLineNumber == -1)
		{
			GD.PushError($"Node declaration not found for node: {node.Name}");
			return code;
		}

		string positionKeyword = UMLSyntax.GetKeyword(UMLNodeProperty.Position);
		string positionLine =
			$"\t{positionKeyword}: [{FormatCoordinate(newPosition.X)}, {FormatCoordinate(newPosition.Y)}]";

		for (int i = declarationLineNumber + 1; i < lines.Length; i++)
		{
			UMLSyntax.SplitComment(lines[i], out string codePart, out string comment);
			string content = codePart.TrimEnd();

			if (content.Length == 0)
			{
				continue;
			}

			if (UMLSyntax.GetIndentation(content) != 1)
			{
				break;
			}

			Match propertyMatch = UMLSyntax.PropertyRegex().Match(content.Substring(1));
			if (propertyMatch.Success && propertyMatch.Groups[1].Value == positionKeyword)
			{
				string separator = codePart.Substring(content.Length);
				lines[i] = comment.Length == 0 ? positionLine : positionLine + separator + comment;
				return string.Join("\n", lines);
			}
		}

		var updatedLines = new List<string>(lines);
		updatedLines.Insert(declarationLineNumber + 1, positionLine);
		return string.Join("\n", updatedLines);
	}

	private static int FindDeclarationLine(string[] lines, UMLNode node)
	{
		for (int i = 0; i < lines.Length; i++)
		{
			string content = UMLSyntax.StripComment(lines[i]);
			if (content.Length == 0 || UMLSyntax.GetIndentation(content) != 0)
			{
				continue;
			}

			Match declarationMatch = UMLSyntax.NodeRegex().Match(content);
			if (declarationMatch.Success && declarationMatch.Groups[2].Value == node.Name)
			{
				return i;
			}
		}

		return -1;
	}

	private static string ReplaceGroup(string line, Group group, string replacement)
	{
		return line.Substring(0, group.Index)
			+ replacement
			+ line.Substring(group.Index + group.Length);
	}

	private static string FormatCoordinate(float value)
	{
		return value.ToString(CoordinateFormat, CultureInfo.InvariantCulture);
	}
}
