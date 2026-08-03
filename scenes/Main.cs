using Godot;

public partial class Main : Control
{
	private UMLParser parser;
	private CodeEditor codeEditor;
	private VisualEditor visualEditor;

	private bool hasError = false;

	public override void _Ready()
	{
		parser = new UMLParser();
		parser.Connect(nameof(UMLParser.ErrorOccurred), new Callable(this, nameof(OnParserError)));

		codeEditor = GetNode<CodeEditor>("%CodeEditor");
		codeEditor.Connect(nameof(CodeEditor.CodeChanged), new Callable(this, nameof(OnCodeChanged)));

		visualEditor = GetNode<VisualEditor>("%VisualEditor");
		visualEditor.Connect(nameof(VisualEditor.NodeNameChanged), new Callable(this, nameof(OnNodeNameChanged)));
		visualEditor.Connect(nameof(VisualEditor.NodePositionChanged), new Callable(this, nameof(OnNodePositionChanged)));
	}

	private void OnParserError(string message, int lineNumber)
	{
		// GD.PrintErr($"Error on line {lineNumber}: {message}");
		codeEditor.ShowError(message, lineNumber);
	}

	private void OnCodeChanged(string code)
	{
		codeEditor.DismissError();
		var diagram = parser.ParseCode(code);
		visualEditor.RenderDiagram(diagram);
	}

	private void OnNodeNameChanged(UMLNode node, string newName)
	{
		if (parser.IsNodeNameValid(newName))
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
}
