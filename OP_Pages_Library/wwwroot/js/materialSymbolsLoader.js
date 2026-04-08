export async function ensureMaterialSymbolsReady() {
    const root = document.documentElement;

    if (root.classList.contains("material-symbols-ready")) {
        return;
    }

    if (!("fonts" in document) || typeof document.fonts.load !== "function") {
        root.classList.add("material-symbols-ready");
        return;
    }

    try {
        await document.fonts.load("24px 'Material Symbols Outlined'");
        await document.fonts.ready;
    } catch {
        return;
    }

    root.classList.add("material-symbols-ready");
}
