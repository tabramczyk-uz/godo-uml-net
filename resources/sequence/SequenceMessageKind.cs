/// <summary>
/// What a message does. Only a synchronous message opens an activation, and only
/// a reply closes one. The operator that writes each kind lives in
/// <see cref="SequenceSyntax"/>.
/// </summary>
public enum SequenceMessageKind
{
	/// <summary>A call the sender waits on: <c>A -&gt; B</c>.</summary>
	Synchronous,

	/// <summary>A call the sender does not wait on: <c>A -&gt;&gt; B</c>.</summary>
	Asynchronous,

	/// <summary>The answer to a synchronous call: <c>B --&gt; A</c>.</summary>
	Reply,
}
