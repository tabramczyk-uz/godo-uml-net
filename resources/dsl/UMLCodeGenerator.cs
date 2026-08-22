using System.Text;

/// <summary>
/// Writes a whole <see cref="UMLDiagram"/> out as GodoUML source code.
///
/// This is the opposite of <see cref="UMLCodeWriter"/>, which edits code that
/// already exists and guards every byte the user typed. Generating from scratch
/// is only ever right when there is no source text yet — after a PlantUML
/// import, which is the one place a diagram enters the app without having been
/// written down first.
/// </summary>
public static class UMLCodeGenerator
{
	public static string Generate(UMLDiagram diagram)
	{
		var code = new StringBuilder();

		foreach (UMLNode node in diagram.Nodes)
		{
			if (code.Length > 0)
			{
				code.Append('\n');
			}

			WriteNode(code, node);
		}

		if (diagram.Relationships.Count > 0)
		{
			if (code.Length > 0)
			{
				code.Append('\n');
			}

			foreach (UMLRelationship relationship in diagram.Relationships)
			{
				WriteRelationship(code, relationship);
			}
		}

		return code.ToString();
	}

	private static void WriteNode(StringBuilder code, UMLNode node)
	{
		code.Append(UMLSyntax.GetKeyword(node.Type)).Append(' ').Append(node.Name).Append('\n');

		code.Append(UMLSyntax.Indent)
			.Append(UMLSyntax.GetKeyword(UMLNodeProperty.Position))
			.Append(": ")
			.Append(UMLSyntax.FormatPosition(node.Position))
			.Append('\n');

		if (node is not UMLClass classifier)
		{
			return;
		}

		foreach (UMLAttribute attribute in classifier.Attributes)
		{
			code.Append(UMLSyntax.Indent).Append(UMLNotation.Format(attribute)).Append('\n');
		}

		foreach (UMLMethod method in classifier.Methods)
		{
			code.Append(UMLSyntax.Indent).Append(UMLNotation.Format(method)).Append('\n');
		}
	}

	private static void WriteRelationship(StringBuilder code, UMLRelationship relationship)
	{
		code.Append(relationship.From.Name).Append(' ');
		AppendMultiplicity(code, relationship.FromMultiplicity);
		code.Append(UMLSyntax.GetOperator(relationship.Type, relationship.Direction)).Append(' ');
		AppendMultiplicity(code, relationship.ToMultiplicity);
		code.Append(relationship.To.Name);

		if (relationship.Label.Length > 0)
		{
			code.Append(" : ").Append(relationship.Label);
		}

		code.Append('\n');
	}

	private static void AppendMultiplicity(StringBuilder code, string multiplicity)
	{
		if (multiplicity.Length > 0)
		{
			code.Append('"').Append(multiplicity).Append("\" ");
		}
	}
}
