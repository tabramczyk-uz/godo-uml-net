using Godot;

public partial class Main : Control
{
	private CodeEditor codeEditor;
	private VisualEditor visualEditor;

	public override void _Ready()
	{
		codeEditor = GetNode<CodeEditor>("%CodeEditor");
		codeEditor.CodeChanged += OnCodeChanged;

		visualEditor = GetNode<VisualEditor>("%VisualEditor");
		visualEditor.NodeNameChanged += OnNodeNameChanged;
		visualEditor.NodePositionChanged += OnNodePositionChanged;
		visualEditor.NodeAdded += OnNodeAdded;
		visualEditor.RelationshipAdded += OnRelationshipAdded;
	}

	private void OnCodeChanged(string code)
	{
		UMLParseResult result = UMLParser.Parse(code);
		if (result.IsSuccess)
		{
			codeEditor.DismissError();
			UMLAutoLayout.ApplyToUnpositioned(result.Diagram);
		}
		else
		{
			codeEditor.ShowError(result.ErrorMessage, result.ErrorLineNumber);
		}

		visualEditor.RenderDiagram(result.Diagram);
	}

	private void OnNodeNameChanged(UMLNode node, string newName)
	{
		if (UMLSyntax.IsValidNodeName(newName))
		{
			codeEditor.ChangeNodeName(node, newName);
		}
		else
		{
			// TODO: Show error message to user
			GD.PrintErr($"Invalid node name: {newName}");
		}
	}

	private void OnNodePositionChanged(UMLNode node, Vector2 newPosition)
	{
		codeEditor.ChangeNodePosition(node, newPosition);
	}

	private void OnNodeAdded(UMLNodeType type, string name, Vector2 position)
	{
		codeEditor.AddNode(type, name, position);
	}

	private void OnRelationshipAdded(
		UMLNode from,
		UMLNode to,
		UMLRelationshipType type,
		UMLRelationshipDirection direction
	)
	{
		codeEditor.AddRelationship(from, to, type, direction);
	}
}
