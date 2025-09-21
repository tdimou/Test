// --- σταθερό RPC ---
const FIXED_RPC_URL = 'ws://192.168.200.72:8090/json-rpc';


let client;
let deviceIP;
const ssdpDevices = [];
let canEnable = false;

function open() {
    client = new WebSocket(`ws://${deviceIP}:8086`);
    client.onopen = onOpen;
    client.onmessage = onMessage;
    client.onerror = () => {
        location.reload();
    }
}

const events = {
    SetConfig: 0,
    ReadConfig: 1,
    ReadConfigResult: 2,
    ScanSSDP: 3,
    SSDPScanResult: 4
}

function send(json) {
    client.send(JSON.stringify(json));
}

function onOpen() {
    document.getElementById('status').innerHTML = 'Connected';
  
  addDeviceIfMissing(FIXED_RPC_URL, 'Hyperion (Fixed)');
    document.getElementById('enabled').onchange = (e) => {
        if (!canEnable) {
            alert('Please select a device first');
            return e.target.checked = false;
        }
        send({ event: events.SetConfig, key: 'enabled', value: e.target.checked.toString() });
    }
    send({ event: events.ReadConfig, key: 'rpcServer' });
    send({ event: events.ReadConfig, key: 'enabled' });
    send({ event: events.ScanSSDP });
    setInterval(() => {
        send({ event: events.ScanSSDP });
    }, 10000);
}

function onMessage(data) {
      addDeviceIfMissing(FIXED_RPC_URL, 'Hyperion (Fixed)');
}

window.setRPC = (url) =>  {
    canEnable = true;
    send({ event: events.SetConfig, key: 'rpcServer', value: url });
}



// helper για ασφαλή προσθήκη + render
function addDeviceIfMissing(url, friendlyName) {
  if (ssdpDevices.some(d => d.url === url)) return;
  ssdpDevices.push({ url, friendlyName });
  const c = document.getElementById('ssdpItems');
  c.insertAdjacentHTML(
    'beforeend',
    `<div class="ssdpItem" data-uri="${url}" data-friendlyName="${friendlyName}" tabindex="0" onclick="setRPC('${url}')">
        <a>${friendlyName}</a>
     </div>`
  );
}
