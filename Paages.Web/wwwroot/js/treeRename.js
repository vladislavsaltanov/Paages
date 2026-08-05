export function focusAndSelect(elementId) {
    requestAnimationFrame(() => {
        const el = document.getElementById(elementId);
        if (!el) return;
        el.focus();
        el.select();
    });
}