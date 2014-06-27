using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gst.GLib;
using Gst;

namespace S2MMSH
{
    public class CustomData
    {
        public MainLoop loop;     // main loop
        public Pipeline pipeline; // main pipeline
        /* source elements */
        public Element source1, queue1, decoder1, scale1, rate1, filter1, videobox1;
        /* video mixer */
        public Element mixer, clrspace, sink;
        /* audio source */
        public Element a_source, a_queue1, a_convert1, a_resample1, a_filter1;
        /* audio mixer */
        public Element a_mixer, a_sink;
        /* caps */
        public Caps filtercaps1;
        public Caps a_filtercaps1;
        /* etc */
        public Element frz1;
        /* for application */
        public Element encoder, a_encoder, asfmux, appsink;

        /* mms source object */
        public MmsData[] mms;

        /* mmsh server*/
        public int mmsh_status;
        public UIntPtr mmsh_socket;
        public MainLoop mmsh_loop;
        public int asf_status;
        public char[] asf_head_buffer = new char[65535];
        public int asf_head_size;
        public int packet_count;
    }
}
