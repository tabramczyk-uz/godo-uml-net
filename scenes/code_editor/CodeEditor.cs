using System;
using Godot;

public partial class CodeEditor : Control
{
	public event Action<string> CodeChanged;

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

		codeEdit.TextChanged += OnTextChanged;
		updateTimer.Timeout += SubmitCode;
	}

	private void SubmitCode()
	{
		CodeChanged?.Invoke(codeEdit.Text);
	}

	public void ChangeNodeName(UMLNode node, string newName)
	{
		codeEdit.Text = UMLCodeWriter.RenameNode(codeEdit.Text, node, newName);
		SubmitCode();
	}

	public void ChangeNodePosition(UMLNode node, Vector2 newPosition)
	{
		codeEdit.Text = UMLCodeWriter.SetNodePosition(codeEdit.Text, node, newPosition);
		SubmitCode();
	}

	public void ShowError(string message, int lineNumber)
	{
		if (errorLine != -1)
		{
			codeEdit.SetLineBackgroundColor(errorLine, new Color(0, 0, 0, 0));
		}

		codeEdit.SetLineBackgroundColor(lineNumber, ErrorColor);
		errorLabel.Text = $"Error on line {lineNumber + 1}: {message}";
		errorContainer.Show();
		errorLine = lineNumber;
	}

	public void DismissError()
	{
		if (errorLine != -1 && errorLine < codeEdit.GetLineCount())
		{
			codeEdit.SetLineBackgroundColor(errorLine, new Color(0, 0, 0, 0));
		}

		errorContainer.Hide();
		errorLine = -1;
	}

	private void OnTextChanged()
	{
		updateTimer.Start();
	}
}
