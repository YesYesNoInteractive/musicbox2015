using System;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using Microsoft.Kinect;
using Microsoft.Kinect.VisualGestureBuilder;
using Rug.Osc;

namespace KinectV2OSC.Model.Network
{
    public class BodySender
    {
        private MessageBuilder messageBuilder;
        private List<OscSender> oscSenders;
        private List<IPAddress> ipAddresses;
        private OscMessage message;
        private Stack<OscMessage> gestureStack;
        private string port;
        private string status;

        public BodySender(string delimitedIpAddresses, string port)
        {
            this.status = "";
            this.ipAddresses = this.Parse(delimitedIpAddresses);
            this.oscSenders = new List<OscSender>();
            this.port = port;
            this.messageBuilder = new MessageBuilder();
            this.TryConnect();
            gestureStack = new Stack<OscMessage>();
        }

        private void TryConnect()
        {
            foreach(var ipAddress in this.ipAddresses)
            {
                try
                {
                    var oscSender = new OscSender(ipAddress, 0, int.Parse(this.port));
                    oscSender.Connect();
                    this.oscSenders.Add(oscSender);
                    this.status += "OSC connection established on\nIP: " + ipAddress + "\nPort: " + port + "\n";
                }
                catch (Exception e)
                {
                    this.status += "Unable to make OSC connection on\nIP: " + ipAddress + "\nPort: " + port + "\n";
                    Console.WriteLine("Exception on OSC connection...");
                    Console.WriteLine(e.StackTrace);
                }
            }

        }

        public void Send(Body[] bodies)
        {
            if (bodies.Length > 2)
            {
                ulong[] closest = FindClosestBody(bodies);

                foreach (Body body in bodies)
                {
                    foreach (var i in closest)
                    {
                        if (body.TrackingId.Equals(i) && body.IsTracked)
                        {
                            Send(body);
                        }
                    }
                }
            }
            else
            {
                foreach (var i in bodies)
                 {
                     if (i.IsTracked)
                     {
                         Send(i);
                     }
                 }
            }
        }

        private double VectorLength(CameraSpacePoint point)
        {
            var result = Math.Pow(point.X, 2) + Math.Pow(point.Y, 2) + Math.Pow(point.Z, 2);
            return result;
        }

        private ulong[] FindClosestBody(Body[] bodies)
        {
            double closestBodyDistance = double.MaxValue;;
            ulong index = 0;
            ulong previousIndex = 0;
            foreach (var body in bodies)
            {
                if (body.IsTracked)
                {
                    var currentLocation = body.Joints[JointType.SpineBase].Position;

                    var currentDistance = VectorLength(currentLocation);

                    if (currentDistance < closestBodyDistance)
                    {

                        previousIndex = index;
                        index = body.TrackingId;
                        closestBodyDistance = currentDistance;
                    }
                    else
                    {
                        previousIndex = body.TrackingId;
                    }
                }
            }
            ulong[] closestIndex;
            if (bodies.Length > 1)
            {
                closestIndex = new ulong[2];
                closestIndex[0] = index;
                closestIndex[1] = previousIndex;
            }
            else
            {
                closestIndex = new ulong[1];
                closestIndex[0] = index;
            }
            return closestIndex;
        }


        public void addGestures(VisualGestureBuilderFrame gestureFrame, ulong id, List<Gesture> discrete, List<Gesture> continuous)
        {
            DateTime reference = new DateTime(2001, 01, 01, 0, 0, 0, DateTimeKind.Utc);
            TimeSpan duration = new TimeSpan(DateTime.UtcNow.Ticks - reference.Ticks);
            ulong minutesCount = Convert.ToUInt64(duration.TotalMinutes);
            OscTimeTag timestamp = new OscTimeTag(minutesCount);
            List<OscMessage> messages = new List<OscMessage>();

            foreach (Gesture gest in discrete)
            {
                DiscreteGestureResult result = gestureFrame.DiscreteGestureResults[gest];
                messages.Add(messageBuilder.BuildGestureMessage(id.ToString(), gest.Name, result.Detected, result.Confidence));
            }

            foreach (Gesture gest in continuous)
            {
                ContinuousGestureResult result = gestureFrame.ContinuousGestureResults[gest];
                messages.Add(messageBuilder.BuildGestureMessage(id.ToString(), gest.Name, result.Progress));
            }


            OscBundle target = new OscBundle(timestamp, messages.ToArray());
            this.Broadcast(target);
        }

        public string GetStatusText()
        {
            return this.status;
        }

        private void Send(Body body)
        {
            Console.WriteLine("send body");
            DateTime reference = new DateTime(2001, 01, 01, 0, 0, 0, DateTimeKind.Utc);
            TimeSpan duration = new TimeSpan(DateTime.UtcNow.Ticks - reference.Ticks);
            ulong minutesCount = Convert.ToUInt64(duration.TotalMinutes);
            OscTimeTag timestamp = new OscTimeTag(minutesCount);
            List<OscMessage> messages = new List<OscMessage>();
           
            foreach (var joint in body.Joints)
            {
                message = messageBuilder.BuildJointMessage(body, joint);
                messages.Add(message);
            }
            message = messageBuilder.BuildHandMessage(body, "Left", body.HandLeftState, body.HandLeftConfidence);
            Broadcast(message);
            message = messageBuilder.BuildHandMessage(body, "Right", body.HandRightState, body.HandRightConfidence);
            Broadcast(message);


            OscBundle target = new OscBundle(timestamp, messages.ToArray());
            this.Broadcast(target);
    


        }

        private void Broadcast(OscBundle bundle)
        {
            foreach (var oscSender in this.oscSenders)
            {
                oscSender.Send(bundle);
            }
        }

        private void Broadcast(OscMessage message)
        {
            foreach (var oscSender in this.oscSenders)
            {
                oscSender.Send(message);
            }
        }

        private List<IPAddress> Parse(string delimitedIpAddresses)
        {
            try
            {
                var ipAddressStrings = delimitedIpAddresses.Split(',');
                var ipAddresses = new List<IPAddress>();
                foreach (var ipAddressString in ipAddressStrings)
                {
                    ipAddresses.Add(IPAddress.Parse(ipAddressString));
                }
                return ipAddresses;
            }
            catch (Exception e)
            {
                status += "Unable to parse IP address string: '" + delimitedIpAddresses + "'";
                Console.WriteLine("Exception parsing IP address string...");
                Console.WriteLine(e.StackTrace);
                return null;
            }
        }
    }
}
