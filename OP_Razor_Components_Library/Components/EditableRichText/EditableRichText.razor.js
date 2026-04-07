function focusElement(element) {
    if (!element) {
        return;
    }

    element.focus();
}

export function setHtml(element, html) {
    if (!element) {
        return;
    }

    element.innerHTML = html ?? "";
}

export function focusEditor(element) {
    focusElement(element);
}

export function applyCommand(element, command) {
    focusElement(element);
    document.execCommand(command, false, null);
}

export function applyColor(element, color) {
    focusElement(element);
    document.execCommand("styleWithCSS", false, true);
    document.execCommand("foreColor", false, color);
}

export function applyFontSize(element, size) {
    focusElement(element);
    document.execCommand("fontSize", false, size);
}

export function promptForLink(element) {
    focusElement(element);

    const url = window.prompt("Zadejte URL odkazu");
    if (!url) {
        return;
    }

    document.execCommand("createLink", false, url);
}

export function getHtml(element) {
    if (!element) {
        return "";
    }

    return normalizeHtml(element.innerHTML);
}

function normalizeHtml(html) {
    if (!html) {
        return "";
    }

    return html
        .replace(/<div><br><\/div>/gi, "<br>")
        .replace(/<div>/gi, "<br>")
        .replace(/<\/div>/gi, "")
        .trim();
}
