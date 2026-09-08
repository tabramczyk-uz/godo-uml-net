using System.Collections.Generic;
using System.Text;
using Godot;

public partial class UMLClassContainer : UMLNodeContainer
{
	private RichTextLabel attributesLabel;
	private RichTextLabel methodsLabel;

	private UMLClass umlClass;
	public UMLClass UmlClass
	{
		get => umlClass;
		set
		{
			umlClass = value;
			UmlNode = value;
		}
	}

	public void SetNode(UMLClass node)
	{
		UmlClass = node;
	}

	public override void Update()
	{
		attributesLabel.Text = Format(umlClass.Attributes);
		methodsLabel.Text = Format(umlClass.Methods);

		base.Update();
	}

	public override void _Ready()
	{
		attributesLabel = GetNode<RichTextLabel>("%Attributes");
		methodsLabel = GetNode<RichTextLabel>("%Methods");

		base._Ready();
	}

	private static string Format(IEnumerable<UMLAttribute> attributes)
	{
		StringBuilder sb = new();

		foreach (UMLAttribute attribute in attributes)
		{
			sb.Append(UMLNotation.GetSymbol(attribute.Visibility));
			sb.Append(attribute.Name);
			sb.Append(string.IsNullOrEmpty(attribute.Type) ? string.Empty : $": {attribute.Type}");
			sb.AppendLine();
		}

		return sb.ToString();
	}

	private static string Format(IEnumerable<UMLMethod> methods)
	{
		StringBuilder sb = new();

		foreach (UMLMethod method in methods)
		{
			sb.Append(UMLNotation.GetSymbol(method.Visibility));
			sb.Append(method.Name);

			sb.Append('(');
			for (int i = 0; i < method.Arguments.Count; i++)
			{
				if (i > 0)
				{
					sb.Append(", ");
				}

				UMLMethodArgument argument = method.Arguments[i];
				sb.Append(argument.Name);
				sb.Append(string.IsNullOrEmpty(argument.Type) ? string.Empty : $": {argument.Type}");
			}
			sb.Append(')');

			sb.Append(string.IsNullOrEmpty(method.ReturnType) ? string.Empty : $": {method.ReturnType}");
			sb.AppendLine();
		}

		return sb.ToString();
	}
}
