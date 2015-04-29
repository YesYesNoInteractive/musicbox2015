using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Kinect;
using Microsoft.Kinect.VisualGestureBuilder;
using Rug.Osc;

namespace KinectV2OSC.Model.Network
{
    public class MessageBuilder
    {
        public OscMessage BuildJointMessage(Body body, KeyValuePair<JointType, Joint> joint)
        {
            var address = String.Format("/bodies/{0}/joints/{1}", body.TrackingId, joint.Key);
            var position = joint.Value.Position;
            //System.Diagnostics.Debug.WriteLine(address);
            return new OscMessage(address, position.X, position.Y, position.Z, joint.Value.TrackingState.ToString());
        }

        public OscMessage BuildHandMessage(Body body, string key, HandState state, TrackingConfidence confidence)
        {
            var address = String.Format("/bodies/{0}/hands/{1}", body.TrackingId, key);
            //System.Diagnostics.Debug.WriteLine(address);
            return new OscMessage(address, state.ToString(), confidence.ToString());
        }

        public OscMessage BuildGestureMessage(string bodyid, string gesture, bool state, float confidenceState)
        {
            var address = String.Format("/bodies/{0}/gesture/{1}", bodyid, gesture);
            return new OscMessage(address, state.ToString(), confidenceState.ToString()); 
        
        }
        public OscMessage BuildGestureMessage(string bodyid, string gesture, float percent)
        {
            var address = String.Format("/bodies/{0}/gesture/{1}", bodyid, gesture);
            return new OscMessage(address, percent.ToString());

        }
    }
}
