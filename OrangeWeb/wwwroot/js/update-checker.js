(() => {
    // URL of a small JSON file served by app that contains the latest deployed version,
    const VERSION_URL = "/app-version.json";
    // How often to check for a newer version (5 minutes).
    const CHECK_INTERVAL_MS = 5 * 60 * 1000;
    // localStorage key where we keep the last version this browser has acknowledged.
    const STORAGE_KEY = "appVersion";

    // Reload the page while busting caches by appending/updating a `v` query parameter.
    // This helps ensure the browser fetches fresh assets for the new version.
    const bustCacheReload = (version) => {
        const url = new URL(window.location.href);
        url.searchParams.set("v", version);
        window.location.replace(url.toString());
    };

    // Fetch the latest version string from VERSION_URL with caching disabled.
    // Returns the version string, or null if anything goes wrong.
    const fetchVersion = async () => {
        const response = await fetch(VERSION_URL, {
            cache: "no-store",
            headers: {
                "Cache-Control": "no-cache"
            }
        });

        // If the request failed, treat as "no update info available".
        if (!response.ok) {
            return null;
        }

        // Parse JSON and validate it contains a string `version` field.
        const data = await response.json();
        if (!data || typeof data.version !== "string") {
            return null;
        }

        return data.version;
    };

    // Compare the fetched "latest" version with what we have stored locally.
    // If different, prompt the user to reload.
    const checkForUpdates = async () => {
        const latestVersion = await fetchVersion();
        if (!latestVersion) {
            return;
        }
     /*   latestKnownVersion = latestVersion;*/

        const storedVersion = localStorage.getItem(STORAGE_KEY);
    /*    const pendingVersion = localStorage.getItem(PENDING_KEY);*/

        // First run: store the current version and do not prompt.
        if (!storedVersion) {
            localStorage.setItem(STORAGE_KEY, latestVersion);
            return;
        }

        // If a new version is deployed since last acknowledged, show the modal.
        if (storedVersion !== latestVersion) {
            localStorage.setItem(STORAGE_KEY, latestVersion);
            bustCacheReload(latestVersion);
        }
    };

    // When the tab becomes visible again, re-check (useful if a deploy happened while user was away).
    document.addEventListener("visibilitychange", () => {
        if (document.visibilityState === "visible") {
            void checkForUpdates();
        }
    });

    // Initial check on page load.
    void checkForUpdates();

    // Periodic checks every CHECK_INTERVAL_MS.
    setInterval(() => {
        void checkForUpdates();
    }, CHECK_INTERVAL_MS);
})();
