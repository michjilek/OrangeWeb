let pendingHref = null;
let rafHandle = 0;

function applyStylesheetRefresh(link) { // This function is called in the next animation frame to update the stylesheet href.
    if (!pendingHref) { // If there is no pending href to apply, simply exit. This can happen if the function is called without a valid pendingHref.
        return;
    }

    const nextHref = pendingHref; // Capture the pending href to apply and clear it before updating the DOM to
    pendingHref = null; // Clear pendingHref to allow new updates to be scheduled while this one is being applied.
    rafHandle = 0; // Clear the rafHandle to indicate that there is no longer a pending animation frame, allowing future calls to schedule new updates.

    // Avoid unnecessary style invalidations when URL did not actually change.
    if (link.getAttribute('href') !== nextHref) { // Only update href if it differs from current value to prevent redundant reloads.
        link.setAttribute('href', nextHref); // Update the href to trigger stylesheet reload in the browser.
    }
}

//Beacuse of Font Editor.After Font change, it was problem with refreshing *.css
//in browser. This tell to browser to reload *.css
export function refreshCustomFontStylesheet() {
    // Try to find the <link> element with this specific id in the DOM
    const link = document.getElementById('custom-font-stylesheet');

    // If it doesn't exist, there's nothing to refresh
    if (!link) {
        return;
    }

    // Get the original URL of the stylesheet (of link).
    // Prefer the value stored in data-base-href (stable), otherwise use current href.
    const baseHref = link.getAttribute('data-base-href') || link.getAttribute('href');
    if (!baseHref) {
        return;
    }

    // Build a URL object based on the original href and current origin
    const url = new URL(baseHref, window.location.origin);

    // Add/update a query parameter "v" (version) with the current timestamp
    // This forces the browser as a fresh URL and re-download the CSS
    url.searchParams.set('v', Date.now().toString());

    // Finally, update the <link> element's href with the new versioned URL
    //link.setAttribute('href', `${url.pathname}${url.search}`);

    // Batch DOM write to the next animation frame to reduce forced reflow risk
    // when this function is invoked repeatedly in a short period.
    pendingHref = `${url.pathname}${url.search}`; // Store the new href in pendingHref to be applied in the next animation frame.

    if (rafHandle !== 0) { // If there is already a scheduled animation frame to apply a stylesheet refresh, do not schedule another one to avoid redundant updates.
        return;
    }

    rafHandle = window.requestAnimationFrame(() => applyStylesheetRefresh(link)); // Schedule the applyStylesheetRefresh
                                                                                  // function to run in the next animation frame,
                                                                                  // passing the link element to update its href.
}
