﻿let map;
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
    if (circle) {
        map.removeLayer(circle);
    }

    const radius = prompt("Enter radius in meters:", "1000");
    if (radius) {
        circle = L.circle(e.latlng, {
            color: 'red',
            fillColor: '#f03',
            fillOpacity: 0.5,
            radius: parseFloat(radius)
        }).addTo(map);

        dotNetHelper.invokeMethodAsync('UpdateSelection', e.latlng.lat, e.latlng.lng, parseFloat(radius));
        dotNetHelper.invokeMethodAsync('AddCoordinateFromMap', e.latlng.lat, e.latlng.lng);
    }
}

window.addMarker = (lat, lng) => {
    const marker = L.marker([lat, lng]).addTo(map);
    markers.push(marker);
    map.setView([lat, lng], 10);
};