// js/wsClient.js

// ===== ΣΤΑΘΕΡΗ ΔΙΕΥΘΥΝΣΗ RPC =====
const FIXED_RPC_URL = 'wss://192.168.200.72:8092/json-rpc';

let client;           // WebSocket προς το local WSServer (deviceIP:8086)
let deviceIP;         // παίρνεται από 127.0.0.1:8081
const ssdpDevices = [];  // { url, friendlyName }
let canEnable = false;

const events = {
    SetConfig: 0,
    ReadConfig: 1,
    ReadConfigResult: 2,
    ScanSSDP: 3,
    SSDPScanResult: 4
};

// ---------- helpers ----------
function addDeviceIfMissing(url, friendlyName) {
    if (ssdpDevices.some(d => d.url === url)) return false;

    ssdpDevices.push({ url, friendlyName });

    // render στο UI
    const container = document.getElementById('ssdpItems');
    container.insertAdjacentHTML('beforeend', `
        <div class="ssdpItem" data-uri="${url}" data-friendlyName="${friendlyName}" tabindex="0" onclick="setRPC('${url}')">
            <a>${friendlyName}</a>
        </div>
    `);
    return true;
}

function send(json) {
    client?.send(JSON.stringify(json));
}

function openWS() {
    client = new WebSocket(`ws://${deviceIP}:8086`);
    client.onopen = onOpen;
    client.onmessage = onMessage;
    client.onerror = () => {
        location.reload();
    };
}

// ---------- lifecycle ----------
function onOpen() {
    // 1) Βάλε άμεσα τον σταθερό server στη λίστα
    addDeviceIfMissing(FIXED_RPC_URL, 'Hyperion (Fixed)');

    // 2) Δείξε status
    document.getElementById('status').innerHTML = 'Connected';

    // 3) Σύνδεσε τον διακόπτη "Enabled"
    document.getElementById('enabled').onchange = (e) => {
        if (!canEnable) {
            alert('Please select a device first');
            e.target.checked = false;
            return;
        }
        send({ event: events.SetConfig, key: 'enabled', value: e.target.checked.toString() });
    };

    // 4) Ζήτα τρέχουσα config
    send({ event: events.ReadConfig, key: 'rpcServer' });
    send({ event: events.ReadConfig, key: 'enabled' });

    // 5) Κάνε αρχικό SSDP scan + periodic
    send({ event: events.ScanSSDP });
    setInterval(() => send({ event: events.ScanSSDP }), 10000);

    // 6) Κάν’ το default άμεσα (αν θες να επιλέγεται αυτόματα πάντα)
    if (typeof window.setRPC === 'function') {
        window.setRPC(FIXED_RPC_URL);
        canEnable = true; // μπορούμε πλέον να επιτρέψουμε enable toggle
        const title = document.getElementById('ssdpDeviceTitle');
        if (title) title.innerText = `SSDP Devices (Currently Connected to ${FIXED_RPC_URL})`;
    }
}

function onMessage(evt) {
    const msg = JSON.parse(evt.data);

    switch (msg.Event) {
        case events.ReadConfigResult: {
            if (msg.key === 'rpcServer' && !msg.error) {
                canEnable = true;
                const title = document.getElementById('ssdpDeviceTitle');
                if (title) title.innerText = `SSDP Devices (Currently Connected to ${msg.value})`;
            } else if (msg.key === 'enabled' && !msg.error) {
                document.getElementById('enabled').checked = (msg.value === 'true');
            }
            break;
        }

        case events.SSDPScanResult: {
            // Πρόσθεσε ό,τι βρει, αποφεύγοντας διπλότυπα
            for (const device of msg.devices || []) {
                const base = device.UrlBase || '';
                const url = base.startsWith('https') ? base.replace('https', 'wss') : base.replace('http', 'ws');
                const friendlyName = device.FriendlyName || 'Unknown';

                addDeviceIfMissing(url, friendlyName);
            }
            // Βεβαιώσου ότι ο fixed υπάρχει (αν π.χ. άδειασε η λίστα αλλού)
            addDeviceIfMissing(FIXED_RPC_URL, 'Hyperion (Fixed)');
            break;
        }
    }
}

// ---------- εκκίνηση εφαρμογής ----------
window.onload = function () {
    // Προσπάθησε να εκκινήσεις το Tizen service (όπως πριν)
    const interval = setInterval(() => {
        try {
            tizen.application.getAppInfo('io.gh.reisxd.HyperTizen');
            tizen.application.launch(
                'io.gh.reisxd.HyperTizen',
                function () {
                    console.log('Launch Service succeeded');
                    clearInterval(interval);
                },
                function (e) {
                    console.log('Launch Service failed: ' + e.message);
                }
            );
        } catch (e) {
            console.log('App not found');
        }
    }, 1000);

    // Απόκτησε το local IP και άνοιξε το WS προς το local WSServer
    fetch('http://127.0.0.1:8081')
        .then(res => res.text())
        .then(ip => {
            deviceIP = ip;

            const testWS = new WebSocket(`ws://${ip}:8086`);
            testWS.onopen = function () {
                testWS.close();
                openWS();
            };
            testWS.onerror = function () {
                location.reload();
            };
        });
};
