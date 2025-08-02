// Original function for simple downloads (auto backups)
window.triggerFileDownload = (fileName, url) => {
    const anchorElement = document.createElement('a');
    anchorElement.href = url;
    anchorElement.download = fileName ?? '';
    anchorElement.click();
    anchorElement.remove();
}

window.triggerFileDownloadWithCallback = (fileName, url, cookieName, dotNetRef, callbackMethod) => {
    let callbackInvoked = false;
    let checkCookie;

    const invokeCallback = () => {
        if (callbackInvoked) return;
        callbackInvoked = true;

        if (checkCookie) {
            clearInterval(checkCookie);
        }

        // Clean up cookie more thoroughly
        const cookieCleanup = `${cookieName}=; expires=Thu, 01 Jan 1970 00:00:00 UTC; path=/; domain=${window.location.hostname};`;
        document.cookie = cookieCleanup;

        try {
            dotNetRef.invokeMethodAsync(callbackMethod);
        } catch (error) {
            console.error('Failed to invoke callback:', error);
        }
    };

    // Set up cookie monitoring
    checkCookie = setInterval(() => {
        if (document.cookie.indexOf(cookieName + '=true') !== -1) {
            invokeCallback();
        }
    }, 500);

    // Trigger download
    const anchorElement = document.createElement('a');
    anchorElement.href = url;
    anchorElement.download = fileName ?? '';
    anchorElement.click();
    anchorElement.remove();

    // Timeout and cleanup on page unload
    const timeout = setTimeout(invokeCallback, 60000);

    const cleanup = () => {
        clearTimeout(timeout);
        if (checkCookie) {
            clearInterval(checkCookie);
        }
    };

    window.addEventListener('beforeunload', cleanup, { once: true });
};