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

// Horizontal rule as a block embed - hr has no text content, so it can't reuse
// the header/blockquote line-format pattern used elsewhere in this file.
const BlockEmbed = Quill.import('blots/block/embed');
class DividerBlot extends BlockEmbed {
    static blotName = 'divider';
    static tagName = 'hr';
}
Quill.register(DividerBlot, true);

// Line-level auto-format patterns, checked after user types space, '*' or '_'.
const LINE_PATTERNS = [
    { regex: /^#\s$/, format: { header: 1 }, stripLen: 2 },
    { regex: /^##\s$/, format: { header: 2 }, stripLen: 3 },
    { regex: /^###\s$/, format: { header: 3 }, stripLen: 4 },
    { regex: /^-\s$/, format: { list: 'bullet' }, stripLen: 2 },
    { regex: /^>\s$/, format: { blockquote: true }, stripLen: 2 },
    { regex: /^\/\/\s$/, format: { comment: true }, stripLen: 3 },
    { regex: /^```$/, format: { 'code-block': true }, stripLen: 3 },
];
const PASTE_BLOCK_PATTERNS = [
    { regex: /^###\s+/, format: { header: 3 } },
    { regex: /^##\s+/, format: { header: 2 } },
    { regex: /^#\s+/, format: { header: 1 } },
    { regex: /^-\s+/, format: { list: 'bullet' } },
    { regex: /^>\s+/, format: { blockquote: true } },
    { regex: /^\/\/\s+/, format: { comment: true } },
];
// Prefix text for reveal-on-selection: reformats one of these back to raw markdown.
const REVEAL_PREFIXES = {
    header: { 1: '# ', 2: '## ', 3: '### ' },
    blockquote: '> ',
    comment: '// '
};

// Block formats that revertOnBackspace should be able to clear.
const REVERTIBLE_FORMATS = ['header', 'list', 'blockquote', 'comment', 'code-block'];

// Inline patterns: **text** for bold, _text_ for italic — handled via formatText, not formatLine.
const BOLD_PATTERN = { regex: /\*\*([^*]+)\*\*$/, format: 'bold' };
const ITALIC_PATTERN = { regex: /_([^_]+)_$/, format: 'italic' };
const CODE_PATTERN = { regex: /`([^`]+)`$/, format: 'code' };
const INLINE_PATTERNS = [BOLD_PATTERN, ITALIC_PATTERN, CODE_PATTERN];

export function createEditor(elementId, initialHtml, dotNetRef) {
    if (!document.getElementById(elementId)) return; // note switched away before mount landed

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
                    },
                    // ArrowRight at the end of an inline code run: caret stays trapped
                    // in the 'code' format otherwise, so plain text keeps appearing as code.
                    exitInlineCode: {
                        key: 'ArrowRight',
                        format: ['code'],
                        handler: function (range) {
                            const nextFormat = quill.getFormat(range.index + 1);
                            if (nextFormat.code) return true; // still inside the run, move normally

                            quill.setSelection(range.index + 1, 0, 'user');
                            quill.format('code', false, 'user');
                            return false;
                        }
                    },
                    // '---' on its own line, followed by Enter, becomes a horizontal rule.
                    insertDivider: {
                        key: 'Enter',
                        handler: function (range, context) {
                            if (context.prefix !== '---') return true; // not a divider line, normal Enter

                            const lineStart = range.index - 3;
                            quill.deleteText(lineStart, 3, 'user');
                            quill.insertEmbed(lineStart, 'divider', true, 'user');
                            quill.insertText(lineStart + 1, '\n', 'user');
                            quill.setSelection(lineStart + 2, 0, 'user');
                            return false;
                        }
                    }
                }
            }
        }
    });
    let isInitialLoad = true;

    if (initialHtml) {
        quill.root.innerHTML = initialHtml;
    } else {
        isInitialLoad = false;
    }

    quill.on('text-change', (delta, oldDelta, source) => {
        if (isInitialLoad) {
            isInitialLoad = false;
            return;
        }
        if (source !== 'user') return;

        const hasDelete = delta.ops.some(op => typeof op.delete === 'number');

        if (hasDelete && quill.getLength() === 1) {
            quill.formatLine(0, 1, { header: false, list: false, blockquote: false, comment: false }, 'user');
        }

        const entry = editors[elementId];
        if (!entry) return;
        handleAutoFormat(quill, delta, entry);
        scheduleSave(elementId);
        scrollCursorIntoView(quill);
    });

    quill.on('selection-change', (range, oldRange, source) => {
        if (source !== 'user') return;

        const entry = editors[elementId];
        if (!entry) return;

        const currentLines = !range
            ? new Set()
            : range.length > 0
                ? new Set(quill.getLines(range.index, range.length))
                : new Set([quill.getLine(range.index)[0]]);

        for (const blot of entry.revealed) {
            if (!currentLines.has(blot)) {
                concealLine(quill, blot);
                entry.revealed.delete(blot);
            }
        }

        if (entry.revealedCodeBlock) {
            const stillInside = [...entry.revealedCodeBlock.blots].some(b => currentLines.has(b));
            if (!stillInside) {
                concealCodeBlock(quill, entry.revealedCodeBlock.blots);
                entry.revealedCodeBlock = null;
            }
        }

        if (range && range.length > 0) {
            for (const blot of currentLines) {
                if (isCodeBlockLine(blot)) {
                    if (!entry.revealedCodeBlock) {
                        const newLines = revealCodeBlock(quill, blot);
                        entry.revealedCodeBlock = { blots: new Set(newLines) };
                    }
                } else if (!entry.revealed.has(blot)) {
                    const revealedBlot = revealLine(quill, blot);
                    if (revealedBlot) entry.revealed.add(revealedBlot);
                }
            }
        }
    });

    const handleKeydown = (e) => {
        if ((e.ctrlKey || e.metaKey) && e.key === 's') {
            e.preventDefault();
            triggerSave(elementId);
        }
    };
    document.addEventListener('keydown', handleKeydown, { capture: true });

    const handlePaste = (e) => {
        if (!quill.root.contains(e.target)) return;
        e.preventDefault();
        e.stopPropagation();

        const text = (e.clipboardData || window.clipboardData).getData('text/plain');
        insertMarkdownPaste(quill, text);
    };
    document.addEventListener('paste', handlePaste, true);

    editors[elementId] = { quill, dotNetRef, saveTimer: null, handleKeydown, handlePaste, revealed: new Set(), revealedCodeBlock: null };
}

function parseInlineRuns(text) {
    const ops = [];
    const regex = /\*\*([^*]+)\*\*|_([^_]+)_|`([^`]+)`/g;
    let lastIndex = 0, match;
    while ((match = regex.exec(text)) !== null) {
        if (match.index > lastIndex) ops.push({ insert: text.slice(lastIndex, match.index) });
        if (match[1] !== undefined) ops.push({ insert: match[1], attributes: { bold: true } });
        else if (match[2] !== undefined) ops.push({ insert: match[2], attributes: { italic: true } });
        else ops.push({ insert: match[3], attributes: { code: true } });
        lastIndex = regex.lastIndex;
    }
    if (lastIndex < text.length) ops.push({ insert: text.slice(lastIndex) });
    return ops;
}

function insertMarkdownPaste(quill, text) {
    const range = quill.getSelection(true);
    if (!range) return;

    const insideCodeBlock = !!quill.getFormat(range.index)['code-block'];
    const lines = text.replace(/\r\n/g, '\n').split('\n');
    if (lines.length > 1 && lines[lines.length - 1] === '') lines.pop(); // drop empty line from a copied full line

    const content = new Delta();
    let insertedLength = 0;

    if (insideCodeBlock) {
        // Raw text inside an active code block - no markdown parsing, keep code-block on every line.
        lines.forEach((line, i) => {
            content.insert(line);
            insertedLength += line.length;
            if (i < lines.length - 1) {
                content.insert('\n', { 'code-block': true });
                insertedLength += 1;
            } else {
                content.retain(1, { 'code-block': true }); // reuse existing closing '\n'
            }
        });
    } else {
        lines.forEach((line, i) => {
            let blockFormat = null;
            let lineText = line;
            for (const p of PASTE_BLOCK_PATTERNS) {
                if (p.regex.test(line)) {
                    blockFormat = p.format;
                    lineText = line.replace(p.regex, '');
                    break;
                }
            }

            parseInlineRuns(lineText).forEach(op => {
                content.insert(op.insert, op.attributes);
                insertedLength += op.insert.length;
            });

            if (i < lines.length - 1) {
                content.insert('\n', blockFormat || undefined);
                insertedLength += 1;
            } else if (blockFormat) {
                content.retain(1, blockFormat); // reuse the existing closing '\n' instead of inserting a new one
            }
        });
    }

    quill.updateContents(
        new Delta().retain(range.index).delete(range.length).concat(content),
        'user'
    );
    quill.setSelection(range.index + insertedLength, 0, 'user');
}

function handleAutoFormat(quill, delta, entry) {
    const insertedChar = getInsertedChar(delta);
    if (insertedChar !== ' ' && insertedChar !== '*' && insertedChar !== '_' && insertedChar !== '`') return;

    const range = quill.getSelection();
    if (!range) return;

    const [line, lineOffset] = quill.getLine(range.index);
    if (!line) return;

    if (entry.revealed.has(line) || (entry.revealedCodeBlock && entry.revealedCodeBlock.blots.has(line))) return;

    const lineText = quill.getText(range.index - lineOffset, lineOffset);

    const currentFormat = quill.getFormat(range.index);
    if (currentFormat['code-block'] && lineText === '```') {
        quill.history.cutoff();
        quill.deleteText(range.index - lineOffset, 3, 'user');
        quill.formatLine(range.index - lineOffset, 1, { 'code-block': false }, 'user');
        quill.history.cutoff();
        return;
    }

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

// Keeps the caret visible when it approaches the edge of the scrolling container.
// Needed because the scrollable element is .note-editor (an ancestor of .ql-editor),
// not .ql-editor itself, so the browser's native caret-follow scrolling doesn't apply.
const SCROLL_MARGIN = 100;
function scrollCursorIntoView(quill) {
    const selection = quill.getSelection();
    if (!selection) return;

    const scrollContainer = quill.root.closest('.note-editor');
    if (!scrollContainer) return;

    const bounds = quill.getBounds(selection.index);
    const editorRect = quill.root.getBoundingClientRect();
    const containerRect = scrollContainer.getBoundingClientRect();

    const cursorBottom = editorRect.top + bounds.bottom;
    const cursorTop = editorRect.top + bounds.top;

    if (cursorBottom > containerRect.bottom - SCROLL_MARGIN) {
        scrollContainer.scrollTop += (cursorBottom - containerRect.bottom + SCROLL_MARGIN);
    } else if (cursorTop < containerRect.top + SCROLL_MARGIN) {
        scrollContainer.scrollTop -= (containerRect.top + SCROLL_MARGIN - cursorTop);
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
    const html = getCleanHtml(entry);
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

function buildRawLine(ops, targetOffsets) {
    let raw = '';
    let formattedPos = 0;
    const sorted = [...targetOffsets].sort((a, b) => a - b);
    const offsetMap = new Map();
    let targetIdx = 0;

    ops.forEach(op => {
        if (typeof op.insert !== 'string') return; // skip embeds
        const text = op.insert;
        const attrs = op.attributes || {};
        const marker = attrs.code ? '`' : attrs.bold ? '**' : attrs.italic ? '_' : '';
        const runEnd = formattedPos + text.length;

        while (targetIdx < sorted.length && sorted[targetIdx] <= runEnd) {
            const inner = Math.max(0, sorted[targetIdx] - formattedPos);
            offsetMap.set(sorted[targetIdx], raw.length + marker.length + inner);
            targetIdx++;
        }

        raw += marker + text + marker;
        formattedPos = runEnd;
    });
    while (targetIdx < sorted.length) {
        offsetMap.set(sorted[targetIdx], raw.length);
        targetIdx++;
    }

    return { text: raw, offsetMap };
}

function revealLine(quill, blot) {
    const index = quill.getIndex(blot);
    const length = blot.length() - 1;
    const format = quill.getFormat(index);

    const blockPrefix = format.header ? REVEAL_PREFIXES.header[format.header]
        : format.blockquote ? REVEAL_PREFIXES.blockquote
        : format.comment ? REVEAL_PREFIXES.comment
        : '';

    const ops = quill.getContents(index, length).ops;
    const hasInline = ops.some(op => op.attributes && (op.attributes.bold || op.attributes.italic || op.attributes.code));
    if (!blockPrefix && !hasInline) return null;

    const preSelection = quill.getSelection();
    const targets = preSelection
        ? [preSelection.index - index, preSelection.index + preSelection.length - index]
            .map(v => Math.max(0, Math.min(length, v)))
        : [];

    const { text: inlineText, offsetMap } = buildRawLine(ops, targets);
    const rawText = blockPrefix + inlineText;

    quill.updateContents(new Delta().retain(index).delete(length).insert(rawText), 'silent');
    quill.formatLine(index, 1, { header: false, blockquote: false, comment: false }, 'silent');

    if (preSelection) {
        const rawStart = blockPrefix.length + offsetMap.get(targets[0]);
        const rawEnd = blockPrefix.length + offsetMap.get(targets[1]);
        quill.setSelection(index + rawStart, rawEnd - rawStart, 'silent');
    } else {
        quill.setSelection(index, rawText.length, 'silent');
    }

    return quill.getLine(index)[0];
}

function concealLine(quill, blot) {
    const index = quill.getIndex(blot);
    const length = blot.length() - 1;
    const lineText = quill.getText(index, length);

    let blockFormat = null;
    let remainingText = lineText;
    for (const p of PASTE_BLOCK_PATTERNS) {
        const match = lineText.match(p.regex);
        if (match) {
            blockFormat = p.format;
            remainingText = lineText.slice(match[0].length);
            break;
        }
    }

    const content = new Delta().retain(index).delete(length);
    parseInlineRuns(remainingText).forEach(op => content.insert(op.insert, op.attributes));
    quill.updateContents(content, 'silent');

    if (blockFormat) quill.formatLine(index, 1, blockFormat, 'silent');

    return quill.getLine(index)[0];
}

function revealCodeBlock(quill, blot) {
    const { first, last } = getCodeBlockExtent(blot);
    const startIndex = quill.getIndex(first);
    const endIndex = quill.getIndex(last) + last.length();
    const contentLength = endIndex - startIndex - 1;

    const contentText = quill.getText(startIndex, contentLength);
    const rawText = '```\n' + contentText + '\n```';

    quill.updateContents(new Delta().retain(startIndex).delete(contentLength).insert(rawText), 'silent');
    quill.formatLine(startIndex, rawText.length + 1, { 'code-block': false }, 'silent');

    return quill.getLines(startIndex, rawText.length + 1);
}

function concealCodeBlock(quill, blots) {
    const sorted = [...blots].sort((a, b) => quill.getIndex(a) - quill.getIndex(b));
    const first = sorted[0], last = sorted[sorted.length - 1];
    const startIndex = quill.getIndex(first);
    const contentLength = quill.getIndex(last) + last.length() - startIndex - 1;

    const rawText = quill.getText(startIndex, contentLength);
    const lines = rawText.split('\n');
    if (lines.length < 2 || lines[0] !== '```' || lines[lines.length - 1] !== '```') {
        return sorted; // fences got edited away - leave as plain text
    }
    const codeText = lines.slice(1, -1).join('\n');

    quill.updateContents(new Delta().retain(startIndex).delete(contentLength).insert(codeText), 'silent');
    quill.formatLine(startIndex, codeText.length + 1, { 'code-block': true }, 'silent');

    return quill.getLines(startIndex, codeText.length + 1);
}

function isCodeBlockLine(blot) {
    return !!(blot && blot.statics && blot.statics.blotName === 'code-block');
}

function getCodeBlockExtent(blot) {
    let first = blot, last = blot;
    while (isCodeBlockLine(first.prev)) first = first.prev;
    while (isCodeBlockLine(last.next)) last = last.next;
    return { first, last };
}

function getCleanHtml(entry) {
    if (entry.revealed.size === 0 && !entry.revealedCodeBlock) return entry.quill.root.innerHTML;

    const concealedBlots = [...entry.revealed].map(blot => concealLine(entry.quill, blot));
    const concealedCodeLines = entry.revealedCodeBlock
        ? concealCodeBlock(entry.quill, entry.revealedCodeBlock.blots)
        : null;

    const html = entry.quill.root.innerHTML;

    entry.revealed.clear();
    for (const blot of concealedBlots) {
        const revealedBlot = revealLine(entry.quill, blot);
        if (revealedBlot) entry.revealed.add(revealedBlot);
    }

    if (concealedCodeLines) {
        const newLines = revealCodeBlock(entry.quill, concealedCodeLines[0]);
        entry.revealedCodeBlock = { blots: new Set(newLines) };
    }

    return html;
}

export function getHtml(elementId) {
    const entry = editors[elementId];
    return entry ? entry.quill.root.innerHTML : '';
}

export function setHtml(elementId, html) {
    const entry = editors[elementId];
    if (entry) entry.quill.clipboard.dangerouslyPasteHTML(html, 'api');
}

export async function destroyEditor(elementId) {
    const entry = editors[elementId];
    if (entry) {
        if (entry.saveTimer) {
            clearTimeout(entry.saveTimer);
            const html = getCleanHtml(entry);
            await entry.dotNetRef.invokeMethodAsync('OnContentChanged', html);
        }
        document.removeEventListener('keydown', entry.handleKeydown, { capture: true });
        document.removeEventListener('paste', entry.handlePaste, true);

        if (entry.titleEl) {
            clearTimeout(entry.titleSaveTimer);
            entry.titleEl.removeEventListener('input', entry.handleTitleInput);
            entry.titleEl.removeEventListener('blur', entry.handleTitleBlur);
            entry.titleEl.removeEventListener('keydown', entry.handleTitleKeydown);
        }
    }
    delete editors[elementId];
}

export function bindTitleEditing(elementId, titleElementId, dotNetRef) {
    const titleEl = document.getElementById(titleElementId);
    if (!titleEl) return;

    const entry = editors[elementId];
    if (!entry) return;

    const scheduleTitleSave = () => {
        clearTimeout(entry.titleSaveTimer);
        entry.titleSaveTimer = setTimeout(() => {
            dotNetRef.invokeMethodAsync('OnTitleChanged', titleEl.textContent);
        }, SAVE_DEBOUNCE_MS);
    };

    const flushTitleSave = () => {
        clearTimeout(entry.titleSaveTimer);
        dotNetRef.invokeMethodAsync('OnTitleChanged', titleEl.textContent);
    };

    entry.handleTitleInput = () => scheduleTitleSave();
    entry.handleTitleBlur = () => flushTitleSave();
    entry.handleTitleKeydown = (e) => {
        if (e.key === 'Enter') {
            e.preventDefault();
            titleEl.blur();
        }
    };
    entry.titleEl = titleEl;

    titleEl.addEventListener('input', entry.handleTitleInput);
    titleEl.addEventListener('blur', entry.handleTitleBlur);
    titleEl.addEventListener('keydown', entry.handleTitleKeydown);
}