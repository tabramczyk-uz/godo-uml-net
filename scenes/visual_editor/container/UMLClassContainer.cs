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

	public override void Update() {
		attributesLabel.Text = umlClass.Attributes.ToListString();
		methodsLabel.Text = umlClass.Methods.ToListString();
	}

	public override void _Ready()
	{
		attributesLabel = GetNode<RichTextLabel>("%Attributes");
		methodsLabel = GetNode<RichTextLabel>("%Methods");

		base._Ready();
	}
}
