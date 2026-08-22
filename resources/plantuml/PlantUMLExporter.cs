using System.Collections.Generic;
using System.Text;

/// <summary>
/// Walks a <see cref="UMLDiagram"/> out to PlantUML source. The result is
/// ordinary PlantUML that any renderer accepts; the layout of the diagram rides
/// along in comments, so a file exported here can be imported back with its
/// nodes where the user left them.
/// </summary>
public static class PlantUMLExporter
{
	private const string MemberIndent = "  ";

	public static string Export(UMLDiagram diagram, bool includePositions = true)
	{
		var output = new StringBuilder();
		output.Append(PlantUMLSyntax.Start).Append('\n').Append('\n');

		foreach (UMLNode node in diagram.Nodes)
		{
			WriteNode(output, node, includePositions);
		}

		if (diagram.Nodes.Count > 0 && diagram.Relationships.Count > 0)
		{
			output.Append('\n');
		}

		foreach (UMLRelationship relationship in diagram.Relationships)
		{
			WriteRelationship(output, relationship);
		}

		return output.Append('\n').Append(PlantUMLSyntax.End).Append('\n').ToString();
	}

	private static void WriteNode(StringBuilder output, UMLNode node, bool includePositions)
	{
		if (includePositions)
		{
			output
				.Append(PlantUMLSyntax.PositionHint)
				.Append(' ')
				.Append(node.Name)
				.Append(' ')
				.Append(UMLSyntax.FormatPosition(node.Position))
				.Append('\n');
		}

		output.Append(PlantUMLSyntax.GetKeyword(node.Type)).Append(' ').Append(node.Name);

		List<string> members = GetMembers(node);
		if (members.Count == 0)
		{
			output.Append('\n');
			return;
		}

		output.Append(" {\n");
		foreach (string member in members)
		{
			output.Append(MemberIndent).Append(member).Append('\n');
		}

		output.Append("}\n");
	}

	private static List<string> GetMembers(UMLNode node)
	{
		List<string> members = [];
		if (node is not UMLClass classifier)
		{
			return members;
		}

		foreach (UMLAttribute attribute in classifier.Attributes)
		{
			members.Add(UMLNotation.Format(attribute));
		}

		foreach (UMLMethod method in classifier.Methods)
		{
			members.Add(UMLNotation.Format(method));
		}

		return members;
	}

	private static void WriteRelationship(StringBuilder output, UMLRelationship relationship)
	{
		output.Append(relationship.From.Name).Append(' ');
		AppendMultiplicity(output, relationship.FromMultiplicity);
		output.Append(PlantUMLSyntax.GetArrow(relationship.Type, relationship.Direction)).Append(' ');
		AppendMultiplicity(output, relationship.ToMultiplicity);
		output.Append(relationship.To.Name);

		if (relationship.Label.Length > 0)
		{
			output.Append(" : ").Append(relationship.Label);
		}

		output.Append('\n');
	}

	private static void AppendMultiplicity(StringBuilder output, string multiplicity)
	{
		if (multiplicity.Length > 0)
		{
			output.Append('"').Append(multiplicity).Append("\" ");
		}
	}
}
