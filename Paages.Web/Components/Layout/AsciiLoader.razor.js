document.querySelectorAll('.ascii-loader').forEach(el => {
    const form = document.getElementById(el.dataset.formId);
    if (!form) return;

    const frames = JSON.parse(el.dataset.frames);
    const interval = Number(el.dataset.interval) || 120;
    const frameEl = el.querySelector('.ascii-loader-frame');
    let timer = null;

    form.addEventListener('submit', () => {
        clearInterval(timer);

        el.hidden = false;
        let i = 0;
        frameEl.textContent = frames[0];
        timer = setInterval(() => {
            i = (i + 1) % frames.length;
            frameEl.textContent = frames[i];
        }, interval);
    });
});