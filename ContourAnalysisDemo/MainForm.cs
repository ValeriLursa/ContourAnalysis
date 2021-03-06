//
//  THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF ANY
//  KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
//  IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR
//  PURPOSE.
//
//  License: GNU General Public License version 3 (GPLv3)
//
//  Email: pavel_torgashov@mail.ru.
//
//  Copyright (C) Pavel Torgashov, 2011. 
// пример

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.Structure;
using ContourAnalysisNS;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Text;
using System.Linq;



namespace ContourAnalysisDemo
{
    public partial class MainForm : Form
    {
        private Capture _capture;
        Image<Bgr, Byte> frame;
        ImageProcessor processor;
        Dictionary<string, Image> AugmentedRealityImages = new Dictionary<string, Image>();

        bool captureFromCam = true;
        int frameCount = 0;
        int oldFrameCount = 0;
        bool showAngle;
        int camWidth = 640;
        int camHeight = 480;
        string templateFile;
        bool startCum = false;
        string textSave = "";

        public MainForm()
        {
            InitializeComponent();
            //create image preocessor
            processor = new ImageProcessor();
            //load default templates
            templateFile = AppDomain.CurrentDomain.BaseDirectory + "\\Tahoma.bin";
            LoadTemplates(templateFile);
            //start capture from cam
            StartCapture();
            //apply settings
            ApplySettings();
            //
            Application.Idle += new EventHandler(Application_Idle);
        }

        private void LoadTemplates(string fileName)
        {
            try
            {
                using(FileStream fs = new FileStream(fileName, FileMode.Open))
                    processor.templates = (Templates)new BinaryFormatter().Deserialize(fs);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void SaveTemplates(string fileName)
        {
            try
            {
                using (FileStream fs = new FileStream(fileName, FileMode.Create))
                    new BinaryFormatter().Serialize(fs, processor.templates);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void StartCapture()
        {
            try
            {
                _capture = new Capture();
                ApplyCamSettings();
            }
            catch (NullReferenceException ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void ApplyCamSettings()
        {
            try
            {
                _capture.SetCaptureProperty(Emgu.CV.CvEnum.CAP_PROP.CV_CAP_PROP_FRAME_WIDTH, camWidth);
               _capture.SetCaptureProperty(Emgu.CV.CvEnum.CAP_PROP.CV_CAP_PROP_FRAME_HEIGHT, camHeight);
               cbCamResolution.Text = camWidth + "x" + camHeight;
            }
            catch (NullReferenceException ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        void Application_Idle(object sender, EventArgs e)
        {
            ProcessFrame();
        }

        private void ProcessFrame()
        {
            try
            {
                if (startCum)
                {
                    if (captureFromCam)
                        frame = _capture.QueryFrame();

                    frameCount++;
                    //
                    processor.ProcessImage(frame);
                    //
                }
                    if (cbShowBinarized.Checked)
                        ibMain.Image = processor.binarizedFrame;
                    else
                        ibMain.Image = frame;
                
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private void tmUpdateState_Tick(object sender, EventArgs e)
        {
            lbFPS.Text = (frameCount - oldFrameCount) + " fps";
            oldFrameCount = frameCount;
            if (processor.contours!=null)
                lbContoursCount.Text = "Всего контуров: "+processor.contours.Count;
            if (processor.foundTemplates != null)
                lbRecognized.Text = "Распознанно контуров: " + processor.foundTemplates.Count;
        }

        private void ibMain_Paint(object sender, PaintEventArgs e)
        {
            if (frame == null)  return; 
            
                Font font = new Font(Font.FontFamily, 24);//16
                //Отображение фпс при включенной камере
                if (captureFromCam) e.Graphics.DrawString(lbFPS.Text, new Font(Font.FontFamily, 16), Brushes.Yellow, new PointF(1, 1));

                Brush bgBrush = new SolidBrush(Color.FromArgb(255, 0, 0, 0));
                Brush foreBrush = new SolidBrush(Color.FromArgb(255, 255, 255, 255));
                Pen borderPen = new Pen(Color.FromArgb(150, 0, 255, 0));
                //
                if (cbShowContours.Checked)
                    foreach (var contour in processor.contours)
                        if (contour.Total > 1)
                            e.Graphics.DrawLines(Pens.Red, contour.ToArray());
                //
                lock (processor.foundTemplates)
                    foreach (FoundTemplateDesc found in processor.foundTemplates)
                    {
                        if (found.template.name.EndsWith(".png") || found.template.name.EndsWith(".jpg"))
                        {
                            DrawAugmentedReality(found, e.Graphics);
                            continue;
                        }

                        Rectangle foundRect = found.sample.contour.SourceBoundingRect;
                        Point p1 = new Point((foundRect.Left + foundRect.Right) / 2, foundRect.Top);
                        string text = found.template.name;
                        
                        if (showAngle)                        
                            text += string.Format("\r\nangle={0:000}°\r\nscale={1:0.0}", 180 * found.angle / Math.PI, found.scale);
                        e.Graphics.DrawRectangle(borderPen, foundRect);
                        e.Graphics.DrawString(text, font, bgBrush, new PointF(p1.X + 1 - font.Height / 3, p1.Y + 1 - font.Height));
                        e.Graphics.DrawString(text, font, foreBrush, new PointF(p1.X - font.Height / 3, p1.Y - font.Height));
                    }
                            
        }

        private void DrawAugmentedReality(FoundTemplateDesc found, Graphics gr)
        {
            string fileName = Path.GetDirectoryName(templateFile) + "\\" + found.template.name;
            if (!AugmentedRealityImages.ContainsKey(fileName))
            {
                if (!File.Exists(fileName)) return;
                AugmentedRealityImages[fileName] = Image.FromFile(fileName);
            }
            Image img = AugmentedRealityImages[fileName];
            Point p = found.sample.contour.SourceBoundingRect.Center();
            var state = gr.Save();
            gr.TranslateTransform(p.X, p.Y);
            gr.RotateTransform((float)(180f * found.angle / Math.PI));
            gr.ScaleTransform((float)(found.scale), (float)(found.scale));
            gr.DrawImage(img, new Point(-img.Width/2, -img.Height/2));
            gr.Restore(state);
        }

        private void cbAutoContrast_CheckedChanged(object sender, EventArgs e)
        {
            ApplySettings();
            //Проверка состояния захвата камеры
            if(cbCaptureFromCam.Checked)
            //Запуск распознавания текста
            startCum = true;
        }

        private void ApplySettings()
        {
            try
            {
                processor.equalizeHist = cbAutoContrast.Checked;
                showAngle = cbShowAngle.Checked;
                captureFromCam = cbCaptureFromCam.Checked;
                btLoadImage.Enabled = !captureFromCam;
                cbCamResolution.Enabled = captureFromCam;
                processor.finder.maxRotateAngle = cbAllowAngleMore45.Checked ? Math.PI : Math.PI / 4;
                processor.minContourArea = (int)nudMinContourArea.Value;
                processor.minContourLength = (int)nudMinContourLength.Value;
                processor.finder.maxACFDescriptorDeviation = (int)nudMaxACFdesc.Value;
                processor.finder.minACF = (double)nudMinACF.Value;
                processor.finder.minICF = (double)nudMinICF.Value;
                processor.blur = cbBlur.Checked;
                processor.noiseFilter = cbNoiseFilter.Checked;
                processor.cannyThreshold = (int)nudMinDefinition.Value;
                nudMinDefinition.Enabled = processor.noiseFilter;
                processor.adaptiveThresholdBlockSize = (int)nudAdaptiveThBlockSize.Value;
                processor.adaptiveThresholdParameter = cbAdaptiveNoiseFilter.Checked?1.5:0.5;
                //cam resolution
                string[] parts = cbCamResolution.Text.ToLower().Split('x');
                if (parts.Length == 2)
                {
                    int camWidth = int.Parse(parts[0]);
                    int camHeight = int.Parse(parts[1]);
                    if (this.camHeight != camHeight || this.camWidth != camWidth)
                    {
                        this.camWidth = camWidth;
                        this.camHeight = camHeight;
                        ApplyCamSettings();
                    }
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btLoadImage_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Image|*.bmp;*.png;*.jpg;*.jpeg";
            if (ofd.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
                try
                {
                    frame = new Image<Bgr, byte>((Bitmap)Bitmap.FromFile(ofd.FileName));
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            else return;
            //Запуск распознавания текста
            startCum = true;
        }

        private void btCreateTemplate_Click(object sender, EventArgs e)
        {
            if(frame!=null)
                new ShowContoursForm(processor.templates, processor.samples, frame).ShowDialog();
        }

        private void btNewTemplates_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Создать новый набор шаблонов?", "Создать новый набор шаблонов", MessageBoxButtons.OKCancel) == System.Windows.Forms.DialogResult.OK)
                processor.templates.Clear();
        }

        private void btOpenTemplates_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Templates(*.bin)|*.bin";
            if (ofd.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
            {
                templateFile = ofd.FileName;
                LoadTemplates(templateFile);
            }
        }

        private void btSaveTemplates_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "Templates(*.bin)|*.bin";
            if (sfd.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
            {
                templateFile = sfd.FileName;
                SaveTemplates(templateFile);
            }
        }

        private void btTemplateEditor_Click(object sender, EventArgs e)
        {
            new TemplateEditor(processor.templates).Show();
        }

        private void btAutoGenerate_Click(object sender, EventArgs e)
        {
            new AutoGenerateForm(processor).ShowDialog();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (startCum) {
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.Filter = "(*.txt) | *.txt";
                sfd.RestoreDirectory = true;
                if (sfd.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
                {
                    try
                    {
                        string templateFileq = sfd.FileName;
                        using (StreamWriter sw = File.CreateText(templateFileq))
                        {
                            Console.WriteLine(textSave);
                            sw.WriteLine(textSave);
                            
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                    }
                }
            }
            else
            {
                MessageBox.Show("Нет изображения для распознавания");
            }
        }

        class YearComparer1 : IComparer<string[]>
        {
            public int Compare(string[] o1, string[] o2)
            {
                int a = Convert.ToInt32(o1[1]);
                int b = Convert.ToInt32(o2[1]);

                if (a > b)
                {
                    return 1;
                }
                else if (a < b)
                {
                    return -1;
                }

                return 0;
            }
        }

        

        class YearComparer : IComparer<string[]>
        {
            public int Compare(string[] o1, string[] o2)
            {
                int x1 = Convert.ToInt32(o1[1]);
                int x2 = Convert.ToInt32(o2[1]);
                int y1 = Convert.ToInt32(o1[2]);
                int y2 = Convert.ToInt32(o2[2]);
                if (y1-y2>0)
                {
                    if (y1 - y2 > 15)
                    {
                        if (x1 + y1 * 30 > x2 + y2 * 30)
                        {
                            return 1;
                        }
                        else if (x1 + y1 * 30 < x2 + y2 * 30)
                        {
                            return -1;
                        }
                    }
                    else
                    {
                        if (x1 + y1 > x2 + y2)
                        {
                            return 1;
                        }
                        else if (x1 + y1 < x2 + y2)
                        {
                            return -1;
                        }
                    }
                }
                else
                {
                    if(y2-y1 > 15)
                    {
                        if (x1 + y1 * 30 > x2 + y2 * 30)
                        {
                            return 1;
                        }
                        else if (x1 + y1 * 30 < x2 + y2 * 30)
                        {
                            return -1;
                        }
                    }
                    else
                    {
                        if (x1 + y1 > x2 + y2)
                        {
                            return 1;
                        }
                        else if (x1 + y1 < x2 + y2)
                        {
                            return -1;
                        }
                    }
                }
              
                return 0;
            }
        }

        static string OutputList(List<string[]> list, string textSave)
        {
            string time = "";
            string strok = null;
            string hight = null;
            string work = null;
            string y = null;
            int tick = 0;
            foreach (string[] current in list)
            {
                if (tick == 0)
                {
                    if (strok != null)
                    {
                        if (Convert.ToInt32(current[1]) - (Convert.ToInt32(strok) + Convert.ToInt32(work)) > (Convert.ToInt32(work) + Convert.ToInt32(current[3]))/3)
                        {
                            // if (Convert.ToInt32(current[1]) - Convert.ToInt32(strok) < 15)
                            if ((Convert.ToInt32(current[2]) - Convert.ToInt32(y)) > Convert.ToInt32(hight)/2)
                            {
                                Console.WriteLine(time);
                                textSave += time + "\n";
                                time = current[0];
                                strok = current[1];
                                work = current[3];
                                hight = current[4];
                                y = current[2];
                            }
                            else
                            {
                                time += ' ' + current[0];
                                strok = current[1];
                                work = current[3];
                                hight = current[4];
                                y = current[2];
                            }

                        }
                        else
                        {
                            //   if (Convert.ToInt32(current[1]) - Convert.ToInt32(strok) < 15)
                            if ((Convert.ToInt32(current[2]) - Convert.ToInt32(y)) > Convert.ToInt32(hight)/2)
                            {
                                Console.WriteLine(time);
                                textSave += time + "\n";
                                time = current[0];
                                strok = current[1];
                                work = current[3];
                                hight = current[4];
                                y = current[2];
                            }
                            else
                                time += current[0];
                            strok = current[1];
                            work = current[3];
                            hight = current[4];
                            y = current[2];
                        }


                    }
                    else
                        time = current[0];
                    strok = current[1];
                    work = current[3];
                    y = current[2];
                }
                else             
                time += current[0]+'-'+'H'+ current[4]+';'+'Y'+ current[2]+' ';
                
            }
                Console.WriteLine(time);
            textSave += time + "\n";
            return textSave;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (startCum)
            {
                processor.ProcessImage(frame);                
                string text = "";              
               // int x = 0;

                List<string[]> points = new List<string[]>();
                foreach (FoundTemplateDesc found in processor.foundTemplates)
                {
                    Rectangle foundRect = found.sample.contour.SourceBoundingRect;//<----this is rect of found contour (in source image coordinates)
                    Point p1 = new Point(foundRect.Left, foundRect.Bottom); // орпеделение координат левой вершины прямоугольника, который был нарисован вокруг распознанного символа
                    points.Add(new string[]{found.template.name, Convert.ToString(p1.X), Convert.ToString(p1.Y), Convert.ToString(foundRect.Width), Convert.ToString(foundRect.Height)});
                    //text += found.template.name + p1; // вывод распознонного символа и коордитан вершины прямоугольника, в который он обрисован.                                      
                    //x += 1;                   
                }                                
               // var sorted = points.OrderBy(z => z.X).OrderBy(z => z.Y);                
               // MessageBox.Show(string.Join(" ", sorted));
               // Console.WriteLine(sorted);

                YearComparer yc = new YearComparer();
               // YearComparer1 yc1 = new YearComparer1();

               points.Sort(yc);
               //points.Sort(yc1);
                textSave = OutputList(points, "");


            }
            else
            {
                MessageBox.Show("Нет изображения для распознавания");
            }
        }
    }
}
