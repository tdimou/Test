// --- σταθερό RPC ---
const FIXED_RPC_URL = 'ws://192.168.200.72:8090/json-rpc';

let client;
let deviceIP;
const ssdpDevices = [];
let canEnable = false;

const events = {
  SetConfig: 0,
  ReadConfig: 1,
  ReadConfigResult: 2,
  ScanSSDP: 3,
  SSDPScanResult: 4
};

function open() {
  client = new WebSocket(`ws://${deviceIP}:8086`);
  client.onopen = onOpen;
  client.onmessage = onMessage;
  client.onerror = () => location.reload();
}

function send(json) {
  client?.send(JSON.stringify(json));
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

function onOpen() {
  // 1) Δείξε status
  document.getElementById('status').innerHTML = 'Connected';

  // 2) Πρόσθεσε ΑΜΕΣΑ το σταθερό server στη λίστα + UI
  addDeviceIfMissing(FIXED_RPC_URL, 'Hyperion (Fixed)');

  // 3) Κάν’ τον και default (τώρα που σίγουρα υπάρχει η setRPC από το άλλο script)
  if (typeof window.setRPC === 'function') {
    window.setRPC(FIXED_RPC_URL);
    canEnable = true;
    const t = document.getElementById('ssdpDeviceTitle');
    if (t) t.innerText = `SSDP Devices (Currently Connected to ${FIXED_RPC_URL})`;
  }

  // 4) Συνέχισε το κανονικό σου flow
  document.getElementById('enabled').onchange = (e) => {
    if (!canEnable) {
      alert('Please select a device first');
      e.target.checked = false;
      return;
    }
    send({ event: events.SetConfig, key: 'enabled', value: e.target.checked.toString() });
  };

  send({ event: events.ReadConfig, key: 'rpcServer' });
  send({ event: events.ReadConfig, key: 'enabled' });
  send({ event: events.ScanSSDP });
  setInterval(() => send({ event: events.ScanSSDP }), 10000);
}

function onMessage(evt) {
  const msg = JSON.parse(evt.data);

  switch (msg.Event) {
    case events.ReadConfigResult:
      if (msg.key === 'rpcServer' && !msg.error) {
        canEnable = true;
        const t = document.getElementById('ssdpDeviceTitle');
        if (t) t.innerText = `SSDP Devices (Currently Connected to ${msg.value})`;
      } else if (msg.key === 'enabled' && !msg.error) {
        document.getElementById('enabled').checked = (msg.value === 'true');
      }
      break;

    case events.SSDPScanResult:
      for (const device of (msg.devices || [])) {
        const base = device.UrlBase || '';
        const url = base.startsWith('https') ? base.replace('https', 'wss') : base.replace('http', 'ws');
        const name = device.FriendlyName || 'Unknown';
        addDeviceIfMissing(url, name);
      }
      // βεβαιώσου ότι ο fixed μένει πάντα ορατός
      addDeviceIfMissing(FIXED_RPC_URL, 'Hyperion (Fixed)');
      break;
  }
}
