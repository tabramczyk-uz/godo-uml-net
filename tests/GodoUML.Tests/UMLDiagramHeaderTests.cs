using Xunit;

namespace GodoUML.Tests;

public class UMLDiagramHeaderTests
{
	private static UMLDiagramHeader ReadOrFail(string code)
	{
		UMLDiagramHeader header = UMLDiagramHeader.Read(code);
		Assert.True(
			header.IsSuccess,
			$"Expected a header read but got: {header.ErrorMessage} on line {header.ErrorLineNumber}"
		);
		return header;
	}

	private static UMLDiagramHeader ReadExpectingFailure(string code)
	{
		UMLDiagramHeader header = UMLDiagramHeader.Read(code);
		Assert.False(header.IsSuccess, "Expected the header read to fail");
		return header;
	}

	// [Theory] runs the test once per [InlineData] row, passing the row's values
	// as the arguments.
	[Theory]
	[InlineData("diagram class", UMLDiagramKind.Class)]
	[InlineData("diagram sequence", UMLDiagramKind.Sequence)]
	[InlineData("diagram \t sequence  ", UMLDiagramKind.Sequence)]
	[InlineData("diagram sequence\nparticipant A", UMLDiagramKind.Sequence)]
	public void ReadsTheKindAHeaderNames(string code, UMLDiagramKind expected)
	{
		UMLDiagramHeader header = ReadOrFail(code);

		Assert.Equal(expected, header.Kind);
		Assert.True(header.IsPresent);
		Assert.Equal(0, header.LineNumber);
	}

	[Theory]
	[InlineData("")]
	[InlineData("\n\n")]
	[InlineData("// only a comment")]
	[InlineData("class A\nclass B\nA --> B")]
	[InlineData("node diagram")]
	[InlineData("class A\ndiagram sequence")]
	public void WithoutAHeaderTheDiagramIsAClassDiagram(string code)
	{
		UMLDiagramHeader header = ReadOrFail(code);

		Assert.Equal(UMLDiagramKind.Class, header.Kind);
		Assert.False(header.IsPresent);
		Assert.Equal(-1, header.LineNumber);
	}

	[Fact]
	public void TheHeaderMayFollowBlankLinesAndComments()
	{
		UMLDiagramHeader header = ReadOrFail("// Ordering\n\n  \n// by Ann\ndiagram sequence");

		Assert.Equal(UMLDiagramKind.Sequence, header.Kind);
		Assert.Equal(4, header.LineNumber);
	}

	[Fact]
	public void TheHeaderMayCarryATrailingComment()
	{
		UMLDiagramHeader header = ReadOrFail("diagram sequence // checkout flow");

		Assert.Equal(UMLDiagramKind.Sequence, header.Kind);
	}

	[Fact]
	public void AnIndentedDiagramLineIsNotAHeader()
	{
		UMLDiagramHeader header = ReadOrFail("\tdiagram sequence");

		Assert.False(header.IsPresent);
	}

	[Theory]
	[InlineData("diagram activity", "Unknown diagram kind: activity")]
	[InlineData("diagram Sequence", "Unknown diagram kind: Sequence")]
	[InlineData("diagram", "Missing diagram kind")]
	[InlineData("diagram sequence class", "A diagram header names exactly one kind")]
	public void AnUnknownOrMalformedHeaderIsAnError(string header, string expectedMessage)
	{
		UMLDiagramHeader result = ReadExpectingFailure("// intro\n" + header + "\nclass A");

		Assert.Equal(expectedMessage, result.ErrorMessage);
		Assert.Equal(1, result.ErrorLineNumber);
	}

	[Theory]
	[InlineData(UMLDiagramKind.Class)]
	[InlineData(UMLDiagramKind.Sequence)]
	public void FormatIsReadBackAsTheSameKind(UMLDiagramKind kind)
	{
		Assert.Equal(kind, ReadOrFail(UMLDiagramHeader.Format(kind)).Kind);
	}

	[Fact]
	public void FormatSpellsOutTheHeaderLine()
	{
		Assert.Equal("diagram sequence", UMLDiagramHeader.Format(UMLDiagramKind.Sequence));
	}

	[Theory]
	[InlineData(UMLDiagramKind.Class)]
	[InlineData(UMLDiagramKind.Sequence)]
	public void ANewDiagramIsReadAsTheKindItWasMadeFor(UMLDiagramKind kind)
	{
		Assert.Equal(kind, ReadOrFail(UMLDiagramHeader.NewDiagramCode(kind)).Kind);
	}

	[Fact]
	public void ANewClassDiagramIsEmpty()
	{
		Assert.Equal(string.Empty, UMLDiagramHeader.NewDiagramCode(UMLDiagramKind.Class));
	}

	[Fact]
	public void ANewSequenceDiagramIsJustItsHeader()
	{
		string code = UMLDiagramHeader.NewDiagramCode(UMLDiagramKind.Sequence);

		Assert.Equal("diagram sequence", code);
		SequenceParseResult result = SequenceParser.Parse(ReadOrFail(code).RemoveFrom(code));
		Assert.True(result.IsSuccess, result.ErrorMessage);
		Assert.Empty(result.Diagram.Participants);
	}

	[Fact]
	public void RemoveFromBlanksOnlyTheHeaderLine()
	{
		string code = "// intro\ndiagram class // kind\nclass A\nclass B";

		string body = ReadOrFail(code).RemoveFrom(code);

		Assert.Equal("// intro\n\nclass A\nclass B", body);
	}

	[Fact]
	public void RemoveFromLeavesCodeWithoutAHeaderAlone()
	{
		string code = "class A\nclass B";

		Assert.Equal(code, ReadOrFail(code).RemoveFrom(code));
	}

	[Fact]
	public void TheClassParserAcceptsAClassDiagramOnceItsHeaderIsRemoved()
	{
		string code = "diagram class\nclass A\nclass B\nA --> B";

		UMLParseResult result = UMLParser.Parse(ReadOrFail(code).RemoveFrom(code));

		Assert.True(result.IsSuccess, result.ErrorMessage);
		Assert.Equal(1, result.Diagram.Nodes[0].SourceLine);
	}
}
