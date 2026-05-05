const MATERIAL_SYMBOLS_ID = "material-symbols-stylesheet";
const MATERIAL_SYMBOLS_HREF = "https://fonts.googleapis.com/css2?family=Material+Symbols+Outlined:opsz,wght,FILL,GRAD@24,400,0,0&icon_names=account_balance,apartment,badge,format_bold,format_clear,format_color_text,format_italic,format_size,format_underlined,history_toggle_off,link,mail,mobile,nest_clock_farsight_analog,person,send";

function ensureMaterialSymbolsStylesheet() {
    let link = document.getElementById(MATERIAL_SYMBOLS_ID);
    if (link) {
        return Promise.resolve(link);
    }

    link = document.createElement("link");
    link.id = MATERIAL_SYMBOLS_ID;
    link.rel = "stylesheet";
    link.href = MATERIAL_SYMBOLS_HREF;

    return new Promise((resolve, reject) => {
        link.addEventListener("load", () => resolve(link), { once: true });
        link.addEventListener("error", reject, { once: true });
        document.head.appendChild(link);
    });
}

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
        await ensureMaterialSymbolsStylesheet();
        await document.fonts.load("24px 'Material Symbols Outlined'");
        await document.fonts.ready;
    } catch {
        return;
    }

    root.classList.add("material-symbols-ready");
}
