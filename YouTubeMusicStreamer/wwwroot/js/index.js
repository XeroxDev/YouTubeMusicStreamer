function handleDeleteButtonClick(event) {
    if (event.target.classList.contains('delete')) {
        if (event.target.parentNode.classList.contains('notification')) {
            event.target.parentNode.remove();
        }
    }
}

function handleCollapsibleClick(event) {
    const trigger = event.target.closest('[data-collapsible]');
    if (!trigger) return;

    const identifier = trigger.getAttribute('data-collapsible');
    const targets = document.querySelectorAll(`[data-collapse-${identifier}]`);

    targets.forEach(target => {
        const toggleClass = target.getAttribute(`data-collapse-${identifier}`);
        target.classList.toggle(toggleClass);
    });
}

document.addEventListener('click', (event) => {
    handleDeleteButtonClick(event);
    handleCollapsibleClick(event);
});