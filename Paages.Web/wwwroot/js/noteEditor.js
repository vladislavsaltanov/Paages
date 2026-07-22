// Хранилище активных экземпляров Quill по elementId,
// чтобы getHtml/setHtml/destroyEditor знали, к какому редактору обращаться.
const editors = {};

// Regex-паттерны auto-format: строка целиком проверяется на совпадение
// после того как пользователь напечатал пробел или '*'.
const LINE_PATTERNS = [
    { regex: /^#\s$/, format: { header: 1 }, stripLen: 2 },
    { regex: /^##\s$/, format: { header: 2 }, stripLen: 3 },
    { regex: /^###\s$/, format: { header: 3 }, stripLen: 4 },
    { regex: /^-\s$/, format: { list: 'bullet' }, stripLen: 2 },
    { regex: /^>\s$/, format: { blockquote: true }, stripLen: 2 },
];

// Паттерн для инлайн-жирного: **текст** — проверяется отдельно,
// так как это не форматирование строки целиком (formatLine), а форматирование диапазона (formatText).
const BOLD_PATTERN = /\*\*([^*]+)\*\*$/;
