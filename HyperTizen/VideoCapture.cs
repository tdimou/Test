using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using HyperTizen.SDK;
using Tizen.Applications;
using Tizen.Applications.Notifications;


namespace HyperTizen
{
    public class ImageData
    {
        public byte[] yData { get; set; }
        public byte[] uvData { get; set; }
    }
    

    public static class VideoCapture
    {
        private static IntPtr pImageY;
        private static IntPtr pImageUV;
        private static byte[] managedArrayY;
        private static byte[] managedArrayUV;
        public static void InitCapture()
        {
            try
            {
                Marshal.PrelinkAll(typeof(SDK.SecVideoCapture));
            }
            catch
            {
                Helper.Log.Write(Helper.eLogType.Error, "VideoCapture.InitCapture() Error: Libarys not found. Check if your Tizenversion is supported");
            }

            int NV12ySize = Globals.Instance.Width * Globals.Instance.Height;
            int NV12uvSize = (Globals.Instance.Width * Globals.Instance.Height) / 2; // UV-Plane is half as big as Y-Plane in NV12
            pImageY = Marshal.AllocHGlobal(NV12ySize);
            pImageUV = Marshal.AllocHGlobal(NV12uvSize);
            managedArrayY = new byte[NV12ySize];
            managedArrayUV = new byte[NV12uvSize];

            int TizenVersionMajor = SystemInfo.TizenVersionMajor;
            int TizenVersionMinor = SystemInfo.TizenVersionMinor;
            bool ImageCapture = SystemInfo.ImageCapture;
            bool VideoRecording = SystemInfo.VideoRecording;
            int ScreenWidth = SystemInfo.ScreenWidth;
            int ScreenHeight = SystemInfo.ScreenHeight;
            string ModelName = SystemInfo.ModelName;

        }

        static bool isRunning = true;
        unsafe public static void DoCapture()
        {
            //These lines need to stay here somehow - they arent used but when i delete them the service breaks ??? weird tizen stuff...
            var width = 480;
            var height = 270;
            var uvBufferSizeYUV420 = (width / 2) * (height / 2);


            int NV12ySize = Globals.Instance.Width * Globals.Instance.Height;
            int NV12uvSize = (Globals.Instance.Width * Globals.Instance.Height) / 2; // UV-Plane is half as big as Y-Plane in NV12

            SDK.SecVideoCapture.Info_t info = new SDK.SecVideoCapture.Info_t();
            info.iGivenBufferSize1 = NV12ySize;
            info.iGivenBufferSize2 = NV12uvSize;
            //info.iWidth = width;
            //info.iHeight = height;
            info.pImageY = pImageY;
            info.pImageUV = pImageUV;
            //info.iRetColorFormat = 0;
            //info.capture3DMode = 0;
            var watchFPS = System.Diagnostics.Stopwatch.StartNew();
            int result = SDK.SecVideoCapture.CaptureScreen(Globals.Instance.Width, Globals.Instance.Height, ref info); //call itself takes 35-40ms in debug mode so it should be 28-25fps
            watchFPS.Stop();
            var elapsedFPS = 1 / watchFPS.Elapsed.TotalSeconds;
            Helper.Log.Write(Helper.eLogType.Performance, "SDK.SecVideoCapture.CaptureScreen FPS: " + elapsedFPS);
            Helper.Log.Write(Helper.eLogType.Performance, "SDK.SecVideoCapture.CaptureScreen elapsed ms: " + watchFPS.ElapsedMilliseconds);
            if (result < 0) //only send Notification once
            {
                if(isRunning)
                    switch (result)
                    {
                        case -4:
                            Helper.Log.Write(Helper.eLogType.Error, "SDK.SecVideoCapture.CaptureScreen Result: -4 [Netflix/ Widevine Drm Error]. Seems like you are watching DRM protected content. Capture is not supported for that yet");
                            break;
                        case -1:
                            Helper.Log.Write(Helper.eLogType.Error, "SDK.SecVideoCapture.CaptureScreen Result: -1 [Input Pram is wrong / req size less or equal that crop size / non video for videoonly found]. This can occur when Settings or Video Inputs of the TV change. Check in HyperHDR if the Live-View is still showing an image.");
                            break;
                        case -2:
                            Helper.Log.Write(Helper.eLogType.Error, "SDK.SecVideoCapture.CaptureScreen Result: -2 [capture type %s, plane %s video only %d / Failed scaler_capture]. Please try restarting the TV (coldboot)");
                            //Application.Current.Exit();
                            break;
                        default:
                            Helper.Log.Write(Helper.eLogType.Error, "SDK.SecVideoCapture.CaptureScreen Result: "+ result + " New Error Occured. Please report the shown Number on Github. Also enable every Log Option in the UI, run the Service in Debug Mode and send the Logs.");
                            break;
                    }
                isRunning = false;
                return;
            }

            isRunning = true;


            Marshal.Copy(info.pImageY, managedArrayY, 0, NV12ySize);
            Marshal.Copy(info.pImageUV, managedArrayUV, 0, NV12uvSize);

            bool hasAllZeroes1 = managedArrayY.All(singleByte => singleByte == 0);
            bool hasAllZeroes2 = managedArrayUV.All(singleByte => singleByte == 0);
            if (hasAllZeroes1 && hasAllZeroes2)
                throw new Exception("Sanity check Error");

            Helper.Log.Write(Helper.eLogType.Info, "DoCapture: NV12ySize: " + managedArrayY.Length);
            //Debug.WriteLine(Convert.ToBase64String(managedArrayY).Length);
            //Debug.WriteLine(Convert.ToBase64String(managedArrayY));
            //Debug.WriteLine(Convert.ToBase64String(managedArrayUV));

            //Networking.SendImage(managedArrayY, managedArrayUV, Globals.Instance.Width, Globals.Instance.Height);
            //Task.Run( ()=>Networking.SendImageAsync(managedArrayY, managedArrayUV, Globals.Instance.Width, Globals.Instance.Height)); doenst work after a few sec
            //Networking.SendImageAsync(managedArrayY, managedArrayUV, Globals.Instance.Width, Globals.Instance.Height); //100-150ms in debug mode
            _ = Networking.SendImageAsync(managedArrayY, managedArrayUV,Globals.Instance.Width, Globals.Instance.Height);
            return;
        }

        unsafe public static void DoDummyCapture()
        {
            int NV12ySize = Globals.Instance.Width * Globals.Instance.Height;
            int NV12uvSize = (Globals.Instance.Width * Globals.Instance.Height) / 2; // UV-Plane is half as big as Y-Plane in NV12
            byte[] managedArrayY = new byte[NV12ySize];
            byte[] managedArrayUV = new byte[NV12uvSize];
            (managedArrayY, managedArrayUV) = GenerateDummyYUVColor(Globals.Instance.Width, Globals.Instance.Height);
            _ = Networking.SendImageAsync(managedArrayY, managedArrayUV, Globals.Instance.Width, Globals.Instance.Height);
            return;
        }

        public static (byte[] yData, byte[] uvData) GenerateDummyYUVRandom(int width, int height)
        {
            int ySize = width * height;
            int uvSize = (width * height) / 2;

            byte[] yData = new byte[ySize]; 
            byte[] uvData = new byte[uvSize];

            Random rnd = new Random();
            rnd.NextBytes(yData);
            rnd.NextBytes(uvData);

            return (yData, uvData);
        }

        public static (byte[] yData, byte[] uvData) GenerateDummyYUVColor(int width, int height)
        {
            int ySize = width * height;
            int uvSize = (width * height) / 2;

            byte[] yData = new byte[ySize]; 
            byte[] uvData = new byte[uvSize];


            for (int i = 0; i < ySize; i++)
            {
                yData[i] = 128; 
            }

            for (int i = 0; i < uvSize; i += 2)
            {
                uvData[i] = 128;
                uvData[i + 1] = 255;
            }

            return (yData, uvData);
        }

    }

}