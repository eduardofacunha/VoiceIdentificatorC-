using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using NAudio.Wave;
using NAudio.CoreAudioApi;
using System.IO;

namespace ScottPlotMicrophoneFFT
{
    public partial class Form1 : Form
    {

        // MICROPHONE ANALYSIS SETTINGS
        private int RATE = 48000;//44100; // sample rate of the sound card
        private int BUFFERSIZE = (int)Math.Pow(2, 11); // must be a multiple of 2

        // prepare class objects
        public BufferedWaveProvider bwp;

        public Form1()
        {
            InitializeComponent();
            SetupGraphLabels();
            StartListeningToMicrophone(1);
            timerReplot.Enabled = true;

            //MakeSound();
        }

        private void MakeSound()
        {
            
            int RIFF = 0x46464952;
            int WAVE = 0x45564157;
            int formatChunkSize = 16;
            int headerSize = 8;
            int format = 0x20746D66;
            short formatType = 1;
            short tracks = 1;
            int samplesPerSecond = 44100;
            short bitsPerSample = 16;
            short frameSize = (short)(tracks * ((bitsPerSample + 7) / 8));
            int bytesPerSecond = samplesPerSecond * frameSize;
            int waveSize = 4;
            int data = 0x61746164;
            int samples = 88200 * 4;
            int dataChunkSize = samples * frameSize;
            int fileSize = waveSize + headerSize + formatChunkSize + headerSize + dataChunkSize;
            FileStream stream = new FileStream("test.wav", FileMode.Create);
            BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(RIFF);
            writer.Write(fileSize);
            writer.Write(WAVE);
            writer.Write(format);
            writer.Write(formatChunkSize);
            writer.Write(formatType);
            writer.Write(tracks);
            writer.Write(samplesPerSecond);
            writer.Write(bytesPerSecond);
            writer.Write(frameSize);
            writer.Write(bitsPerSample);
            writer.Write(data);
            writer.Write(dataChunkSize);
            double aNatural = 120.0;
            double ampl = 10000;
            double perfect = 1.5;
            double concert = 1.498307077;
            double freq = aNatural * perfect;
            for (int i = 0; i < samples / 4; i++)
            {
                double t = (double)i / (double)samplesPerSecond;
                short s = (short)(ampl * (Math.Sin(t * freq * 2.0 * Math.PI)));
                writer.Write(s);
            }
            freq = aNatural * concert;
            for (int i = 0; i < samples / 4; i++)
            {
                double t = (double)i / (double)samplesPerSecond;
                short s = (short)(ampl * (Math.Sin(t * freq * 2.0 * Math.PI)));
                writer.Write(s);
            }
            for (int i = 0; i < samples / 4; i++)
            {
                double t = (double)i / (double)samplesPerSecond;
                short s = (short)(ampl * (Math.Sin(t * freq * 2.0 * Math.PI) + Math.Sin(t * freq * perfect * 2.0 * Math.PI)));
                writer.Write(s);
            }
            for (int i = 0; i < samples / 4; i++)
            {
                double t = (double)i / (double)samplesPerSecond;
                short s = (short)(ampl * (Math.Sin(t * freq * 2.0 * Math.PI) + Math.Sin(t * freq * concert * 2.0 * Math.PI)));
                writer.Write(s);
            }
            writer.Close();
            stream.Close();
        }

        void AudioDataAvailable(object sender, WaveInEventArgs e)
        {
            bwp.AddSamples(e.Buffer, 0, e.BytesRecorded);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
        }

        public void SetupGraphLabels()
        {
            scottPlotUC1.fig.labelTitle = "Microphone PCM Data";
            scottPlotUC1.fig.labelY = "Amplitude (PCM)";
            scottPlotUC1.fig.labelX = "Time (ms)";
            scottPlotUC1.Redraw();

            scottPlotUC2.fig.labelTitle = "Microphone FFT Data";
            scottPlotUC2.fig.labelY = "Power (raw)";
            scottPlotUC2.fig.labelX = "Frequency (Hz)";
            scottPlotUC2.Redraw();
        }

        public void StartListeningToMicrophone(int audioDeviceNumber = 0)
        {
            WaveIn wi = new WaveIn();
            wi.DeviceNumber = audioDeviceNumber;
            wi.WaveFormat = new NAudio.Wave.WaveFormat(RATE, 1);
            wi.BufferMilliseconds = (int)((double)BUFFERSIZE / (double)RATE * 1000.0);
            wi.DataAvailable += new EventHandler<WaveInEventArgs>(AudioDataAvailable);
            bwp = new BufferedWaveProvider(wi.WaveFormat);
            bwp.BufferLength = BUFFERSIZE * 2;
            bwp.DiscardOnBufferOverflow = true;
            try
            {
                wi.StartRecording();
            }
            catch
            {
                string msg = "Could not record from audio device!\n\n";
                msg += "Is your microphone plugged in?\n";
                msg += "Is it set as your default recording device?";
                MessageBox.Show(msg, "ERROR");
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            // turn off the timer, take as long as we need to plot, then turn the timer back on
            timerReplot.Enabled = false;
            PlotLatestData();
            timerReplot.Enabled = true;
        }

        public int numberOfDraws = 0;
        public bool needsAutoScaling = true;
        public void PlotLatestData()
        {
            // check the incoming microphone audio
            int frameSize = BUFFERSIZE;
            var audioBytes = new byte[frameSize];
            bwp.Read(audioBytes, 0, frameSize);

            // return if there's nothing new to plot
            if (audioBytes.Length == 0)
                return;
            if (audioBytes[frameSize - 2] == 0)
                return;

            // incoming data is 16-bit (2 bytes per audio point)
            int BYTES_PER_POINT = 2;

            // create a (32-bit) int array ready to fill with the 16-bit data
            int graphPointCount = audioBytes.Length / BYTES_PER_POINT;

            // create double arrays to hold the data we will graph
            double[] pcm = new double[graphPointCount];
            double[] fft = new double[graphPointCount];
            double[] fftReal = new double[graphPointCount/2];
            
            // populate Xs and Ys with double data
            for (int i = 0; i < graphPointCount; i++)
            {
                // read the int16 from the two bytes
                Int16 val = BitConverter.ToInt16(audioBytes, i * 2);
                var calculo = (double)(val) / Math.Pow(2, 16) * 200.0;
                // store the value in Ys as a percent (+/- 100% = 200%)
                pcm[i] = (double)(val) / Math.Pow(2,16) * 200.0;
            }
            // calculate the full FFT
            fft = FFT(pcm);

            // determine horizontal axis units for graphs
            double pcmPointSpacingMs = RATE / 1000;
            double fftMaxFreq = RATE / 2;
            double fftPointSpacingHz = fftMaxFreq / graphPointCount;

            // just keep the real half (the other half imaginary)
            Array.Copy(fft, fftReal, fftReal.Length);
            
            // plot the Xs and Ys for both graphs
            scottPlotUC1.Clear();
            scottPlotUC1.PlotSignal(pcm, pcmPointSpacingMs, Color.Blue);
            scottPlotUC2.Clear();
            scottPlotUC2.PlotSignal(fftReal, fftPointSpacingHz, Color.Blue);

            // optionally adjust the scale to automatically fit the data
            if (needsAutoScaling)
            {
                scottPlotUC1.AxisAuto();
                scottPlotUC2.AxisAuto();
                needsAutoScaling = false;
            }

            //scottPlotUC1.PlotSignal(Ys, RATE);

            numberOfDraws += 1;
            lblStatus.Text = $"Analyzed and graphed PCM and FFT data {numberOfDraws} times";

            // this reduces flicker and helps keep the program responsive
            Application.DoEvents(); 

        }

        private void autoScaleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            needsAutoScaling = true;
        }

        private void infoMessageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string msg = "";
            msg += "left-click-drag to pan\n";
            msg += "right-click-drag to zoom\n";
            msg += "middle-click to auto-axis\n";
            msg += "double-click for graphing stats\n";
            MessageBox.Show(msg);
        }

        private void websiteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/swharden/Csharp-Data-Visualization");
        }

        public double[] FFT(double[] data)
        {
            double[] fft = new double[data.Length];
            System.Numerics.Complex[] fftComplex = new System.Numerics.Complex[data.Length];
            for (int i = 0; i < data.Length; i++)
                fftComplex[i] = new System.Numerics.Complex(data[i], 0.0);
            Accord.Math.FourierTransform.FFT(fftComplex, Accord.Math.FourierTransform.Direction.Forward);
            for (int i = 0; i < data.Length; i++)
                fft[i] = fftComplex[i].Magnitude;
            return fft;
        }
    }
}
