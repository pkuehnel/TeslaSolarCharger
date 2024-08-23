let map;
let circle;
let dotNetHelper;
let markers = [];

window.initializeMap = (helper) => {
    dotNetHelper = helper;
    if (typeof L !== 'undefined') {
        createMap();
    } else {
        document.addEventListener('DOMContentLoaded', createMap);
    }
};

function createMap() {
    map = L.map('map').setView([0, 0], 2);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png').addTo(map);
    map.on('click', onMapClick);
}

function onMapClick(e) {
    dotNetHelper.invokeMethodAsync('UpdateSelection', e.latlng.lat, e.latlng.lng);
}

window.updateCircle = (lat, lng, radius) => {
    if (circle) {
        map.removeLayer(circle);
    }
    circle = L.circle([lat, lng], {
        color: '#1b6ec2',
        fillColor: '#1b6ec2',
        fillOpacity: 0.5,
        radius: radius
    }).addTo(map);

    // Calculate appropriate zoom level based on radius
    let zoom = getZoomForRadius(radius);

    // Use flyTo for smoother transition
    map.flyTo([lat, lng], zoom, {
        duration: 0.5 // Duration of animation in seconds
    });
};

function getZoomForRadius(radius) {
    // These values can be adjusted based on your preferences
    if (radius < 100) return 17;
    if (radius < 500) return 14;
    if (radius < 1000) return 13;
    if (radius < 5000) return 12;
    if (radius < 10000) return 11;
    if (radius < 50000) return 10;
    if (radius < 100000) return 9;
    if (radius < 500000) return 8;
    if (radius < 1000000) return 7;
    return 6; // For very large radii
}

window.addMarker = (lat, lng) => {
    const marker = L.marker([lat, lng]).addTo(map);
    markers.push(marker);
    map.setView([lat, lng], 13); // Zoom level 13 for markers
};