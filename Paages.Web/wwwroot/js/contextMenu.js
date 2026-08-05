let dotNetRef = null;
let handler = null;
let observer = null;

function clamp(menuElement) {
    const rect = menuElement.getBoundingClientRect();
    const overflowX = rect.right - window.innerWidth;
    const overflowY = rect.bottom - window.innerHeight;

    if (overflowX > 0)
        menuElement.style.left = `${Math.max(8, rect.left - overflowX - 8)}px`;
    if (overflowY > 0)
        menuElement.style.top = `${Math.max(8, rect.top - overflowY - 8)}px`;
}

export function initOutsideClose(menuElementId, dotNetReference) {
    dotNetRef = dotNetReference;

    if (!observer) {
        observer = new MutationObserver(() => {
            const menuElement = document.getElementById(menuElementId);
            if (menuElement) clamp(menuElement);
        });
        observer.observe(document.body, { childList: true, subtree: true });
    }

    if (handler) return;

    handler = (event) => {
        const menuElement = document.getElementById(menuElementId);
        if (!menuElement) return;

        if (!menuElement.contains(event.target))
            dotNetRef.invokeMethodAsync('CloseFromJs');
    };

    document.addEventListener('click', handler, true);
    document.addEventListener('contextmenu', handler, true);
    document.addEventListener('keydown', (event) => {
        if (event.key === 'Escape')
            dotNetRef.invokeMethodAsync('CloseFromJs');
    }, true);
}