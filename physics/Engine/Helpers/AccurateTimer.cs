// AccurateTimer.cs
using System;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace physics.Engine.Helpers
{
    public class PreciseTimer
    {

        [System.Security.SuppressUnmanagedCodeSecurity]
        [DllImport("kernel32")]
        private static extern bool QueryPerformanceFrequency(ref long PerformanceFrequency);

        [System.Security.SuppressUnmanagedCodeSecurity]
        [DllImport("kernel32")]
        private static extern bool QueryPerformanceCounter(ref long PerformanceCount);

        long _ticksPerSecond = 0;
        long _previousElapsedTime = 0;

        public PreciseTimer()
        {
            QueryPerformanceFrequency(ref _ticksPerSecond);
            GetElapsedTime(); // Get rid of first rubbish result
        }
        public double GetElapsedTime()
        {
            long time = 0;
            QueryPerformanceCounter(ref time);
            double elapsedTime = (double)(time - _previousElapsedTime) / (double)_ticksPerSecond;
            _previousElapsedTime = time;
            return elapsedTime;
        }
    }


    //http://what-when-how.com/Tutorial/topic-103/C-Game-Programming-For-Serious-Game-Creation-100.html
    public class FastLoop
    {
        PreciseTimer _timer = new PreciseTimer();
        public delegate void LoopCallback(double elapsedTime);
        LoopCallback _callback;

        public FastLoop(LoopCallback callback)
        {
            _callback = callback;
            Application.Idle += OnApplicationEnterIdle;
        }

        private void OnApplicationEnterIdle(object sender, EventArgs e)
        {
            while (CInterop.IsApplicationIdle_Peek())
            {
                _callback(_timer.GetElapsedTime());
            }
        }
    }

    public static class CInterop
    {

        //And the declarations for those two native methods members:        
        [StructLayout(LayoutKind.Sequential)]
        public struct Message
        {
            public IntPtr hWnd;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public System.Drawing.Point p;
        }

        const uint QS_MASK = 0x1FF;

        [System.Security.SuppressUnmanagedCodeSecurity] // We won’t use this maliciously
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern uint GetQueueStatus(uint flags);

        //https://stackoverflow.com/questions/21692790/code-optimization-causes-null-reference-exception-when-using-peekmessage
        public static bool IsApplicationIdle()
        {
            // The high-order word of the return value indicates
            // the types of messages currently in the queue. 
            return 0 == (GetQueueStatus(QS_MASK) >> 16 & QS_MASK);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NativeMessage
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public System.Drawing.Point point;
        }

        
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool PeekMessage(ref NativeMessage lpMsg, IntPtr hwnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);


        public static bool IsApplicationIdle_Peek()
        {
            NativeMessage msg = new NativeMessage();
            return !PeekMessage(ref msg, IntPtr.Zero, 0, 0, 0);
        }

    }
}
            
