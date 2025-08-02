// Original function for simple downloads (auto backups)
window.triggerFileDownload = (fileName, url) => {
    const anchorElement = document.createElement('a');
    anchorElement.href = url;
    anchorElement.download = fileName ?? '';
    anchorElement.click();
    anchorElement.remove();
}

window.triggerFileDownloadWithCallback = (fileName, url, cookieName, dotNetRef, callbackMethod) => {
    // Set up cookie monitoring
    const checkCookie = setInterval(() => {
        if (document.cookie.indexOf(cookieName + '=true') !== -1) {
            clearInterval(checkCookie);
            // Remove the cookie
            document.cookie = cookieName + '=; expires=Thu, 01 Jan 1970 00:00:00 UTC; path=/;';
            // Notify Blazor
            dotNetRef.invokeMethodAsync(callbackMethod);
        }
    }, 100); // Check every 100ms

    // Trigger download
    const anchorElement = document.createElement('a');
    anchorElement.href = url;
    anchorElement.download = fileName ?? '';
    anchorElement.click();
    anchorElement.remove();

    // Timeout after 60 seconds (for very slow connections)
    setTimeout(() => {
        clearInterval(checkCookie);
        dotNetRef.invokeMethodAsync(callbackMethod);
    }, 60000);
};