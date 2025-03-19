using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using HyperTizen.SDK;
using Tizen.Applications.Notifications;


namespace HyperTizen
{

    public static class VideoCapture
    {
        public static void InitCapture()
        {
            try
            {
                Marshal.PrelinkAll(typeof(SDK.SecVideoCapture));
            }
            catch
            {
                Debug.WriteLine("VideoCapture InitCapture Error: Libarys not found");
                Notification notification4 = new Notification
                {
                    Title = "HyperTizen",
                    Content = "VideoCapture InitCapture Error: Libarys not found. Check if your Tizenversion is supported",
                    Count = 1
                };
                NotificationManager.Post(notification4);
            }

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
            info.pImageY = Marshal.AllocHGlobal(NV12ySize);
            info.pImageUV = Marshal.AllocHGlobal(NV12uvSize);
            //info.iRetColorFormat = 0;
            //info.capture3DMode = 0;

            int result = SDK.SecVideoCapture.CaptureScreen(Globals.Instance.Width, Globals.Instance.Height, ref info); //call itself takes 35-40ms in debug mode so it should be 28-25fps
            if (result < 0) //only send Notification once
            {
                if(isRunning)
                    switch (result)
                    {
                        case -4:
                            Debug.WriteLine("CaptureScreen Result: -4 [Netflix/ Widevine Drm Error]");
                            Notification notification4 = new Notification
                            {
                                Title = "HyperTizen",
                                Content = "Capture Error: Seems like you are watching DRM protected content",
                                Count = 1
                            };
                            NotificationManager.Post(notification4);
                            break;
                        case -1:
                            Debug.WriteLine("CaptureScreen Result: -1 [Input Pram is wrong / req size less or equal that crop size / non video for videoonly found]");
                            Notification notification1 = new Notification
                            {
                                Title = "HyperTizen",
                                Content = "Capture Error: Input Pram seems wrong",
                                Count = 1
                            };
                            NotificationManager.Post(notification1);
                            break;
                        case -2:
                            Debug.WriteLine("CaptureScreen Result: -2 [capture type %s, plane %s video only %d / Failed scaler_capture]");
                            Notification notification2 = new Notification
                            {
                                Title = "HyperTizen",
                                Content = "Capture Error: Failed scaler",
                                Count = 1
                            };
                            NotificationManager.Post(notification2);
                            break;
                        default:
                            Notification notificationN = new Notification
                            {
                                Title = "HyperTizen",
                                Content = "Capture Error: New Error occured. Please report ID:" + result,
                                Count = 1
                            };
                            NotificationManager.Post(notificationN);
                            break;
                    }
                isRunning = false;
                return;
            }

            isRunning = true;

            byte[] managedArrayY = new byte[NV12ySize];
            Marshal.Copy(info.pImageY, managedArrayY, 0, NV12ySize);

            byte[] managedArrayUV = new byte[NV12uvSize];
            Marshal.Copy(info.pImageUV, managedArrayUV, 0, NV12uvSize);

            bool hasAllZeroes1 = managedArrayY.All(singleByte => singleByte == 0);
            bool hasAllZeroes2 = managedArrayUV.All(singleByte => singleByte == 0);
            if (hasAllZeroes1 && hasAllZeroes2)
                throw new Exception("Sanity check Error");

            Debug.WriteLine("DoCapture: NV12ySize: " + managedArrayY.Length);
            //Debug.WriteLine(Convert.ToBase64String(managedArrayY).Length);
            //Debug.WriteLine(Convert.ToBase64String(managedArrayY));
            //Debug.WriteLine(Convert.ToBase64String(managedArrayUV));
            //(managedArrayY, managedArrayUV) = GenerateDummyYUVColor(Globals.Instance.Width, Globals.Instance.Height);

            Task.Run( ()=>Networking.SendImageAsync(managedArrayY, managedArrayUV, Globals.Instance.Width, Globals.Instance.Height));
            //Networking.SendImageAsync(managedArrayY, managedArrayUV, Globals.Instance.Width, Globals.Instance.Height); //100-150ms in debug mode

            int test = 3;
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