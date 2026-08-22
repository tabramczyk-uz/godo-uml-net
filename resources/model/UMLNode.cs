using Godot;

/// <summary>
/// A box on the canvas. Subclasses add the contents specific to a node type;
/// every node knows its type so the renderer and the writers never have to
/// downcast to find out.
/// </summary>
public class UMLNode
{
	public UMLNode(string name = "Node", Vector2? position = null)
		: this(UMLNodeType.Node, name, position) { }

	protected UMLNode(UMLNodeType type, string name, Vector2? position)
	{
		Type = type;
		Name = name;
		Position = position ?? Vector2.Zero;
	}

	public UMLNodeType Type { get; }
	public string Name { get; set; }
	public Vector2 Position { get; set; }

	/// <summary>
	/// Zero-based index of the line the node was declared on, or -1 when the node
	/// did not come from source code. The writers use it to edit exactly the line
	/// the node came from.
	/// </summary>
	public int SourceLine { get; set; } = -1;

	/// <summary>
	/// The one place that knows which class backs which node type. Everything that
	/// turns a keyword into a node — the parser and the PlantUML importer — goes
	/// through here.
	/// </summary>
	public static UMLNode Create(UMLNodeType type, string name, Vector2? position = null)
	{
		return type switch
		{
			UMLNodeType.UseCase => new UMLUseCase(name, position),
			UMLNodeType.Actor => new UMLActor(name, position),
			_ when type.IsClassifier() => new UMLClass(type, name, position),
			_ => new UMLNode(name, position),
		};
	}
}
