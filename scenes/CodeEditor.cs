using Godot;

public class CodeEditor : Control
{
	[Signal]
	public delegate void CodeChanged(string code);

	[Export]
	public Color StringColor { get; set; } = new Color();

	[Export]
	public Color CommentColor { get; set; } = new Color();

	[Export]
	public Color ErrorColor { get; set; } = new Color();

	private TextEdit codeEdit;
	private Timer updateTimer;
	private MarginContainer errorContainer;
	private RichTextLabel errorLabel;

	private int errorLine = -1;

	public override void _Ready()
	{
		codeEdit = GetNode<TextEdit>("%CodeEdit");
		updateTimer = GetNode<Timer>("%UpdateTimer");
		errorContainer = GetNode<MarginContainer>("%ErrorContainer");
		errorLabel = GetNode<RichTextLabel>("%ErrorLabel");

		codeEdit.Connect("text_changed", this, nameof(OnTextChanged));
		updateTimer.Connect("timeout", this, nameof(SubmitCode));

		codeEdit.AddColorRegion("\"", "\"", StringColor, false);
		codeEdit.AddColorRegion(UMLParser.CommentPrefix, "", CommentColor, true);
	}

	private void SubmitCode()
	{
		EmitSignal(nameof(CodeChanged), codeEdit.Text);
	}

	public void ChangeNodeName(UMLNode node, string newName)
	{
		codeEdit.Text = UMLParser.ChangeNodeName(codeEdit.Text, node, newName);
		SubmitCode();
	}

	public void ChangeNodePosition(UMLNode node, Vector2 newPosition)
	{
		codeEdit.Text = UMLParser.ChangeNodePosition(codeEdit.Text, node, newPosition);
		SubmitCode();
	}

	public void ShowError(string message, int lineNumber)
	{
		if (errorLine != -1)
		{
			codeEdit.SetLineAsBreakpoint(errorLine, false);
		}

		codeEdit.SetLineAsBreakpoint(lineNumber, true);
		errorLabel.Text = $"Error on line {lineNumber + 1}: {message}";
		errorContainer.Show();
		errorLine = lineNumber;
	}

	public void DismissError()
	{
		if (errorLine != -1 && errorLine < codeEdit.GetLineCount())
		{
			codeEdit.SetLineAsBreakpoint(errorLine, false);
		}

		errorContainer.Hide();
		errorLine = -1;
	}

	private void OnTextChanged()
	{
		updateTimer.Start();
	}
}

