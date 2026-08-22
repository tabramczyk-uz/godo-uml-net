using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Godot;

/// <summary>
/// Reads PlantUML source into a <see cref="UMLDiagram"/>. It is a second, fully
/// independent front end: it shares the model with the GodoUML language and
/// nothing else.
///
/// The supported subset is class and use case diagrams — declarations with their
/// members, the relationship arrows, multiplicities and labels. Diagram-wide
/// directives such as <c>skinparam</c>, notes and layout hints are skipped, and
/// anything else is reported as a warning.
/// </summary>
public sealed partial class PlantUMLImporter
{
	private const string PositionHintPattern = "^'@position[ \\t]+([A-Za-z_][A-Za-z0-9_]*)[ \\t]+(.+)$";
	private const string GroupPattern =
		"^(?:package|namespace|together|box|partition|state|group|mainframe)\\b.*\\{[ \\t]*$";
	private const string NotePattern = "^(?:note|legend|caption)\\b";
	private const string NoteEndPattern = "^(?:end[ \\t]*note|end[ \\t]*legend|end[ \\t]*caption)\\b";
	private const string TypedMemberPattern =
		"^(?<visibility>[+\\-#~])?[ \\t]*(?<type>[A-Za-z_][A-Za-z0-9_.<>\\[\\], ]*?)[ \\t]+"
		+ "(?<name>[A-Za-z_][A-Za-z0-9_]*)(?:[ \\t]*\\((?<arguments>[^)]*)\\))?[ \\t]*$";

	private readonly UMLDiagram diagram = new();
	private readonly Dictionary<string, UMLNode> nodesByPlantName = new(StringComparer.Ordinal);
	private readonly HashSet<string> usedIdentifiers = new(StringComparer.Ordinal);
	private readonly Dictionary<string, Vector2> positionHints = new(StringComparer.Ordinal);
	private readonly List<string> warnings = [];

	private UMLClass memberBlockOwner;
	private int groupDepth;
	private bool inBlockComment;
	private bool inNote;
	private int lineNumber;

	private PlantUMLImporter() { }

	public static PlantUMLImportResult Import(string source)
	{
		return new PlantUMLImporter().Run(source);
	}

	[GeneratedRegex(PositionHintPattern)]
	private static partial Regex PositionHintRegex();

	[GeneratedRegex(GroupPattern, RegexOptions.IgnoreCase)]
	private static partial Regex GroupRegex();

	[GeneratedRegex(NotePattern, RegexOptions.IgnoreCase)]
	private static partial Regex NoteRegex();

	[GeneratedRegex(NoteEndPattern, RegexOptions.IgnoreCase)]
	private static partial Regex NoteEndRegex();

	[GeneratedRegex(TypedMemberPattern)]
	private static partial Regex TypedMemberRegex();

	private PlantUMLImportResult Run(string source)
	{
		string[] lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
		for (lineNumber = 0; lineNumber < lines.Length; lineNumber++)
		{
			ReadLine(lines[lineNumber].Trim());
		}

		ApplyLayout();
		return new PlantUMLImportResult(diagram, warnings);
	}

	private void ReadLine(string line)
	{
		if (inBlockComment)
		{
			inBlockComment = !line.Contains(PlantUMLSyntax.BlockCommentEnd, StringComparison.Ordinal);
			return;
		}

		if (inNote)
		{
			inNote = !NoteEndRegex().IsMatch(line);
			return;
		}

		if (line.StartsWith(PlantUMLSyntax.BlockCommentStart, StringComparison.Ordinal))
		{
			inBlockComment = !line.Contains(PlantUMLSyntax.BlockCommentEnd, StringComparison.Ordinal);
			return;
		}

		if (line.StartsWith(PlantUMLSyntax.CommentPrefix, StringComparison.Ordinal))
		{
			ReadComment(line);
			return;
		}

		if (line.Length == 0)
		{
			return;
		}

		if (memberBlockOwner != null)
		{
			ReadMemberLine(line);
			return;
		}

		if (line.StartsWith('}'))
		{
			CloseGroup(line);
			return;
		}

		if (GroupRegex().IsMatch(line))
		{
			groupDepth += 1;
			return;
		}

		// A statement that describes the diagram is read before notes and
		// directives are skipped, so a node called Legend, Title or Note is never
		// mistaken for one.
		if (ReadDeclaration(line) || ReadRelationship(line))
		{
			return;
		}

		if (NoteRegex().IsMatch(line))
		{
			// A note is either written inline after a colon, or spread over lines
			// until its end marker.
			inNote = !line.Contains(':', StringComparison.Ordinal);
			return;
		}

		if (PlantUMLSyntax.IgnoredRegex().IsMatch(line))
		{
			return;
		}

		Warn(line, "unsupported statement");
	}

	private void ReadComment(string line)
	{
		Match hint = PositionHintRegex().Match(line);
		if (!hint.Success)
		{
			return;
		}

		Match position = UMLSyntax.PositionRegex().Match(hint.Groups[2].Value.Trim());
		if (
			position.Success
			&& float.TryParse(
				position.Groups[1].Value,
				System.Globalization.NumberStyles.Float,
				System.Globalization.CultureInfo.InvariantCulture,
				out float x
			)
			&& float.TryParse(
				position.Groups[2].Value,
				System.Globalization.NumberStyles.Float,
				System.Globalization.CultureInfo.InvariantCulture,
				out float y
			)
		)
		{
			positionHints[hint.Groups[1].Value] = new Vector2(x, y);
		}
	}

	private void CloseGroup(string line)
	{
		if (groupDepth > 0)
		{
			groupDepth -= 1;
			return;
		}

		Warn(line, "unmatched closing brace");
	}

	private bool ReadDeclaration(string line)
	{
		Match match = PlantUMLSyntax.DeclarationRegex().Match(line);
		if (!match.Success)
		{
			return false;
		}

		if (!PlantUMLSyntax.TryGetNodeType(match.Groups["keyword"].Value, out UMLNodeType nodeType))
		{
			return false;
		}

		string displayName = PlantUMLSyntax.UnwrapName(match.Groups["name"].Value, out _);
		string alias = match.Groups["alias"].Success
			? PlantUMLSyntax.UnwrapName(match.Groups["alias"].Value, out _)
			: null;

		UMLNode node = GetOrCreateNode(alias ?? displayName, nodeType, alias == null ? null : displayName);

		if (match.Groups["open"].Success)
		{
			// Only a classifier has compartments; every other keyword opens a
			// container whose contents are read as ordinary statements.
			if (node is UMLClass classifier && nodeType.IsClassifier())
			{
				memberBlockOwner = classifier;
			}
			else
			{
				groupDepth += 1;
			}
		}

		return true;
	}

	private void ReadMemberLine(string line)
	{
		if (line.StartsWith('}'))
		{
			memberBlockOwner = null;
			return;
		}

		string member = PlantUMLSyntax.ModifierRegex().Replace(line, "").Trim();
		if (member.Length == 0 || PlantUMLSyntax.SeparatorRegex().IsMatch(member))
		{
			return;
		}

		if (ReadMember(member) || ReadTypedMember(member))
		{
			return;
		}

		Warn(line, "unsupported member");
	}

	/// <summary>Reads the UML spelling of a member: <c>+ name : Type</c>.</summary>
	private bool ReadMember(string member)
	{
		Match match = PlantUMLSyntax.MemberRegex().Match(member);
		if (!match.Success)
		{
			return false;
		}

		UMLVisibility visibility = UMLNotation.GetVisibility(match.Groups["visibility"].Value);
		string name = match.Groups["name"].Value;
		string type = CleanType(match.Groups["type"].Value);

		if (match.Groups["arguments"].Success)
		{
			memberBlockOwner.Methods.Add(
				new UMLMethod(name, type, visibility, ReadArguments(match.Groups["arguments"].Value))
			);
		}
		else
		{
			memberBlockOwner.Attributes.Add(new UMLAttribute(name, type, visibility));
		}

		return true;
	}

	/// <summary>Reads the programming-language spelling: <c>+ Type name</c>.</summary>
	private bool ReadTypedMember(string member)
	{
		Match match = TypedMemberRegex().Match(member);
		if (!match.Success)
		{
			return false;
		}

		UMLVisibility visibility = UMLNotation.GetVisibility(match.Groups["visibility"].Value);
		string name = match.Groups["name"].Value;
		string type = CleanType(match.Groups["type"].Value);

		if (match.Groups["arguments"].Success)
		{
			memberBlockOwner.Methods.Add(
				new UMLMethod(name, type, visibility, ReadArguments(match.Groups["arguments"].Value))
			);
		}
		else
		{
			memberBlockOwner.Attributes.Add(new UMLAttribute(name, type, visibility));
		}

		return true;
	}

	private static List<UMLMethodArgument> ReadArguments(string argumentList)
	{
		List<UMLMethodArgument> arguments = [];
		if (string.IsNullOrWhiteSpace(argumentList))
		{
			return arguments;
		}

		foreach (string argument in argumentList.Split(','))
		{
			string trimmed = argument.Trim();
			if (trimmed.Length == 0)
			{
				continue;
			}

			Match match = PlantUMLSyntax.ArgumentRegex().Match(trimmed);
			if (match.Success)
			{
				arguments.Add(
					new UMLMethodArgument(match.Groups["name"].Value, CleanType(match.Groups["type"].Value))
				);
				continue;
			}

			// "Type name", the other order PlantUML accepts.
			int separator = trimmed.LastIndexOf(' ');
			arguments.Add(
				separator > 0
					? new UMLMethodArgument(
						PlantUMLSyntax.ToIdentifier(trimmed[(separator + 1)..]),
						CleanType(trimmed[..separator])
					)
					: new UMLMethodArgument(PlantUMLSyntax.ToIdentifier(trimmed))
			);
		}

		return arguments;
	}

	private bool ReadRelationship(string line)
	{
		Match match = PlantUMLSyntax.RelationshipRegex().Match(line);
		if (!match.Success)
		{
			return false;
		}

		UMLNode from = GetOrCreateRelated(match.Groups["from"].Value);
		UMLNode to = GetOrCreateRelated(match.Groups["to"].Value);

		PlantUMLSyntax.GetRelationship(
			match.Groups[1].Value,
			match.Groups[2].Value,
			match.Groups[3].Value,
			out UMLRelationshipType type,
			out UMLRelationshipDirection direction
		);

		diagram.Relationships.Add(
			new UMLRelationship(from, to, type, direction)
			{
				FromMultiplicity = Unquote(match.Groups["fromMultiplicity"].Value),
				ToMultiplicity = Unquote(match.Groups["toMultiplicity"].Value),
				Label = match.Groups["label"].Value.Trim(),
			}
		);
		return true;
	}

	/// <summary>
	/// Looks up a node named by a relationship, declaring it on the spot when the
	/// file never did — which PlantUML allows and its examples rely on.
	/// </summary>
	private UMLNode GetOrCreateRelated(string plantName)
	{
		string displayName = PlantUMLSyntax.UnwrapName(plantName, out UMLNodeType? impliedType);
		return GetOrCreateNode(displayName, impliedType ?? UMLNodeType.Class, null);
	}

	private UMLNode GetOrCreateNode(string plantName, UMLNodeType nodeType, string displayName)
	{
		if (nodesByPlantName.TryGetValue(plantName, out UMLNode existing))
		{
			return existing;
		}

		string identifier = MakeUniqueIdentifier(PlantUMLSyntax.ToIdentifier(plantName));
		UMLNode node = UMLNode.Create(nodeType, identifier);
		node.SourceLine = lineNumber;

		diagram.Nodes.Add(node);
		nodesByPlantName[plantName] = node;

		if (displayName != null && displayName != plantName)
		{
			// The alias is what relationships use, but the label is what the file
			// wanted on screen; keep the label reachable under both names.
			nodesByPlantName.TryAdd(displayName, node);
		}

		return node;
	}

	private string MakeUniqueIdentifier(string identifier)
	{
		string unique = identifier;
		for (int suffix = 2; !usedIdentifiers.Add(unique); suffix++)
		{
			unique = identifier + suffix;
		}

		return unique;
	}

	/// <summary>
	/// Places the imported nodes. Layout hints left by GodoUML's own exporter win;
	/// a file that does not carry one for every node is laid out from scratch,
	/// because a half-placed diagram is worse than a generated one.
	/// </summary>
	private void ApplyLayout()
	{
		int placed = 0;
		foreach (UMLNode node in diagram.Nodes)
		{
			if (positionHints.TryGetValue(node.Name, out Vector2 position))
			{
				node.Position = position;
				placed += 1;
			}
		}

		if (placed < diagram.Nodes.Count)
		{
			UMLAutoLayout.Apply(diagram);
		}
	}

	/// <summary>
	/// Keeps a type only when the GodoUML language can spell it, so an import can
	/// never produce source code that will not parse.
	/// </summary>
	private static string CleanType(string type)
	{
		string cleaned = type?.Trim().Replace(" ", "") ?? "";
		return cleaned.Length > 0 && UMLSyntax.TypeNameRegex().IsMatch(cleaned) ? cleaned : "";
	}

	private static string Unquote(string value)
	{
		return value.Length >= 2 && value[0] == '"' ? value[1..^1] : value;
	}

	private void Warn(string line, string reason)
	{
		warnings.Add($"Line {lineNumber + 1}: {reason} ({line})");
	}
}
