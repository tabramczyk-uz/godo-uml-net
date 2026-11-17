# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

GodoUML is a Godot 4 / C# desktop app: a UML designer where a text DSL and a visual canvas edit the same diagram side by side. Godot engine 4.7.1 (mono), `Godot.NET.Sdk/4.7.1`, `net8.0`. `README.md` is the user-facing manual (DSL syntax, canvas controls, files, building, releases).

## Thesis scope

This repo is an engineer's thesis implementation — *"Implementacja hybrydowego edytora diagramów UML łączącego edycję wizualną z reprezentacją tekstową"*. The brief requires the visual and textual representations to be **equivalent**, every diagram editable either way; that is what the round trip below delivers, and no change should weaken it.

The brief's code deliverables now all exist:

- **PlantUML import and export** — `resources/plantuml/`, reachable from the File menu. The brief does not demand full coverage of PlantUML.
- **Tests** — an xUnit project in `tests/GodoUML.Tests` (see Commands).
- **Documentation** — `README.md` covers usage and building.

What remains of the brief is write-up work: a survey of existing UML tools, and the GUI and architecture design. Prefer supporting those (and keeping tests and docs in step with the code) over further feature polish.

## Design records and task list

- `CONTEXT.md` is the glossary. Use its terms in code, docs and commit messages, and add to it when a new domain term settles.
- `docs/adr/` records the decisions that are hard to reverse: one pipeline per diagram kind (0001), and the layered layout driven by hierarchical relationships (0002).
- `docs/tasks.md` is the agreed work queue, untracked and edited only in the main checkout. Its "How to work on a task" section governs branches, commits, merging and manual checks for agents working from it. Outside that workflow, changes stay uncommitted in the main checkout. **Nobody pushes.**

## Commands

```bash
dotnet build                       # compiles to .godot/mono/temp/bin/Debug/GodoUML.dll
dotnet test tests/GodoUML.Tests    # xUnit; runs without Godot
```

The test project references `GodoUML.csproj` directly and exercises the plain-C# parts — model, parser, code writer, generator, clipboard, document/file format, auto-layout, geometry, PlantUML import/export and round trips. Anything that is a Godot `Node` (the editors, containers, `Main`) is not covered; verifying those means running the app. `tests/.gdignore` keeps Godot from importing the test folder.

Running the app requires the Godot editor binary (there is no `godot` on PATH). On this machine:

```bash
GODOT="/c/Users/Nikodem/Desktop/Godot/Godot_v4.7.1-stable_mono_win64/Godot_v4.7.1-stable_mono_win64.exe"
"$GODOT" --path .                  # open the editor
"$GODOT" --path . scenes/Main.tscn # run the app directly
```

## The DSL

The parser (`resources/dsl/UMLParser.cs`) and every regex/keyword table (`resources/dsl/UMLSyntax.cs`) define a line-based language:

```
abstract Shape             // level 0: <type> <Name>
	position: [10, 20]     // level 1: property (`position` is the only one)
	# name: String         // level 1: attribute
	+ area(): Decimal      // level 1: method
enum Palette
	RED                    // bare name: enum literal (an attribute)
Circle "1" o-- "0..*" Shape : groups   // level 0: relationship
```

- Node types: `node`, `class`, `abstract`, `interface`, `enum`, `usecase`, `actor`. Only classifiers (`class`/`abstract`/`interface`/`enum`, see `UMLNodeType.IsClassifier`) may have members.
- Indentation is **tabs only**. `UMLSyntax.GetIndentation` counts leading tabs, and that count doubles as the offset of the line's content.
- Identifiers are `[a-zA-Z_][a-zA-Z0-9_]*`. Visibility symbols are `+ - # ~` (`UMLNotation`).
- A body line with parentheses is a method; otherwise `name: Type` is ambiguous with a property, and `UMLParser.IsAttributeLine` settles it (visibility symbol or bare name → attribute; a property keyword wins).
- Relationship operators are head decoration + body (`-`/`--` solid, `.`/`..` dashed) + tail decoration, decorations being `<|`/`|>` (triangle), `<`/`>` (arrow), `o` (hollow diamond), `*` (filled diamond). `UMLSyntax.TryGetRelationship` maps them to a `UMLRelationshipType` (association, dependency, aggregation, composition, generalization, realization) plus a `UMLRelationshipDirection`; `GetOperator` is its inverse. Optional quoted multiplicities sit beside each end and `: label` follows.
- `//` runs to end of line. Comments are stripped before parsing and preserved across rewrites.
- Parsing stops at the **first** error and returns `UMLParseResult.Failure(message, lineNumber)`. Duplicate node names, duplicate properties on one node, members on a non-classifier, and relationships naming an undeclared node are all errors.

## Architecture: the code ⇄ visual round trip

`scenes/Main.cs` is the only place the two editors are wired together; `CodeEditor` and `VisualEditor` never reference each other. They talk to `Main` through plain C# `event Action<...>`s.

**The source text is the single source of truth.** The visual side never mutates the model in place; a visual edit is only real once it has been written into the code and re-parsed. Consequences worth knowing:

- Every re-parse rebuilds **all** node containers (`VisualEditor.RenderDiagram`), so canvas state that must survive an edit is keyed by node **name** (`selectedNames`, `UMLLayoutMemory`), never by `UMLNode`/container reference.
- Multi-node edits go through batch writer methods (`SetNodePositions`, `RemoveNodes`, `RemoveNodePositions`) so the code is rewritten and parsed once.
- Undo/redo on the canvas just calls `CodeEditor.Undo/Redo`: each canvas edit is an assignment to `TextEdit.Text`, which Godot 4 records in the editor's undo history.

**Text → diagram.** `CodeEditor` debounces `TextEdit.TextChanged` through a 0.3s one-shot `%UpdateTimer`, then raises `CodeChanged`. `Main` parses, shows or dismisses the inline error, runs `UMLAutoLayout.ApplyToUnpositioned`, and calls `VisualEditor.RenderDiagram(result.Diagram)` — passing `null` on a parse failure, which grays the canvas out and disables node input and the Add/Connect menus while leaving the last good render on screen.

**Diagram → text.** `VisualEditor` raises `NodesMoved`, `NodeNameChanged`, `NodeAdded`, `RelationshipAdded`, `RelationshipRemoved`, `NodesRemoved`, `NodePositionsReset`, `NodesPasted`, `UndoRequested`/`RedoRequested`. `Main` validates where needed (e.g. `UMLDiagram.DescribeRenameProblem`, shown in an error dialog) and calls the matching `CodeEditor` method, which rewrites the text via `UMLCodeWriter` and immediately re-submits it.

**`UMLCodeWriter` edits text surgically, never regenerates it.** It locates the declaration, property or relationship line (first trying the `SourceLine` the parser recorded, then searching by name), splices in the new substring, and leaves comments, blank lines, indentation and spacing alone. New nodes, relationships and pasted blocks are appended at the end so they are always declared before use. Any new visual-edit feature needs a matching writer method that upholds the same minimal-diff rule. `UMLCodeGenerator` is the opposite — it writes a whole diagram from scratch — and is used only where there is no existing text: PlantUML import and the clipboard.

### Auto-layout

`UMLNode.Position` is `Vector2?`; `null` means the source has no `position:` line. `UMLAutoLayout.ApplyToUnpositioned` places those nodes with a layered (Sugiyama-style) layout — edges with generalization/realization/aggregation/composition pointing parent-up, cycles broken by turning DFS back edges around (`BreakCycles`), longest-path layers, barycentre row ordering, a 240×170 grid that skips cells near pinned nodes — and sets `IsAutoPositioned`. These positions never reach the source.

On the canvas, auto-positioned nodes are pushed out of the way of other nodes (`UMLAutoLayout.Separate`, called from `VisualEditor.PushAutoPositioned`, including during drags). Pushes always start from each node's rest position, so the result depends only on where the other nodes are now. Rest positions live in `UMLLayoutMemory` (keyed by name, updated on every render): a node keeps the spot the layout first gave it, so adding nodes doesn't shift the ones on screen; a deleted or pinned node loses its spot; and when the above/below edges between auto-positioned nodes (`UMLAutoLayout.Orient`) change — one added, removed or turned around — every spot is dropped and the fresh layout is taken. An edge that only leaves the layout because a node was pinned doesn't count, and canvas renames are applied to the remembered edges once the re-parse confirms them. `VisualEditor.ForgetLayout` clears the memory on New/Open/Import. Dropping a dragged node writes its `position:`, pinning it; Reset Position deletes the line, un-pinning it.

### Visual editor

`VisualEditor.cs` is large; its main parts:

- **View:** nodes are children of `%Anchor`; pan moves it, zoom scales it through the discrete `ZoomLevels` (index 8 = 100%) around a pivot. `_Draw` applies the anchor's transform with `DrawSetTransform`, so relationship lines and selection frames are drawn in diagram coordinates.
- **Modes:** `CanvasMode` — `Normal`, `Connecting` (click source then target; a preview line follows the mouse), `DeletingConnection` (hover highlights the nearest line within `LinePickDistance` screen pixels). Picking modes disable container input and are handled in `HandleModeInput`; Esc or right-click cancels. Drawing and picking share `TryGetRelationshipEdges`.
- **Selection:** click, Shift+click, box select on empty canvas; dragging a selected node moves the whole selection (`GetSelectionGroup`). The right-click node menu is built per click (`OpenNodeMenu`) and acts on the selection.
- **Keys:** Delete, Ctrl+A, Ctrl+C/V, Ctrl+Z/Y act only while the canvas has focus, so they don't steal from the code editor or the rename popup.
- **Clipboard:** `UMLClipboard` copies selected nodes plus the relationships among them as DSL text; paste parses it, renames clashes (`Order2` → `Order3`), offsets by whole `NewNodeOffset` steps until clear, and appends it via `UMLCodeWriter.AppendBlock`.

### Files and PlantUML (`Main.cs`)

- A `.guml` file is the editor text verbatim (`UMLFileFormat`; line endings normalized to `\n` on open). `UMLDocument` tracks the path and the last-saved text; "modified" means the text differs from it.
- `ConfirmDiscardingChanges(reason, action)` gates New/Open/Import/window close behind a Save / Don't Save / Cancel dialog, with `pendingAction` and `afterSaveAs` carrying what to do afterwards. Closing is intercepted via `AutoAcceptQuit = false` and `NotificationWMCloseRequest`.
- **Export** re-parses the current code (refusing on an error) and runs `PlantUMLExporter`, which emits positions as `'@position Name [x, y]` comments so a re-import keeps the layout.
- **Import** runs `PlantUMLImporter`, an independent front end with its own grammar in `PlantUMLSyntax`. It never fails outright: unsupported lines become warnings, which the dialog lists before an "Import Anyway". The result goes through `UMLCodeGenerator` and opens as a new Untitled diagram. `UMLSyntax`'s tables describe the in-app language only and should not be stretched to cover PlantUML.

## Extending the model

Adding a **node type** touches: the `UMLNodeType` enum (and `IsClassifier` if it has members), `NodeTypeKeywords` in `UMLSyntax`, a `UMLNode` subclass with a `ToContainer` override, the switch in `UMLNode.Create`, a container scene + script under `scenes/visual_editor/container/`, and the PlantUML importer/exporter (`PlantUMLSyntax`). Stereotype text lives in `UMLNotation.GetStereotype`.

Adding a **node property** touches: `UMLNodeProperty` + `NodePropertyKeywords`, a case in `UMLParser.ParseProperty`, a writer method in `UMLCodeWriter`, and `UMLCodeGenerator`.

Adding a **relationship type** touches: `UMLRelationshipType` and its extensions (`GetEnding`, `IsDashed`), the operator symbols in `UMLSyntax` (`TryGetRelationship`/`GetOperator` must stay inverses), `DrawEnding` in `VisualEditor`, and the PlantUML modules. The Connect menus are filled from the enum automatically.

Add tests for anything in the plain-C# layer.

## Godot conventions used here

- Scripts fetch children by unique-name path in `_Ready`: `GetNode<T>("%Name")`, backed by `unique_name_in_owner = true` in the `.tscn`. Renaming a node in a scene silently breaks the C# lookup at runtime. When loading `.tscn` files, prioritize UIDs over file paths — UIDs are stable across refactors and renames, while file paths can break silently.
- Communication between scripts uses C# `event Action<...>` rather than Godot `[Signal]`s. All `Node`/`Resource` subclasses must be `partial` for the Godot source generators.
- Keep the model, DSL, layout and PlantUML code (`resources/`) free of engine calls beyond Godot value types like `Vector2`/`Rect2`, so the tests can run it without Godot (`UMLGeometry` exists for this reason).
- Regexes use `[GeneratedRegex]` on `static partial` methods in `UMLSyntax` (or `PlantUMLImporter`/`PlantUMLSyntax` for PlantUML) — add new patterns there rather than inline.
- Input action names (`Drag`, `AltDrag`, `ZoomMode`, `ZoomIn`/`Out`, `Scroll*`, `Submit`, `Cancel`, `Delete`) are defined in `project.godot`; the canvas also uses Godot's built-in `ui_undo`, `ui_redo`, `ui_copy`, `ui_paste`, `ui_text_select_all`.
- Indentation is tabs in most files, but `EditPopup.cs`, `UMLNodeContainer.cs` and parts of `VisualEditor.cs` use spaces — match the file you are editing.

## Migration state

The repo was migrated from Godot 3.6 to 4.x and still carries leftovers; do not assume something is intentional just because it is committed:

- All scenes are `format=3`, but `resources/font/*.tres` are still `format=2` (Godot 3).
- `project.godot` keeps Godot 3 sections that 4.x ignores: `[mono]` (duplicating `[dotnet]`), `quality/driver/driver_name="GLES2"`, `vram_compression/import_etc`, `physics/common/enable_pause_aware_picking`.
- `GodoUML.csproj.old` is the retained Godot 3 project file; `.vscode/settings.json` still points at the 3.6.2 editor; `.import/` and `.mono/` are stale Godot 3 artifacts (gitignored).
