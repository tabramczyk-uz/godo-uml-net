using System.Collections.Generic;
using Godot;

/// <summary>
/// A classifier: a named box with an attribute compartment and an operation
/// compartment. Backs every <see cref="UMLNodeTypeExtensions.IsClassifier"/>
/// node type, which differ only in how they are drawn.
/// </summary>
public class UMLClass : UMLNode
{
	private static readonly PackedScene UmlClassContainer = GD.Load<PackedScene>(
		"uid://miycnuypaj3e"
	);

	public List<UMLAttribute> Attributes { get; set; }
	public List<UMLMethod> Methods { get; set; }

	public UMLClass(
		string name = "Class",
		List<UMLAttribute> attributes = null,
		List<UMLMethod> methods = null
	)
		: this(UMLNodeType.Class, name, null, attributes, methods) { }

	public UMLClass(
		UMLNodeType type,
		string name,
		Vector2? position = null,
		List<UMLAttribute> attributes = null,
		List<UMLMethod> methods = null
	)
		: base(type, name, position)
	{
		Attributes = attributes ?? [];
		Methods = methods ?? [];
	}

	public override UMLNodeContainer ToContainer()
	{
		UMLClassContainer container = UmlClassContainer.Instantiate() as UMLClassContainer;
		container.UmlClass = this;
		return container;
	}
}
