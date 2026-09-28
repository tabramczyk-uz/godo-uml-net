using System.Collections.Generic;
using System.Text.RegularExpressions;
using Godot;

/// <summary>
/// Applies edits made in the visual editor back to the UML source code,
/// touching as little of it as possible. Every method is a pure function of the
/// code it is handed, and returns it unchanged when the edit does not apply.
/// </summary>
public static class UMLCodeWriter
{
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
			if (relationshipMatch.Groups[5].Value == node.Name)
			{
				renamedLine = ReplaceGroup(renamedLine, relationshipMatch.Groups[5], newName);
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
			return code;
		}

		string positionLine =
			$"{UMLSyntax.Indent}{UMLSyntax.GetKeyword(UMLNodeProperty.Position)}: "
			+ UMLSyntax.FormatPosition(newPosition);

		int i = FindPositionLine(lines, declarationLineNumber);
		if (i != -1)
		{
			UMLSyntax.SplitComment(lines[i], out string codePart, out string comment);
			string separator = codePart[codePart.TrimEnd().Length..];
			lines[i] = comment.Length == 0 ? positionLine : positionLine + separator + comment;
			return string.Join("\n", lines);
		}

		var updatedLines = new List<string>(lines);
		updatedLines.Insert(declarationLineNumber + 1, positionLine);
		return string.Join("\n", updatedLines);
	}

	/// <summary>
	/// Deletes the position lines of the <paramref name="nodes"/>, so the
	/// layout places them again, all in one edit. Nodes without a position
	/// line are left as they are, and so is every other line, comments on the
	/// deleted lines aside.
	/// </summary>
	public static string RemoveNodePositions(string code, IEnumerable<UMLNode> nodes)
	{
		string[] lines = code.Split('\n');
		var removed = new HashSet<int>();

		foreach (UMLNode node in nodes)
		{
			int declarationLineNumber = FindDeclarationLine(lines, node);
			if (declarationLineNumber == -1)
			{
				continue;
			}

			int positionLineNumber = FindPositionLine(lines, declarationLineNumber);
			if (positionLineNumber != -1)
			{
				removed.Add(positionLineNumber);
			}
		}

		return removed.Count == 0 ? code : JoinExcept(lines, removed);
	}

	/// <summary>
	/// The line of the position property belonging to the declaration on
	/// <paramref name="declarationLineNumber"/>, or -1 when it has none.
	/// </summary>
	private static int FindPositionLine(string[] lines, int declarationLineNumber)
	{
		string positionKeyword = UMLSyntax.GetKeyword(UMLNodeProperty.Position);
		foreach (int i in GetBodyLines(lines, declarationLineNumber))
		{
			string content = UMLSyntax.StripComment(lines[i]);
			Match propertyMatch = UMLSyntax.PropertyRegex().Match(content[1..]);
			if (propertyMatch.Success && propertyMatch.Groups[1].Value == positionKeyword)
			{
				return i;
			}
		}

		return -1;
	}

	/// <summary>
	/// Rewrites the positions of several nodes at once, as when a selection is
	/// dragged, so the code is changed, and parsed, a single time. Inserting a
	/// position line moves the lines below it, but each node is found again by
	/// name, so the ones after still land on their own declarations.
	/// </summary>
	public static string SetNodePositions(string code, IReadOnlyDictionary<UMLNode, Vector2> positions)
	{
		foreach ((UMLNode node, Vector2 position) in positions)
		{
			code = SetNodePosition(code, node, position);
		}

		return code;
	}

	/// <summary>
	/// Appends a declaration, and the position that goes with it, to the end of
	/// the code. New nodes go last so they can never be used before they are
	/// declared.
	/// </summary>
	public static string AddNode(string code, UMLNodeType type, string name, Vector2 position)
	{
		string declaration = $"{UMLSyntax.GetKeyword(type)} {name}";
		string positionLine =
			$"{UMLSyntax.Indent}{UMLSyntax.GetKeyword(UMLNodeProperty.Position)}: "
			+ UMLSyntax.FormatPosition(position);
		return Append(code, declaration + "\n" + positionLine);
	}

	/// <summary>
	/// Removes several nodes at once, as when a selection is deleted, so the
	/// code is changed, and parsed, a single time. Each node is found again by
	/// name, so removing one does not throw off where the next one is.
	/// </summary>
	public static string RemoveNodes(string code, IEnumerable<UMLNode> nodes)
	{
		foreach (UMLNode node in nodes)
		{
			code = RemoveNode(code, node);
		}

		return code;
	}

	/// <summary>
	/// Removes a node's declaration, the indented lines that belong to it, and
	/// every relationship that would be left dangling.
	/// </summary>
	public static string RemoveNode(string code, UMLNode node)
	{
		string[] lines = code.Split('\n');

		int declarationLineNumber = FindDeclarationLine(lines, node);
		if (declarationLineNumber == -1)
		{
			return code;
		}

		var removed = new HashSet<int> { declarationLineNumber };
		foreach (int i in GetBodyLines(lines, declarationLineNumber))
		{
			removed.Add(i);
		}

		for (int i = 0; i < lines.Length; i++)
		{
			if (!TryMatchRelationship(lines[i], out Match match))
			{
				continue;
			}

			if (match.Groups[1].Value == node.Name || match.Groups[5].Value == node.Name)
			{
				removed.Add(i);
			}
		}

		return JoinExcept(lines, removed);
	}

	/// <summary>
	/// Appends a relationship line to the end of the code.
	/// </summary>
	public static string AddRelationship(
		string code,
		UMLNode from,
		UMLNode to,
		UMLRelationshipType type,
		UMLRelationshipDirection direction
	)
	{
		string relationshipOperator = UMLSyntax.GetOperator(type, direction);
		return Append(code, $"{from.Name} {relationshipOperator} {to.Name}");
	}

	/// <summary>
	/// Removes the line the relationship was written on.
	/// </summary>
	public static string RemoveRelationship(string code, UMLRelationship relationship)
	{
		string[] lines = code.Split('\n');
		int lineNumber = FindRelationshipLine(lines, relationship);
		return lineNumber == -1 ? code : JoinExcept(lines, [lineNumber]);
	}

	/// <summary>
	/// Swaps the operator of an existing relationship line, leaving its node
	/// names, multiplicities, label and comment where they are.
	/// </summary>
	public static string SetRelationshipType(
		string code,
		UMLRelationship relationship,
		UMLRelationshipType type,
		UMLRelationshipDirection direction
	)
	{
		string[] lines = code.Split('\n');
		int lineNumber = FindRelationshipLine(lines, relationship);
		if (lineNumber == -1)
		{
			return code;
		}

		UMLSyntax.SplitComment(lines[lineNumber], out string codePart, out string comment);
		Match match = UMLSyntax.RelationshipRegex().Match(codePart.TrimEnd());
		lines[lineNumber] =
			ReplaceGroup(codePart, match.Groups[3], UMLSyntax.GetOperator(type, direction)) + comment;
		return string.Join("\n", lines);
	}

	/// <summary>
	/// Line numbers of the indented lines that belong to the declaration on
	/// <paramref name="declarationLineNumber"/>, blank lines included.
	/// </summary>
	private static IEnumerable<int> GetBodyLines(string[] lines, int declarationLineNumber)
	{
		for (int i = declarationLineNumber + 1; i < lines.Length; i++)
		{
			string content = UMLSyntax.StripComment(lines[i]);
			if (content.Length == 0)
			{
				continue;
			}

			if (UMLSyntax.GetIndentation(content) == 0)
			{
				yield break;
			}

			yield return i;
		}
	}

	private static int FindDeclarationLine(string[] lines, UMLNode node)
	{
		if (IsDeclarationOf(lines, node.SourceLine, node.Name))
		{
			return node.SourceLine;
		}

		for (int i = 0; i < lines.Length; i++)
		{
			if (IsDeclarationOf(lines, i, node.Name))
			{
				return i;
			}
		}

		return -1;
	}

	private static bool IsDeclarationOf(string[] lines, int lineNumber, string nodeName)
	{
		if (lineNumber < 0 || lineNumber >= lines.Length)
		{
			return false;
		}

		string content = UMLSyntax.StripComment(lines[lineNumber]);
		if (content.Length == 0 || UMLSyntax.GetIndentation(content) != 0)
		{
			return false;
		}

		Match match = UMLSyntax.NodeRegex().Match(content);
		return match.Success && match.Groups[2].Value == nodeName;
	}

	private static int FindRelationshipLine(string[] lines, UMLRelationship relationship)
	{
		if (IsRelationshipBetween(lines, relationship.SourceLine, relationship))
		{
			return relationship.SourceLine;
		}

		for (int i = 0; i < lines.Length; i++)
		{
			if (IsRelationshipBetween(lines, i, relationship))
			{
				return i;
			}
		}

		return -1;
	}

	private static bool IsRelationshipBetween(
		string[] lines,
		int lineNumber,
		UMLRelationship relationship
	)
	{
		if (lineNumber < 0 || lineNumber >= lines.Length)
		{
			return false;
		}

		return TryMatchRelationship(lines[lineNumber], out Match match)
			&& match.Groups[1].Value == relationship.From.Name
			&& match.Groups[5].Value == relationship.To.Name;
	}

	private static bool TryMatchRelationship(string line, out Match match)
	{
		match = null;
		string content = UMLSyntax.StripComment(line);
		if (content.Length == 0 || UMLSyntax.GetIndentation(content) != 0)
		{
			return false;
		}

		if (UMLSyntax.NodeRegex().IsMatch(content))
		{
			return false;
		}

		match = UMLSyntax.RelationshipRegex().Match(content);
		return match.Success;
	}

	/// <summary>
	/// Appends a block of declarations and relationships, such as pasted nodes,
	/// to the end of the code, set off from what comes before by a blank line.
	/// Going last, its relationships can only name nodes declared before them.
	/// </summary>
	public static string AppendBlock(string code, string block)
	{
		string separator = code.Trim().Length == 0 ? string.Empty : "\n";
		return Append(code, separator + block.TrimEnd('\n'));
	}

	private static string Append(string code, string addition)
	{
		string separator = code.Length == 0 || code.EndsWith('\n') ? string.Empty : "\n";
		return code + separator + addition + "\n";
	}

	private static string JoinExcept(string[] lines, HashSet<int> removed)
	{
		var kept = new List<string>(lines.Length - removed.Count);
		for (int i = 0; i < lines.Length; i++)
		{
			if (!removed.Contains(i))
			{
				kept.Add(lines[i]);
			}
		}

		return string.Join("\n", kept);
	}

	private static string ReplaceGroup(string line, Group group, string replacement)
	{
		return line[..group.Index] + replacement + line[(group.Index + group.Length)..];
	}
}
