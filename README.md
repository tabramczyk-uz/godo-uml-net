# GodoUML.NET
Godot-powered UML designer that combines diagram-as-code with visual editing.

## Installing a release

Pre-built versions are published on the [Releases page](https://github.com/tabramczyk-uz/godo-uml-net/releases).

1. Open the latest release and, under **Assets**, download the `.zip` archive.
2. Extract the whole archive to a folder of your choice (for example `C:\Programs\GodoUML`).
3. Run `GodoUML.exe` from that folder.

The executable is not code-signed, so on first launch Windows SmartScreen may show "Windows protected your PC". Click **More info → Run anyway**.

To update, delete the old folder and extract the new release in its place. To uninstall, delete the folder.

## Using GodoUML

The window is split in two: a **code editor** on the left and a **diagram canvas** on the right. Both show the same diagram — edit either one and the other follows.

### Writing a diagram as text

Each element is declared on its own line, with its contents indented by **one tab** underneath. Relationships go on their own lines:

```
abstract Shape
	# name: String
	+ area(): Decimal

class Circle
	position: [40, 210]
	- radius: Decimal
	+ area(): Decimal

interface Drawable
	+ draw(canvas: Canvas, layer: Integer): void

enum Palette
	RED
	GREEN

actor Designer

usecase Render

Circle --|> Shape
Circle ..|> Drawable
Shape "1" o-- "0..*" Circle : groups
Designer --> Render : starts
```

<img width="865" height="788" alt="obraz" src="https://github.com/user-attachments/assets/c043689f-1769-46fc-9ea1-629ad1d108c0" />

- **Element types:** `class`, `abstract`, `interface`, `enum`, `node`, `usecase`, `actor`.
- **Members:** attributes `name: Type` and methods `name(arg: Type): ReturnType`, prefixed with a visibility: `+` public, `-` private, `#` protected, `~` package. Enum values are written as bare names.
- **Relationships:** `-->` association, `--|>` generalization, `..|>` realization, `..>` dependency, `o--` aggregation, `*--` composition, `--` plain link. Multiplicities go in quotes next to each end, and a label after `:`.
- **`position: [x, y]`** pins an element on the canvas. Elements without one are placed automatically.
- `//` starts a comment that runs to the end of the line.

If the text contains an error, the faulty line is reported in the editor and the canvas is greyed out until it is fixed.

### Editing on the canvas

| Action | How |
| --- | --- |
| Add an element | **Add** menu above the canvas |
| Connect two elements | **Connect** menu → pick a relationship type, then click the two elements; or right-click an element → **Connect from Here** |
| Remove a connection | **Connect → Delete Connection**, then click the line |
| Move an element | Drag it with the left mouse button |
| Select several elements | Drag a box over empty canvas; hold `Shift` to add to the selection |
| Rename | Double-click the name, or right-click → **Rename…** |
| Reset position, delete | Right-click an element |
| Delete the selection | `Delete` |
| Select all | `Ctrl+A` |
| Copy / paste | `Ctrl+C` / `Ctrl+V` |
| Undo / redo | `Ctrl+Z` / `Ctrl+Y` |
| Cancel the current action | `Esc` |
| Pan the view | Drag with the middle mouse button, or hold `Alt` and drag; the mouse wheel scrolls |
| Zoom | `Ctrl` + mouse wheel, or the **View** menu (which also has **Frame Diagram**) |

Every change made on the canvas is written back into the text, keeping the comments and formatting you typed.

### Files and PlantUML

The **File** menu has **New** (`Ctrl+N`), **Open…** (`Ctrl+O`), **Save** (`Ctrl+S`) and **Save As…** (`Ctrl+Shift+S`). Diagrams are saved as `.guml` text files, and the app asks before discarding unsaved changes.

- **Export to PlantUML…** shows the diagram as PlantUML code, with a **Copy** button that puts it on the clipboard.
- **Import from PlantUML…** takes PlantUML code pasted from `@startuml` to `@enduml` and opens it as a new diagram. If some lines cannot be imported, the dialog lists them and offers **Import Anyway**.

## Building from source

### Requirements

- **Godot Engine (.NET edition) 4.7.1 or newer** — download it from <https://godotengine.org/download>.
- **.NET SDK 8.0 or newer** — <https://dotnet.microsoft.com/download>.

### Getting the source

```bash
git clone https://github.com/tabramczyk-uz/godo-uml-net.git
cd godo-uml-net
```

### Building

```bash
dotnet build
```

This restores the NuGet packages and compiles the C# scripts to `.godot/mono/temp/bin/Debug/GodoUML.dll`. The Godot editor also builds the project automatically when you open or run it, so this step is optional, but it is a quick way to check that the code compiles.

### Running

Replace `<godot>` below with the path to your Godot .NET executable (for example `Godot_v4.7.1-stable_mono_win64.exe`).

Run the app directly:

```bash
<godot> --path . scenes/Main.tscn
```

Or open the project in the editor and press **F5** (Run Project):

```bash
<godot> --path . --editor
```

You can also start the Godot project manager, choose **Import**, and select the `project.godot` file in the repository.

### Running the tests

The unit tests are an xUnit project in `tests/GodoUML.Tests`. They exercise the model, the DSL parser and writer, and the PlantUML import/export directly, so they do not need Godot:

```bash
dotnet test tests/GodoUML.Tests
```

### Building a standalone executable

This is how the release builds are made. The repository does not ship export presets, so the first export is done from the editor:

1. In Godot, open **Editor → Manage Export Templates** and download the templates for 4.7.1 (.NET).
2. Open **Project → Export**, click **Add…** and pick your target platform (e.g. *Windows Desktop*).
3. Click **Export Project** and choose an output folder. Keep the generated `data_GodoUML_*` folder next to the executable, and zip the two together for a release.

After the first export the preset is saved in `export_presets.cfg`, and later builds can be made from the command line:

```bash
<godot> --headless --path . --export-release "Windows Desktop" build/GodoUML.exe
```

## License

See [LICENSE](LICENSE).
