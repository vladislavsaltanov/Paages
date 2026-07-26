// Active Quill instances keyed by elementId, so getHtml/setHtml/destroyEditor know which editor to use.
const editors = {};

// Custom block format for "// comment" lines. A dedicated blot (not reusing the
// built-in italic format) so a manually-italicized line is never mistaken for a
// comment line, and so styling (italic + 60% opacity) is fully controlled by CSS.
const BlockBlot = Quill.import('blots/block');
const Delta = Quill.import('delta');
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

export function createEditor(elementId, initialHtml, dotNetRef) {
    const quill = new Quill('#' + elementId, {
        theme: 'snow',
        modules: {
            // Built-in Snow toolbar disabled: app uses its own floating toolbar.
            toolbar: false,
            clipboard: {
                matchVisual: false
            },
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
                    }, 
                    saveNow: {
                        key: 's',
                        shortKey: true,
                        handler: function () {
                            triggerSave(elementId, true);
                            return false;
                        }
                    }
                }
            }
        }
    });
    
    quill.clipboard.addMatcher(Node.ELEMENT_NODE, (node, delta) => {
        const plainDelta = new Delta();
        delta.ops.forEach(op => {
            if (typeof op.insert === 'string') {
                plainDelta.insert(op.insert);
            }
        });
        return plainDelta;
    });

    if (initialHtml) {
        quill.clipboard.dangerouslyPasteHTML(initialHtml, 'api');
    }

    editors[elementId] = { quill, dotNetRef, saveTimer: null };

    quill.on('text-change', (delta, oldDelta, source) => {
        if (source !== 'user') return;

        const hasDelete = delta.ops.some(op => typeof op.delete === 'number');

        if (hasDelete && quill.getLength() === 1) {
            quill.formatLine(0, 1, { header: false, list: false, blockquote: false, comment: false }, 'user');
        }

        handleAutoFormat(quill, delta);
        scheduleSave(elementId);
    });

    document.addEventListener('keydown', (e) => {
        if ((e.ctrlKey || e.metaKey) && e.key === 's') {
            e.preventDefault();
            triggerSave(elementId);
        }
    }, { capture: true });
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

const SAVE_DEBOUNCE_MS = 1500;
function scheduleSave(elementId) 
{
    const entry = editors[elementId];
    if (!entry) return;

    clearTimeout(entry.saveTimer);
    entry.saveTimer = setTimeout(() => {
        triggerSave(elementId);
    }, SAVE_DEBOUNCE_MS);
}

function triggerSave(elementId, immediate = false) {
    const entry = editors[elementId];
    if (!entry || !entry.dotNetRef) return;

    clearTimeout(entry.saveTimer);
    const html = entry.quill.root.innerHTML;
    entry.dotNetRef.invokeMethodAsync('OnContentChanged', html);
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
    const entry = editors[elementId];
    return entry ? entry.quill.root.innerHTML : '';
}

export function setHtml(elementId, html) {
    const entry = editors[elementId];
    if (entry) entry.quill.clipboard.dangerouslyPasteHTML(html, 'api');
}

export function destroyEditor(elementId) {
    const entry = editors[elementId];
    if (entry) clearTimeout(entry.saveTimer);
    delete editors[elementId];
}