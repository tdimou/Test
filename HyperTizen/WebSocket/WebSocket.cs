using System;
using System.Collections.Generic;
using WebSocketSharp.Server;
using WebSocketSharp;
using Newtonsoft.Json;
using HyperTizen.WebSocket.DataTypes;
using Rssdp;
using Tizen.Applications;
using static HyperTizen.WebSocket.DataTypes.SSDPScanResultEvent;
using System.Threading.Tasks;

namespace HyperTizen.WebSocket
{
    public class WSServer : WebSocketBehavior
    {
        private List<string> usnList = new List<string>()
        {
            "urn:hyperion-project.org:device:basic:1",
            "urn:hyperhdr.eu:device:basic:1"
        };

        protected override async void OnMessage(MessageEventArgs e)
        {
            BasicEvent data = JsonConvert.DeserializeObject<BasicEvent>(e.Data);

            switch (data.Event)
            {
                case Event.ScanSSDP:
                {
                        Task.Run(async () =>
                        {
                            using (var deviceLocator = new SsdpDeviceLocator())
                            {
                                var foundDevices = await deviceLocator.SearchAsync();
                                List<SSDPDevice> devices = new List<SSDPDevice>();
                                foreach (var foundDevice in foundDevices)
                                {
                                    if (!usnList.Contains(foundDevice.NotificationType)) continue;

                                    var fullDevice = await foundDevice.GetDeviceInfo();
                                    Uri descLocation = foundDevice.DescriptionLocation;
                                    devices.Add(new SSDPDevice(fullDevice.FriendlyName, descLocation.OriginalString.Replace(descLocation.PathAndQuery, "")));
                                }

                                string resultEvent = JsonConvert.SerializeObject(new SSDPScanResultEvent(devices));
                                Send(resultEvent);
                            }
                        });
                        break;
                }

                case Event.ReadConfig:
                    {
                        ReadConfigEvent readConfigEvent = JsonConvert.DeserializeObject<ReadConfigEvent>(e.Data);

                        string result;
                        if (!Preference.Contains(readConfigEvent.key))
                        {
                            result = JsonConvert.SerializeObject(new ReadConfigResultEvent(true, readConfigEvent.key, "Key doesn't exist."));
                        } else
                        {
                            string value = Preference.Get<string>(readConfigEvent.key);
                            result = JsonConvert.SerializeObject(new ReadConfigResultEvent(false, readConfigEvent.key, value));
                        }

                        Send(result);
                        break;
                    }

                case Event.SetConfig:
                    {
                        SetConfigEvent setConfigEvent = JsonConvert.DeserializeObject<SetConfigEvent>(e.Data);
                        SetConfiguration(setConfigEvent);
                        break;
                    }
            }
        }

        void SetConfiguration(SetConfigEvent setConfigEvent)
        {
            switch (setConfigEvent.key)
            {
                case "rpcServer":
                    {
                        App.Configuration.RPCServer = setConfigEvent.value;
                        App.client.UpdateURI(setConfigEvent.value);
                        break;
                    }
                case "enabled":
                    {
                        bool value = bool.Parse(setConfigEvent.value);
                        if (!App.Configuration.Enabled && value)
                        {
                            App.Configuration.Enabled = value;
                            Task.Run(() => App.client.Start(value));
                        }
                        else App.Configuration.Enabled = value;
                        break;
                    }
            }

            Preference.Set(setConfigEvent.key, setConfigEvent.value);
        }
    }

    public static class WebSocketServer
    {
        public static void StartServer()
        {
            var wsServer = new WebSocketSharp.Server.WebSocketServer(8086);
            wsServer.AddWebSocketService<WSServer>("/");
            wsServer.Start();
        }
    }

    public class WebSocketClient
    {
        private string uri;
        public WebSocketSharp.WebSocket client;
        private byte errorTimes = 0;

        public WebSocketClient(string uri)
        {
            this.uri = uri;
            client = new WebSocketSharp.WebSocket(uri);
            client.OnMessage += OnMessage;
            client.OnClose += OnClose;
            client.OnError += OnError;
        }

        private void OnError(object sender, ErrorEventArgs e)
        {
            if (client != null)
            {
                client.OnClose -= OnClose;
                client.Close();
            }
            if (errorTimes >= 3) return;
            client = new WebSocketSharp.WebSocket(uri);
            client.OnMessage += OnMessage;
            client.OnClose += OnClose;
            client.OnError += OnError;
            errorTimes++;
        }

        private void OnClose(object sender, CloseEventArgs e)
        {
            client.OnClose -= OnClose;
            client.Close();
            client = new WebSocketSharp.WebSocket(uri);
            client.OnMessage += OnMessage;
            client.OnClose += OnClose;
            client.OnError += OnError;
        }

        private void OnMessage(object sender, MessageEventArgs message)
        {

        }
    }

}
