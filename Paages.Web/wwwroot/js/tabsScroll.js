const instancies = {};

export function initTabsScroll(scrollElementId, rowElementId){
    const scrollEl = document.getElementById(scrollElementId);
    const rowEl = document.getElementById(rowElementId);
    if (!scrollEl || !rowEl) return;

    const update = () =>
    {
        rowEl.classList.toggle('can-left', scrollEl.scrollLeft > 0);
        rowEl.classList.toggle('can-right', scrollEl.scrollLeft < scrollEl.scrollWidth - scrollEl.clientWidth - 1);
    };

    scrollEl.addEventListener('scroll', update);

    const observer = new ResizeObserver(update);
    observer.observe(scrollEl);

    instancies[scrollElementId] = { scrollEl, update, observer };
    update();
}

export function scrollByAmount(scrollElementId, delta){
    const entry = instancies[scrollElementId];
    if (!entry) return;

    entry.scrollEl.scrollBy({ left: delta, behavior: 'smooth' });
}

export function destroyTabsScroll(scrollElementId){
    const entry = instancies[scrollElementId];
    if (!entry) return;

    entry.scrollEl.removeEventListener('scroll', entry.update);
    entry.observer.disconnect();
    delete instancies[scrollElementId];
}

export function refreshScrollState(scrollElementId) {
    const entry = instancies[scrollElementId];
    if (!entry) return;
    entry.update();
}