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
