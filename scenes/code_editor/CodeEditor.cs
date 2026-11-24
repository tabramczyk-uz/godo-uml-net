using System;
using Godot;

// `partial` lets Godot's source generators add the glue code every C# node
// class needs.
public partial class CodeEditor : Control
{
	public event Action<string> CodeChanged;

	[Export]
	public Color ErrorColor { get; set; } = new Color();

	private TextEdit codeEdit;
	private Timer updateTimer;
	private MarginContainer errorContainer;
	private RichTextLabel errorLabel;

	private int errorLine = -1;

	/// <summary>The source code as it stands in the editor.</summary>
	public string Code => codeEdit.Text;

	public override void _Ready()
	{
		// "%Name" finds the node marked as a unique name in this scene (Access as
		// Unique Name in the editor), wherever it sits in the tree.
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

	/// <summary>
	/// Replaces the whole source with <paramref name="code"/>, as when opening a
	/// file, and parses it straight away. Undo starts afresh, so it cannot bring
	/// back the previous diagram.
	/// </summary>
	public void LoadCode(string code)
	{
		codeEdit.Text = code;
		codeEdit.ClearUndoHistory();
		SubmitCode();
	}

	/// <summary>
	/// Steps back through the editor's history, which holds the canvas edits
	/// too, since each of those is an edit of the code. Parses straight away
	/// rather than after the typing delay.
	/// </summary>
	public void Undo()
	{
		codeEdit.Undo();
		SubmitCode();
	}

	/// <inheritdoc cref="Undo"/>
	public void Redo()
	{
		codeEdit.Redo();
		SubmitCode();
	}

	/// <summary>
	/// Replaces the source with <paramref name="newText"/>, as a canvas edit
	/// does, and parses it straight away rather than after the typing delay.
	/// The change is one step in the editor's undo history.
	/// </summary>
	public void ApplyEdit(string newText)
	{
		codeEdit.Text = newText;
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
