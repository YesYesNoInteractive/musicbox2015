namespace KinectV2OSC
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Microsoft.Kinect;
    using Microsoft.Kinect.VisualGestureBuilder;
    using Model.Drawing;
    using Model.Network;
  
    /// <summary>
    /// Interaction logic for MainWindow
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private DrawingImage imageSource;
        private KinectSensor kinectSensor;
        private BodyFrameReader bodyFrameReader;
        private Body[] bodies;
        private FrameTimer timer;
        private KinectCanvas kinectCanvas;
        private BodySender bodySender;

        // Gesture Builder
        private VisualGestureBuilderDatabase gestureDatabase;
        private VisualGestureBuilderFrameSource gestureFrameSource;
        private VisualGestureBuilderFrameReader gestureFrameReader;

        // Gestures
        private List<Gesture> discreteGestures;
        private List<Gesture> continuousGestures;
        private List<Gesture> gestures;


        public event PropertyChangedEventHandler PropertyChanged;

        public ImageSource ImageSource
        {
            get { return this.imageSource; }
        }

        private string framesText;
        public string FramesText
        {
            get { return this.framesText; }
            set
            {
                this.framesText = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged(this, new PropertyChangedEventArgs("FramesText"));
                }
            }
        }

        private string uptimeText;
        public string UptimeText
        {
            get { return this.uptimeText; }
            set
            {
                this.uptimeText = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged(this, new PropertyChangedEventArgs("UptimeText"));
                }
            }
        }

        private string oscText;
        public string OscText
        {
            get { return this.oscText; }
            set
            {
                this.oscText = value;
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged(this, new PropertyChangedEventArgs("OscText"));
                }
            }
        }

        public MainWindow()
        {
            this.timer = new FrameTimer();
            this.InitKinect();
            this.InitNetwork();
            this.InitWindowObjectAsViewModel();
        }

        private void InitKinect()
        {
            Size displaySize = new Size(0, 0);
            this.kinectSensor = KinectSensor.GetDefault();

            if (this.kinectSensor != null)
            {
                this.kinectSensor.Open();

                var frameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;
                displaySize.Width= frameDescription.Width;
                displaySize.Height = frameDescription.Height;
                gestureDatabase = new VisualGestureBuilderDatabase(@"../../../Gestures/gestures.gbd");
                gestureFrameSource = new VisualGestureBuilderFrameSource(this.kinectSensor, 0);
                this.discreteGestures = new List<Gesture>();
                this.continuousGestures = new List<Gesture>();
   
                foreach (Gesture gesture in gestureDatabase.AvailableGestures)
                {
                    if (gesture.GestureType == GestureType.Discrete)
                    {
                        this.discreteGestures.Add(gesture);
                    }
                    else if (gesture.GestureType == GestureType.Continuous)
                    {
                        this.continuousGestures.Add(gesture);
                    }
 
                    this.gestureFrameSource.AddGesture(gesture);
                }

                this.bodyFrameReader = this.kinectSensor.BodyFrameSource.OpenReader();
                this.gestureFrameReader = gestureFrameSource.OpenReader();
                this.gestureFrameReader.IsPaused =true;
                this.gestureFrameReader.FrameArrived += gestureFrameReader_FrameArrived;

                this.UptimeText = Properties.Resources.InitializingStatusTextFormat;

               
            }
            else
            {
                this.UptimeText = Properties.Resources.NoSensorFoundText;
            }

            this.kinectCanvas = new KinectCanvas(this.kinectSensor, displaySize);
        }

        private void InitNetwork()
        {

            //read from XML file on desktop?!?

            string ipAddress = "127.0.0.1";
            string port = "12345";


            //var ipAddress = Properties.Resources.IPAddress;
            //var port = Properties.Resources.PortNumber;
            this.bodySender = new BodySender(ipAddress, port);
        }

        private void InitWindowObjectAsViewModel()
        {
            this.imageSource = this.kinectCanvas.GetDrawingImage();
            this.DataContext = this;
            this.InitializeComponent();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (this.bodyFrameReader != null)
            {
                this.bodyFrameReader.FrameArrived += this.Reader_FrameArrived;
                // Gesturesの初期設定
               
  
            }
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (this.bodyFrameReader != null)
            {
                this.bodyFrameReader.Dispose();
                this.bodyFrameReader = null;
            }

            if (this.kinectSensor != null)
            {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }

            if (this.gestureFrameReader != null)
            {
                this.gestureFrameReader.Dispose();
                this.gestureFrameReader = null;
            }
        }

        private void Reader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            var frameReference = e.FrameReference;

            try
            {
                var frame = frameReference.AcquireFrame();

                if (frame != null)
                {
                    using (frame)
                    {
                        this.timer.AddFrame(frameReference);
                        this.setStatusText();
                        this.updateBodies(frame);
                        this.kinectCanvas.Draw(this.bodies);
                        this.bodySender.Send(this.bodies);

                        Console.WriteLine(this.bodies.Length);
                        foreach (var body in bodies)
                        {
                            if (body != null && body.IsTracked)
                            {
                                gestureFrameSource.TrackingId = body.TrackingId;
                                gestureFrameReader.IsPaused = false;
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                Console.WriteLine("Frame exception encountered...");
            }

        }

        private void gestureFrameReader_FrameArrived(object sender, VisualGestureBuilderFrameArrivedEventArgs e)
        {
            using (var gestureFrame = e.FrameReference.AcquireFrame())
            {
                if (gestureFrame != null && gestureFrame.DiscreteGestureResults != null)
                {

                    bodySender.addGestures(gestureFrame, gestureFrame.TrackingId, this.discreteGestures, this.continuousGestures);

                }
            }
        }

        private void setStatusText()
        {
            var framesPerSecond = timer.GetFramesPerSecond();
            var runningTime = timer.GetRunningTime();
            this.FramesText = string.Format(Properties.Resources.StandardFramesTextFormat, framesPerSecond);
            this.UptimeText = string.Format(Properties.Resources.StandardUptimeTextFormat, runningTime);
            this.OscText = bodySender.GetStatusText();
        }

        private void updateBodies(BodyFrame frame)
        {
            if (this.bodies == null)
            {
                this.bodies = new Body[frame.BodyCount];
            }

            // The first time GetAndRefreshBodyData is called, Kinect will allocate each Body in the array.
            // As long as those body objects are not disposed and not set to null in the array,
            // those body objects will be re-used.
            frame.GetAndRefreshBodyData(this.bodies);
        }
    }
}
