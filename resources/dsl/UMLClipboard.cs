using System.Collections.Generic;
using Godot;

/// <summary>
/// Copying and pasting nodes. The clipboard holds them as GodoUML source code,
/// so a copy can be pasted into another diagram, into the code editor, or
/// anywhere else text goes, and any code that parses can be pasted as nodes.
/// </summary>
public static class UMLClipboard
{
	/// <summary>
	/// The <paramref name="nodes"/> as code: each with its position and
	/// members, and the relationships between them. Relationships to nodes
	/// left behind are left out, since the copy could not be pasted with them.
	/// </summary>
	public static string Copy(UMLDiagram diagram, IReadOnlyCollection<UMLNode> nodes)
	{
		var copied = new UMLDiagram();
		var included = new HashSet<UMLNode>(nodes);

		foreach (UMLNode node in diagram.Nodes)
		{
			if (included.Contains(node))
			{
				copied.Nodes.Add(node);
			}
		}

		foreach (UMLRelationship relationship in diagram.Relationships)
		{
			if (included.Contains(relationship.From) && included.Contains(relationship.To))
			{
				copied.Relationships.Add(relationship);
			}
		}

		return UMLCodeGenerator.Generate(copied);
	}

	/// <summary>
	/// Turns clipboard <paramref name="text"/> into code ready to append to
	/// <paramref name="target"/>. Names already in use are numbered on, as the
	/// Add menu does, and the relationships follow the renamed nodes. The nodes
	/// keep their layout, moved on by whole <paramref name="step"/>s until none
	/// lands on a node already there: pasted into another diagram they stay
	/// put, pasted over their originals they shift one step, and every paste
	/// after that one step more. Nodes the text gives no position are laid out
	/// first. Fails when the text does not parse or declares no nodes.
	/// </summary>
	public static bool TryPaste(
		string text,
		UMLDiagram target,
		Vector2 step,
		out string code,
		out List<string> names
	)
	{
		code = null;
		names = [];

		if (string.IsNullOrWhiteSpace(text))
		{
			return false;
		}

		UMLParseResult result = UMLParser.Parse(UMLFileFormat.NormalizeLineEndings(text));
		if (!result.IsSuccess || result.Diagram.Nodes.Count == 0)
		{
			return false;
		}

		UMLDiagram pasted = result.Diagram;
		UMLAutoLayout.ApplyToUnpositioned(pasted);

		var taken = new HashSet<string>();
		foreach (UMLNode node in target.Nodes)
		{
			taken.Add(node.Name);
		}

		Vector2 offset = GetFreeOffset(pasted, target, step);
		foreach (UMLNode node in pasted.Nodes)
		{
			node.Name = GetFreeName(node.Name, taken);
			node.Position += offset;
			taken.Add(node.Name);
			names.Add(node.Name);
		}

		code = UMLCodeGenerator.Generate(pasted);
		return true;
	}

	/// <summary>
	/// The smallest whole number of <paramref name="step"/>s that keeps every
	/// pasted node off the spots the target's nodes already sit on.
	/// </summary>
	private static Vector2 GetFreeOffset(UMLDiagram pasted, UMLDiagram target, Vector2 step)
	{
		float closeEnough = step.Length() / 2.0f;
		for (int steps = 0; ; steps++)
		{
			Vector2 offset = step * steps;
			bool clashes = false;
			foreach (UMLNode node in pasted.Nodes)
			{
				foreach (UMLNode existing in target.Nodes)
				{
					if (
						existing.Position != null
						&& (node.Position.Value + offset).DistanceTo(existing.Position.Value) < closeEnough
					)
					{
						clashes = true;
					}
				}
			}

			if (!clashes || step == Vector2.Zero)
			{
				return offset;
			}
		}
	}

	/// <summary>
	/// <paramref name="name"/> itself when it is free, and otherwise the first
	/// free one numbered on from its stem, so a copy of <c>Order2</c> becomes
	/// <c>Order3</c> rather than <c>Order22</c>.
	/// </summary>
	private static string GetFreeName(string name, HashSet<string> taken)
	{
		if (!taken.Contains(name))
		{
			return name;
		}

		string stem = name.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
		for (int suffix = 2; ; suffix++)
		{
			string candidate = stem + suffix;
			if (!taken.Contains(candidate))
			{
				return candidate;
			}
		}
	}
}
