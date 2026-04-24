using Godot;

public class Main : Control
{
	private UMLParser parser;
	private CodeEditor codeEditor;
	private VisualEditor visualEditor;

	private bool hasError = false;

	public override void _Ready()
	{
		parser = new UMLParser();
		parser.Connect(nameof(UMLParser.ErrorOccurred), this, nameof(OnParserError));

		codeEditor = GetNode<CodeEditor>("%CodeEditor");
		codeEditor.Connect(nameof(CodeEditor.CodeChanged), this, nameof(OnCodeChanged));

		visualEditor = GetNode<VisualEditor>("%VisualEditor");
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
}
