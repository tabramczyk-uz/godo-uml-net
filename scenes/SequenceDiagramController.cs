using System;
using Godot;

/// <summary>
/// The sequence diagram's side of the round trip, between the shared code
/// editor and the sequence canvas. It parses the code and shows the result,
/// and turns every canvas edit into a code edit through
/// <see cref="SequenceCodeWriter"/>, which works on the whole editor text,
/// header included. See ADR-0001.
/// </summary>
public class SequenceDiagramController : IDiagramController
{
	/// <inheritdoc/>
	public event Action<string, string> ErrorReported;

	private readonly CodeEditor codeEditor;
	private readonly SequenceDiagramCanvas canvas;

	/// <summary>
	/// The last diagram the code parsed into, which is the one on the canvas:
	/// a failed parse leaves it in place, grayed out. The writer needs it to
	/// turn the canvas's indices into lines.
	/// </summary>
	private SequenceDiagram diagram = new();

	public SequenceDiagramController(CodeEditor codeEditor, SequenceDiagramCanvas canvas)
	{
		this.codeEditor = codeEditor;
		this.canvas = canvas;

		canvas.ParticipantAdded += OnParticipantAdded;
		canvas.ParticipantNameChanged += OnParticipantNameChanged;
		canvas.ParticipantRemoved += OnParticipantRemoved;
		canvas.ParticipantMoved += OnParticipantMoved;
		canvas.MessageAdded += OnMessageAdded;
		canvas.MessageRemoved += OnMessageRemoved;
		canvas.MessageMoved += OnMessageMoved;
		canvas.UndoRequested += codeEditor.Undo;
		canvas.RedoRequested += codeEditor.Redo;
	}

	public void SetActive(bool active)
	{
		canvas.Visible = active;
		canvas.ProcessMode = active ? Node.ProcessModeEnum.Inherit : Node.ProcessModeEnum.Disabled;
	}

	public void ShowCode(string body)
	{
		SequenceParseResult result = SequenceParser.Parse(body);
		if (result.IsSuccess)
		{
			codeEditor.DismissError();
			diagram = result.Diagram;
		}
		else
		{
			codeEditor.ShowError(result.ErrorMessage, result.ErrorLineNumber);
		}

		canvas.RenderDiagram(result.Diagram);
	}

	public void ShowInvalid()
	{
		canvas.RenderDiagram(null);
	}

	public void FrameDiagram()
	{
		canvas.FrameDiagram();
	}

	private void OnParticipantAdded(SequenceParticipantKind kind, string name)
	{
		codeEditor.ApplyEdit(SequenceCodeWriter.AddParticipant(codeEditor.Code, kind, name));
	}

	private void OnParticipantNameChanged(SequenceParticipant participant, string newName)
	{
		string problem = diagram.DescribeRenameProblem(participant, newName);
		if (problem == null)
		{
			codeEditor.ApplyEdit(SequenceCodeWriter.RenameParticipant(codeEditor.Code, participant, newName));
		}
		else
		{
			ErrorReported?.Invoke("Invalid Name", $"{participant.Name} was not renamed.\n{problem}");
		}
	}

	/// <summary>
	/// Deletes the participant and every message it sends or receives in one
	/// edit, so it is a single undo step.
	/// </summary>
	private void OnParticipantRemoved(SequenceParticipant participant)
	{
		codeEditor.ApplyEdit(SequenceCodeWriter.RemoveParticipant(codeEditor.Code, participant));
	}

	private void OnParticipantMoved(SequenceParticipant participant, int newIndex)
	{
		codeEditor.ApplyEdit(
			SequenceCodeWriter.MoveParticipant(codeEditor.Code, diagram, participant, newIndex)
		);
	}

	private void OnMessageAdded(
		SequenceParticipant from,
		SequenceParticipant to,
		SequenceMessageKind kind,
		int index
	)
	{
		codeEditor.ApplyEdit(
			SequenceCodeWriter.InsertMessage(codeEditor.Code, diagram, index, from, to, kind)
		);
	}

	private void OnMessageRemoved(SequenceMessage message)
	{
		codeEditor.ApplyEdit(SequenceCodeWriter.RemoveMessage(codeEditor.Code, message));
	}

	private void OnMessageMoved(SequenceMessage message, int newIndex)
	{
		codeEditor.ApplyEdit(SequenceCodeWriter.MoveMessage(codeEditor.Code, diagram, message, newIndex));
	}
}
