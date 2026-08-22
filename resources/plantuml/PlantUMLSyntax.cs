using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// The slice of the PlantUML grammar the import and export modules understand.
/// It is deliberately separate from <see cref="UMLSyntax"/>: PlantUML is a
/// foreign language that happens to describe the same diagrams, and stretching
/// one grammar over both would leave neither honest.
/// </summary>
public static partial class PlantUMLSyntax
{
	public const string Start = "@startuml";
	public const string End = "@enduml";
	public const string CommentPrefix = "'";
	public const string BlockCommentStart = "/'";
	public const string BlockCommentEnd = "'/";

	/// <summary>
	/// Marker of the layout hint GodoUML writes on export. It is an ordinary
	/// PlantUML comment, so every other tool ignores it, and it lets a diagram
	/// exported from GodoUML come back with its layout intact.
	/// </summary>
	public const string PositionHint = "'@position";

	private const string Name = "(?:\"[^\"]*\"|\\([^)]*\\)|:[^:]*:|[A-Za-z_][A-Za-z0-9_.]*)";
	private const string Multiplicity = "\"[^\"]*\"";

	// A PlantUML arrow: an optional head, a body of dashes or dots that may carry
	// a colour and a direction hint, and an optional tail.
	private const string ArrowHead = "(?:<\\||<|\\*|o|\\+|#)";
	private const string ArrowTail = "(?:\\|>|>|\\*|o|\\+|#)";
	private const string ArrowBody =
		"(?:[-.=]+(?:\\[[^\\]]*\\])?(?:up|down|left|right|[udlr])?[-.=]*)";
	private const string Arrow = "(" + ArrowHead + "?)(" + ArrowBody + ")(" + ArrowTail + "?)";

	private const string DeclarationPattern =
		"^(?<keyword>abstract[ \\t]+class|abstract|class|interface|enum|entity|object|circle"
		+ "|rectangle|node|component|usecase|actor|participant|boundary|control|collections"
		+ "|database|folder|frame|cloud|storage|artifact|agent|person|queue)"
		+ "[ \\t]+(?<name>"
		+ Name
		+ ")"
		+ "(?:[ \\t]+as[ \\t]+(?<alias>"
		+ Name
		+ "))?"
		+ "(?:[ \\t]*<<(?<stereotype>[^>]*)>>)?"
		+ "(?:[ \\t]*#[A-Za-z0-9_]+)?"
		+ "[ \\t]*(?<open>\\{)?[ \\t]*$";

	private const string RelationshipPattern =
		"^(?<from>"
		+ Name
		+ ")[ \\t]*(?:(?<fromMultiplicity>"
		+ Multiplicity
		+ ")[ \\t]*)?"
		+ Arrow
		+ "[ \\t]*(?:(?<toMultiplicity>"
		+ Multiplicity
		+ ")[ \\t]*)?(?<to>"
		+ Name
		+ ")[ \\t]*(?::[ \\t]*(?<label>.*?))?[ \\t]*$";

	private const string MemberPattern =
		"^(?<visibility>[+\\-#~])?[ \\t]*(?<name>[A-Za-z_][A-Za-z0-9_]*)"
		+ "(?:[ \\t]*\\((?<arguments>[^)]*)\\))?"
		+ "(?:[ \\t]*:[ \\t]*(?<type>.+?))?[ \\t]*$";

	private const string ArgumentPattern =
		"^(?<name>[A-Za-z_][A-Za-z0-9_]*)(?:[ \\t]*:[ \\t]*(?<type>.+?))?[ \\t]*$";

	private const string SeparatorPattern = "^(?:--|\\.\\.|==|__).*$";
	private const string ModifierPattern = "\\{(?:static|abstract|field|method|classifier)\\}";
	private const string IgnoredPattern =
		"^(?:@startuml|@enduml|!|<style|</style"
		+ "|(?:skinparam|title|header|footer|caption|legend|hide|show|scale|autonumber"
		+ "|allow_mixing|allowmixing|end[ \\t]*note|end[ \\t]*legend|end[ \\t]*box"
		+ "|left to right direction|top to bottom direction)\\b)";

	private static readonly Dictionary<UMLNodeType, string> Keywords = new()
	{
		{ UMLNodeType.Node, "rectangle" },
		{ UMLNodeType.Class, "class" },
		{ UMLNodeType.Interface, "interface" },
		{ UMLNodeType.AbstractClass, "abstract class" },
		{ UMLNodeType.Enum, "enum" },
		{ UMLNodeType.UseCase, "usecase" },
		{ UMLNodeType.Actor, "actor" },
	};

	/// <summary>
	/// Which GodoUML node type each PlantUML keyword becomes. Several PlantUML
	/// keywords collapse onto one GodoUML type, which is the part of the language
	/// the import module does not claim to cover in full.
	/// </summary>
	private static readonly Dictionary<string, UMLNodeType> NodeTypesByKeyword = new(
		StringComparer.Ordinal
	)
	{
		{ "class", UMLNodeType.Class },
		{ "entity", UMLNodeType.Class },
		{ "object", UMLNodeType.Class },
		{ "interface", UMLNodeType.Interface },
		{ "abstract", UMLNodeType.AbstractClass },
		{ "abstract class", UMLNodeType.AbstractClass },
		{ "enum", UMLNodeType.Enum },
		{ "usecase", UMLNodeType.UseCase },
		{ "actor", UMLNodeType.Actor },
		{ "participant", UMLNodeType.Actor },
		{ "person", UMLNodeType.Actor },
		{ "agent", UMLNodeType.Node },
		{ "rectangle", UMLNodeType.Node },
		{ "node", UMLNodeType.Node },
		{ "component", UMLNodeType.Node },
		{ "circle", UMLNodeType.Node },
		{ "boundary", UMLNodeType.Node },
		{ "control", UMLNodeType.Node },
		{ "collections", UMLNodeType.Node },
		{ "database", UMLNodeType.Node },
		{ "folder", UMLNodeType.Node },
		{ "frame", UMLNodeType.Node },
		{ "cloud", UMLNodeType.Node },
		{ "storage", UMLNodeType.Node },
		{ "artifact", UMLNodeType.Node },
		{ "queue", UMLNodeType.Node },
	};

	[GeneratedRegex(DeclarationPattern)]
	public static partial Regex DeclarationRegex();

	[GeneratedRegex(RelationshipPattern)]
	public static partial Regex RelationshipRegex();

	[GeneratedRegex(MemberPattern)]
	public static partial Regex MemberRegex();

	[GeneratedRegex(ArgumentPattern)]
	public static partial Regex ArgumentRegex();

	[GeneratedRegex(SeparatorPattern)]
	public static partial Regex SeparatorRegex();

	[GeneratedRegex(ModifierPattern)]
	public static partial Regex ModifierRegex();

	[GeneratedRegex(IgnoredPattern, RegexOptions.IgnoreCase)]
	public static partial Regex IgnoredRegex();

	public static string GetKeyword(UMLNodeType nodeType)
	{
		return Keywords[nodeType];
	}

	public static bool TryGetNodeType(string keyword, out UMLNodeType nodeType)
	{
		return NodeTypesByKeyword.TryGetValue(NormalizeKeyword(keyword), out nodeType);
	}

	/// <summary>
	/// Spells out the PlantUML arrow for a relationship. The spellings happen to
	/// match the ones GodoUML uses, but they are written out here so the two
	/// grammars stay free to move apart.
	/// </summary>
	public static string GetArrow(UMLRelationshipType type, UMLRelationshipDirection direction)
	{
		string head = direction.DecoratesFrom() ? GetHeadSymbol(type) : string.Empty;
		string tail = direction.DecoratesTo() ? GetTailSymbol(type) : string.Empty;
		return head + (type.IsDashed() ? ".." : "--") + tail;
	}

	/// <summary>
	/// Reads a PlantUML arrow back as the relationship it draws.
	/// </summary>
	public static void GetRelationship(
		string head,
		string body,
		string tail,
		out UMLRelationshipType type,
		out UMLRelationshipDirection direction
	)
	{
		UMLRelationshipEnding fromEnding = GetEnding(head);
		UMLRelationshipEnding toEnding = GetEnding(tail);
		UMLRelationshipEnding ending = fromEnding >= toEnding ? fromEnding : toEnding;
		bool isDashed = body.Contains('.');

		type = ending switch
		{
			UMLRelationshipEnding.HollowTriangle => isDashed
				? UMLRelationshipType.Realization
				: UMLRelationshipType.Generalization,
			UMLRelationshipEnding.HollowDiamond => UMLRelationshipType.Aggregation,
			UMLRelationshipEnding.FilledDiamond => UMLRelationshipType.Composition,
			_ => isDashed ? UMLRelationshipType.Dependency : UMLRelationshipType.Association,
		};

		bool decoratesFrom = ending != UMLRelationshipEnding.None && fromEnding == ending;
		bool decoratesTo = ending != UMLRelationshipEnding.None && toEnding == ending;
		direction = (decoratesFrom, decoratesTo) switch
		{
			(true, true) => UMLRelationshipDirection.Both,
			(true, false) => UMLRelationshipDirection.Backward,
			(false, true) => UMLRelationshipDirection.Forward,
			_ => UMLRelationshipDirection.None,
		};
	}

	/// <summary>
	/// Strips the quoting PlantUML puts around a name: <c>"A name"</c> for a
	/// label, <c>(A use case)</c> and <c>:An actor:</c> for the shorthand forms.
	/// </summary>
	public static string UnwrapName(string name, out UMLNodeType? impliedType)
	{
		impliedType = null;
		if (name.Length < 2)
		{
			return name;
		}

		if (name[0] == '"' && name[^1] == '"')
		{
			return name[1..^1].Trim();
		}

		if (name[0] == '(' && name[^1] == ')')
		{
			impliedType = UMLNodeType.UseCase;
			return name[1..^1].Trim();
		}

		if (name[0] == ':' && name[^1] == ':')
		{
			impliedType = UMLNodeType.Actor;
			return name[1..^1].Trim();
		}

		return name;
	}

	/// <summary>
	/// Turns a PlantUML display name into a name the GodoUML language accepts.
	/// </summary>
	public static string ToIdentifier(string name)
	{
		var identifier = new StringBuilder(name.Length);
		foreach (char character in name)
		{
			identifier.Append(char.IsLetterOrDigit(character) || character == '_' ? character : '_');
		}

		while (identifier.Length > 0 && identifier[^1] == '_')
		{
			identifier.Length -= 1;
		}

		if (identifier.Length == 0 || char.IsDigit(identifier[0]))
		{
			identifier.Insert(0, 'N');
		}

		return identifier.ToString();
	}

	private static string NormalizeKeyword(string keyword)
	{
		return WhitespaceRegex().Replace(keyword, " ");
	}

	[GeneratedRegex("[ \\t]+")]
	private static partial Regex WhitespaceRegex();

	private static string GetHeadSymbol(UMLRelationshipType type)
	{
		return type switch
		{
			UMLRelationshipType.Generalization or UMLRelationshipType.Realization => "<|",
			UMLRelationshipType.Aggregation => "o",
			UMLRelationshipType.Composition => "*",
			_ => "<",
		};
	}

	private static string GetTailSymbol(UMLRelationshipType type)
	{
		return type switch
		{
			UMLRelationshipType.Generalization or UMLRelationshipType.Realization => "|>",
			UMLRelationshipType.Aggregation => "o",
			UMLRelationshipType.Composition => "*",
			_ => ">",
		};
	}

	private static UMLRelationshipEnding GetEnding(string decoration)
	{
		if (decoration.Length == 0)
		{
			return UMLRelationshipEnding.None;
		}

		if (decoration.Contains('|'))
		{
			return UMLRelationshipEnding.HollowTriangle;
		}

		return decoration[0] switch
		{
			'*' => UMLRelationshipEnding.FilledDiamond,
			'o' => UMLRelationshipEnding.HollowDiamond,
			_ => UMLRelationshipEnding.OpenArrow,
		};
	}
}
