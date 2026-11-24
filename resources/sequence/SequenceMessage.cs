/// <summary>
/// A single communication from one participant to another, or to itself. A
/// diagram's messages are ordered top to bottom in source order.
/// </summary>
public sealed class SequenceMessage
{
	public SequenceMessage(
		SequenceParticipant from,
		SequenceParticipant to,
		SequenceMessageKind kind,
		string label = ""
	)
	{
		From = from;
		To = to;
		Kind = kind;
		Label = label;
	}

	public SequenceParticipant From { get; }
	public SequenceParticipant To { get; }
	public SequenceMessageKind Kind { get; }

	/// <summary>Text drawn along the arrow; empty when there is none.</summary>
	public string Label { get; }

	/// <summary>
	/// Zero-based index of the line the message was written on, or -1 when it did
	/// not come from source code.
	/// </summary>
	public int SourceLine { get; init; } = -1;

	public bool IsSelfMessage => From == To;
}
