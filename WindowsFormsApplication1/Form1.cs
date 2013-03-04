using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Diagnostics;
using System.IO;
using System.Drawing.Imaging;
using System.Threading.Tasks;

namespace WindowsFormsApplication1
{
    public partial class Form1 : Form
    {
        myColor[] myColorArr = new myColor[256];
        Thread threadMovie = new Thread(SetImage);

        public Form1()
        {
            InitializeComponent();
            int i = Environment.ProcessorCount;
            i = System.IntPtr.Size;
            Init_szinskala();
        }

        private void Init_szinskala()
        {
            Label lbl;
            
            flowLayoutPanel1.Controls.Clear();
            for (int i = 0; i < myColorArr.Length; i++)
            {
                myColorArr[i] = new myColor {   R = (myColorArr.Length - 1) - i, 
                                                G = (myColorArr.Length - 1) - i, 
                                                B = (myColorArr.Length - 1) - i };
                lbl = new Label { Size =  new Size(20, 2), Margin = new System.Windows.Forms.Padding {All = 0}};
                lbl.BackColor = Color.FromArgb(myColorArr[i].R, myColorArr[i].G, myColorArr[i].B);
                flowLayoutPanel1.Controls.Add(lbl);
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            #region declaration
            int bufferedSeconds = 4;
            byte[] bytes;
            const int FILEBUFFERLENGTH = 65536;
            long fsLength = 65536;
            int fsReaded;
            int i = 0;
            bool isJpegEnded = false;
            bool isJpegStarted = false;
            int jpegLen = 0;
            int maxImageCnt;
            int offset = 0;
            int readByteCnt;
            #endregion

            try
            {
                lblMsg.Text = "Pufferelés...";
                lblFileName.Text = @"P1000783.mov";
                mJPEGSruct.FrameRate = 30;
                timer1.Interval = (1000 / mJPEGSruct.FrameRate);

                using (FileStream fs = new FileStream(@"C:\hg\_diplomamunka\forgalomszamlalo\P1000783.mov", FileMode.Open, FileAccess.Read))
                {
                    fsLength = fs.Length;
                    bytes = new byte[fsLength];

                    maxImageCnt = (int)(fsLength / 45600);     //becsült képek száma
                    progressBar1.Value = 0;
                    progressBar1.Maximum = maxImageCnt / mJPEGSruct.FrameRate;

                    while (fsLength > 0)
                    {
                        readByteCnt = (fsLength >= FILEBUFFERLENGTH) ? FILEBUFFERLENGTH : (int)fsLength;
                        fsReaded = fs.Read(bytes, offset, readByteCnt);

                        #region adatok beolvasása
                        for (i = offset; i < (offset + fsReaded); i++)
                        {
                            if (!isJpegStarted)
                            {
                                #region FF D8 marker keresése, byte-ok eldobása
                                if (i > offset || i > 0)
                                {
                                    if (bytes[i - 1] == 0xFF && bytes[i] == 0xD8)
                                    {
                                        jpegLen = 2;
                                        isJpegStarted = true;
                                        isJpegEnded = false;

                                        if (threadMovie.IsAlive)
                                            threadMovie.Join();
                                        mJPEGSruct.Stream.Seek(0, SeekOrigin.End);
                                        mJPEGSruct.Stream.WriteByte(bytes[i - 1]);
                                        mJPEGSruct.Stream.WriteByte(bytes[i]);
                                    }
                                }
                                #endregion
                            }
                            else if (isJpegStarted && !isJpegEnded)
                            {
                                #region write jpegdata
                                if (threadMovie.IsAlive)
                                    threadMovie.Join();
                                mJPEGSruct.Stream.Seek(0, SeekOrigin.End);
                                mJPEGSruct.Stream.WriteByte(bytes[i]);

                                jpegLen++;

                                if (i > offset || i > 0)
                                {
                                    if (bytes[i - 1] == 0xFF && bytes[i] == 0xD9)
                                    {
                                        #region position és offset FFD9 alapján
                                        isJpegStarted = false;
                                        isJpegEnded = true;
                                        mJPEGSruct.Items.Add(new mJPEG() { FieldSize = jpegLen, Offset = mJPEGSruct.Stream.Position - jpegLen });
                                        mJPEGSruct.ItemsWait++;
                                        #endregion
                                    }
                                }
                                #endregion
                            }
                        }
                        #endregion

                        offset = i;
                        fsLength -= fsReaded;

                        if (!mJPEGSruct.IsShowStarted && ((fsLength == 0) || (mJPEGSruct.Items.Count > (bufferedSeconds * mJPEGSruct.FrameRate))))
                        {
                            #region start movie
                            mJPEGSruct.IsShowStarted = true;
                            timer1.Enabled = true;
                            timer1.Start();
                            lblMsg.Text = "";
                            #endregion
                        }
                    }
                }

                mJPEGSruct.IsPreProcessingFinished = true;

                if (progressBar1.Maximum < (mJPEGSruct.Items.Count / (mJPEGSruct.FrameRate / 10)))
                {
                    progressBar1.Maximum = mJPEGSruct.Items.Count / (mJPEGSruct.FrameRate / 10);
                }
            }
            catch (Exception err)
            {
                string error = err.ToString();
            }
        }

        private static void SetImage(object target)
        {
            mJPEGSruct.ItemsWait--;

            byte[] imageData = new byte[mJPEGSruct.CurrentLength];
            mJPEGSruct.Stream.Seek(mJPEGSruct.CurrentOffset, SeekOrigin.Begin);
            mJPEGSruct.Stream.Read(imageData, 0, (int)mJPEGSruct.CurrentLength);

            if (target is PictureBox)
            {
                using (MemoryStream ms = new MemoryStream(imageData))
                {
                    ((PictureBox)target).Image = Image.FromStream(ms);
                }
            }
            else if (target is Stream)
            {
                ((Stream)target).Seek(0, SeekOrigin.End);
                ((Stream)target).Write(imageData, 0, imageData.Length);
            }

            mJPEGSruct.ItemDisplayed++;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {

            try
            {
                if (mJPEGSruct.ItemsWait > 0)
                {
                    mJPEGSruct.ElapsedTime = mJPEGSruct.ElapsedTime.Add(TimeSpan.FromMilliseconds(timer1.Interval));

                    #region dual timer
                    if (mJPEGSruct.IsDualTimerUsed)
                    {
                        mJPEGSruct.Tickitem++;
                        if (mJPEGSruct.Tickitem == mJPEGSruct.LastTickItem)
                        {
                            timer1.Interval = mJPEGSruct.IntervalAsync;
                            mJPEGSruct.Tickitem = 0;
                        }
                        else if (mJPEGSruct.Tickitem == 1 && (mJPEGSruct.ItemDisplayed != 0))
                        {
                            timer1.Interval = mJPEGSruct.Interval;
                        }
                    }
                    #endregion

                    #region player info
                    if ((mJPEGSruct.ElapsedTime.Milliseconds % 100 == 0) || (mJPEGSruct.ElapsedTime.Milliseconds % 100 < (timer1.Interval - 2)))
                    {
                        lblTime1.Text = String.Format("{0:HH:mm:ss/f0}", new DateTime(mJPEGSruct.ElapsedTime.Ticks));
                        progressBar1.Increment(1);
                    }
                    #endregion

                    #region show movie - thread
                    //threadMovie = new Thread(SetImage);
                    //threadMovie.Start(pictureBox1);
                    #endregion

                    #region show movie - normal
                    SetImage(pictureBox1);
                    #endregion
                }
                else
                {
                    #region buffering
                    timer1.Stop();
                    mJPEGSruct.IsShowStarted = false;
                    if (!mJPEGSruct.IsPreProcessingFinished) { lblMsg.Text = "Pufferelés..."; }
                    #endregion
                }
            }
            catch (Exception)
            {
            }
        }

        private void btnRGB1_Click(object sender, EventArgs e)
        {
            colorDialog1.ShowDialog();
            txtR1.Text = ((int)colorDialog1.Color.R).ToString();
            txtG1.Text = ((int)colorDialog1.Color.G).ToString();
            txtB1.Text = ((int)colorDialog1.Color.B).ToString();
        }
    }

    public class mJPEGSruct
    {
        private static MemoryStream ms;
        private static List<mJPEG> items;

        public static long CurrentLength { get { return items[ItemDisplayed].FieldSize; } }
        public static long CurrentOffset { get { return items[ItemDisplayed].Offset; } }
        public static TimeSpan ElapsedTime { get; set; }
        public static int FrameRate { get; set; }
        public static int Interval { get { return (1000 / mJPEGSruct.FrameRate); } }
        public static int IntervalAsync { get { return Interval + (100 - ((Interval * FrameRate) / 10)); } }
        public static bool IsDualTimerUsed { get { return (100 / (decimal)FrameRate) != Math.Round((100 / (decimal)FrameRate)); } }
        public static bool IsPreProcessingFinished { get; set; }
        public static bool IsShowStarted { get; set; }
        public static int ItemDisplayed { get; set; }
        public static List<mJPEG> Items { get { return items; } }
        public static int ItemsWait { get; set; }
        public static decimal LastTickItem { get { return Math.Round((100 / (decimal)FrameRate)); } }
        public static MemoryStream Stream { get { return ms; } }
        public static int Tickitem { get; set; }

        static mJPEGSruct()
        {
            ms = new MemoryStream();
            items = new List<mJPEG>();

            Tickitem = 0;
            ItemsWait = 0;
            ItemDisplayed = 0;
            ElapsedTime = new TimeSpan(0, 0, 0, 0, 1);     // day,hour,min,sec,ms
            IsShowStarted = false;
            IsPreProcessingFinished = false;
            FrameRate = 25;
        }

        ~mJPEGSruct()
        {
            ms.Close();
        }

    }

    public class mJPEG
    {
        public long Offset { get; set; }
        public long FieldSize { get; set; }
    }

    public class myColor
    {
        public int R { get; set; }
        public int G { get; set; }
        public int B { get; set; }
    }

    public class bw_Args 
    {
        public String Name { get; set; }
        public String ID { get; set; }
    }
}
