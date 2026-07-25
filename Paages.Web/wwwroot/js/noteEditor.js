// Active Quill instances keyed by elementId, so getHtml/setHtml/destroyEditor know which editor to use.
const editors = {};

// Custom block format for "// comment" lines. A dedicated blot (not reusing the
// built-in italic format) so a manually-italicized line is never mistaken for a
// comment line, and so styling (italic + 60% opacity) is fully controlled by CSS.
const BlockBlot = Quill.import('blots/block');
class CommentBlot extends BlockBlot {
    static blotName = 'comment';
    static tagName = 'div';
    static className = 'ql-comment';
}
Quill.register(CommentBlot, true);

// Line-level auto-format patterns, checked after user types space, '*' or '_'.
const LINE_PATTERNS = [
    { regex: /^#\s$/, format: { header: 1 }, stripLen: 2 },
    { regex: /^##\s$/, format: { header: 2 }, stripLen: 3 },
    { regex: /^###\s$/, format: { header: 3 }, stripLen: 4 },
    { regex: /^-\s$/, format: { list: 'bullet' }, stripLen: 2 },
    { regex: /^>\s$/, format: { blockquote: true }, stripLen: 2 },
    { regex: /^\/\/\s$/, format: { comment: true }, stripLen: 3 },
];

// Block formats that revertOnBackspace should be able to clear.
const REVERTIBLE_FORMATS = ['header', 'list', 'blockquote', 'comment'];

// Inline patterns: **text** for bold, _text_ for italic — handled via formatText, not formatLine.
const BOLD_PATTERN = { regex: /\*\*([^*]+)\*\*$/, format: 'bold' };
const ITALIC_PATTERN = { regex: /_([^_]+)_$/, format: 'italic' };
const INLINE_PATTERNS = [BOLD_PATTERN, ITALIC_PATTERN];

export function createEditor(elementId, initialHtml) {
    const quill = new Quill('#' + elementId, {
        theme: 'snow',
        modules: {
            // Built-in Snow toolbar disabled: app uses its own floating toolbar.
            toolbar: false,
            // Custom keyboard binding must be registered here, at construction time.
            // Bindings added later via addBinding() run after Quill's built-in handlers
            // and cannot intercept Backspace in time.
            keyboard: {
                bindings: {
                    // Declarative context: Quill only calls this handler when the
                    // cursor is on an empty line (empty: true implies collapsed + offset 0).
                    revertOnBackspace: {
                        key: 'Backspace',
                        empty: true,
                        handler: function (range, context) {
                            const activeFormat = REVERTIBLE_FORMATS.find(f => context.format[f]);
                            if (!activeFormat) return true; // no block format here, normal Backspace

                            quill.formatLine(range.index, 1, activeFormat, false, 'user');
                            return false; // handled, stop Quill's default Backspace
                        }
                    },
                    // Ctrl+Enter inside a blockquote exits it: default Enter creates a new
                    // line, then we drop the blockquote format so it becomes a plain paragraph.
                    exitBlockquote: {
                        key: 'Enter',
                        shortKey: true,
                        format: ['blockquote'],
                        handler: function (range) {
                            quill.insertText(range.index, '\n', 'user');
                            quill.formatLine(range.index + 1, 1, 'blockquote', false, 'user');
                            quill.setSelection(range.index + 1, 0, 'user');
                        }
                    },
                    // Plain Enter inside a blockquote: create the new line as usual, then
                    // re-apply blockquote so it reads as a continuation, not a fresh quote.
                    continueBlockquote: {
                        key: 'Enter',
                        format: ['blockquote'],
                        handler: function (range) {
                            quill.insertText(range.index, '\n', 'user');
                            quill.formatLine(range.index + 1, 1, 'blockquote', true, 'user');
                            quill.setSelection(range.index + 1, 0, 'user');
                        }
                    },
                    // Enter inside a comment line always exits it: unlike blockquote,
                    // comments are single-line, so the next line must be plain text.
                    exitComment: {
                        key: 'Enter',
                        format: ['comment'],
                        handler: function (range) {
                            quill.insertText(range.index, '\n', 'user');
                            quill.formatLine(range.index + 1, 1, 'comment', false, 'user');
                            quill.setSelection(range.index + 1, 0, 'user');
                        }
                    }
                }
            }
        }
    });

    if (initialHtml) {
        quill.root.innerHTML = initialHtml;
    }

    quill.on('text-change', (delta, oldDelta, source) => {
        if (source !== 'user') return;

        // Editor fully cleared (e.g. select all + delete): Quill keeps the
        // block format of whatever line survives. Reset it to plain text.
        const hasDelete = delta.ops.some(op => typeof op.delete === 'number');
        if (hasDelete && quill.getLength() === 1) {
            quill.formatLine(0, 1, { header: false, list: false, blockquote: false, comment: false }, 'user');
        }

        handleAutoFormat(quill, delta);
    });

    editors[elementId] = quill;
}

function handleAutoFormat(quill, delta) {
    const insertedChar = getInsertedChar(delta);
    if (insertedChar !== ' ' && insertedChar !== '*' && insertedChar !== '_') return;

    const range = quill.getSelection();
    if (!range) return;

    const [line, lineOffset] = quill.getLine(range.index);
    if (!line) return;

    const lineText = quill.getText(range.index - lineOffset, lineOffset);

    // Check line-start patterns (#, -, >) first.
    for (const pattern of LINE_PATTERNS) {
        if (pattern.regex.test(lineText)) {
            applyLineFormat(quill, range.index, lineOffset, pattern);
            return;
        }
    }

    // Check inline patterns (bold, italic).
    for (const pattern of INLINE_PATTERNS) {
        const match = lineText.match(pattern.regex);
        if (match) {
            applyInlineFormat(quill, range.index, match, pattern.format);
            return;
        }
    }
}

// Delta from a single keystroke normally has one 'insert' op; extract its last character.
function getInsertedChar(delta) {
    const insertOp = delta.ops.find(op => typeof op.insert === 'string');
    if (!insertOp) return null;
    return insertOp.insert[insertOp.insert.length - 1];
}

function applyLineFormat(quill, cursorIndex, lineOffset, pattern) {
    const lineStart = cursorIndex - lineOffset;

    quill.history.cutoff();
    quill.deleteText(lineStart, pattern.stripLen, 'user');
    quill.formatLine(lineStart, 1, pattern.format, 'user');
    quill.history.cutoff();
}

function applyInlineFormat(quill, cursorIndex, match, formatName) {
    const matchStart = cursorIndex - match[0].length;
    const innerText = match[1];

    quill.history.cutoff();
    quill.deleteText(matchStart, match[0].length, 'user');
    quill.insertText(matchStart, innerText, { [formatName]: true }, 'user');
    quill.setSelection(matchStart + innerText.length, 0, 'user');
    // Clear the caret's active format, otherwise Quill keeps applying
    // it to whatever is typed next after the formatted text.
    quill.format(formatName, false, 'user');
    quill.history.cutoff();
}

export function getHtml(elementId) {
    const quill = editors[elementId];
    return quill ? quill.root.innerHTML : '';
}

export function setHtml(elementId, html) {
    const quill = editors[elementId];
    if (quill) quill.root.innerHTML = html;
}

export function destroyEditor(elementId) {
    delete editors[elementId];
}
