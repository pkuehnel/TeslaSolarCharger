function countVisibleLineBreaks(textareaId) {
    console.log("Textarea id:", textareaId);
    let textarea = document.getElementById(textareaId);
    let lineHeight = parseInt(getComputedStyle(textarea).lineHeight);
    console.log("Textarea lineHeight:", lineHeight);
    let height = textarea.scrollHeight;
    console.log("Textarea height:", height);
    return Math.floor(height / lineHeight);
}

function isInputTextCutOff(inputId) {
    // Create a temporary span to measure the text width
    const span = document.createElement("span");
    span.style.position = "absolute";  // Position it out of the flow
    span.style.top = "-9999px";        // Make sure it's off-screen
    span.style.left = "-9999px";
    span.style.whiteSpace = "pre";  // Prevents the content from wrapping
    let inputElement = document.getElementById(inputId);
    if (!(inputElement instanceof Element)) {
        return false;
    }
    span.style.font = getComputedStyle(inputElement).font; // Match the input's font

    // Use the value of the input as the text content of the span
    span.textContent = inputElement.value;

    document.body.appendChild(span);

    const textWidth = span.getBoundingClientRect().width;
    const inputWidth = inputElement.getBoundingClientRect().width -
        parseInt(getComputedStyle(inputElement).paddingLeft) -
        parseInt(getComputedStyle(inputElement).paddingRight);

    // Cleanup: Remove the temporary span from the DOM
    document.body.removeChild(span);

    // Compare widths and return the result
    return textWidth > inputWidth;
}

function setFocusToInput(elementId) {
    const element = document.getElementById(elementId);
    if (element) {
        element.focus();
    }
}
