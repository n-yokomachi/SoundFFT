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
        /// <summary>
        /// 60(fps)の逆数(100ms)
        /// </summary>
        private readonly long reciprocal_of_FPS = 167000;

        /// <summary>
        /// 音楽プレーヤ
        /// </summary>
        private WaveOutEvent outputDevice;

        /// <summary>
        /// フーリエ変換前の音楽データ
        /// </summary>
        private AudioFileReader AudioStream;

        /// <summary>
        /// フーリエ変換後の音楽データ
        /// </summary>
        private float[,] result;

        /// <summary>
        /// タイマー割り込みに使用するタイマー
        /// </summary>
        private DispatcherTimer timer = null;

        /// <summary>
        /// 再生する音楽ファイルのパス
        /// </summary>
        private string filename;

        /// <summary>
        /// 音声波形表示に使用するLine
        /// </summary>
        private Line[] bar;

        /// <summary>
        /// 音声波形表示のLineに使用するブラシ
        /// </summary>
        private Brush brush;

        /// <summary>
        /// 1秒あたりのバイト数
        /// </summary>
        private int bytePerSec;

        /// <summary>
        /// 音楽の長さ（秒）
        /// </summary>
        private int musicLength_s;

        /// <summary>
        /// 再生位置（秒）
        /// </summary>
        private int playPosition_s;

        /// <summary>
        /// 音声波形表示位置
        /// </summary>
        private int drawPosition;

        /// <summary>
        /// 描画済みのLineがあるかを示すフラグ
        /// </summary>
        private bool barDrawn = false;
        
        /// <summary>
        /// コンストラクタ
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            // ウィンドウをマウスのドラッグで移動できるようにする
            this.MouseLeftButtonDown += (sender, e) => this.DragMove();

            // Loaded(要素のレイアウトやレンダリングが完了し、操作を受け入れる準備が整ったときに発生)イベントの登録
            this.Loaded += new RoutedEventHandler(MainWindow_Loaded);
        }

        /// <summary>
        /// MainWindowの初期化が終わったとき(Loadedが発生した時)のイベントハンドラ
        /// </summary>
        /// <param name="sender">イベント送信元</param>
        /// <param name="e">イベント引数</param>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // タイマーの生成
            timer = new DispatcherTimer(DispatcherPriority.Normal);
            // Tickの発生間隔の設定
            timer.Interval = new TimeSpan(reciprocal_of_FPS);
            // タイマーイベントの登録
            timer.Tick += new EventHandler(Timer_Tick);

            // 再生する音楽ファイル名
            filename = @"music\01 - Lone Digger.mp3";

            // ファイル名の拡張子によって、異なるストリームを生成
            AudioStream = new AudioFileReader(filename);

            // ハミング窓をかけ、高速フーリエ変換を行ったデータを配列resultに格納
            result = FFT_HammingWindow_ver1();

            // 音声波形表示に使用するLineの配列を確保（この時点ではコンストラクタは呼び出されていない）
            bar = new Line[result.GetLength(1)];
            for (int i = 0; i < result.GetLength(1); i++)
            {
                bar[i] = new Line();    // 各要素のコンストラクタを明示的に呼び出す
            }
            // Lineに使用するブラシ
            brush = new SolidColorBrush(Color.FromArgb(128, 61, 221, 200));

            // コンストラクタを読んだ後に、Positionが最後尾に移動したため、0に戻す
            AudioStream.Position = 0;

            // プレーヤの生成
            outputDevice = new WaveOutEvent();
            // 音楽ストリームの入力
            outputDevice.Init(AudioStream);

            // 1秒あたりのバイト数を計算
            bytePerSec = (AudioStream.WaveFormat.BitsPerSample / 8) * AudioStream.WaveFormat.SampleRate * AudioStream.WaveFormat.Channels;
            
            // 音楽の長さを計算
            musicLength_s = (int)AudioStream.Length / bytePerSec;

            // 音楽の再生（非同期？）
            outputDevice.Play();

            // タイマーの実行開始
            timer.Start();
        }

        /// <summary>
        /// Timer.Tickが発生した時のイベントハンドラ
        /// </summary>
        /// <param name="sender">イベント送信元</param>
        /// <param name="e">イベント引数</param>
        private void Timer_Tick(object sender, EventArgs e)
        {
            // 再生位置を計算
            playPosition_s = (int)AudioStream.Position / bytePerSec;

            // 音声波形表示を描画する配列のオフセットを計算
            drawPosition = (int)(((double)AudioStream.Position / (double)AudioStream.Length) * result.GetLength(0));
            Make_AudioSpectrum();
        }

        /// <summary>
        /// 音声波形表示を描画
        /// </summary>
        private void Make_AudioSpectrum()
        {
            // 描画済みのLineがある場合
            if (barDrawn)
            {
                for (int j = 0; j < result.GetLength(1); j++)
                {
                    // 画面からLineを削除
                    grid.Children.Remove(bar[j]);
                }
            }

            if (drawPosition >= result.GetLength(0))
                return;

            for (int j = 0; j < result.GetLength(1); )
            {
                // 描画する方法を設定
                bar[j].Stroke = brush;
                // （親要素内に作成されるときに適用される）水平方向の配置特性を、（親要素のレイアウトのスロットの）左側に設定
                bar[j].HorizontalAlignment = HorizontalAlignment.Left;
                // （親要素内に作成されるときに適用される）垂直方向の配置特性を、（親要素のレイアウトのスロットの）中央に設定
                bar[j].VerticalAlignment = VerticalAlignment.Center;

                // 始点のＸ座標を設定
                bar[j].X1 = j * 7 + 32;
                // 終点のＸ座標を設定
                bar[j].X2 = j * 7 + 32;

                // 始点のＹ座標を設定
                bar[j].Y1 = 0;
                // 終点のＹ座標を設定
                bar[j].Y2 = 7700 * result[drawPosition, j];
                // Lineの長さが400より大きい場合は長さを400にする
                if (bar[j].Y2 >= 400)
                    bar[j].Y2 = 400;

                // 幅を設定
                bar[j].StrokeThickness = 5;

                // 画面にLineを追加
                grid.Children.Add(bar[j]);

                j += 1;
            }
            // 描画済みにする
            barDrawn = true;
        }

        /// <summary>
        /// 音楽の波形データにハミング窓をかけ、高速フーリエ変換を行う
        /// </summary>
        /// <returns></returns>
        private float[,] FFT_HammingWindow_ver1()
        {
            // 波形データを配列samplesに格納
            float[] samples = new float[AudioStream.Length / AudioStream.BlockAlign * AudioStream.WaveFormat.Channels];
            AudioStream.Read(samples, 0, samples.Length);

            // 1サンプルのデータ数
            int fftLength = 256;
            // 1サンプルごとに実行するためのイテレータ用変数
            int fftPos = 0;

            // フーリエ変換後の音楽データを格納する配列
            float[,] result = new float[samples.Length / fftLength, fftLength / 2];

            // 波形データにハミング窓をかけたデータを核のする配列
            Complex[] buffer = new Complex[fftLength];
            for (int i = 0; i < samples.Length; i++)
            {
                // ハミング窓をかける
                buffer[fftPos].X = (float)(samples[i] * FastFourierTransform.HammingWindow(fftPos, fftLength));
                buffer[fftPos].Y = 0.0f;
                fftPos++;

                // 1サンプル分のデータがたまったとき
                if (fftLength <= fftPos)
                {
                    fftPos = 0;

                    // サンプル数の対数を取る（高速フーリエ変換に使用）
                    int m = (int)Math.Log(fftLength, 2.0);
                    // 高速フーリエ変換
                    FastFourierTransform.FFT(true, m, buffer);

                    for (int k = 0; k < result.GetLength(1); k++)
                    {
                        // 複素数の大きさを計算
                        double diagnoal = Math.Sqrt(buffer[k].X * buffer[k].X + buffer[k].Y * buffer[k].Y);
                        double intensityDB = 10.0 * Math.Log10(diagnoal);

                        const double minDB = -60.0;

                        // 音の大きさを百分率に変換
                        double percent = (intensityDB < minDB) ? 1.0 : intensityDB / minDB;

                        // 結果を代入
                        result[i / fftLength, k] = (float)diagnoal;
                    }
                }
            }


            return result;
        }

        /// <summary>
        /// コンテキストメニューのExitが押された時のイベントハンドラ
        /// </summary>
        /// <param name="sender">イベント送信元</param>
        /// <param name="e">イベント引数</param>
        private void Quit_Clicked(object sender, RoutedEventArgs e)
        {
            Close();
        }

    }


}
