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

function scrollToElementById(elementId) {
    const element = document.getElementById(elementId);
    if (element) {
        element.scrollIntoView({ behavior: 'smooth', block: 'start' });
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

//Every browser on iOS and iPadOS runs on WebKit, as does Safari on macOS. Only those run the app about ten times
//slower without HTTPS (issue #2866); Chrome and Firefox on macOS and everything on Windows, Linux and Android do not.
function isWebKitBrowser() {
    const userAgent = navigator.userAgent || navigator.vendor || window.opera || '';
    if (/iPad|iPhone|iPod/.test(userAgent) && !window.MSStream) {
        return true;
    }
    const isMac = /Macintosh|Mac OS X/.test(userAgent);
    //An iPad reports itself as a Mac, its touch screen tells it apart
    if (isMac && navigator.maxTouchPoints > 1) {
        return true;
    }
    //Chrome, Edge, Opera and Firefox on macOS carry Safari in their user agent as well, but their own name too
    return isMac && /Safari\//.test(userAgent) && !/Chrome|Chromium|Edg\/|OPR\/|Firefox/.test(userAgent);
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

function saveToLocalStorage(key, value) {
    try {
        localStorage.setItem(key, value);
        return true;
    } catch (e) {
        console.error('Error saving to localStorage:', e);
        return false;
    }
}

function readFromLocalStorage(key) {
    try {
        return localStorage.getItem(key);
    } catch (e) {
        console.error('Error reading from localStorage:', e);
        return null;
    }
}