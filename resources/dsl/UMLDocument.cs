/// <summary>
/// The file the diagram in the editor belongs to, and its source code as it
/// was when last opened or saved. Comparing that with the editor's code tells
/// whether there are unsaved changes, so undoing back to the saved text counts
/// as no change at all.
/// </summary>
public class UMLDocument
{
	public const string UntitledName = "Untitled";

	/// <summary>
	/// Where the diagram was last opened from or saved to, or <c>null</c> if it
	/// has never been.
	/// </summary>
	public string Path { get; private set; }

	/// <summary>The source code as it was at that moment.</summary>
	public string SavedCode { get; private set; }

	public UMLDocument(string savedCode = "")
	{
		SavedCode = savedCode;
	}

	/// <summary>The file name shown to the user, without its folder.</summary>
	public string Name => Path == null ? UntitledName : System.IO.Path.GetFileName(Path);

	public bool IsModified(string code)
	{
		return code != SavedCode;
	}

	/// <summary>Records that <paramref name="code"/> now sits in <paramref name="path"/>.</summary>
	public void MarkSaved(string path, string code)
	{
		Path = path;
		SavedCode = code;
	}

	/// <summary>
	/// The window title, e.g. <c>shop.guml* - GodoUML</c>, with the star
	/// marking unsaved changes.
	/// </summary>
	public string GetTitle(string code, string appName)
	{
		string modified = IsModified(code) ? "*" : string.Empty;
		return $"{Name}{modified} - {appName}";
	}
}
