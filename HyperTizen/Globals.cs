using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperTizen
{
    public sealed class Globals
    {
        private static readonly Globals instance = new Globals();

        // Explicit static constructor to tell C# compiler
        // not to mark type as beforefieldinit
        static Globals()
        {
        }

        private Globals()
        {
        }

        public static Globals Instance
        {
            get
            {
                return instance;
            }
        }
        public void SetConfig()
        {
            ServerIp = "192.168.69.200";//25
            ServerPort = 19400;
            Width = 480;
            Height = 270;
        }

        public string ServerIp; //IP of hyperhdr server
        public int ServerPort; //Port of hyperhdr server
        public int Width; //Capture Width
        public int Height; //Capture Height
    }
}
