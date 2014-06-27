using System;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;
using System.IO;
using System.Text;
using Gst;
using Gst.BasePlugins;
using Gst.GLib;

namespace S2MMSH
{
    public partial class MainForm : Form
    {
        //private static Boolean serverstatus = true;
        //private Process process = null;
        //private Thread th_server = null;
        //private Thread th_ffmpeg = null;
        public LogoutputDelegate logoutputDelegate;
        public ConnectButtonDelegate connectButtonStateDeligate;
        public DisconnectButtonDelegate disconnectButtonStateDeligate;

        /* macro */
        public const UInt64 LATENCY = 20000000000;  // default latency in nanoseconds
        public const UInt64 BUFFER_TIMEOUT = 30000000000;  // buffer timeout amount in nanoseconds
        public const UInt64 CLOCK_TIMEOUT = 60;          // clock timeout amount in seconds

//#define MMS_HOST "localhost"
//#define MMS_BASE_PORT 47000
//#define MMS_EX_PORT 48000
//#define HTTP_OK 200 // httpresponsecode200
//#define HTTP_BUSY 503 //

//#define MMSH_STATUS_null 0
//#define MMSH_STATUS_CONNECTED 1
//#define MMSH_STATUS_HTTP_HEADER_SEND 2
//#define MMSH_STATUS_ASF_HEADER_SEND 3
//#define MMSH_STATUS_ASF_DATA_SENDING 4

//#define ASF_STATUS_null 0
//#define ASF_STATUS_SET_HEADER 1
//#define ASF_HEADER_BUFSIZE 65535

//#define PATHNAME_SIZE 2048

//#define Debug.PrintMOD

//#ifdef Debug.PrintMOD
//#define Debug.Print(fmt, ...) g_print(g_strdup_printf("%d: %s",__LINE__, fmt), __VA_ARGS__)
//#define Debug.PrintLINE() g_print(g_strdup_printf("Debug.PrintLINE : %d\n",__LINE__))
//#else
//#define Debug.Print(fmt, ...) g_print("")
//#define Debug.PrintLINE() g_print("")
//#endif
        Boolean sigint_flag = false;
        UInt64 latency = LATENCY;
        //int polling_second = POLLING_SECOND;
        //int stream_amount = STREAM_AMOUNT;
        //int mms_base_port = MMS_BASE_PORT;
        //int mms_ex_port = MMS_EX_PORT;
        //int canvas_width = 3;
        
        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            this.textBox_ffmpegPath.Text = Properties.Settings.Default.ffmpegpath;
            this.textBox_inputstream.Text = Properties.Settings.Default.inputstream;
            this.textBox_audiorate.Text = Properties.Settings.Default.audiorate;
            this.textBox_videorate.Text = Properties.Settings.Default.videorate;
            this.textBox_exPort.Text = Properties.Settings.Default.port;

            if (Properties.Settings.Default.reencode_flag)
            { // 再エンコあり
                radioButton_reencode_1.Checked = true;
                groupBox_reencode.Enabled = true;
            }
            else {
                radioButton_reencode_2.Checked = true;
                groupBox_reencode.Enabled = false;
            }

            if (Properties.Settings.Default.ffmpegcom_flag)
            { // 再エンコあり
                radioButton_ffmpegcom_1.Checked = true;
                textBox_ffmpegcom.Enabled = true;
            }
            else
            {
                radioButton_ffmpegcom_2.Checked = true;
                textBox_ffmpegcom.Enabled = false;
            }

            this.textBox_enc_width.Text = Properties.Settings.Default.enc_width;
            this.textBox_enc_height.Text = Properties.Settings.Default.enc_height;
            this.textBox_enc_bitrate_v.Text = Properties.Settings.Default.enc_bitrate_v;
            this.textBox_enc_bitrate_a.Text = Properties.Settings.Default.enc_bitrate_a;
            this.textBox_enc_framerate.Text = Properties.Settings.Default.enc_framerate;

            this.textBox_ffmpegcom.Text = Properties.Settings.Default.ffmpegcom;

            this.tbx_Title.Text = Properties.Settings.Default.content_title;
            this.tbx_Auther.Text = Properties.Settings.Default.content_auther;
            this.tbx_Copyright.Text = Properties.Settings.Default.content_copyright;
            this.tbx_Description.Text = Properties.Settings.Default.content_description;
            this.tbx_Rating.Text = Properties.Settings.Default.content_rating;

            logoutputDelegate = new LogoutputDelegate(logoutput);
            connectButtonStateDeligate = new ConnectButtonDelegate(connectButtonState);
            disconnectButtonStateDeligate = new DisconnectButtonDelegate(disconnectButtonState);

            //自分自身のAssemblyを取得
            System.Reflection.Assembly asm =
                System.Reflection.Assembly.GetExecutingAssembly();
            //バージョンの取得
            System.Version ver = asm.GetName().Version;
            this.label_version.Text = "Ver. " + ver.ToString() ;

            //詳細表示にする
            listView1.View = View.Details;

            //ヘッダーを追加する（ヘッダー名、幅、アライメント）
            listView1.Columns.Add("配信ソース",453);

            ListViewItem itemx1 = new ListViewItem();

            listView1.GridLines = true;

            var path = @"C:\gstreamer";
            Environment.SetEnvironmentVariable("GST_PLUGIN_PATH", "");
            Environment.SetEnvironmentVariable("GST_PLUGIN_SYSTEM_PATH", String.Format(@"{0}\bin\plugins", path));
            Environment.SetEnvironmentVariable("PATH", String.Format(@"C:\Windows;{0}\lib;{0}\bin", path));

            // デバッグログ出力設定
            Environment.SetEnvironmentVariable("GST_Debug.Print", "*:3");
            Environment.SetEnvironmentVariable("GST_Debug.Print_FILE", "GstreamerLog.txt");
            Environment.SetEnvironmentVariable("GST_Debug.Print_DUMP_DOT_DIR", path);

            Gst.Application.Init(); //GStreamerの初期化

            
            this.button_disconnect.Enabled = false;
            this.textBox_log.AppendText("アプリケーションが開始しました。\r\n");
        }

        private void button_ffmpegPathReffer_Click(object sender, EventArgs e)
        {
            //OpenFileDialogクラスのインスタンスを作成
            OpenFileDialog ofd = new OpenFileDialog();

            ofd.FileName = "";
            ofd.InitialDirectory = @"C:\";
            ofd.Filter =
                "EXEファイル(*.exe)|*.exe|すべてのファイル(*.*)|*.*";
            ofd.FilterIndex = 1;
            ofd.Title = "ファイルを選択";
            //ダイアログボックスを閉じる前に現在のディレクトリを復元するようにする
            ofd.RestoreDirectory = true;
            ofd.CheckFileExists = true;
            ofd.CheckPathExists = true;

            //ダイアログを表示する
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                //OKボタンがクリックされたとき
                textBox_ffmpegPath.Text = ofd.FileName;
            }
        }

        // connection
        private void button_exec_Click(object sender, EventArgs e)
        {
            //永続化する場合
            if (radioButton_permanent_1.Checked) { 
                //永続化ストリーム立ち上げ
                logoutputDelegate("永続化ストリームを作成します。"); 

                //gstreamer準備


                


            


            }



            //this.textBox_log.AppendText("入力ストリームに接続します。\r\n");
            logoutputDelegate("入力ストリームに接続します。"); 

            ProcessManager pm = ProcessManager.Instance;
            pm.ffmpegstatus = FFMPEG_STATUS.FFMPEG_STATUS_PROCESS;

            // init
            this.button_exec.Enabled = false;

            // httpserver listening
            pm.th_server = new System.Threading.Thread(
                new ThreadStart(HttpThread)
            );
            pm.th_server.Start(); 

            // ffmpeg tcp stream receive process
            if (pm.process == null)
            {
                if (pm.th_ffmpeg == null)
                {
                    pm.th_ffmpeg = new System.Threading.Thread(new ThreadStart(delegate()
                    {
                        try
                        {
                            pm.process = new System.Diagnostics.Process();
                            pm.process.SynchronizingObject = this;
                            //イベントハンドラの追加
                            pm.process.Exited += new EventHandler(p_Exited);
                            pm.process.ErrorDataReceived += PrintErrorData;
                            //pm.process.ErrorDataReceived += new DataReceivedEventHandler(NetErrorDataHandler);
                            //プロセスが終了したときに Exited イベントを発生させる
                            pm.process.EnableRaisingEvents = true;

                            // ffmpeg commandline
                            //string command = 
                            //     this.textBox_ffmpegPath.Text + 
                            // " -v quiet -i tcp://127.0.0.1:6665 -c copy -f asf_stream -";
                            ProcessStartInfo startInfo = new ProcessStartInfo();
                            startInfo.FileName = this.textBox_ffmpegPath.Text;
                            if (this.radioButton_ffmpegcom_1.Checked) {
                                startInfo.Arguments = this.textBox_ffmpegcom.Text;
                            }
                            else
                            {
                                string url = this.textBox_inputstream.Text;
                                url = System.Text.RegularExpressions.Regex.Replace(
                                    url,
                                    @"^mms://",
                                    "mmsh://");
                                url = System.Text.RegularExpressions.Regex.Replace(
                                   url,
                                   @"^http://",
                                   "mmsh://");
                                if (this.radioButton_reencode_1.Checked == false)
                                {
                                    startInfo.Arguments = String.Format(" -v error -i {0} -c copy -f asf_stream -", url);
                                }
                                else
                                {
                                    int width = 0;
                                    int height = 0;
                                    int bitrate_v = 0;
                                    int bitrate_a = 0;
                                    float framerate = 0;
                                    try
                                    {
                                        width = int.Parse(this.textBox_enc_width.Text);
                                        height = int.Parse(this.textBox_enc_height.Text);
                                        bitrate_v = int.Parse(this.textBox_enc_bitrate_v.Text) * 1000;
                                        bitrate_a = int.Parse(this.textBox_enc_bitrate_a.Text) * 1000;
                                        framerate = float.Parse(this.textBox_enc_framerate.Text);
                                    }
                                    catch (Exception)
                                    {
                                        logoutput("エンコード設定が不正です。再エンコードしません。");
                                        startInfo.Arguments = String.Format(" -v error -i {0} -c copy -f asf_stream -", url);
                                    }
                                    string strsize = String.Format("{0}x{1}", width, height);
                                    if (width == 0 || height == 0) strsize = "320x240";
                                    if (bitrate_v == 0) bitrate_v = 256000;
                                    if (bitrate_a == 0) bitrate_a = 128000;
                                    if (framerate == 0) framerate = 15;

                                    startInfo.Arguments = String.Format(
                                        " -v error -i {0} -acodec wmav2 -ab {1} -vcodec wmv2 -vb {2} -s {3} -r {4} -f asf_stream -",
                                        url,
                                        bitrate_a,
                                        bitrate_v,
                                        strsize,
                                        framerate);
                                }
                            }
                            //startInfo.Arguments = " -v quiet -i tcp://127.0.0.1:6665 -c copy -f asf_stream -";
                            //startInfo.Arguments = " -v quiet -i rtsp://184.72.239.149/vod/mp4:BigBuckBunny_115k.mov -c copy -f asf_stream -";
                            //startInfo.Arguments = " -v quiet -i mmsh://win.global.playstream.com/showcase/mathtv/trig_4.5_350k.wmv -c copy -f asf_stream -";
                            //startInfo.Arguments = " -v quiet -i mmsh://218.228.167.141:8888 -c copy -f asf_stream -";
                            startInfo.CreateNoWindow = true; startInfo.CreateNoWindow = true;
                            startInfo.RedirectStandardOutput = true;
                            startInfo.RedirectStandardError = true; // 標準エラー
                            startInfo.UseShellExecute = false;

                            Console.WriteLine(startInfo.Arguments);

                            pm.process.StartInfo = startInfo;
                            pm.process.Start();

                            pm.process.BeginErrorReadLine(); // 標準エラーは別スレッドでとる

                            int c = 0;
                            const int nBytes = 65535;
                            byte[] buf = new byte[nBytes];
                            
                            // 標準出力はこのスレッドでとる
                            BinaryReader br = new BinaryReader(pm.process.StandardOutput.BaseStream);
                            Boolean flg = true;
                            AsfData asfData = AsfData.Instance;
                            while (flg)
                            {
                                if (br.Read(buf, 0, 4) == 4)
                                {
                                    if (buf[0] == '$')
                                    {
                                        c = buf[2] + (buf[3] << 8); //ヘッダサイズ
                                        if (br.Read(buf, 4, c) != c)
                                        {
                                            break;
                                        }
                                    }
                                    else
                                    {
                                        break;
                                    }

                                    if (buf[1] == 'H')// header packet
                                    {
                                        //bitrate property
                                        int bitrate;
                                        int audiorate;
                                        try
                                        {
                                            bitrate = int.Parse(this.textBox_videorate.Text) * 1000;
                                            audiorate = int.Parse(this.textBox_audiorate.Text) * 1000;
                                        }
                                        catch (Exception)
                                        {
                                            //this.textBox_log.AppendText(ex.Message);
                                            //break;
                                            bitrate = 256000;
                                            audiorate = 128000;
                                        }
                                        if (bitrate == 0) bitrate = 256000;
                                        if (audiorate == 0) audiorate = 128000;

                                        // Stream Bitrate Properties Object
                                        byte[] bitrate_property =
                                            new byte[]{
        0xCE, 0x75, 0xF8, 0x7B, 0x8D, 0x46, 0xD1, 0x11,
        0x8D, 0x82, 0x00, 0x60, 0x97, 0xC9, 0xA2, 0xB2,
        0x26, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x02, 0x00, //Bitrate Records Count
        // video bitrate
        0x01, 0x00, 
        (byte)bitrate, 
        (byte)((bitrate >> 8) & 0xFF), 
        (byte)((bitrate >> 16) & 0xFF), 
        (byte)((bitrate >> 24) & 0xFF), 
        // audio bitrate 12800bit/sec
        0x02, 0x00, 
        (byte)audiorate, 
        (byte)((audiorate >> 8) & 0xFF), 
        (byte)((audiorate >> 16) & 0xFF), 
        (byte)((audiorate >> 24) & 0xFF)
                                    };

                                        // existence check
                                        // 既に登録されてるかどうかの確認！
                                        int j, k;
                                        int y;
                                        Boolean pflg = true;
                                        for (j = 0; j < c; j++)
                                        {
                                            y = 0;
                                            for (k = 0; k < 16; k++)
                                            {
                                                if (bitrate_property[k] == buf[j + k])
                                                {
                                                    y++;
                                                }
                                                else { break; }
                                            }
                                            if (y == 16)
                                            {
                                                pflg = false;
                                                break;
                                            }
                                        }

                                        if (pflg)
                                        {
                                            byte[] data50 = new byte[50];
                                            int i;
                                            for (i = 0; i < 50; i++)
                                            {
                                                data50[i] = buf[c + 4 - 50 + i];
                                            }
                                            for (i = 0; i < 38; i++)
                                            {
                                                buf[c + 4 - 50 + i] = bitrate_property[i];
                                            }
                                            for (i = 0; i < 50; i++)
                                            {
                                                buf[c + 4 - 50 + 38 + i] = data50[i];
                                            }

                                            buf[2] = (byte)(c + 38);
                                            buf[3] = (byte)((c + 38) >> 8);
                                            buf[10] = buf[2];
                                            buf[11] = buf[3];
                                            buf[36] = (byte)(buf[36] + 1);
                                            c = c + 38;
                                        }

                                        // Content Description Object用
                                        // DescriptionのLengthをとってオブジェクトを作成する。
                                        // サイズを取得する
                                        byte[] title = Encoding.GetEncoding("UTF-16LE").GetBytes(this.tbx_Title.Text);
                                        byte[] auther = Encoding.GetEncoding("UTF-16LE").GetBytes(this.tbx_Auther.Text);
                                        byte[] copyright = Encoding.GetEncoding("UTF-16LE").GetBytes(this.tbx_Copyright.Text);
                                        byte[] description = Encoding.GetEncoding("UTF-16LE").GetBytes(this.tbx_Description.Text);
                                        byte[] rating = Encoding.GetEncoding("UTF-16LE").GetBytes(this.tbx_Rating.Text);

                                        int Title_length = title.Length;
                                        int Auther_length = auther.Length;
                                        int Copyright_length = copyright.Length;
                                        int Description_length = description.Length;
                                        int Rating_length = rating.Length;

                                        int content_description_object_size = 34 + Title_length + Auther_length + Copyright_length + Description_length + Rating_length;

                                        // Content Description Object
                                        byte[] content_description_object =
                                            new byte[]{
        0x33, 0x26, 0xB2, 0x75, 0x8E, 0x66, 0xCF, 0x11,
        0xA6, 0xD9, 0x00, 0xAA, 0x00, 0x62, 0xCE, 0x6C,
        (byte)(((Int64)content_description_object_size >> (8 * 0)) & 0xFF),
        (byte)(((Int64)content_description_object_size >> (8 * 1)) & 0xFF),
        (byte)(((Int64)content_description_object_size >> (8 * 2)) & 0xFF),
        (byte)(((Int64)content_description_object_size >> (8 * 3)) & 0xFF),
        (byte)(((Int64)content_description_object_size >> (8 * 4)) & 0xFF),
        (byte)(((Int64)content_description_object_size >> (8 * 5)) & 0xFF),
        (byte)(((Int64)content_description_object_size >> (8 * 6)) & 0xFF),
        (byte)(((Int64)content_description_object_size >> (8 * 7)) & 0xFF),
        (byte)((Title_length >> (8 * 0)) & 0xFF),
        (byte)((Title_length >> (8 * 1)) & 0xFF),
        (byte)((Auther_length >> (8 * 0)) & 0xFF),
        (byte)((Auther_length >> (8 * 1)) & 0xFF),
        (byte)((Copyright_length >> (8 * 0)) & 0xFF),
        (byte)((Copyright_length >> (8 * 1)) & 0xFF),
        (byte)((Description_length >> (8 * 0)) & 0xFF),
        (byte)((Description_length >> (8 * 1)) & 0xFF),
        (byte)((Rating_length >> (8 * 0)) & 0xFF),
        (byte)((Rating_length >> (8 * 1)) & 0xFF)
                                    };
                                        
                                       
                                        System.Collections.Generic.List<byte>
    mergedList = new System.Collections.Generic.List<byte>(content_description_object_size);

                                        mergedList.AddRange(content_description_object);
                                        mergedList.AddRange(title);
                                        mergedList.AddRange(auther);
                                        mergedList.AddRange(copyright);
                                        mergedList.AddRange(description);
                                        mergedList.AddRange(rating);

                                        content_description_object = mergedList.ToArray();

                                        // existence check
                                        // 既に登録されてるかどうかの確認！
                                        
                                        Boolean pflg2 = true;
                                        for (j = 0; j < c; j++)
                                        {
                                            y = 0;
                                            for (k = 0; k < 16; k++)
                                            {
                                                if (content_description_object[k] == buf[j + k])
                                                {
                                                    y++;
                                                }
                                                else { break; }
                                            }
                                            if (y == 16)
                                            {
                                                pflg2 = false;
                                                break;
                                            }
                                        }

                                        if (pflg2)
                                        {
                                            byte[] data50 = new byte[50];
                                            int i;
                                            for (i = 0; i < 50; i++)
                                            {
                                                data50[i] = buf[c + 4 - 50 + i];
                                            }
                                            for (i = 0; i < content_description_object_size; i++)
                                            {
                                                buf[c + 4 - 50 + i] = content_description_object[i];
                                            }
                                            for (i = 0; i < 50; i++)
                                            {
                                                buf[c + 4 - 50 + content_description_object_size + i] = data50[i];
                                            }

                                            buf[2] = (byte)(c + content_description_object_size);
                                            buf[3] = (byte)((c + content_description_object_size) >> 8);
                                            buf[10] = buf[2];
                                            buf[11] = buf[3];
                                            buf[36] = (byte)(buf[36] + 1);
                                            c = c + content_description_object_size;
                                        }

                                        // File Properties Object にGUIDを設定する
                                        // GUIDを生成
                                        byte[] s2mmsh_guid = Guid.NewGuid().ToByteArray();

                                        // グリニッジ標準の現在時刻
                                        DateTime dtNow = DateTime.Now.ToUniversalTime();

                                        // グリニッジ標準の開始時刻
                                        DateTime dtEpoc = new DateTime(1601, 1, 1, 0, 0, 0, DateTimeKind.Utc);

                                        // グリニッジ経過時刻を取得
                                        TimeSpan tsEpoc = dtNow.Subtract(dtEpoc);

                                        Int64 tm = Convert.ToInt64(tsEpoc.TotalMilliseconds * 10000); // 100ナノ秒
                                        //Int64 tm = 1;
                                        byte[] starttime = BitConverter.GetBytes(tm);
　　
                                        // File Properties Object
                                        byte[] file_properties_object =
                                            new byte[]{
        0xA1, 0xDC, 0xAB, 0x8C, 0x47, 0xA9, 0xCF, 0x11,
        0x8E, 0xE4, 0x00, 0xC0, 0x0C, 0x20, 0x53, 0x65
                                            };

                                        // オブジェクトの場所を見つける
                                        for (j = 0; j < c; j++)
                                        {
                                            y = 0;
                                            for (k = 0; k < 16; k++)
                                            {
                                                if (file_properties_object[k] == buf[j + k])
                                                {
                                                    y++;
                                                }
                                                else { break; }
                                            }
                                            if (y == 16)
                                            {
                                                //pflg2 = false;
                                                for (int m = 0; m < 16; m++)
                                                {
                                                    buf[j + 24 + m] = s2mmsh_guid[m];
                                                }
                                                for (int n = 0; n < 8; n++)
                                                {
                                                    buf[j + 48 + n] = starttime[n];
                                                }

                                                break;
                                            }
                                        }


                                        buf.CopyTo(asfData.asf_header, 0);
                                        Console.WriteLine("ASF header registered.");
                                        this.BeginInvoke(new Action<String>(delegate(String str) { this.logoutput("ASFヘッダを登録しました。"); }), new object[] { "" });
                                        if (pm.serverstatus) this.BeginInvoke(new Action<String>(delegate(String str) { this.logoutput("クライアント接続を受け付けます。"); }), new object[] { "" });
                                        asfData.asf_header_size = c + 4; //todo ここも追加する
                                        //asfData.asf_header_size = c + 4;
                                        asfData.asf_status = ASF_STATUS.ASF_STATUS_SET_HEADER;

                                    }
                                    else
                                        if (buf[1] == 'D' && (
                                        asfData.mmsh_status == MMSH_STATUS.MMSH_STATUS_ASF_HEADER_SEND
                                        || asfData.mmsh_status == MMSH_STATUS.MMSH_STATUS_ASF_DATA_SENDING
                                        ))
                                        {
                                            try
                                            {
                                                if (asfData.mms_sock != null)
                                                {
                                                    int a = asfData.mms_sock.Available;
                                                    asfData.mms_sock.Send(buf, c + 4, SocketFlags.None);
                                                    //Console.WriteLine("ASF Data sent.");
                                                    asfData.mmsh_status = MMSH_STATUS.MMSH_STATUS_ASF_DATA_SENDING;
                                                    a = asfData.mms_sock.Available;
                                                }
                                            }
                                            catch (SocketException ex)
                                            {

                                                Console.WriteLine("{0} Error code: {1}.", ex.Message, ex.ErrorCode);
                                                this.BeginInvoke(new Action<String>(delegate(String str) { this.logoutput(ex.Message); }), new object[] { "" }); 
                                                asfData.mmsh_status = MMSH_STATUS.MMSH_STATUS_NULL;
                                            }
                                        }
                                        else
                                        {
                                            //Console.WriteLine("unknown header.");
                                        }
                                }
                                else {
                                    //EOF
                                    this.BeginInvoke(new Action<String>(delegate(String str) { this.logoutput("入力がありません。配信を終了します。"); }), new object[] { "" });
                                        
                                    break; 
                                }
                            }
                            if (asfData.mms_sock != null)
                            {
                                asfData.mms_sock.Close();
                                asfData.mms_sock = null;

                            }
                        }
                        catch (ThreadAbortException) {
                            //無視
                        }
                        catch (Exception exx){
                            MessageBox.Show(exx.Message,
        "エラー",
        MessageBoxButtons.OK,
        MessageBoxIcon.Error);
                            ProcessInitialize();
                            
                        }
                        // todo 初期化

                    }));
                    pm.th_ffmpeg.Start();
                }
            }
            
            // closing
            this.button_disconnect.Enabled = true;
        }

        /// <summary>
        /// プロセス破棄
        /// </summary>
        private void ProcessInitialize()
        {
            ProcessManager pm = ProcessManager.Instance;

            if (pm.ffmpegstatus == FFMPEG_STATUS.FFMPEG_STATUS_PROCESS) // 初期化
            {
                pm.ffmpegstatus = FFMPEG_STATUS.FFMPEG_STATUS_INITIALIZING;
                //this.button_disconnect.Enabled = false;
                //disconnectButtonStateDeligate(false);
                this.BeginInvoke(new Action<Boolean>(delegate(Boolean bl) { this.disconnectButtonStateDeligate(false); }), new object[] { false });
                if (pm.process != null)
                {
                    pm.process.Dispose();
                    pm.process = null;
                }

                if (pm.th_server != null)
                {
                    pm.server.Close();
                    if (pm.th_server.IsAlive)
                        pm.th_server.Abort();
                    pm.th_server = null;
                }

                // 初期化
                AsfData asfData = AsfData.Instance;
                asfData.asf_status = ASF_STATUS.ASF_STATUS_NULL;
                asfData.asf_header_size = 0;
                asfData.asf_header = new byte[65535];
                asfData.mms_sock = null;
                asfData.mmsh_status = MMSH_STATUS.MMSH_STATUS_NULL;

                //this.button_exec.Enabled = true;
                //connectButtonStateDeligate(true);
                this.BeginInvoke(new Action<Boolean>(delegate(Boolean bl) { this.connectButtonStateDeligate(true); }), new object[] { true });

                //logoutput("接続を初期化しました。");
                this.BeginInvoke(new Action<String>(delegate(String str) { this.logoutput("接続を初期化しました。"); }), new object[] { "" });

                pm.ffmpegstatus = FFMPEG_STATUS.FFMPEG_STATUS_INITIALIZED;

                // スレッド自身の削除
                if (pm.th_ffmpeg != null)
                {
                    System.Threading.Thread tmp = pm.th_ffmpeg;
                    pm.th_ffmpeg = null;
                    if (tmp.IsAlive)
                        tmp.Abort(); // ここでスレッドは終了
                }
            }
        }

        private void p_Exited(object sender, EventArgs e)
        {
            //プロセスが終了したときに実行される
            logoutput("ffmpegが終了しました。");
            ProcessInitialize();
        }

        private void PrintErrorData(object sender, DataReceivedEventArgs e)
        {
            System.Diagnostics.Process p = (System.Diagnostics.Process)sender;

            if (!string.IsNullOrEmpty(e.Data))
                this.BeginInvoke(new Action<String>(delegate(String str) { this.logoutput(e.Data); }), new object[] { "" }); ;
        }
        private static void NetErrorDataHandler(object sendingProcess,
           DataReceivedEventArgs errLine)
        {
            Console.WriteLine("エラーが書き込まれました。");
        }

        private void HttpThread() {
            // サーバーソケット初期化
            ProcessManager pm = ProcessManager.Instance;
            AsfData asfData = AsfData.Instance;
            this.BeginInvoke(new Action<String>(delegate(String str) { this.logoutput("サーバを初期化します。"); }), new object[] { "" });
            //logoutputDelegate("サーバを初期化します。"); 
            pm.server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPAddress ip = IPAddress.Parse("127.0.0.1");
            IPEndPoint ipEndPoint = new IPEndPoint(ip, int.Parse(this.textBox_exPort.Text));

            pm.server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
            pm.server.Bind(ipEndPoint);
            pm.server.Listen(5);
            // 要求待ち（無限ループ）
            while (pm.serverstatus)
            {
                Socket sock = null;
                if (asfData.asf_status == ASF_STATUS.ASF_STATUS_SET_HEADER) this.BeginInvoke(new Action<String>(delegate(String str) { this.logoutput("クライアント接続を受け付けます。"); }), new object[] { "" });
                try
                {
                    sock = pm.server.Accept();
                }
                catch 
                {
                    //this.BeginInvoke(new Action<String>(delegate(String str) { this.textBox_log.AppendText("ソケット接続エラー。\r\n"); }), new object[] { "" });
                    //throw ex;
                    break;
                }
                HttpServer response = new HttpServer(sock, this);
                response.Start();
            }

            // server dispose
            if (pm.server != null)
            {
                pm.server.Close();
            }
            this.BeginInvoke(new Action<String>(delegate(String str) { this.logoutput("サーバを終了します。"); }), new object[] { "" });

        }



        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // スレッド・プロセス終了
            ProcessManager pm = ProcessManager.Instance;

            if (pm.process != null)
            {
                if (!pm.process.HasExited)
                {
                    // pm.process.CancelOutputRead();
                    pm.process.CancelErrorRead();
                    pm.process.Kill();
                    //pm.process.WaitForExit();
                }

                pm.process = null;
            }
            if (pm.th_server != null && pm.th_server.IsAlive)
            {
                
                pm.th_server.Abort();
                pm.th_server = null;
            }

            if (pm.th_ffmpeg != null && pm.th_ffmpeg.IsAlive)
            {
                pm.th_ffmpeg.Abort();
                pm.th_ffmpeg = null;
            }

            Properties.Settings.Default.ffmpegpath = this.textBox_ffmpegPath.Text;
            Properties.Settings.Default.inputstream = this.textBox_inputstream.Text;
            Properties.Settings.Default.audiorate = this.textBox_audiorate.Text;
            Properties.Settings.Default.videorate = this.textBox_videorate.Text;
            Properties.Settings.Default.port = this.textBox_exPort.Text;
            Properties.Settings.Default.reencode_flag = radioButton_reencode_1.Checked;
            Properties.Settings.Default.enc_width = this.textBox_enc_width.Text;
            Properties.Settings.Default.enc_height = this.textBox_enc_height.Text;
            Properties.Settings.Default.enc_bitrate_v = this.textBox_enc_bitrate_v.Text;
            Properties.Settings.Default.enc_bitrate_a = this.textBox_enc_bitrate_a.Text;
            Properties.Settings.Default.enc_framerate = this.textBox_enc_framerate.Text;

            Properties.Settings.Default.ffmpegcom_flag = radioButton_ffmpegcom_1.Checked;
            Properties.Settings.Default.ffmpegcom = this.textBox_ffmpegcom.Text;

            Properties.Settings.Default.content_title = this.tbx_Title.Text;
            Properties.Settings.Default.content_auther = this.tbx_Auther.Text;
            Properties.Settings.Default.content_copyright = this.tbx_Copyright.Text;
            Properties.Settings.Default.content_description = this.tbx_Description.Text;
            Properties.Settings.Default.content_rating = this.tbx_Rating.Text;

            Properties.Settings.Default.Save();
            
            base.OnClosing(e);
            Environment.Exit(0);
        }

        private void button_disconnect_Click(object sender, EventArgs e)
        {
            ProcessManager pm = ProcessManager.Instance;
            if (pm.ffmpegstatus == FFMPEG_STATUS.FFMPEG_STATUS_PROCESS) // 初期化
            {
                pm.ffmpegstatus = FFMPEG_STATUS.FFMPEG_STATUS_INITIALIZING;
                logoutput("接続を初期化します。");
                this.button_disconnect.Enabled = false;

                // スレッド・プロセス終了
                if (pm.process != null)
                {
                    if (!pm.process.HasExited)
                    {
                        try
                        {
                            //pm.process.CancelOutputRead();
                            pm.process.CancelErrorRead();
                        }
                        catch (Exception ex)
                        {
                            Console.Write(ex.Message);
                        }
                        pm.process.Kill();
                    }

                    pm.process = null;
                }
                if (pm.th_server != null)
                {
                    pm.server.Close();
                    if (pm.th_server.IsAlive)
                        pm.th_server.Abort();
                    pm.th_server = null;
                }

                if (pm.th_ffmpeg != null)
                {
                    if (pm.th_ffmpeg.IsAlive)
                        pm.th_ffmpeg.Abort();
                    pm.th_ffmpeg = null;
                }

                // 初期化
                AsfData asfData = AsfData.Instance;
                asfData.asf_status = ASF_STATUS.ASF_STATUS_NULL;
                asfData.asf_header_size = 0;
                asfData.asf_header = new byte[65535];
                asfData.mms_sock = null;
                asfData.mmsh_status = MMSH_STATUS.MMSH_STATUS_NULL;

                this.button_exec.Enabled = true;

                logoutput("接続を初期化しました。");

                pm.ffmpegstatus = FFMPEG_STATUS.FFMPEG_STATUS_INITIALIZED;
            }
            else {
                logoutput("接続は初期化中です。");
            }
        }

        public delegate void LogoutputDelegate(String str);
        public void logoutput(String str) {
            this.textBox_log.AppendText(DateTime.Now.ToString() + " " + str + "\r\n");
        }

        public delegate void ConnectButtonDelegate(Boolean bl);
        public void connectButtonState(Boolean bl)
        {
            this.button_exec.Enabled = bl;
        }

        public delegate void DisconnectButtonDelegate(Boolean bl);
        public void disconnectButtonState(Boolean bl)
        {
            this.button_disconnect.Enabled = bl;
        }

        private void radioButton_reencode_1_CheckedChanged(object sender, EventArgs e)
        {
            RadioButton radioButton = sender as RadioButton;

            if (radioButton.Checked)
            {
                Console.WriteLine(radioButton.Text + "が選択されました。");
                groupBox_reencode.Enabled = true;
            }
        }

        private void radioButton_reencode_2_CheckedChanged(object sender, EventArgs e)
        {
            RadioButton radioButton = sender as RadioButton;

            if (radioButton.Checked)
            {
                Console.WriteLine(radioButton.Text + "が選択されました。");
                groupBox_reencode.Enabled = false;
            }
        }

        private void textBox_ffmpegPath_DragDrop(object sender, DragEventArgs e)
        {
            //コントロール内にドロップされたとき実行される
            //ドロップされたすべてのファイル名を取得する
            string[] fileName =
                (string[])e.Data.GetData(DataFormats.FileDrop, false);
            textBox_ffmpegPath.Text = fileName[0];
        }

        private void textBox_ffmpegPath_DragEnter(object sender, DragEventArgs e)
        {
            //コントロール内にドラッグされたとき実行される
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                //ドラッグされたデータ形式を調べ、ファイルのときはコピーとする
                e.Effect = DragDropEffects.Copy;
            else
                //ファイル以外は受け付けない
                e.Effect = DragDropEffects.None;
        }

        private void radioButton_ffmpegcom_1_CheckedChanged(object sender, EventArgs e)
        {
            RadioButton radioButton = sender as RadioButton;

            if (radioButton.Checked)
            {
                Console.WriteLine(radioButton.Text + "が選択されました。");
                textBox_ffmpegcom.Enabled = true;
            }
        }

        private void radioButton_ffmpegcom_2_CheckedChanged(object sender, EventArgs e)
        {
            RadioButton radioButton = sender as RadioButton;

            if (radioButton.Checked)
            {
                Console.WriteLine(radioButton.Text + "が選択されました。");
                textBox_ffmpegcom.Enabled = false;
            }
        }

        private void button_current_command_Click(object sender, EventArgs e)
        {
            string url = this.textBox_inputstream.Text;
            url = System.Text.RegularExpressions.Regex.Replace(
                url,
                @"^mms://",
                "mmsh://");
            url = System.Text.RegularExpressions.Regex.Replace(
               url,
               @"^http://",
               "mmsh://");
            if (this.radioButton_reencode_1.Checked == false)
            {
                textBox_ffmpegcom.Text = String.Format(" -v error -i {0} -c copy -f asf_stream -", url);
            }
            else
            {
                int width = 0;
                int height = 0;
                int bitrate_v = 0;
                int bitrate_a = 0;
                float framerate = 0;
                try
                {
                    width = int.Parse(this.textBox_enc_width.Text);
                    height = int.Parse(this.textBox_enc_height.Text);
                    bitrate_v = int.Parse(this.textBox_enc_bitrate_v.Text) * 1000;
                    bitrate_a = int.Parse(this.textBox_enc_bitrate_a.Text) * 1000;
                    framerate = float.Parse(this.textBox_enc_framerate.Text);
                }
                catch (Exception)
                {
                    //logoutput("エンコード設定が不正です。再エンコードしません。");
                    textBox_ffmpegcom.Text = String.Format(" -v error -i {0} -c copy -f asf_stream -", url);
                }
                string strsize = String.Format("{0}x{1}", width, height);
                if (width == 0 || height == 0) strsize = "320x240";
                if (bitrate_v == 0) bitrate_v = 256000;
                if (bitrate_a == 0) bitrate_a = 128000;
                if (framerate == 0) framerate = 15;

                textBox_ffmpegcom.Text = String.Format(
                    " -v error -i {0} -acodec wmav2 -ab {1} -vcodec wmv2 -vb {2} -s {3} -r {4} -f asf_stream -",
                    url,
                    bitrate_a,
                    bitrate_v,
                    strsize,
                    framerate);
            }
        }



        /* tagenya transport */
        ///* print debug info */
        //static bool print_element_info(CustomData data)
        //{
        //    Iterator ite;
        //    Element elem;
        //    int i;
        //    bool done;

        //    //if (sigint_flag)
        //    //{
        //        //sigint_flag = FALSE;

        //        Debug.Print("received SIGINT: print all elements' information. \n");
        //        Debug.Print("<<<<parent pipeline>>>> \n");
        //        Debug.Print("pipeline %s states: %d\n", ((Gst.Object)data.pipeline).Name, ((Element)data.pipeline).CurrentState;

        //        ite = ((Gst.Bin)data.pipeline).;
        //        done = FALSE;
        //        while (!done)
        //        {
        //            switch (gst_iterator_next(ite, (GValue*)&elem))
        //            {
        //                case GST_ITERATOR_OK:

        //                    g_print("%s states: %d\n", GST_ELEMENT_NAME(elem), GST_STATE(elem));
        //                    Debug.Print("base timestamp: %llu  \n", gst_element_get_base_time(elem));
        //                    Debug.Print("start timestamp: %llu  \n", gst_element_get_start_time(elem));

        //                    break;
        //                case GST_ITERATOR_RESYNC:
        //                    gst_iterator_resync(ite);
        //                    break;
        //                case GST_ITERATOR_ERROR:
        //                    done = TRUE;
        //                    break;
        //                case GST_ITERATOR_DONE:
        //                    done = TRUE;
        //                    break;
        //            }
        //        }
        //        gst_iterator_free(ite);

        //        g_print("<<<<child pipeline>>>> \n");

        //        for (i = 0; i < stream_amount; i++)
        //        {
        //            g_print("pipeline %s states: %d\n", GST_ELEMENT_NAME(data->mms[i]->pipeline), GST_STATE(data->mms[i]->pipeline));
        //            ite = gst_bin_iterate_elements(GST_BIN(data->mms[i]->pipeline));
        //            done = FALSE;
        //            while (!done)
        //            {
        //                switch (gst_iterator_next(ite, (GValue*)&elem))
        //                {
        //                    case GST_ITERATOR_OK:

        //                        g_print("%s states: %d\n", GST_ELEMENT_NAME(elem), GST_STATE(elem));
        //                        Debug.Print("base timestamp: %llu  \n", gst_element_get_base_time(elem));
        //                        Debug.Print("start timestamp: %llu  \n", gst_element_get_start_time(elem));

        //                        break;
        //                    case GST_ITERATOR_RESYNC:
        //                        gst_iterator_resync(ite);
        //                        break;
        //                    case GST_ITERATOR_ERROR:
        //                        done = TRUE;
        //                        break;
        //                    case GST_ITERATOR_DONE:
        //                        done = TRUE;
        //                        break;
        //                }
        //            }
        //            gst_iterator_free(ite);
        //        }

        //        /* write debug info */
        //        print_pad_capabilities(data->sink, "sink");
        //        print_pad_capabilities(data->mms[0]->v_appsink, "sink");
        //    //}

        //    return true;

        //}

        /* quit all generated loops */
        //void main_loop_quit_all(CustomData data)
        //{
        //    //gint i;
        //    //for (i = 0; i < stream_amount; i++)
        //    //{
        //    //    g_main_loop_quit(data.mms[i].loop);
        //    //}
        //    data.loop.Quit();
        //}

        /* bus message from pipeline */
        static bool
          bus_call(
          Gst.Bus bus,
          Gst.Message msg,
          CustomData gdata)
        {
            CustomData data = gdata;
            MainLoop loop = data.loop;
            string[] str;
            MmsData mmsdata;
            UInt64 number;
            System.Object a, b;

            string[] delimiter = { "__" };

            switch (msg.Type)
            {
                case MessageType.Element:
                    Debug.Print("Element message received at parent pipeline\n");
                    /* unlink mms stream elements */
                    str = msg.Src.Name.Split(delimiter, StringSplitOptions.RemoveEmptyEntries);
                    //str = String.Split(msg.Src.Name, "__", 2);
                    if (str[1] != null)
                    {
                        /* parse number */
                        //number = g_ascii_strtoull(str[1], null, 10);
                        number = UInt64.Parse(str[1]);
                        Debug.Print("Caught EOS event at %s on #%d\n", msg.Src.Name, number);
                        mmsdata = data.mms[number];

                        /* dispose */
                        a = (System.Object)msg.Src;
                        if (a == (System.Object)mmsdata.a_app_q)
                        {
                            mmsdata.a_appsrc.SetState(Gst.State.Null);
                            mmsdata.a_app_q.SetState(Gst.State.Null);
                            Element[] ere = { data.pipeline, mmsdata.a_appsrc, mmsdata.a_app_q };
                            data.pipeline.Remove(ere);

                            mmsdata.prob_hd_a_eos = 0;
                        }
                        else if (a == (System.Object)mmsdata.v_app_q)
                        {
                            mmsdata.v_appsrc.SetState(Gst.State.Null);
                            mmsdata.v_app_q.SetState(Gst.State.Null);
                            Element[] ere = { mmsdata.v_appsrc, mmsdata.v_app_q };
                            data.pipeline.Remove(ere);
                            mmsdata.prob_hd_v_eos = 0;
                        }

                        break;
                    }
                    break;
                case MessageType.Eos:
                    Debug.Print("End of stream at parent pipeline\n");
                    /* unlink mms stream elements */
                    str = msg.Src.Name.Split(delimiter, StringSplitOptions.RemoveEmptyEntries);
                    if (str[1] != null)
                    {
                        /* parse number */
                        number = UInt64.Parse(str[1]);
                        Debug.Print("Caught EOS event at %s on #%d\n", msg.Src.Name, number);
                        mmsdata = data.mms[number];

                        /* dispose */
                        a = (System.Object)msg.Src;
                        b = (System.Object)mmsdata.a_app_q;
                        if (a == b)
                        {
                            mmsdata.a_appsrc.SetState(Gst.State.Null);
                            mmsdata.a_app_q.SetState(Gst.State.Null);
                            Element[] ere = { data.pipeline, mmsdata.a_appsrc, mmsdata.a_app_q };
                            data.pipeline.Remove(ere);

                            mmsdata.prob_hd_a_eos = 0;
                        }
                        else if (a == (System.Object)mmsdata.v_app_q)
                        {
                            mmsdata.v_appsrc.SetState(Gst.State.Null);
                            mmsdata.v_app_q.SetState(Gst.State.Null);
                            Element[] ere = { mmsdata.v_appsrc, mmsdata.v_app_q };
                            data.pipeline.Remove(ere);
                            mmsdata.prob_hd_v_eos = 0;
                        }

                        break;
                    }

                    main_loop_quit_all(data);
                    break;

                case MessageType.Error:
                    {
                        //string debug;
                        Enum error;
                        Debug.Print("Message received. Type:%s from %s\n", msg.Type, msg.Src.Name);
                        //gst_message_parse_error(msg, &error, &debug);
                        msg.ParseError(out error);
                        Debug.Print("Error received from element %s: %s\n", msg.Src.Name, error);
                        //Debug.Print("Debugging information: %s\n", debug == null ? debug : "none");

                        /* unlink mms stream elements */
                        str = msg.Src.Name.Split(delimiter, StringSplitOptions.RemoveEmptyEntries);
                        if (str[1] != null)
                        {
                            /* parse number */
                            number = UInt64.Parse(str[1]);
                            mmsdata = data.mms[number];
                            /* remove from bin */
                            if (mmsdata != null)
                            {
                                /* dispose */
                                dispose_mms_stream(mmsdata);

                            }
                            else
                            {
                                Debug.Print("mms stream #%d isn't find.\n", number);
                            }

                            //g_error_free(error);
                            //g_free(debug);
                            break;
                        }

                        // if parent stream error coused , close process
                        //g_error_free(error);
                        //g_free(debug);

                        main_loop_quit_all(data);
                        break;
                    }
                case MessageType.Warning:
                    {
                        //string debug;
                        Enum error;
                        Debug.Print("Message received. Type:%s from %s\n", msg.Type, msg.Src.Name);
                        msg.ParseWarning(out error);
                        Debug.Print("Warning received from element %s: %s\n", msg.Src.Name, error);
                        //g_printerr("Debugging information: %s\n", debug ? debug : "none");
                        break;
                    }
                case MessageType.StateChanged:
                    /* We are only interested in state-changed messages from the pipeline */
                    {
                        State old_state, new_state, pending_state;

                        msg.ParseStateChanged(out old_state, out new_state, out pending_state);
                        //gst_message_parse_state_changed(msg, &old_state, &new_state, &pending_state);

                        Debug.Print("Element %s state changed from %s to %s:\n",
                          msg.Src.Name, old_state, new_state);

                    }
                    break;
                case MessageType.NewClock:
                    Debug.Print("New Clock Created\n");
                    break;
                case MessageType.ClockLost:
                    Debug.Print("Clock Lost\n");
                    break;
                case MessageType.Latency:
                    Debug.Print("Pipeline required latency.\n");
                    break;
                case MessageType.StreamStatus:
                    {
                        StreamStatusType type;
                        Element owner;
                        msg.ParseStreamStatus(out type, out owner);
                        //gst_message_parse_stream_status(msg, &type, &owner);
                        Debug.Print("Stream_status received from element %s type: %d owner:%s\n", 
                            msg.Src.Name, type, owner.Name);
                        break;
                    }
                default:
                    Debug.Print("Message received. Type:%s from %s\n", msg.Type, msg.Src.Name);
                    break;
            }

            return true;
        }

        /* bus message from mms pipeline */
        static bool
          bus_call_sub(Gst.Bus bus,
          Gst.Message msg,
          MmsData gdata)
        {
            MmsData mmsdata = gdata;
            MainLoop loop = mmsdata.loop;
            string[] str;
            UInt64 number;
            // GstPad *pad;
            CustomData parent = mmsdata.parent;
            string[] delimiter = { "__" };
            switch (msg.Type)
            {

                case MessageType.Eos:
                    Debug.Print("End of stream at sub stream pipeline\n");
                    /* unlink mms stream elements */
                    Debug.Print("GST_OBJECT_NAME (msg->src): %s\n", msg.Src.Name);
                    str = msg.Src.Name.Split(delimiter, StringSplitOptions.RemoveEmptyEntries);

                    if (str[1] != null)
                    {
                        /* parse number */
                        number = UInt64.Parse(str[1]);
                        Debug.Print("Caught EOS event at %s on #%d\n", msg.Src.Name, number);

                        /* dispose */
                        dispose_mms_stream(mmsdata);
                        break;
                    }
                    break;

                case MessageType.Error:
                    {
                        //gchar* debug;
                        Enum error;
                        Debug.Print("Message received. Type:%s from %s\n", msg.Type, msg.Src.Name);
                        msg.ParseError(out error);
                        Debug.Print("Error received from element %s: %s\n", msg.Src.Name, error);
                        //g_printerr("Debugging information: %s\n", debug ? debug : "none");
                        /* dispose */
                        dispose_mms_stream(mmsdata);

                        break;
                    }
                case MessageType.Warning:
                    {
                        //gchar* debug;
                        Enum error;
                        Debug.Print("Message received. Type:%s from %s\n", msg.Type, msg.Src.Name);
                        msg.ParseWarning(out error);
                        //gst_message_parse_warning(msg, &error, &debug);
                        Debug.Print("Warning received from element %s: %s\n", msg.Src.Name, error);
                        //g_printerr("Debugging information: %s\n", debug ? debug : "none");
                        break;
                    }
                case MessageType.StateChanged:
                    /* We are only interested in state-changed messages from the pipeline */
                    {
                        State old_state, new_state, pending_state;
                        msg.ParseStateChanged(out old_state, out new_state, out pending_state);

                        Debug.Print("Element %s state changed from %s to %s:\n",
                          msg.Src.Name, old_state, new_state);
                    }
                    break;
                case MessageType.NewClock:
                    Debug.Print("New Clock Created\n");
                    break;
                case MessageType.ClockLost:
                    Debug.Print("Clock Lost\n");
                    break;
                case MessageType.Latency:
                    Debug.Print("Pipeline required latency.\n");
                    break;
                case MessageType.StreamStatus:
                    {
                        StreamStatusType type;
                        Element owner;
                        msg.ParseStreamStatus(out type, out owner);
                        Debug.Print("Stream_status received from element %s type: %d owner:%s\n", msg.Src.Name, type, owner.Name);
                        break;
                    }
                default:
                    Debug.Print("Message received. Type:%s from %s\n", msg.Type, msg.Src.Name);
                    break;
            }

            return true;
        }

        /* This function will be called by the pad-added signal */
        static void pad_added_handler1(Element src, Pad new_pad, CustomData data)
        {
            Pad vsink_pad = data.scale1.GetStaticPad("sink");
            Caps sink_pad_caps = null;

            PadLinkReturn ret = PadLinkReturn.Noformat;
            Caps new_pad_caps = null;
            Structure new_pad_struct = null;
            string new_pad_type = null;

            Debug.Print("Received new pad '%s' from '%s':\n", new_pad.Name, src.Name);

            /* Check the new pad's type */
            //new_pad_caps   = gst_pad_get_caps (new_pad);
            //new_pad_caps = gst_pad_query_caps(new_pad, null);
            new_pad_caps = new_pad.Caps;
            //new_pad_struct = gst_caps_get_structure(new_pad_caps, 0);
            new_pad_struct = new_pad_caps[0];
            //new_pad_type = gst_structure_get_name(new_pad_struct);
            new_pad_type = new_pad_struct.Name;

            //sink_pad_caps = gst_pad_get_caps (vsink_pad);
            //sink_pad_caps = gst_pad_query_caps(vsink_pad, NULL);
            sink_pad_caps = vsink_pad.Caps;
            
            /* Attempt the link */
            if (new_pad_type.StartsWith("audio/x-raw"))
            {
                //goto exig;
            }
            else
            {
                //ret = gst_pad_link(new_pad, vsink_pad);
                ret = new_pad.Link(vsink_pad);
            }

            if (ret != PadLinkReturn.Ok)
            {
                Debug.Print("  Type is '%s' but link failed.\n", new_pad_type);
            }
            else
            {
                Debug.Print("  Link succeeded (type '%s').\n", new_pad_type);
            }

        //exig:
            /* Unreference the new pad's caps, if we got them */
            //if (new_pad_caps != null)
            //    gst_caps_unref(new_pad_caps);

            //if (sink_pad_caps != null)
            //    gst_caps_unref(sink_pad_caps);

            ///* Unreference the sink pad */
            //gst_object_unref(vsink_pad);
        }

        /* This function will be called by the pad-added signal */
        /* for mms stream */
        static void mms_pad_added_handler(Element src, Pad new_pad, MmsData data)
        {
            Pad vsink_pad;
            Pad asink_pad;

            PadLinkReturn ret = PadLinkReturn.Noformat;
            Caps new_pad_caps = null;
            Structure new_pad_struct = null;
            string new_pad_type = null;

            Debug.Print("Received new pad '%s' from '%s':\n", new_pad.Name, src.Name);

            /* Check the new pad's type */
            //new_pad_caps   = gst_pad_get_caps (new_pad);
            //new_pad_caps = gst_pad_query_caps(new_pad, NULL);
            //new_pad_struct = gst_caps_get_structure(new_pad_caps, 0);
            //new_pad_type = gst_structure_get_name(new_pad_struct);
            new_pad_caps = new_pad.Caps;
            new_pad_struct = new_pad_caps[0];
            new_pad_type = new_pad_struct.Name;

            /* Attempt the link */
            if (new_pad_type.StartsWith("audio/x-raw"))
            {
                Element[] ele = { data.a_queue, data.a_convert, data.a_resample, data.a_appsink };
                data.pipeline.Add(ele);
                data.a_queue.Link(data.a_convert);
                data.a_convert.Link(data.a_resample);
                data.a_resample.Link(data.a_appsink);

                asink_pad = data.a_queue.GetStaticPad("sink");
                ret = new_pad.Link(asink_pad);

                data.a_appsink.SetState(State.Playing);
                data.a_resample.SetState(State.Playing);
                data.a_convert.SetState(State.Playing);
                data.a_queue.SetState(State.Playing);

                //gst_object_unref(asink_pad);

            }
            else if (new_pad_type.StartsWith("video"))
            {
                Element[] ele = { data.v_queue, data.colorspace, data.scale, data.rate, data.filter, data.videobox, data.v_appsink, };
                data.pipeline.Add(ele);
                data.v_queue.Link(data.colorspace);
                data.colorspace.Link(data.scale);
                data.scale.Link(data.filter);
                data.filter.Link(data.videobox);
                data.videobox.Link(data.v_appsink);
                vsink_pad = data.v_queue.GetStaticPad("sink");
                ret = new_pad.Link(vsink_pad);

                data.v_appsink.SetState(State.Playing);
                data.videobox.SetState(State.Playing);
                data.filter.SetState(State.Playing);
                data.rate.SetState(State.Playing);
                data.scale.SetState(State.Playing);
                data.colorspace.SetState(State.Playing);
                data.v_queue.SetState(State.Playing);

                //gst_object_unref(vsink_pad);
            }

            if (ret != PadLinkReturn.Ok)
            {
                Debug.Print("  Type is '%s' but link failed.\n", new_pad_type);
            }
            else
            {
                Debug.Print("  Link succeeded (type '%s').\n", new_pad_type);
            }

            /* Unreference the new pad's caps, if we got them */
            //if (new_pad_caps != NULL)
            //    gst_caps_unref(new_pad_caps);
        }



        /* quit all generated loops */
        static public void main_loop_quit_all(CustomData data)
        {
            data.mms[0].loop.Quit();
            data.loop.Quit();
        }

        /* Dispose mms stream and appsrc injection */
        static void
          dispose_mms_stream(MmsData mmsdata)
        {
            Pad pad;
            CustomData parent = mmsdata.parent;
            //FlowReturn ret;
            //gboolean ret;

            Debug.Print("Dispose mms stream on #%d stream...\n", mmsdata.number);

            pad = mmsdata.source.GetStaticPad("src");
            if (pad == null) Debug.Print("fail to get pad.\n");
            //gst_pad_remove_buffers_probe (pad, mmsdata->prob_hd);
            pad.RemoveEventProbe(mmsdata.prob_hd);
            //gst_pad_remove_probe(pad, mmsdata->prob_hd);
            mmsdata.prob_hd = 0;
            
            //gst_object_unref(pad);
            //gst_element_set_state(mmsdata->pipeline, GST_STATE_null);
            mmsdata.pipeline.SetState(Gst.State.Null);

            // main
            Element[] ele = {mmsdata.source, mmsdata.queue, mmsdata.decoder};
            mmsdata.pipeline.Remove(ele);

            // video

            if (mmsdata.pipeline.GetByName(mmsdata.v_appsink.Name) != null)
            {
                Element[] ele2 = { mmsdata.v_queue, mmsdata.scale, mmsdata.rate, mmsdata.filter, mmsdata.videobox, mmsdata.v_appsink };
                mmsdata.pipeline.Remove(ele2);
            }

            // audio
            if (mmsdata.pipeline.GetByName(mmsdata.a_appsink.Name) != null)
            {
                Element[] ele2 = { mmsdata.a_queue, mmsdata.a_convert, mmsdata.a_resample, mmsdata.a_appsink };
                mmsdata.pipeline.Remove(ele2);
            }

            mmsdata.buffer_time = 0;
            mmsdata.clock = (long)Clock.Second;

            /* dispose appsrc */

            // video
            if (mmsdata.pipeline.GetByName(mmsdata.v_appsink.Name) != null)
            {
                mmsdata.v_appsrc.Emit("end-of-stream");
                //g_signal_emit_by_name(mmsdata->v_appsrc, "end-of-stream", &ret);
            }

            // audio
            if (mmsdata.pipeline.GetByName(mmsdata.a_appsink.Name) != null)
            {
                mmsdata.a_appsrc.Emit("end-of-stream");
            }

            Debug.Print("Disposed #%d stream...\n", mmsdata.number);
        }
    }
}


//todo
//D&D対応 OK
//ASFコメント追加
//EOF時の自動切断 OK
//ffmpegコンソール直修
//バージョン管理
//プレイリストクラスの生成
//ストリーム保存