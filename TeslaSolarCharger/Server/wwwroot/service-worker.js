// Registered so browsers can install Solar4Car as an app. It deliberately has no fetch handler: one that does not
// cache anything would only route every request through the service worker, which slows loading down. Over plain
// HTTP no service worker runs at all, over HTTPS it would.
