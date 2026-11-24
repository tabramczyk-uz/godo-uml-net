/// <summary>
/// The one participant or message selected on the sequence canvas. Every edit
/// re-parses the code into new objects, so the selection is kept by what
/// identifies them in the text instead: a participant by its name, a message
/// by its sender, kind, receiver and label, and which of the messages written
/// exactly alike it is, so edits elsewhere in the code do not move it.
/// </summary>
public class SequenceSelection
{
	private string participantName;

	private (string From, SequenceMessageKind Kind, string To, string Label)? messageKey;

	/// <summary>
	/// Which of the messages that match <see cref="messageKey"/> is selected,
	/// counting from the top.
	/// </summary>
	private int messageOccurrence;

	/// <summary>
	/// The index of a message that is to be selected once the code has been
	/// re-parsed, such as one just dragged there, or -1.
	/// </summary>
	private int pendingMessageIndex = -1;

	public bool IsEmpty => participantName == null && messageKey == null && pendingMessageIndex < 0;

	public void SelectParticipant(SequenceParticipant participant)
	{
		Clear();
		participantName = participant.Name;
	}

	public void SelectMessage(SequenceDiagram diagram, int messageIndex)
	{
		Clear();
		SequenceMessage message = diagram.Messages[messageIndex];
		messageKey = GetKey(message);
		messageOccurrence = 0;
		for (int n = 0; n < messageIndex; n++)
		{
			if (GetKey(diagram.Messages[n]) == messageKey)
			{
				messageOccurrence++;
			}
		}
	}

	/// <summary>
	/// Selects the message that will be at <paramref name="messageIndex"/>
	/// after the next re-parse, which <see cref="Update"/> settles.
	/// </summary>
	public void SelectMessageAfterEdit(int messageIndex)
	{
		Clear();
		pendingMessageIndex = messageIndex;
	}

	/// <summary>Deselects everything. Returns whether anything was selected.</summary>
	public bool Clear()
	{
		bool wasSelected = !IsEmpty;
		participantName = null;
		messageKey = null;
		messageOccurrence = 0;
		pendingMessageIndex = -1;
		return wasSelected;
	}

	/// <summary>
	/// Catches up with a newly parsed <paramref name="diagram"/>: settles a
	/// pending message and drops a selection the diagram no longer has.
	/// </summary>
	public void Update(SequenceDiagram diagram)
	{
		if (pendingMessageIndex >= 0)
		{
			int index = pendingMessageIndex;
			pendingMessageIndex = -1;
			if (index < diagram.Messages.Count)
			{
				SelectMessage(diagram, index);
			}

			return;
		}

		if (GetParticipant(diagram) == null && GetMessageIndex(diagram) < 0)
		{
			Clear();
		}
	}

	/// <summary>The selected participant in <paramref name="diagram"/>, or <c>null</c>.</summary>
	public SequenceParticipant GetParticipant(SequenceDiagram diagram)
	{
		if (participantName == null)
		{
			return null;
		}

		return diagram.Participants.Find(participant => participant.Name == participantName);
	}

	/// <summary>The index of the selected message in <paramref name="diagram"/>, or -1.</summary>
	public int GetMessageIndex(SequenceDiagram diagram)
	{
		if (messageKey == null)
		{
			return -1;
		}

		int occurrence = 0;
		for (int n = 0; n < diagram.Messages.Count; n++)
		{
			if (GetKey(diagram.Messages[n]) == messageKey)
			{
				if (occurrence == messageOccurrence)
				{
					return n;
				}

				occurrence++;
			}
		}

		return -1;
	}

	private static (string, SequenceMessageKind, string, string) GetKey(SequenceMessage message)
	{
		return (message.From.Name, message.Kind, message.To.Name, message.Label);
	}
}
