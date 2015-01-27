﻿using System;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Collections;

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
            this.textBox_pushAddr.Text = Properties.Settings.Default.push_addr;

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
            Button btn = sender as Button;
            Boolean push_mode = false;
            if (btn.Name.Equals("button_exec_push"))
            {
                push_mode = true;
            }

            // 設定チェック
            if (push_mode) 
            {
                // アドレスチェック (おさしみ標準スタイル)
                if (!System.Text.RegularExpressions.Regex.IsMatch(
                    textBox_pushAddr.Text,
                    @"[-a-zA-Z_0-9\.]+:[0-9]{1,5}",
                    System.Text.RegularExpressions.RegexOptions.ECMAScript))
                {
                    logoutputDelegate("PUSH先アドレスが不正です。");
                    return;
                }
            }
            else 
            { 
                // ポートチェック
                if(this.textBox_exPort.Text == "")
                {
                    logoutputDelegate("配信ポートが未入力です。");
                    return;
                }
                try
                {
                    int test = int.Parse(this.textBox_exPort.Text);
                    if (test > 65535 || test < 0)
                    {
                        logoutputDelegate("配信ポートが不正です。");
                        return;
                    }
                }
                catch {
                    logoutputDelegate("配信ポートが不正です。");
                    return;
                }
            }

            //this.textBox_log.AppendText("入力ストリームに接続します。\r\n");
            logoutputDelegate("入力ストリームに接続します。"); 

            ProcessManager pm = ProcessManager.Instance;
            pm.ffmpegstatus = FFMPEG_STATUS.FFMPEG_STATUS_PROCESS;

            // init
            this.button_exec.Enabled = false;
            this.button_exec_push.Enabled = false;
            this.button_disconnect.Enabled = true;

            if (!push_mode)
            {
                // httpserver listening
                pm.th_server = new Thread(
                    new ThreadStart(HttpThread)
                );
                pm.th_server.Start();
            }

            // ffmpeg tcp stream receive process
            if (pm.process == null)
            {
                if (pm.th_ffmpeg == null)
                {
                    pm.th_ffmpeg = new Thread(new ThreadStart(delegate()
                    {
                        try
                        {
                            pm.process = new Process();
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

                            int c = 0; // peyload size
                            int h = 0; // header object size
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
                                        // ヘッダオブジェクトサイズ
                                        h = buf[28] + (buf[29] << 8);

                                        // ストリーム番号を取得する

                                        // Stream Properties Object
                                        byte[] stream_properties_object =
                                            new byte[]{
                                                0x91, 0x07, 0xDC, 0xB7, 0xB7, 0xA9, 0xCF, 0x11,
                                                0x8E, 0xE6, 0x00, 0xC0, 0x0C, 0x20, 0x53, 0x65
                                            };

                                        byte[] stream_type_video =
                                            new byte[]{
                                                0xC0, 0xEF, 0x19, 0xBC, 0x4D, 0x5B, 0xCF, 0x11,
                                                0xA8, 0xFD, 0x00, 0x80, 0x5F, 0x5C, 0x44, 0x2B
                                            };

                                        byte[] stream_type_audio =
                                            new byte[]{
                                                0x40, 0x9E, 0x69, 0xF8, 0x4D, 0x5B, 0xCF, 0x11,
                                                0xA8, 0xFD, 0x00, 0x80, 0x5F, 0x5C, 0x44, 0x2B
                                            };

                                        int[] stream_num = new int[8];
                                        int[] stream_type = new int[8];
                                        int stream_amount = 0;

                                        // オブジェクトの場所を見つける
                                        for (int spo_j = 0; spo_j < c; spo_j++)
                                        {
                                            int spo_y = 0;
                                            for (int spo_k = 0; spo_k < 16; spo_k++)
                                            {
                                                if (stream_properties_object[spo_k] == buf[spo_j + spo_k])
                                                {
                                                    spo_y++;
                                                }
                                                else { break; }
                                            }
                                            if (spo_y == 16) // 読みだす
                                            {
                                                if (compBinaryList(buf, spo_j + 24, stream_type_video, 0, 16))
                                                {
                                                    stream_num[stream_amount] = buf[spo_j + 72];
                                                    stream_type[stream_amount] = 1;
                                                    stream_amount++;
                                                }
                                                else if (compBinaryList(buf, spo_j + 24, stream_type_audio, 0, 16))
                                                {
                                                    stream_num[stream_amount] = buf[spo_j + 72];
                                                    stream_type[stream_amount] = 2;
                                                    stream_amount++;
                                                }

                                                //break;
                                            }
                                        }

                                        if (stream_amount == 0) {
                                            this.BeginInvoke(new Action<String>(delegate(String str) { this.logoutput("有効なストリームがありません。"); }), new object[] { "" });
                                            break;
                                        }
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
                                        
                                        byte[] bitrate_property_head =
                                            new byte[]{
                                                0xCE, 0x75, 0xF8, 0x7B, 0x8D, 0x46, 0xD1, 0x11,
                                                0x8D, 0x82, 0x00, 0x60, 0x97, 0xC9, 0xA2, 0xB2,
                                                (byte)(26+6*stream_amount), 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                                                (byte)stream_amount, 0x00 //Bitrate Records Count
                                            };

                                        ArrayList list1 = new ArrayList(bitrate_property_head);

                                        for(int bph_i=0;bph_i<stream_amount;bph_i++)
                                        {
                                            
                                            if (stream_type[bph_i] == 1) // video
                                            {
                                                list1.AddRange(
                                                    new ArrayList(new byte[]{
                                                    // video bitrate
                                                    (byte)stream_num[bph_i], 0x00, 
                                                    (byte)bitrate, 
                                                    (byte)((bitrate >> 8) & 0xFF), 
                                                    (byte)((bitrate >> 16) & 0xFF), 
                                                    (byte)((bitrate >> 24) & 0xFF)
                                                }));
                                            }
                                            else if (stream_type[bph_i] == 2) // audio
                                            {
                                                list1.AddRange(
                                                    new ArrayList(
                                                    new byte[]{
                                                    // video bitrate
                                                    (byte)stream_num[bph_i], 0x00, 
                                                    (byte)audiorate, 
                                                    (byte)((audiorate >> 8) & 0xFF), 
                                                    (byte)((audiorate >> 16) & 0xFF), 
                                                    (byte)((audiorate >> 24) & 0xFF)
                                                }));
                                            }
                                        }

                                        byte[] bitrate_property = (byte[])list1.ToArray(typeof (byte));
                                        int bitrate_property_size = list1.Count;

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
                                            for (i = 0; i < bitrate_property_size; i++)
                                            {
                                                buf[c + 4 - 50 + i] = bitrate_property[i];
                                            }
                                            for (i = 0; i < 50; i++)
                                            {
                                                buf[c + 4 - 50 + bitrate_property_size + i] = data50[i];
                                            }

                                            buf[2] = (byte)(c + bitrate_property_size);
                                            buf[3] = (byte)((c + bitrate_property_size) >> 8);
                                            buf[10] = buf[2];
                                            buf[11] = buf[3];
                                            buf[28] = (byte)(h + bitrate_property_size);
                                            buf[29] = (byte)((h + bitrate_property_size) >> 8);
                                            buf[36] = (byte)(buf[36] + 1);
                                            c = c + bitrate_property_size;
                                            h = h + bitrate_property_size;
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
                                        
                                       
                                        System.Collections.Generic.List<byte> mergedList = 
                                            new System.Collections.Generic.List<byte>(content_description_object_size);

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
                                            buf[28] = (byte)(h + content_description_object_size);
                                            buf[29] = (byte)((h + content_description_object_size) >> 8);
                                            buf[36] = (byte)(buf[36] + 1);
                                            c = c + content_description_object_size;
                                            h = h + content_description_object_size;
                                        }

                                        // header extension objectに追加する
                                        // 何も入ってない状態を前提とする

                                        // Header Extension Object 場所特定 
                                        byte[] header_extension_object =
                                            new byte[]{
                                                0xB5, 0x03, 0xBF, 0x5F, 0x2E, 0xA9, 0xCF, 0x11,
                                                0x8E, 0xE3, 0x00, 0xC0, 0x0C, 0x20, 0x53, 0x65
                                            };
                                        int heo_p=0;
                                        for (int heo_j = 0; heo_j < c; heo_j++)
                                        {
                                            int heo_y = 0;
                                            for (int heo_k = 0; heo_k < 16; heo_k++)
                                            {
                                                if (header_extension_object[heo_k] == buf[heo_j + heo_k])
                                                {
                                                    heo_y++;
                                                }
                                                else { break; }
                                            }
                                            if (heo_y == 16) // 読みだす
                                            {
                                                heo_p = heo_j;
                                                break;
                                            }
                                        }

                                        // Extended Stream Properties Object 作成
                                        byte[] extended_stream_properties_object =
                                            new byte[]{
                                                0xCB, 0xA5, 0xE6, 0x14, 0x72, 0xC6, 0x32, 0x43,
                                                0x83, 0x99, 0xA9, 0x69, 0x52, 0x06, 0x5B, 0x5A
                                            };

                                        mergedList = new System.Collections.Generic.List<byte>();

                                        for (int i = 0; i < stream_amount; i++) {
                                            if (stream_type[i] == 1) // video
                                            {
                                                mergedList.AddRange(
                                                    new System.Collections.Generic.List<byte>(extended_stream_properties_object));
                                                mergedList.AddRange(
                                                    new System.Collections.Generic.List<byte>(new byte[]{
                                                    0x58, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // object size
                                                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // start time
                                                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // end time
                                                    (byte)bitrate, 
                                                    (byte)((bitrate >> 8) & 0xFF), 
                                                    (byte)((bitrate >> 16) & 0xFF), 
                                                    (byte)((bitrate >> 24) & 0xFF),
                                                    0x88, 0x13, 0x00, 0x00, // buffer size 5000
                                                    0x00, 0x00, 0x00, 0x00,
                                                    (byte)bitrate, 
                                                    (byte)((bitrate >> 8) & 0xFF), 
                                                    (byte)((bitrate >> 16) & 0xFF), 
                                                    (byte)((bitrate >> 24) & 0xFF),
                                                    0x88, 0x13, 0x00, 0x00,
                                                    0x00, 0x00, 0x00, 0x00,
                                                    0x00, 0x00, 0x00, 0x00, // muximam object size 
                                                    0x00, 0x00, 0x00, 0x00, // flag
                                                    (byte)stream_num[i], 0x00, // stream number
                                                    0x00, 0x00, 
                                                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // avarage time per frame
                                                    0x00, 0x00, 
                                                    0x00, 0x00

                                                }));
                                            }
                                            else if (stream_type[i] == 2) // audio
                                            {
                                                mergedList.AddRange(
                                                    new System.Collections.Generic.List<byte>(extended_stream_properties_object));
                                                mergedList.AddRange(
                                                    new System.Collections.Generic.List<byte>(new byte[]{
                                                    0x58, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // object size
                                                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // start time
                                                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // end time
                                                    (byte)audiorate, 
                                                    (byte)((audiorate >> 8) & 0xFF), 
                                                    (byte)((audiorate >> 16) & 0xFF), 
                                                    (byte)((audiorate >> 24) & 0xFF),
                                                    0x88, 0x13, 0x00, 0x00, // buffer size 5000
                                                    0x00, 0x00, 0x00, 0x00,
                                                    (byte)audiorate, 
                                                    (byte)((audiorate >> 8) & 0xFF), 
                                                    (byte)((audiorate >> 16) & 0xFF), 
                                                    (byte)((audiorate >> 24) & 0xFF),
                                                    0x88, 0x13, 0x00, 0x00,
                                                    0x00, 0x00, 0x00, 0x00,
                                                    0x00, 0x00, 0x00, 0x00, // muximam object size 
                                                    0x00, 0x00, 0x00, 0x00, // flag
                                                    (byte)stream_num[i], 0x00, // stream number
                                                    0x00, 0x00, 
                                                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // avarage time per frame
                                                    0x00, 0x00, 
                                                    0x00, 0x00

                                                }));
                                            }
                                        }

                                        int extended_stream_properties_object_size = mergedList.Count;
                                        extended_stream_properties_object = mergedList.ToArray();

                                        // Header Extension Object 修正
                                        buf[heo_p + 16] = (byte)(46 + extended_stream_properties_object_size);
                                        buf[heo_p + 17] = (byte)((46 + extended_stream_properties_object_size) >> 8);
                                        buf[heo_p + 42] = (byte)(extended_stream_properties_object_size);
                                        buf[heo_p + 43] = (byte)((extended_stream_properties_object_size) >> 8);
                                        
                                        // データ挿入
                                        // byte[] dataRemain = new byte[c + 4 - (heo_p + 46)];
                                        int heo_i;
                                        for (heo_i = 0; heo_i < c + 4 - (heo_p + 46); heo_i++)
                                        {
                                            buf[c + 4 + extended_stream_properties_object_size - 1 - heo_i] = buf[c + 4 - 1 - heo_i];
                                        }
                                        for (heo_i = 0; heo_i < extended_stream_properties_object_size; heo_i++)
                                        {
                                            buf[heo_p + 46 + heo_i] = extended_stream_properties_object[heo_i];
                                        }

                                        // サイズ更新
                                        buf[2] = (byte)(c + extended_stream_properties_object_size);
                                        buf[3] = (byte)((c + extended_stream_properties_object_size) >> 8);
                                        buf[10] = buf[2];
                                        buf[11] = buf[3];
                                        buf[28] = (byte)(h + extended_stream_properties_object_size);
                                        buf[29] = (byte)((h + extended_stream_properties_object_size) >> 8);
                                        c = c + extended_stream_properties_object_size;
                                        h = h + extended_stream_properties_object_size;

                                        // File Properties Object にGUIDを設定する
                                        // GUIDを生成
                                        byte[] s2mmsh_guid = Guid.NewGuid().ToByteArray();
                                        // 末尾FF固定
                                        s2mmsh_guid[15] = 0xFF;

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
                                            if (y == 16) // 書き込む
                                            {
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

                                        // Data ObjectにFileID書き込む
                                        for (int m = 0; m < 16; m++)
                                        {
                                            buf[c + 4 - 1 - 10 - m] = s2mmsh_guid[15 - m];
                                        }

                                        byte[] dist = new byte[65535];
                                        if (push_mode)
                                        {
                                            asfData.asf_header_size = deleteMmsPreHeader(buf, c + 4, ref dist) + 4;
                                            //asfData.asf_header_size = c + 4;
                                        }
                                        else
                                        {
                                            asfData.asf_header_size = c + 4;
                                            dist = buf;
                                        }
                                        dist.CopyTo(asfData.asf_header, 0);
                                        Console.WriteLine("ASF header registered.");
                                        asfData.asf_status = ASF_STATUS.ASF_STATUS_SET_HEADER;

                                        this.BeginInvoke(new Action<String>(delegate(String str) { this.logoutput("ASFヘッダを登録しました。"); }), new object[] { "" });
                                        if (push_mode)
                                        {
                                            this.BeginInvoke(new Action<String>(delegate(String str) { this.logoutput("PUSH接続を開始します。"); }), new object[] { "" });
                                            // PUSHサーバに接続
                                            // クライアントソケット作成
                                            // host:port取得
                                            // サーバ接続
                                            // Response読み取り
                                            

                                            //サーバーのホスト名とポート番号
                                            var r =
                                                new System.Text.RegularExpressions.Regex(
                                                     @"([-a-zA-Z_0-9\.]+):([0-9]{1,5})");
                                            var m = r.Match(textBox_pushAddr.Text);

                                            string host = m.Groups[1].Value;
                                            int port = int.Parse(m.Groups[2].Value);

                                            IPAddress ipaddress = null;
                                            IPHostEntry ipentry = Dns.GetHostEntry(host);

                                            foreach (IPAddress ip in ipentry.AddressList)
                                            {
                                                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                                                {
                                                    ipaddress = ip;
                                                    break;
                                                }
                                            }
                                            if (ipaddress == null)
                                            {
                                                this.BeginInvoke(new Action<String>(delegate(String str) { this.logoutput("接続先アドレスが不正です。"); }), new object[] { "" });
                                                break;
                                            }
                                            IPEndPoint RHost = new IPEndPoint(ipaddress, port);
                                            Socket mClient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                                            mClient.Connect(RHost);

                                            String httpHeader = String.Format(
                                                "POST / HTTP/1.1\r\n" +
                                                "Content-Type: application/x-wms-pushsetup\r\n" +
                                                "X-Accept-Authentication: Negotiate, NTLM, Digest\r\n" +
                                                "User-Agent: WMEncoder/12.0.7601.17514\r\n" +
                                                "Host: " + host + ":" + port + "\r\n" +
                                                "Content-Length: 0\r\n" +
                                                "Connection: Keep-Alive\r\n" +
                                                "Cache-Control: no-cache\r\n" +
                                                "Cookie: push-id=0\r\n" +
                                                "\r\n"
                                                //, asfData.asf_header_size
                                            );

                                            byte[] httpHeaderBuffer = Encoding.UTF8.GetBytes(httpHeader);
                                            mClient.Send(httpHeaderBuffer);
                                            while (mClient.Available <= 0);

                                            byte[] buffer = new byte[(int)mClient.ReceiveBufferSize];

                                            int recvLen = 0;
                                            try
                                            {
                                                while (!mClient.Poll(100, SelectMode.SelectRead)) {}
                                                while (mClient.Available > 0)
                                                {
                                                    recvLen += mClient.Receive(buffer);
                                                }
                                            }
                                            catch (SocketException ex)
                                            {
                                                Console.WriteLine("{0} Error code: {1}.", ex.Message, ex.ErrorCode);
                                                throw ex;
                                            }
                                            String msg = "接続しました。";

                                            try
                                            {
                                                msg = "接続しました。[" + mClient.RemoteEndPoint.ToString() + "]";
                                            }
                                            catch
                                            {
                                            }
                                            this.BeginInvoke(new Action<String>(delegate(String str) { this.logoutput(msg); }), new object[] { "" });
                                            String message = Encoding.ASCII.GetString(buffer, 0, recvLen);
                                            Console.Write("httprequest:" + message);

                                            if (message.Contains("HTTP/1.1 204"))
                                            //if (true)
                                            {
                                                r =
                                                new System.Text.RegularExpressions.Regex(
                                                     @"push-id=([0-9]+)");
                                                m = r.Match(message);
                                                String pushid = m.Groups[1].Value;

                                                httpHeader = String.Format(
                                                    "POST / HTTP/1.1\r\n" +
                                                    "Content-Type: application/x-wms-pushstart\r\n" +
                                                    "X-Accept-Authentication: Negotiate, NTLM, Digest\r\n" +
                                                    "User-Agent: WMEncoder/12.0.7601.17514\r\n" +
                                                    "Host: " + host + ":" + port + "\r\n" +
                                                    "Content-Length: 2147483647\r\n" +
                                                    "Connection: Keep-Alive\r\n" +
                                                    "Cache-Control: no-cache\r\n" +
                                                    "Cookie: push-id=" + pushid + "\r\n" +
                                                    "\r\n"
                                                );

                                                // ヘッダ送信
                                                httpHeaderBuffer = Encoding.UTF8.GetBytes(httpHeader);
                                                mClient.Send(httpHeaderBuffer, SocketFlags.None);
                                                mClient.Send(asfData.asf_header, asfData.asf_header_size, SocketFlags.None);
                                                Console.WriteLine("ASF header sent.");
                                                try //クライアントが即死する場合がある
                                                {
                                                    this.BeginInvoke(new Action<String>(delegate(String str) { this.logoutput("ASFヘッダを送信しました。[" + mClient.RemoteEndPoint.ToString() + "]"); }), new object[] { "" });

                                                }
                                                catch (Exception)
                                                {
                                                   
                                                }
                                                // MMSソケット登録
                                                asfData.mms_sock = mClient;
                                                // ステータス更新
                                                asfData.mmsh_status = MMSH_STATUS.MMSH_STATUS_ASF_HEADER_SEND;
                                                
                                            }
                                            else
                                            {
                                                this.BeginInvoke(new Action<String>(delegate(String str) { this.logoutput("PUSHサーバとの接続に失敗しました。"); }), new object[] { "" });
                                                mClient.Close();

                                            }
                                        }
                                        else
                                        {
                                            if (pm.serverstatus) this.BeginInvoke(new Action<String>(delegate(String str) { this.logoutput("クライアント接続を受け付けます。"); }), new object[] { "" });
                                        }
                                    }
                                    else // Headerではない場合
                                        if (buf[1] == 'D' && (
                                        asfData.mmsh_status == MMSH_STATUS.MMSH_STATUS_ASF_HEADER_SEND
                                        || asfData.mmsh_status == MMSH_STATUS.MMSH_STATUS_ASF_DATA_SENDING
                                        ))
                                        {
                                            try
                                            {
                                                if (asfData.mms_sock != null)
                                                {
                                                    if (push_mode)
                                                    //if (false)
                                                    {
                                                        byte[] dist = new byte[65535];
                                                        int size = deleteMmsPreHeader(buf, c + 4, ref dist);
                                                        asfData.mms_sock.Send(dist, size + 4, SocketFlags.None);
                                                        asfData.mmsh_status = MMSH_STATUS.MMSH_STATUS_ASF_DATA_SENDING;
                                                        
                                                    }
                                                    else
                                                    {
                                                        //int a = asfData.mms_sock.Available;
                                                        asfData.mms_sock.Send(buf, c + 4, SocketFlags.None);
                                                        //Console.WriteLine("ASF Data sent.");
                                                        asfData.mmsh_status = MMSH_STATUS.MMSH_STATUS_ASF_DATA_SENDING;
                                                        //a = asfData.mms_sock.Available;
                                                    }
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
                            //MessageBox.Show(exx.Message,
                            //    "エラー",
                            //    MessageBoxButtons.OK,
                            //    MessageBoxIcon.Error);
                            this.BeginInvoke(new Action<String>(delegate(String str) { this.logoutput(exx.Message); }), new object[] { "" });
                                    
                            ProcessInitialize();
                            
                        }
                        

                    }));
                    pm.th_ffmpeg.Start();
                }
            }
            
            // closing
            //this.button_disconnect.Enabled = true;
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

                this.BeginInvoke(new Action<String>(delegate(String str) { this.logoutput("接続を初期化しました。"); }), new object[] { "" });

                pm.ffmpegstatus = FFMPEG_STATUS.FFMPEG_STATUS_INITIALIZED;

                // スレッド自身の削除
                if (pm.th_ffmpeg != null)
                {
                    Thread tmp = pm.th_ffmpeg;
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
            ProcessManager pm = ProcessManager.Instance;
            if (pm.ffmpegstatus == FFMPEG_STATUS.FFMPEG_STATUS_PROCESS) // 初期化
            {
                //pm.ffmpegstatus = FFMPEG_STATUS.FFMPEG_STATUS_INITIALIZING;
                //logoutput("接続を初期化します。");
                //this.button_disconnect.Enabled = false;

                // スレッド・プロセス終了
                if (pm.process != null)
                {
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
                this.button_exec_push.Enabled = true;

                logoutput("接続を初期化しました。");

                pm.ffmpegstatus = FFMPEG_STATUS.FFMPEG_STATUS_INITIALIZED;
            }
            else
            {
                logoutput("接続は初期化中です。");
            }
            ProcessInitialize();
        }

        private void PrintErrorData(object sender, DataReceivedEventArgs e)
        {
            Process p = (Process)sender;

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
            Properties.Settings.Default.push_addr = this.textBox_pushAddr.Text;
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
                //pm.ffmpegstatus = FFMPEG_STATUS.FFMPEG_STATUS_INITIALIZING;
                //logoutput("接続を初期化します。");
                this.button_disconnect.Enabled = false;

                if (pm.process == null)
                {
                    ProcessInitialize();
                }
                
                // スレッド・プロセス終了
                else
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

                    //pm.process = null;
                }
                
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
            this.button_exec_push.Enabled = bl;
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
                // 再エンコードGBを無効化
                panel1.Enabled = false;
                // 再エンコード入力を無効化
                groupBox_reencode.Enabled = false;
                // 入力ストリームを無効化
                textBox_inputstream.Enabled = false;
            }
        }

        private void radioButton_ffmpegcom_2_CheckedChanged(object sender, EventArgs e)
        {
            RadioButton radioButton = sender as RadioButton;

            if (radioButton.Checked)
            {
                Console.WriteLine(radioButton.Text + "が選択されました。");
                textBox_ffmpegcom.Enabled = false;
                // 再エンコードGBを有効化
                panel1.Enabled = true;
                // 再エンコードありならば再エンコード入力を有効化
                if (radioButton_reencode_1.Checked) {
                    groupBox_reencode.Enabled = true;
                }
                // 入力ストリームを有効化
                textBox_inputstream.Enabled = true;
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

        /// <summary>
        /// パケットからMMS Pre-Header を取り除く
        /// </summary>
        /// <param name="buf">対象パケット</param>
        /// <param name="c">パケット長</param>
        /// <returns>パケット長 - 8 - 4を固定で返す</returns>
        private int deleteMmsPreHeader(byte[] buf, int c, ref byte[] dist)
        {
            dist[0] = buf[0];
            dist[1] = buf[1];
            dist[2] = (byte)(c - 12);
            dist[3] = (byte)((c - 12) >> 8);

            Array.Copy(buf, 12, dist, 4, c - 12);


            //for (int i = 12; i < c; i++)
            //{
            //    buf[i - 8] = buf[i];
            //}


            return c - 12;
        }

        private Boolean compBinaryList(byte[] buf, int c, byte[] stream, int d, int size)
        {
            int y = 0;
            for (int i = 0; i < size; i++) {
                if (buf[i + c] == stream[i + d])
                {
                    y++;
                }
            }
            if (y == size) return true;
            else return false;
            
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