using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Globalization;
using Microsoft.Win32;
using System.Windows.Input;

namespace Wpftotal1
{
    public partial class MainWindow : Window
    {
        #region 1. ตัวแปรระบบ
        private Random rnd = new Random();
        private readonly string thaiChars = "กขฃคฅฆงจฉชซฌญฎฏฐฑฒณดตถทธนบปผฝพฟภมยรลวศษสหฬอฮ";
        private int exportCount = 1;
        private string originalFileName = "";
        #endregion

        public MainWindow()
        {
            InitializeComponent();
        }

        #region 2. ปุ่มกดและการทำงานหลัก
        private void LblCredit_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            MessageBox.Show("Say My Name", "Do you know who I am ?", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog
            {
                Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*"
            };

            if (ofd.ShowDialog() == true)
            {
                try
                {
                    originalFileName = Path.GetFileNameWithoutExtension(ofd.FileName);
                    string content = File.ReadAllText(ofd.FileName, Encoding.UTF8);

                    Match mSta = Regex.Match(content, @"ตั้งกล้อง\s*:\s*(\w+)");
                    if (mSta.Success) txtStation.Text = mSta.Groups[1].Value;

                    Match mBS = Regex.Match(content, @"ธงหลัง\s*:\s*(\w+)");
                    if (mBS.Success) txtBS.Text = mBS.Groups[1].Value;

                    Match mDist = Regex.Match(content, @"ระยะ.*?ถึง.*?:\s*([\d\.]+)");
                    if (mDist.Success) txtBSDist.Text = mDist.Groups[1].Value;

                    StringBuilder sb = new StringBuilder();
                    string[] rawLines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string line in rawLines)
                    {
                        Match m = Regex.Match(line, @"\|\s*([ก-ฮ\w-]+)\s+([\d\.]+)\s+([\d\.]+)");
                        if (m.Success)
                        {
                            sb.AppendLine($"|{m.Groups[1].Value}|{m.Groups[2].Value}|{m.Groups[3].Value}|");
                        }
                    }
                    txtInput.Text = sb.ToString();
                }
                catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
            }
        }

        private void BtnCalculate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txtInput.Text)) return;

                StringBuilder sb = new StringBuilder();
                string station = txtStation.Text.Trim();
                string bsPoint = txtBS.Text.Trim();

                double bsDist = 0;
                double.TryParse(txtBSDist.Text.Trim(), out bsDist);

                sb.AppendLine($"M,00.0.0,{station},STA,0.000,0,0,0.000,0.000,0.000,0,0,0,-30,0,0,0,0,0,0,0,0,");
                sb.Append(GenerateRecord(bsPoint, 0.0, bsDist, true));

                string[] lines = txtInput.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                foreach (string line in lines)
                {
                    string[] parts = line.Split('|');
                    if (parts.Length >= 4)
                    {
                        double angle = 0;
                        double dist = 0;
                        double.TryParse(parts[2], out angle);
                        double.TryParse(parts[3], out dist);

                        sb.Append(GenerateRecord(parts[1], angle, dist, false));
                    }
                }

                SetRichTextOutput(sb.ToString().TrimEnd());
            }
            catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
        }

        private void BtnExportSCR_Click(object sender, RoutedEventArgs e)
        {
            string outputText = new TextRange(txtOutput.Document.ContentStart, txtOutput.Document.ContentEnd).Text.Trim();
            if (string.IsNullOrWhiteSpace(outputText)) return;

            SaveFileDialog sfd = new SaveFileDialog
            {
                Filter = "SCR Files (*.scr)|*.scr",
                FileName = $"S{DateTime.Now.ToString("ddMMyy", new CultureInfo("th-TH"))}{exportCount:D2}"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(sfd.FileName, outputText, Encoding.UTF8);
                    exportCount++;
                    MessageBox.Show("บันทึก SCR สำเร็จ");
                }
                catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
            }
        }

        private void BtnExportTXT_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtInput.Text)) return;

            SaveFileDialog sfd = new SaveFileDialog
            {
                Filter = "Text Files (*.txt)|*.txt",
                FileName = string.IsNullOrEmpty(originalFileName) ? "ExportData" : originalFileName + "_expoint"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(sfd.FileName, txtInput.Text, Encoding.UTF8);
                    MessageBox.Show("บันทึก TXT สำเร็จ");
                }
                catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
            }
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show("ล้างข้อมูลทั้งหมด?", "ยืนยันการล้างข้อมูล", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (result == MessageBoxResult.OK)
            {
                txtStation.Clear();
                txtBS.Clear();
                txtBSDist.Clear();
                txtInput.Clear();
                txtOutput.Document.Blocks.Clear();
                originalFileName = "";
            }
        }
        #endregion

        #region 3. ระบบจัดการชื่อหมุด 
        private void TxtInput_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (txtInput.Text.Length == 0) return;

            int charIdx = txtInput.CaretIndex;
            int lineIdx = txtInput.GetLineIndexFromCharacterIndex(charIdx);
            string[] lines = txtInput.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

            if (lineIdx >= 0 && lineIdx < lines.Length)
            {
                string line = lines[lineIdx];
                string[] parts = line.Split('|');

                if (parts.Length >= 4)
                {
                    string curName = parts[1];
                    string? newName = ShowEditPopup(curName);

                    if (!string.IsNullOrEmpty(newName) && newName != curName)
                    {
                        foreach (string l in lines)
                        {
                            string[] p = l.Split('|');
                            if (p.Length >= 4 && p[1].Trim() == newName.Trim())
                            {
                                MessageBox.Show("ชื่อหมุดซ้ำ!", "คำเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
                                return;
                            }
                        }
                        parts[1] = newName;
                        lines[lineIdx] = string.Join("|", parts);
                        txtInput.Text = string.Join(Environment.NewLine, lines);
                    }
                }
            }
        }

        private string? ShowEditPopup(string cur)
        {
            Window f = new Window()
            {
                Width = 300,
                Height = 160,
                ResizeMode = ResizeMode.NoResize,
                Title = "แก้ไขชื่อหมุด",
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };
            Grid g = new Grid();
            Label l = new Label() { Margin = new Thickness(20, 20, 0, 0), Content = "ชื่อหมุดใหม่:", HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            TextBox t = new TextBox() { Margin = new Thickness(20, 45, 0, 0), Width = 240, Height = 25, Text = cur, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            Button b = new Button() { Content = "ตกลง", Margin = new Thickness(160, 80, 0, 0), Width = 100, Height = 30, IsDefault = true, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };

            b.Click += (s, ev) => { f.DialogResult = true; };
            g.Children.Add(l); g.Children.Add(t); g.Children.Add(b);
            f.Content = g;

            return f.ShowDialog() == true ? t.Text : null;
        }
        #endregion

        #region 4. ฟังก์ชันคำนวณและตกแต่งข้อความ WPF
        private string GenerateRecord(string name, double targetAngle, double targetHDist, bool isBS)
        {
            string encName = EncodeThaiName(name);
            bool perfect = rnd.Next(1, 101) <= 30;
            double dErr = perfect ? 0 : rnd.Next(1, 5) / 1000.0;
            int aErr = (isBS || perfect) ? 0 : rnd.Next(1, 31);

            double lH = Math.Abs(targetHDist + dErr), rH = Math.Abs(targetHDist - dErr);
            int lA = DdMmsToSeconds(targetAngle) + aErr, rA = DdMmsToSeconds(targetAngle) + (180 * 3600) - aErr;
            if (isBS) { lA = 0; rA = 180 * 3600; }

            int zL = (90 * 3600) + (perfect ? 0 : rnd.Next(-1800, 1800)), zR = (360 * 3600) - zL;
            double sdL = Math.Abs(lH / Math.Sin((zL / 3600.0) * Math.PI / 180.0));
            double sdR = Math.Abs(rH / Math.Sin((zR / 3600.0) * Math.PI / 180.0));

            return $"{encName},Ea,0000,{(isBS ? "BS" : "SS")},0.000,0,{sdL:F3},{SecondsToDdMms(zL)},{SecondsToDdMms(lA)},\r\n" +
                   $"{encName},Ea,0000,{(isBS ? "BS" : "SS")},0.000,0,{sdR:F3},{SecondsToDdMms(zR)},{SecondsToDdMms(rA)},\r\n";
        }

        private string EncodeThaiName(string n)
        {
            for (int i = 0; i < thaiChars.Length; i++) n = n.Replace(thaiChars[i] + "-", $"/{i + 1:D2}/");
            return n;
        }

        private int DdMmsToSeconds(double a)
        {
            string s = a.ToString("0.0000");
            string[] p = s.Split('.');
            return (int.Parse(p[0]) * 3600) + (int.Parse(p[1].Substring(0, 2)) * 60) + int.Parse(p[1].Substring(2, 2));
        }

        private string SecondsToDdMms(int s)
        {
            s = (s % 1296000 + 1296000) % 1296000;
            return $"{s / 3600}.{(s % 3600 / 60):D2}{(s % 60):D2}";
        }

        private void SetRichTextOutput(string text)
        {
            FlowDocument doc = new FlowDocument();
            Paragraph p = new Paragraph();

            // แยกและทำสี Keyword สำหรับ WPF
            string[] tokens = Regex.Split(text, @"(STA|BS|SS)");
            foreach (string token in tokens)
            {
                Run run = new Run(token);
                if (token == "STA") run.Foreground = Brushes.Red;
                else if (token == "BS") run.Foreground = Brushes.Blue;
                else if (token == "SS") run.Foreground = Brushes.Gold;
                else run.Foreground = Brushes.Black;

                p.Inlines.Add(run);
            }
            doc.Blocks.Add(p);
            txtOutput.Document = doc;
        }
        #endregion
    }
}