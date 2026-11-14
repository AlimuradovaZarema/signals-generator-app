using Microsoft.Win32;
using System;
using ScottPlot;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;

using SignalProcessing.Core; namespace SignalGenerator.App
{
    public partial class MainWindow : Window
    {
        private List<SingleChannelSignal> signalList = new List<SingleChannelSignal>();
        private List<(int start, int end)> outOfRangeMarkers = new List<(int, int)>();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void addSignal(SingleChannelSignal signal)
        {
            signalList.Add(signal);
            WpfPlot1.Plot.Add.ScatterLine(signal.TimeTick, signal.Value);
            WpfPlot1.Plot.Axes.AutoScale();
            WpfPlot1.Refresh();
        }

        private void Button_Click_Sin(object sender, RoutedEventArgs e)
        {
            float len = float.Parse(len_textBox.Text, CultureInfo.InvariantCulture);
            float freq = float.Parse(freq_textBox.Text, CultureInfo.InvariantCulture);
            float period = float.Parse(period_textBox.Text, CultureInfo.InvariantCulture);
            float phase = float.Parse(phase_textBox.Text, CultureInfo.InvariantCulture);
            float scale = float.Parse(scale_textBox.Text, CultureInfo.InvariantCulture);
            addSignal(Tools.Sin(len, freq, period, phase, scale));
        }

        private void Button_Click_Line(object sender, RoutedEventArgs e)
        {
            float len = float.Parse(len_textBox.Text, CultureInfo.InvariantCulture);
            float freq = float.Parse(freq_textBox.Text, CultureInfo.InvariantCulture);
            float k = float.Parse(k_textBox.Text, CultureInfo.InvariantCulture);
            float b = float.Parse(b_textBox.Text, CultureInfo.InvariantCulture);
            addSignal(Tools.Line(len, freq, k, b));
        }

        private void Button_Click_Noise(object sender, RoutedEventArgs e)
        {
            float len = float.Parse(len_textBox.Text, CultureInfo.InvariantCulture);
            float freq = float.Parse(freq_textBox.Text, CultureInfo.InvariantCulture);
            float amplitude = float.Parse(amplitude_textBox.Text, CultureInfo.InvariantCulture);
            addSignal(Tools.Noise(len, freq, amplitude));
        }

        private void Button_Delete(object sender, RoutedEventArgs e)
        {
            if (signalList.Count == 0) return;
            signalList.RemoveAt(signalList.Count - 1);
            var list = WpfPlot1.Plot.PlottableList;
            if (list.Count > 0)
                WpfPlot1.Plot.Remove(list[list.Count - 1]);
            WpfPlot1.Plot.Axes.AutoScale();
            WpfPlot1.Refresh();
        }

        private void Button_Sum(object sender, RoutedEventArgs e)
        {
            addSignal(Tools.Summ(signalList));
        }

        private void Button_Skley(object sender, RoutedEventArgs e)
        {
            addSignal(Tools.Skley(signalList));
        }

        private void Button_Clear(object sender, RoutedEventArgs e)
        {
            signalList.Clear();
            outOfRangeMarkers.Clear();
            WpfPlot1.Plot.Clear();
            WpfPlot1.Refresh();
        }

        private void Button_Load(object sender, RoutedEventArgs e)
        {
            OpenFileDialog myDialog = new OpenFileDialog();
            myDialog.Filter = "csv-файл(*.csv)|*.csv";
            myDialog.CheckFileExists = true;
            myDialog.Multiselect = false;
            if (myDialog.ShowDialog() == true)
                addSignal(SingleChannelSignalReader.SignalReader(myDialog.FileName));
        }

        private void Button_Save(object sender, RoutedEventArgs e)
        {
            if (signalList.Count == 0) return;
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "csv-файл (*.csv)|*.csv";
            saveFileDialog.FileName = "MySignal";
            saveFileDialog.DefaultExt = "csv";
            if (saveFileDialog.ShowDialog() == true)
                SingleChannelSignalWriter.SignalWriter(signalList[signalList.Count - 1], saveFileDialog.FileName);
        }

        private void Button_FilterOffline(object sender, RoutedEventArgs e)
        {
            if (signalList.Count == 0) return;

            float freq = signalList[signalList.Count - 1].Freq;
            float windowSec = float.Parse(window_textBox.Text, CultureInfo.InvariantCulture);
            int windowSize = (int)(windowSec * freq + 0.5);
            if (windowSize % 2 == 0) windowSize++;

            float[] filtered = OfflineFilter.Filter(signalList[signalList.Count - 1].Value, windowSize);
            addSignal(new SingleChannelSignal(freq, filtered));
        }

        private void Button_FilterOnline(object sender, RoutedEventArgs e)
        {
            if (signalList.Count == 0) return;

            float freq = signalList[signalList.Count - 1].Freq;
            float windowSec = float.Parse(window_textBox.Text, CultureInfo.InvariantCulture);
            int windowSize = (int)(windowSec * freq + 0.5);
            if (windowSize % 2 == 0) windowSize++;

            float[] data = signalList[signalList.Count - 1].Value;
            OnlineFilter onlineFilter = new OnlineFilter(windowSize);

            int chunkSize = 7; 
            List<float> result = new List<float>();

            for (int i = 0; i < data.Length; i += chunkSize)
            {
                int actualSize = Math.Min(chunkSize, data.Length - i);
                float[] chunk = new float[actualSize];
                for (int j = 0; j < actualSize; j++)
                    chunk[j] = data[i + j];
                result.AddRange(onlineFilter.Filter(chunk));
            }

            addSignal(new SingleChannelSignal(freq, result.ToArray()));
        }


        private void freq_textBox_TextChanged(object sender, TextChangedEventArgs e) { }

        private void Button_SaveMarkers(object sender, RoutedEventArgs e)
        {
            if (outOfRangeMarkers.Count == 0)
            {
                MessageBox.Show("Нет разметки для сохранения.", "Сохранение");
                return;
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "csv-файл (*.csv)|*.csv";
            saveFileDialog.FileName = "Markers";
            saveFileDialog.DefaultExt = "csv";

            if (saveFileDialog.ShowDialog() == true)
            {
                using (StreamWriter writer = new StreamWriter(saveFileDialog.FileName))
                {
                    foreach (var marker in outOfRangeMarkers)
                        writer.WriteLine($"{marker.start};{marker.end}");
                }
            }
        }

        private void Button_LoadMarkers(object sender, RoutedEventArgs e)
        {
            if (signalList.Count == 0)
            {
                MessageBox.Show("Сначала загрузите сигнал.", "Загрузка");
                return;
            }

            OpenFileDialog myDialog = new OpenFileDialog();
            myDialog.Filter = "csv-файл(*.csv)|*.csv";
            myDialog.CheckFileExists = true;
            myDialog.Multiselect = false;

            if (myDialog.ShowDialog() == true)
            {
                outOfRangeMarkers.Clear();
                float freq = signalList[signalList.Count - 1].Freq;

                using (StreamReader reader = new StreamReader(myDialog.FileName))
                {
                    while (true)
                    {
                        string line = reader.ReadLine();
                        if (line == null) break;

                        string[] parts = line.Split(';');
                        int start = int.Parse(parts[0]);
                        int end = int.Parse(parts[1]);

                        outOfRangeMarkers.Add((start, end));

                        float tStart = start / freq;
                        float tEnd = end / freq;
                        WpfPlot1.Plot.Add.VerticalLine(tStart, 2, ScottPlot.Color.FromHex("#FF0000"));
                        WpfPlot1.Plot.Add.VerticalLine(tEnd, 2, ScottPlot.Color.FromHex("#FF0000"));
                    }
                }

                WpfPlot1.Plot.Axes.AutoScale();
                WpfPlot1.Refresh();
                MessageBox.Show($"Загружено разметок: {outOfRangeMarkers.Count}", "Загрузка");
            }
        }

        private void Button_y(object sender, RoutedEventArgs e)
        {
            float len = float.Parse(len_textBox.Text, CultureInfo.InvariantCulture);
            float freq = float.Parse(freq_textBox.Text, CultureInfo.InvariantCulture);
            float c = float.Parse(cTextBox.Text, CultureInfo.InvariantCulture);
            addSignal(Tools.Line(len, freq, 0, c));
        }
    }
}

