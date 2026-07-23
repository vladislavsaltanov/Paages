// Active Quill instances keyed by elementId, so getHtml/setHtml/destroyEditor know which editor to use.
const editors = {};

// Line-level auto-format patterns, checked after user types space or '*'.
const LINE_PATTERNS = [
    { regex: /^#\s$/, format: { header: 1 }, stripLen: 2 },
    { regex: /^##\s$/, format: { header: 2 }, stripLen: 3 },
    { regex: /^###\s$/, format: { header: 3 }, stripLen: 4 },
    { regex: /^-\s$/, format: { list: 'bullet' }, stripLen: 2 },
    { regex: /^>\s$/, format: { blockquote: true }, stripLen: 2 },
];

// Block formats that revertOnBackspace should be able to clear.
const REVERTIBLE_FORMATS = ['header', 'list', 'blockquote'];

// Inline bold pattern: **text** — handled separately via formatText, not formatLine.
const BOLD_PATTERN = /\*\*([^*]+)\*\*$/;

export function createEditor(elementId, initialHtml) {
    const quill = new Quill('#' + elementId, {
        theme: 'snow',
        modules: {
            toolbar: [['bold', 'italic'], ['blockquote'], [{ header: [1, 2, 3, false] }], [{ list: 'bullet' }]],
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
        handleAutoFormat(quill, delta);
    });

    editors[elementId] = quill;
}

function handleAutoFormat(quill, delta) {
    const insertedChar = getInsertedChar(delta);
    if (insertedChar !== ' ' && insertedChar !== '*') return;

    const range = quill.getSelection();
    if (!range) return;

    const [line, lineOffset] = quill.getLine(range.index);
    if (!line) return;

    const lineText = quill.getText(range.index - lineOffset, lineOffset + 1);

    // Check line-start patterns (#, -, >) first.
    for (const pattern of LINE_PATTERNS) {
        if (pattern.regex.test(lineText)) {
            applyLineFormat(quill, range.index, lineOffset, pattern);
            return;
        }
    }

    // Check inline bold pattern.
    const boldMatch = lineText.match(BOLD_PATTERN);
    if (boldMatch) {
        applyBoldFormat(quill, range.index, boldMatch);
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

function applyBoldFormat(quill, cursorIndex, match) {
    const matchStart = cursorIndex - match[0].length;
    const innerText = match[1];

    quill.history.cutoff();
    quill.deleteText(matchStart, match[0].length, 'user');
    quill.insertText(matchStart, innerText, { bold: true }, 'user');
    quill.setSelection(matchStart + innerText.length, 0, 'user');
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
