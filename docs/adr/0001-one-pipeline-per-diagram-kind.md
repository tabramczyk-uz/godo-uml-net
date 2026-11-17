# One pipeline per diagram kind behind a shared shell

Adding sequence diagrams meant choosing between widening the existing model, parser and canvas to hold participants and messages too, or giving each diagram kind its own model, parser, code writer, generator and canvas. We chose separate pipelines. Only the shell is shared: the text editor, the debounce, the document and file handling, undo, the File menu and error display. The shell reads the file's diagram header and connects the matching pipeline. Sequence and class diagrams have little structure in common: an ordered timeline on one side, freely placed nodes on the other. Merging them would have added branches to the most complex code in the project, while separate pipelines keep each kind as simple as a single-kind editor.

## Consequences

- A file holds exactly one diagram of one kind. Mixing kinds, or linking diagrams of different kinds, would need a new design and is not supported.
- Editing the header switches the canvas on the next parse. Nothing tries to convert the existing content to the new kind.
- Each new kind brings its own PlantUML import and export.
