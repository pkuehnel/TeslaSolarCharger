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
        color: 'red',
        fillColor: '#f03',
        fillOpacity: 0.5,
        radius: radius
    }).addTo(map);
    map.setView([lat, lng], 10);
};

window.addMarker = (lat, lng) => {
    const marker = L.marker([lat, lng]).addTo(map);
    markers.push(marker);
    map.setView([lat, lng], 10);
};