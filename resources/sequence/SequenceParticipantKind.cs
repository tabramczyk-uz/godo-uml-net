/// <summary>
/// How a participant is drawn at the head of its lifeline. The keyword that
/// declares each kind lives in <see cref="SequenceSyntax"/>.
/// </summary>
public enum SequenceParticipantKind
{
	/// <summary>A box with the participant's name.</summary>
	Participant,

	/// <summary>A stick figure with the participant's name.</summary>
	Actor,
}
