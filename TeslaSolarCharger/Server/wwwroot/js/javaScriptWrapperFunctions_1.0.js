function setFocus(elementId) {
    const element = document.getElementById(elementId);
    if (element) {
        element.focus();
        return true;
    }
    return false;
}

function removeFocus(elementId) {
    const element = document.getElementById(elementId);
    if (element) {
        element.blur();
        return true;
    }
    return false;
}

function openInNewTab(url) {
    window.open(url, '_blank');
}

function detectDevice() {
    var ua = navigator.userAgent || navigator.vendor || window.opera;
    // iOS detection
    if (/iPad|iPhone|iPod/.test(ua) && !window.MSStream) {
        return "iOS";
    }
    return "Other";
}

function getTimeZone() {
    return Intl.DateTimeFormat().resolvedOptions().timeZone;
}

async function forceHardReload() {
    // Clear all caches if service workers are being used
    if ('caches' in window) {
        const cacheNames = await caches.keys();
        await Promise.all(
            cacheNames.map(cacheName => caches.delete(cacheName))
        );
    }

    // Unregister service workers if any
    if ('serviceWorker' in navigator) {
        const registrations = await navigator.serviceWorker.getRegistrations();
        for (let registration of registrations) {
            await registration.unregister();
        }
    }

    // Force reload
    location.reload(true);
}