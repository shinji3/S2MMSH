using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gst.GLib;
using Gst;

namespace S2MMSH
{
    public class MmsData
    {
        /* Structure to contain mms stream infomation. */

        public MainLoop loop;   // main loop
        public uint number;       // stream number
        public Int64 buffer_time; // current time
        public long clock;      // apprication time
        public ulong prob_hd;     // probe hundler ID for mmssrc
        public ulong prob_hd_v_eos;
        public ulong prob_hd_a_eos;
        public bool v_appflag, a_appflag;
        public char mms_location; // target url

        public Pipeline pipeline;

        /* source elements */
        public Element source;
        public Element queue;
        public Element decoder;
        public Element v_queue;
        public Element colorspace;
        public Element scale;
        public Element rate;
        public Element filter;
        public Element videobox;
        /* audio source */
        public Element a_queue;
        public Element a_convert;
        public Element a_resample;
        public Element a_filter;
        /* caps */
        public Caps filtercaps;
        public Caps a_filtercaps;

        /* app */
        public Element v_appsink;
        public Element v_appsrc;
        public Element v_app_q;
        //GstElement *v_app_filter;
        public Element a_appsink;
        public Element a_appsrc;
        public Element a_app_q;
        //GstElement *a_app_filter;

        /* Parent Object (CustomData) */
        public CustomData parent;

        public int width, height;


    }
}
