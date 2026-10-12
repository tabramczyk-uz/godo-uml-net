using Godot;
using Xunit;

namespace GodoUML.Tests;

public class UMLLayoutMemoryTests
{
	/// <summary>Parses and lays out the code as the app does after every edit.</summary>
	private static UMLDiagram Parse(params string[] lines)
	{
		UMLParseResult result = UMLParser.Parse(string.Join("\n", lines));
		Assert.True(result.IsSuccess, result.ErrorMessage);
		UMLAutoLayout.ApplyToUnpositioned(result.Diagram);
		return result.Diagram;
	}

	private static Vector2 Fresh(UMLDiagram diagram, string name)
	{
		return diagram.FindNode(name).Position.Value;
	}

	private static Vector2 Rest(UMLLayoutMemory memory, UMLDiagram diagram, string name)
	{
		return memory.GetRestPosition(diagram.FindNode(name));
	}

	[Fact]
	public void KeepsSpotsWhenANodeIsAdded()
	{
		var memory = new UMLLayoutMemory();
		UMLDiagram before = Parse("class A", "class B", "B --|> A");
		memory.Update(before);

		// C widens the top row, which recentres B's row in the fresh layout.
		UMLDiagram after = Parse("class A", "class B", "class C", "B --|> A");
		memory.Update(after);

		Assert.NotEqual(Fresh(before, "B"), Fresh(after, "B"));
		Assert.Equal(Fresh(before, "B"), Rest(memory, after, "B"));
	}

	[Fact]
	public void LaysOutAfreshWhenARelationshipIsAdded()
	{
		var memory = new UMLLayoutMemory();
		UMLDiagram before = Parse("class A", "class B", "class C");
		memory.Update(before);

		UMLDiagram after = Parse("class A", "class B", "class C", "C --|> A");
		memory.Update(after);

		Assert.NotEqual(Fresh(before, "C"), Fresh(after, "C"));
		Assert.Equal(Fresh(after, "C"), Rest(memory, after, "C"));
		Assert.True(Rest(memory, after, "C").Y > Rest(memory, after, "A").Y);
	}

	[Fact]
	public void LaysOutAfreshWhenARelationshipTurnsAround()
	{
		var memory = new UMLLayoutMemory();
		memory.Update(Parse("class A", "class B", "B --|> A"));

		UMLDiagram after = Parse("class A", "class B", "A --|> B");
		memory.Update(after);

		Assert.True(Rest(memory, after, "A").Y > Rest(memory, after, "B").Y);
	}

	[Fact]
	public void LaysOutAfreshWhenARelationshipIsRemoved()
	{
		var memory = new UMLLayoutMemory();
		UMLDiagram before = Parse("class A", "class B", "class C", "B --|> A");
		memory.Update(before);

		UMLDiagram after = Parse("class A", "class B", "class C");
		memory.Update(after);

		Assert.NotEqual(Fresh(before, "B"), Fresh(after, "B"));
		Assert.Equal(Fresh(after, "B"), Rest(memory, after, "B"));
	}

	[Fact]
	public void LaysOutAfreshWhenANodeIsDeletedWithItsRelationships()
	{
		var memory = new UMLLayoutMemory();
		UMLDiagram before = Parse("class A", "class B", "class C", "B --|> A");
		memory.Update(before);

		UMLDiagram after = Parse("class B", "class C");
		memory.Update(after);

		Assert.NotEqual(Fresh(before, "B"), Fresh(after, "B"));
		Assert.Equal(Fresh(after, "B"), Rest(memory, after, "B"));
	}

	[Fact]
	public void KeepsSpotsWhenANodeIsPinned()
	{
		var memory = new UMLLayoutMemory();
		UMLDiagram before = Parse("class A", "class B", "class C", "C --|> A");
		memory.Update(before);

		// Dropping A after a drag writes its position, taking its relationship
		// out of what the layout sees, though the relationship is still there.
		UMLDiagram after = Parse("class A", "\tposition: [600, 600]", "class B", "class C", "C --|> A");
		memory.Update(after);

		Assert.NotEqual(Fresh(before, "C"), Fresh(after, "C"));
		Assert.Equal(Fresh(before, "C"), Rest(memory, after, "C"));
		Assert.Equal(Fresh(before, "B"), Rest(memory, after, "B"));
	}

	[Fact]
	public void KeepsSpotsWhenARelationshipToAPinnedNodeIsRemoved()
	{
		var memory = new UMLLayoutMemory();
		UMLDiagram before = Parse("class A", "\tposition: [600, 600]", "class B", "class C", "C --|> B", "C --> A");
		memory.Update(before);

		UMLDiagram after = Parse("class A", "\tposition: [600, 600]", "class B", "class C", "C --|> B", "class D");
		memory.Update(after);

		Assert.Equal(Fresh(before, "C"), Rest(memory, after, "C"));
	}

	[Fact]
	public void ForgetsADeletedNode()
	{
		var memory = new UMLLayoutMemory();
		UMLDiagram original = Parse("class B", "class A");
		memory.Update(original);
		memory.Update(Parse("class A"));

		UMLDiagram restored = Parse("class A", "class B");
		memory.Update(restored);

		Assert.NotEqual(Fresh(original, "B"), Fresh(restored, "B"));
		Assert.Equal(Fresh(restored, "B"), Rest(memory, restored, "B"));
	}

	[Fact]
	public void ForgetsAPinnedNodeOnceItIsReset()
	{
		var memory = new UMLLayoutMemory();
		UMLDiagram original = Parse("class B", "class A");
		memory.Update(original);
		memory.Update(Parse("class Z", "class B", "\tposition: [600, 600]", "class A"));

		UMLDiagram reset = Parse("class Z", "class B", "class A");
		memory.Update(reset);

		Assert.NotEqual(Fresh(original, "B"), Fresh(reset, "B"));
		Assert.Equal(Fresh(reset, "B"), Rest(memory, reset, "B"));
	}

	[Fact]
	public void KeepsSpotsAcrossARename()
	{
		var memory = new UMLLayoutMemory();
		memory.Update(Parse("class A", "class B", "B --|> A"));
		UMLDiagram before = Parse("class A", "class B", "class C", "B --|> A");
		memory.Update(before);
		Vector2 spot = Rest(memory, before, "B");

		memory.Rename("B", "D");
		UMLDiagram after = Parse("class A", "class D", "class C", "D --|> A");
		memory.Update(after);

		Assert.NotEqual(spot, Fresh(after, "D"));
		Assert.Equal(spot, Rest(memory, after, "D"));
	}

	[Fact]
	public void KeepsSpotsWhenARenameIsRefused()
	{
		var memory = new UMLLayoutMemory();
		memory.Update(Parse("class A", "class B", "B --|> A"));
		UMLDiagram before = Parse("class A", "class B", "class C", "B --|> A");
		memory.Update(before);
		Vector2 spot = Rest(memory, before, "B");

		memory.Rename("B", "C");
		UMLDiagram after = Parse("class A", "class B", "class C", "B --|> A");
		memory.Update(after);

		Assert.Equal(spot, Rest(memory, after, "B"));
		Assert.Equal(Rest(memory, before, "C"), Rest(memory, after, "C"));
	}

	[Fact]
	public void ForgetsEverythingWhenCleared()
	{
		var memory = new UMLLayoutMemory();
		memory.Update(Parse("class A", "class B", "B --|> A"));
		memory.Update(Parse("class A", "class B", "class C", "B --|> A"));

		memory.Clear();
		UMLDiagram after = Parse("class A", "class B", "class C", "B --|> A");
		memory.Update(after);

		Assert.Equal(Fresh(after, "B"), Rest(memory, after, "B"));
	}
}
