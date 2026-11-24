/// <summary>
/// An entity in a sequence diagram that sends and receives messages. Participants
/// are placed left to right in the order they are declared.
/// </summary>
public sealed class SequenceParticipant
{
	public SequenceParticipant(string name, SequenceParticipantKind kind)
	{
		Name = name;
		Kind = kind;
	}

	public string Name { get; }
	public SequenceParticipantKind Kind { get; }

	/// <summary>
	/// Zero-based index of the line the participant was declared on, or -1 when it
	/// did not come from source code.
	/// </summary>
	public int SourceLine { get; init; } = -1;
}
