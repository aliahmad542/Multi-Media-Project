using Microsoft.Win32;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace PixelLab;

public partial class MainWindow : System.Windows.Window
{
    private Mat? originalImage;
    private Mat? processedImage;
    
   
    private double cameraRadius = 8.0;
    private double cameraAlpha = 45.0; 
    private double cameraBeta = 45.0;  
    private GeometryModel3D? colorMarker; 

    public MainWindow()
    {
        InitializeComponent();
        Initialize3DSpace();
    }

    
    private void Initialize3DSpace()
    {
        if (CubeModelGroup == null) return;

        int steps = 8; 
        double dotSize = 0.08;
        for (int r = 0; r < steps; r++) {
            for (int g = 0; g < steps; g++) {
                for (int b = 0; b < steps; b++) {
                    byte rv = (byte)(r * 255 / (steps - 1));
                    byte gv = (byte)(g * 255 / (steps - 1));
                    byte bv = (byte)(b * 255 / (steps - 1));

                    double x = (r / (double)(steps - 1) * 3) - 1.5;
                    double y = (g / (double)(steps - 1) * 3) - 1.5;
                    double z = (b / (double)(steps - 1) * 3) - 1.5;

                    CubeModelGroup.Children.Add(CreateSphere(x, y, z, dotSize, Color.FromRgb(rv, gv, bv), 0.3));
                }
            }
        }

        colorMarker = CreateSphere(0, 0, 0, 0.25, Colors.White, 1.0);
        CubeModelGroup.Children.Add(colorMarker);
    }

    private GeometryModel3D CreateSphere(double x, double y, double z, double r, Color color, double opacity)
    {
        MeshGeometry3D mesh = new();
        mesh.Positions = new Point3DCollection {
            new Point3D(x-r, y-r, z-r), new Point3D(x+r, y-r, z-r), new Point3D(x+r, y+r, z-r), new Point3D(x-r, y+r, z-r),
            new Point3D(x-r, y-r, z+r), new Point3D(x+r, y-r, z+r), new Point3D(x+r, y+r, z+r), new Point3D(x-r, y+r, z+r)
        };
        mesh.TriangleIndices = new Int32Collection { 0,2,1, 0,3,2, 1,6,5, 1,2,6, 5,7,4, 5,6,7, 4,3,0, 4,7,3, 3,6,2, 3,7,6, 4,1,5, 4,0,1 };
        
        var brush = new SolidColorBrush(color) { Opacity = opacity };
        return new GeometryModel3D(mesh, new DiffuseMaterial(brush));
    }

    
    private void MainImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (originalImage == null || colorMarker == null) return;

        var pos = e.GetPosition(MainImage);
        int px = (int)(pos.X * originalImage.Width / MainImage.ActualWidth);
        int py = (int)(pos.Y * originalImage.Height / MainImage.ActualHeight);

        if (px >= 0 && px < originalImage.Width && py >= 0 && py < originalImage.Height)
        {
            Vec3b bgr = originalImage.At<Vec3b>(py, px);
            byte r = bgr.Item2; byte g = bgr.Item1; byte b = bgr.Item0;

            TxtColorValues.Text = $"Picked Color: RGB({r}, {g}, {b})\nSynced to 3D Cube Position.";

            double tx = (r / 255.0 * 3) - 1.5;
            double ty = (g / 255.0 * 3) - 1.5;
            double tz = (b / 255.0 * 3) - 1.5;
            colorMarker.Transform = new TranslateTransform3D(tx, ty, tz);
        }
    }

    private void UpdateCamera() {
        double ar = cameraAlpha * Math.PI / 180.0;
        double br = cameraBeta * Math.PI / 180.0;
        TheCamera.Position = new Point3D(cameraRadius * Math.Cos(br) * Math.Sin(ar), cameraRadius * Math.Sin(br), cameraRadius * Math.Cos(br) * Math.Cos(ar));
        TheCamera.LookDirection = new Vector3D(-TheCamera.Position.X, -TheCamera.Position.Y, -TheCamera.Position.Z);
    }
    private void RotateY_Click(object sender, RoutedEventArgs e) { cameraAlpha += 15; UpdateCamera(); }
    private void RotateX_Click(object sender, RoutedEventArgs e) { cameraBeta += 10; UpdateCamera(); }
    private void ZoomIn_Click(object sender, RoutedEventArgs e) { cameraRadius = Math.Max(3, cameraRadius - 0.5); UpdateCamera(); }
    private void ZoomOut_Click(object sender, RoutedEventArgs e) { cameraRadius = Math.Min(15, cameraRadius + 0.5); UpdateCamera(); }

    private void OpenImage_Click(object sender, RoutedEventArgs e) {
        var d = new OpenFileDialog { Filter = "Images|*.jpg;*.png;*.bmp" };
        if (d.ShowDialog() == true) LoadImage(d.FileName);
    }
    private void Window_Drop(object sender, DragEventArgs e) {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] f) LoadImage(f[0]);
    }
    private void LoadImage(string p) {
        originalImage = Cv2.ImRead(p);
        if (originalImage == null || originalImage.Empty()) return;
        processedImage = originalImage.Clone();
        UpdateUI();
        var fi = new FileInfo(p);
        TxtImageInfo.Content = $"File: {fi.Name} | {originalImage.Width}x{originalImage.Height} | {fi.Length/1024:F1}KB";
    }

    private void UpdateUI()
    {
        if (processedImage == null || processedImage.Empty()) return;
        MainImage.Source = BitmapSourceConverter.ToBitmapSource(processedImage);
    }

   
    private void ApplyColorEffects() {
        if (originalImage == null || SliderComponent == null || ChkChannel1 == null || ChkChannel2 == null || ChkChannel3 == null) return;

        try
        {
            Mat workingMat = originalImage.Clone();

            if (CboColorSpace.SelectedIndex == 5) 
            {
                Mat[] bgrChannels = Cv2.Split(workingMat);
                Mat bMat = bgrChannels[0]; Mat gMat = bgrChannels[1]; Mat rMat = bgrChannels[2];

                Mat cMat = new(workingMat.Size(), MatType.CV_32FC1);
                Mat mMat = new(workingMat.Size(), MatType.CV_32FC1);
                Mat yMat = new(workingMat.Size(), MatType.CV_32FC1);
                Mat kMat = new(workingMat.Size(), MatType.CV_32FC1);

                rMat.ConvertTo(rMat, MatType.CV_32FC1, 1.0 / 255.0);
                gMat.ConvertTo(gMat, MatType.CV_32FC1, 1.0 / 255.0);
                bMat.ConvertTo(bMat, MatType.CV_32FC1, 1.0 / 255.0);

                Mat maxRG = new(); Cv2.Max(rMat, gMat, maxRG);
                Mat maxRGB = new(); Cv2.Max(maxRG, bMat, maxRGB);
                kMat = Scalar.All(1.0) - maxRGB;

                Cv2.Divide(Scalar.All(1.0) - rMat, Scalar.All(1.0) - kMat, cMat);
                Cv2.Divide(Scalar.All(1.0) - gMat, Scalar.All(1.0) - kMat, mMat);
                Cv2.Divide(Scalar.All(1.0) - bMat, Scalar.All(1.0) - kMat, yMat);

                Cv2.Threshold(kMat, kMat, 0.999, 1.0, ThresholdTypes.Binary);
                cMat.SetTo(0, kMat); mMat.SetTo(0, kMat); yMat.SetTo(0, kMat);
                kMat = Scalar.All(1.0) - maxRGB;

                if (SliderComponent.Value != 0) cMat.ConvertTo(cMat, -1, 1, SliderComponent.Value / 100.0);
                if (ChkChannel1.IsChecked == false) cMat.SetTo(0);
                if (ChkChannel2.IsChecked == false) mMat.SetTo(0);
                if (ChkChannel3.IsChecked == false) yMat.SetTo(0);

                Mat rOut = (Scalar.All(1.0) - cMat) * (Scalar.All(1.0) - kMat) * 255.0;
                Mat gOut = (Scalar.All(1.0) - mMat) * (Scalar.All(1.0) - kMat) * 255.0;
                Mat bOut = (Scalar.All(1.0) - yMat) * (Scalar.All(1.0) - kMat) * 255.0;

                rOut.ConvertTo(rOut, MatType.CV_8UC1); gOut.ConvertTo(gOut, MatType.CV_8UC1); bOut.ConvertTo(bOut, MatType.CV_8UC1);
                processedImage = new Mat(); Cv2.Merge(new Mat[] { bOut, gOut, rOut }, processedImage);
            }
            else 
            {
                ColorConversionCodes code = CboColorSpace.SelectedIndex switch { 1=>ColorConversionCodes.BGR2HSV, 2=>ColorConversionCodes.BGR2YCrCb, 3=>ColorConversionCodes.BGR2Lab, 4=>ColorConversionCodes.BGR2YUV, _=>ColorConversionCodes.BGR2RGB };
                Mat converted = new(); Cv2.CvtColor(workingMat, converted, code);
                Mat[] ch = Cv2.Split(converted);

                if (SliderComponent.Value != 0) ch[0].ConvertTo(ch[0], -1, 1, SliderComponent.Value);
                if (ChkChannel1.IsChecked == false) ch[0].SetTo(0);
                if (ChkChannel2.IsChecked == false) ch[1].SetTo(0);
                if (ChkChannel3.IsChecked == false) ch[2].SetTo(0);

                Cv2.Merge(ch, converted);
                ColorConversionCodes rev = CboColorSpace.SelectedIndex switch { 1=>ColorConversionCodes.HSV2BGR, 2=>ColorConversionCodes.YCrCb2BGR, 3=>ColorConversionCodes.Lab2BGR, 4=>ColorConversionCodes.YUV2BGR, _=>ColorConversionCodes.RGB2BGR };
                processedImage = new Mat(); Cv2.CvtColor(converted, processedImage, rev);
            }
            UpdateUI();
        }
        catch (Exception) { }
    }

   
    private void SliderColorCount_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
        if (originalImage == null || TxtColorCount == null) return;
        int k = (int)SliderColorCount.Value; TxtColorCount.Text = $"{k} Colors";
        Mat div = originalImage.Clone(); Cv2.Divide(div, 256/k, div); Cv2.Multiply(div, 256/k, div);
        processedImage = div; 
        UpdateUI();
    }

    private void SliderComponent_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => ApplyColorEffects();
    private void Channel_Toggle(object sender, RoutedEventArgs e) => ApplyColorEffects();
    private void CboColorSpace_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => ApplyColorEffects();
    
    private void ResetImage_Click(object sender, RoutedEventArgs e) {
        if (originalImage == null) return;
        processedImage = originalImage.Clone(); SliderComponent.Value = 0;
        ChkChannel1.IsChecked = ChkChannel2.IsChecked = ChkChannel3.IsChecked = true;
        UpdateUI();
    }
    private void SaveImage_Click(object sender, RoutedEventArgs e) {
        if (processedImage == null) return;
        var d = new SaveFileDialog { Filter = "PNG|*.png|JPG|*.jpg" };
        if (d.ShowDialog() == true) processedImage.SaveImage(d.FileName);
    }
}