export function getRelativeY(elementId, clientY) {
    const el = document.getElementById(elementId);
    if (!el) return 0.5;
    const rect = el.getBoundingClientRect();
    return (clientY - rect.top) / rect.height;
}

let lastInsideRow = null;

export function initTreeDragOver(containerId) {
    const container = document.getElementById(containerId);
    if (!container) return;
    const line = container.querySelector('.drop-line');

    container.addEventListener('dragover', (e) => {
        const row = e.target.closest('.sb-tree-row');
        if (!row) { hideIndicator(line); return; }

        const rect = row.getBoundingClientRect();
        const treeRect = line.parentElement.getBoundingClientRect(); // .sb-tree, not the delegated aside container
        const relY = (e.clientY - rect.top) / rect.height;
        const isFolder = row.dataset.isFolder === 'true';

        if (lastInsideRow && lastInsideRow !== row) {
            lastInsideRow.classList.remove('drop-inside');
            lastInsideRow = null;
        }

        if (isFolder && relY >= 0.4 && relY <= 0.6) {
            line.style.display = 'none';
            row.classList.add('drop-inside');
            lastInsideRow = row;
        } else {
            row.classList.remove('drop-inside');
            if (lastInsideRow === row) lastInsideRow = null;

            line.style.display = 'block';
            line.style.left = (rect.left - treeRect.left) + 'px';
            line.style.width = rect.width + 'px';
            line.style.top = (relY < 0.5 ? rect.top : rect.bottom) - treeRect.top + 'px';
        }
    });
}

function hideIndicator(line) {
    line.style.display = 'none';
    if (lastInsideRow) {
        lastInsideRow.classList.remove('drop-inside');
        lastInsideRow = null;
    }
}

export function clearDragIndicator() {
    // global sweep, not reliant on a possibly-stale row reference
    document.querySelectorAll('.drop-line').forEach(l => l.style.display = 'none');
    document.querySelectorAll('.sb-tree-row.drop-inside').forEach(r => r.classList.remove('drop-inside'));
    lastInsideRow = null;
}