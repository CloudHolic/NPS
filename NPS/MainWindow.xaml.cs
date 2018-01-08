using System;
using System.ComponentModel;
using System.Drawing.Imaging;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

// ReSharper disable InconsistentNaming
// ReSharper disable CompareOfFloatsByEqualityOperator
namespace NPS
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow
    {
        private readonly GlobalKeyHook gHook;
        private readonly bool[] KeyPressed;
        private readonly int[] Count;
        private int CurSect;
        private bool isRunning;
        private bool isDecimal;
        private bool isGradation;
        private readonly FontFamily curFont;
        private string filePath;

        public MainWindow()
        {
            InitializeComponent();

            KeyPressed = new bool[Enum.GetValues(typeof(Keys)).Length];
            Count = new int[40];
            isRunning = true;
            isDecimal = true;
            isGradation = false;
            curFont = KeyBlock.FontFamily;

            gHook = new GlobalKeyHook();
            gHook.KeyDown += gHook_KeyDown;
            gHook.KeyUp += gHook_KeyUp;
            foreach(Keys key in Enum.GetValues(typeof(Keys)))
                gHook.HookedKeys.Add(key);
            gHook.hook();

            var workerThread = new Thread(Worker);
            workerThread.Start();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (AppConfig.GetAppConfig("Exists") == null || AppConfig.GetAppConfig("Exists") != "True")
                return;

            var cvt = new FontFamilyConverter();

            NpsBlock.FontFamily = KeyBlock.FontFamily = (FontFamily) cvt.ConvertFromString(AppConfig.GetAppConfig("Font"));
            NpsBlock.FontStyle = KeyBlock.FontStyle =
                Convert.ToBoolean(AppConfig.GetAppConfig("Italic")) ? FontStyles.Italic : FontStyles.Normal;
            NpsBlock.TextDecorations = KeyBlock.TextDecorations =
                Convert.ToBoolean(AppConfig.GetAppConfig("Underline")) ? TextDecorations.Underline : null;
            
            isGradation = GradationMenu.IsChecked = Convert.ToBoolean(AppConfig.GetAppConfig("Gradation"));

            var fontclr = AppConfig.GetAppConfig("FontColor").Split(',');
            NpsBlock.Foreground = KeyBlock.Foreground = new SolidColorBrush(new Color
            {
                A = Convert.ToByte(fontclr[0]),
                R = Convert.ToByte(fontclr[1]),
                G = Convert.ToByte(fontclr[2]),
                B = Convert.ToByte(fontclr[3])
            });

            var back = AppConfig.GetAppConfig("Background");
            if (back.StartsWith("I>"))
                MainGrid.Background = new ImageBrush(new BitmapImage(new Uri(back.Substring(2))));
            else
            {
                var split = back.Split(',');
                MainGrid.Background = new SolidColorBrush(new Color
                {
                    A = Convert.ToByte(split[0]),
                    R = Convert.ToByte(split[1]),
                    G = Convert.ToByte(split[2]),
                    B = Convert.ToByte(split[3])
                });
            }

            isDecimal = DecimalMenu.IsChecked = Convert.ToBoolean(AppConfig.GetAppConfig("Decimal"));
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            isRunning = false;
            gHook.unhook();

            var isItalic = NpsBlock.FontStyle == FontStyles.Italic;
            var isUnderline = Equals(NpsBlock.TextDecorations, TextDecorations.Underline);
            var cvt = new FontFamilyConverter();

            AppConfig.SetAppConfig("Exists", "True");

            AppConfig.SetAppConfig("Font", cvt.ConvertToString(NpsBlock.FontFamily));
            AppConfig.SetAppConfig("Italic", isItalic.ToString());
            AppConfig.SetAppConfig("Underline", isUnderline.ToString());

            AppConfig.SetAppConfig("Gradation", isGradation.ToString());

            var fontClr = ((SolidColorBrush) NpsBlock.Foreground).Color;
            AppConfig.SetAppConfig("FontColor", $"{fontClr.A},{fontClr.R},{fontClr.G},{fontClr.B}");

            if (MainGrid.Background is SolidColorBrush)
            {
                var backClr = ((SolidColorBrush) MainGrid.Background).Color;
                AppConfig.SetAppConfig("Background", $"{backClr.A},{backClr.R},{backClr.G},{backClr.B}");
            }
            else if (MainGrid.Background is ImageBrush)
                AppConfig.SetAppConfig("Background", "I>" + filePath);

            AppConfig.SetAppConfig("Decimal", isDecimal.ToString());
        }

        private void FontMenu_Click(object sender, RoutedEventArgs e)
        {
            var font = new FontDialog
            {
                AllowVerticalFonts = false,
                ShowColor = true,
            };

            if (font.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var clr = new Color
                {
                    A = font.Color.A,
                    R = font.Color.R,
                    G = font.Color.G,
                    B = font.Color.B
                };

                KeyBlock.FontFamily = NpsBlock.FontFamily = new FontFamily(font.Font.Name);
                KeyBlock.FontStyle = NpsBlock.FontStyle = font.Font.Italic ? FontStyles.Italic : FontStyles.Normal;
                KeyBlock.TextDecorations = NpsBlock.TextDecorations = font.Font.Underline ? TextDecorations.Underline : null;
                KeyBlock.Foreground = NpsBlock.Foreground = new SolidColorBrush(clr);
            }
        }

        private void GradationMenu_Click(object sender, RoutedEventArgs e)
        {
            isGradation = !isGradation;
        }

        private void BackgroundMenu_Click(object sender, RoutedEventArgs e)
        {
            var color = new ColorDialog();

            if (color.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var clr = new Color
                {
                    A = color.Color.A,
                    R = color.Color.R,
                    G = color.Color.G,
                    B = color.Color.B
                };

                MainGrid.Background = new SolidColorBrush(clr);
                filePath = null;
            }
        }

        private void ImageMenu_Click(object sender, RoutedEventArgs e)
        {
            var file = new Microsoft.Win32.OpenFileDialog { Filter = "" };
            var codecs = ImageCodecInfo.GetImageEncoders();
            var sep = string.Empty;

            foreach (var cur in codecs)
            {
                var codecName = cur.CodecName.Substring(8).Replace("Codec", "Files").Trim();
                file.Filter = string.Format("{0}{1}{2} ({3})|{3}", file.Filter, sep, codecName, cur.FilenameExtension);
                sep = "|";
            }

            file.Filter = string.Format("{0}{1}{2} ({3})|{3}", file.Filter, sep, "All files", "*.*");
            file.DefaultExt = ".png";

            if (file.ShowDialog() == true)
            {
                filePath = file.FileName;
                MainGrid.Background = new ImageBrush(new BitmapImage(new Uri(filePath)));
            }
        }

        private void DecimalMenu_Click(object sender, RoutedEventArgs e)
        {
            isDecimal = !isDecimal;
        }

        private void DefaultMenu_Click(object sender, RoutedEventArgs e)
        {
            var black = new Color{ A = 255, R = 0, G = 0, B = 0 };
            var white = new Color { A = 255, R = 255, G = 255, B = 255 };
            MainGrid.Background = new SolidColorBrush(black);
            KeyBlock.Foreground = NpsBlock.Foreground = new SolidColorBrush(white);

            KeyBlock.FontFamily = NpsBlock.FontFamily = curFont;
            KeyBlock.FontStyle = NpsBlock.FontStyle = FontStyles.Normal;
            KeyBlock.TextDecorations = NpsBlock.TextDecorations = null;

            isDecimal = DecimalMenu.IsChecked = true;
            isGradation = GradationMenu.IsChecked = false;
        }

        public void gHook_KeyDown(object sender, KeyEventArgs e)
        {
            if (!KeyPressed[e.KeyValue])
            {
                KeyPressed[e.KeyValue] = true;
                Count[CurSect]++;
            }
        }

        public void gHook_KeyUp(object sender, KeyEventArgs e)
        {
            KeyPressed[e.KeyValue] = false;
        }

        public void Worker()
        {
            var averageNPS = 0.0;
            var color = new Color{ A=255 };

            while (isRunning)
            {
                Thread.Sleep(25);
                CurSect++;
                if (CurSect >= 40)
                    CurSect = 0;
                Count[CurSect] = 0;

                var sectNPS = 0;
                for (var i = 0; i < 40; i++)
                    sectNPS += Count[i];

                averageNPS = (averageNPS + sectNPS) / 2;

                string curText;
                if (isDecimal)
                {
                    curText = Math.Round(averageNPS, 1, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture);
                    if (!curText.Contains("."))
                        curText += ".0";
                }
                else
                    curText = Math.Round(averageNPS, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture);

                if (isGradation)
                {
                    var colorTween = (int)((155 * (averageNPS - (Math.Floor(averageNPS / 8) * 8))) / 8);
                    if (colorTween > 155)
                        colorTween = 155;

                    if (averageNPS >= 48.0)
                    {
                        color.R = byte.MaxValue;
                        color.G = 0;
                        color.B = byte.MaxValue;
                    }
                    else if (averageNPS >= 40.0)
                    {
                        color.R = byte.MaxValue;
                        color.G = 0;
                        color.B = (byte) (100 + colorTween);
                    }
                    else if (averageNPS >= 32.0)
                    {
                        color.R = byte.MaxValue;
                        color.G = (byte) (155 - colorTween);
                        color.B = 100;
                    }
                    else if (averageNPS >= 24.0)
                    {
                        color.R = byte.MaxValue;
                        color.G = (byte) (byte.MaxValue - colorTween);
                        color.B = 100;
                    }
                    else if (averageNPS >= 16.0)
                    {
                        color.R = (byte) (100 + colorTween);
                        color.G = byte.MaxValue;
                        color.B = 100;
                    }
                    else if (averageNPS >= 8.0)
                    {
                        color.R = 100;
                        color.G = byte.MaxValue;
                        color.B = (byte) (byte.MaxValue - colorTween);
                    }
                    else
                    {
                        color.R = (byte) (byte.MaxValue - colorTween);
                        color.G = byte.MaxValue;
                        color.B = byte.MaxValue;
                    }
                }

                Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate
                {
                    KeyBlock.Text = curText;

                    if (isGradation)
                        KeyBlock.Foreground = NpsBlock.Foreground = new SolidColorBrush(color);
                }));
            }
        }
    }
}