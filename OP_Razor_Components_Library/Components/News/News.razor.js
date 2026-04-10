function getMeasurementElements(root) {
    return Array.from(root?.querySelectorAll(".news-content--measure") ?? []);
}

export function syncNewsContentHeight(root) {
    if (!root) {
        return;
    }

    const measurementElements = getMeasurementElements(root);
    if (measurementElements.length === 0) {
        root.style.removeProperty("--news-content-min-height");
        return;
    }

    // Use the tallest hidden variant so switching items does not change the visible card height.
    let maxHeight = 0;
    for (const element of measurementElements) {
        maxHeight = Math.max(maxHeight, Math.ceil(element.getBoundingClientRect().height));
    }

    if (maxHeight > 0) {
        root.style.setProperty("--news-content-min-height", `${maxHeight}px`);
    }
}

export function initializeNewsAutoHeight(root) {
    if (!root || root.__newsAutoHeight) {
        return;
    }

    let rafId = 0;
    const recalc = () => {
        // Batch recalculation into the next frame to avoid repeated forced layouts.
        if (rafId !== 0) {
            window.cancelAnimationFrame(rafId);
        }

        rafId = window.requestAnimationFrame(() => {
            rafId = 0;
            syncNewsContentHeight(root);
        });
    };

    // Watch both the visible root and the hidden measurement container for responsive layout changes.
    const resizeObserver = new ResizeObserver(recalc);
    resizeObserver.observe(root);

    const measurementContainer = root.querySelector(".news-measurements");
    if (measurementContainer) {
        resizeObserver.observe(measurementContainer);
    }

    // Recalculate once web fonts finish loading because text wrapping can change.
    if (document.fonts?.ready) {
        document.fonts.ready.then(recalc);
    }

    root.__newsAutoHeight = {
        recalc,
        resizeObserver
    };

    recalc();
}

export function disposeNewsAutoHeight(root) {
    const state = root?.__newsAutoHeight;
    if (!state) {
        return;
    }

    state.resizeObserver?.disconnect();
    delete root.__newsAutoHeight;
}
