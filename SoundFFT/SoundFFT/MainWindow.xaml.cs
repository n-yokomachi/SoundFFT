using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using NAudio.Wave;
using NAudio.Dsp;

namespace SoundFFT
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly long reciprocal_of_FPS = 167000;

        private WaveOutEvent outputDevice;

        private AudioFileReader AudioStream;

        private float[,] result;

        private DispatcherTimer timer = null;

        private string filename;

        private Line[] bar;

        private Brush brush;

        private int bytePerSec;

        private int musicLength_s;

        private int playPosition_s;

        private int drawPosition;

        private bool barDrawn = false;




        public MainWindow()
        {
            InitializeComponent();

            this.MouseLeftButtonDown += (sender, e) => this.DragMove();

            this.Loaded += new RoutedEventHandler(MainWindow_Loaded);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            timer = new DispatcherTimer(DispatcherPriority.Normal);

            timer.Interval = new TimeSpan(reciprocal_of_FPS);

            timer.Tick += new EventHandler(Timer_Tick);

            filename = @"music\01 - Lone Digger.mp3";

            AudioStream = new AudioFileReader(filename);

            result = FFT_HammingWindow_ver1();

            bar = new Line[result.GetLength(1)];
            for (int i = 0; i < result.GetLength(1); i++)
            {
                bar[i] = new Line();
            }

            brush = new SolidColorBrush(Color.FromArgb(128, 61, 221, 200));

            AudioStream.Position = 0;

            outputDevice = new WaveOutEvent();
            outputDevice.Init(AudioStream);

            bytePerSec = (AudioStream.WaveFormat.BitsPerSample / 8) * AudioStream.WaveFormat.SampleRate * AudioStream.WaveFormat.Channels;


            musicLength_s = (int)AudioStream.Length / bytePerSec;

            outputDevice.Play();

            timer.Start();
        }


        private void Timer_Tick(object sender, EventArgs e)
        {
            playPosition_s = (int)AudioStream.Position / bytePerSec;

            drawPosition = (int)(((double)AudioStream.Position / (double)AudioStream.Length) * result.GetLength(0));

            Make_AudioSpectrum();
        }

        private void Make_AudioSpectrum()
        {
            if (barDrawn)
            {
                for (int j = 0; j < result.GetLength(1); j++)
                {
                    grid.Children.Remove(bar[j]);
                }
            }

            if (drawPosition >= result.GetLength(0))
                return;

            for (int j = 0; j < result.GetLength(1);)
            {
                bar[j].Stroke = brush;

                bar[j].HorizontalAlignment = HorizontalAlignment.Left;

                bar[j].VerticalAlignment = VerticalAlignment.Center;

                bar[j].X1 = j * 7 + 32;

                bar[j].X2 = j * 7 + 32;

                bar[j].Y1 = 0;

                bar[j].Y2 = 7700 * result[drawPosition, j];

                if (bar[j].Y2 >= 400)
                    bar[j].Y2 = 400;

                bar[j].StrokeThickness = 5;

                grid.Children.Add(bar[j]);

                j += 1;
            }

            barDrawn = true;
        }

        private float[,] FFT_HammingWindow_ver1()
        {
            float[] samples = new float[AudioStream.Length / AudioStream.BlockAlign * AudioStream.WaveFormat.Channels];
            AudioStream.Read(samples, 0, samples.Length);

            int fftLength = 256;

            int fftPos = 0;

            float[,] result = new float[samples.Length / fftLength, fftLength / 2];

            Complex[] buffer = new Complex[fftLength];

            for (int i = 0; i < samples.Length; i++)
            {
                buffer[fftPos].X = (float)(samples[i] * FastFourierTransform.HammingWindow(fftPos, fftLength));
                buffer[fftPos].Y = 0.0f;
                fftPos++;


                if (fftLength <= fftPos)
                {
                    fftPos = 0;

                    int m = (int)Math.Log(fftLength, 2.0);

                    FastFourierTransform.FFT(true, m, buffer);

                    for (int k = 0; k < result.GetLength(1); k++)
                    {
                        double diagnoal = Math.Sqrt(buffer[k].X * buffer[k].X + buffer[k].Y * buffer[k].Y);
                        double intensityDB = 10.0 * Math.Log10(diagnoal);

                        const double minDB = -60.0;

                        double percent = (intensityDB < minDB) ? 1.0 : intensityDB / minDB;

                        result[i / fftLength, k] = (float)diagnoal;
                    }
                }
            }


            return result;
        }


        private void Quit_Clicked(object sender, RoutedEventArgs e)
        {
            Close();
        }

    }


}
