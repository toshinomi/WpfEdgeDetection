using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Threading.Tasks;
using System.Threading;

namespace WpfEdgeDetection
{
    public partial class MainWindow : Window
    {
        private const int m_nMaskSize = 3;
        private const int m_nFilterMax = 1;
        private double[,] m_dMask;
        private BitmapImage m_bitmapImageOriginal;
        private WriteableBitmap m_bitmapImageFilter;
        private string m_strOpenFileName;
        private CancellationTokenSource m_tokenSource;

        public MainWindow()
        {
            InitializeComponent();

            btnFileSelect.IsEnabled = true;
            btnFilterStart.IsEnabled = false;
            btnStop.IsEnabled = false;

            m_dMask = new double[m_nMaskSize, m_nMaskSize]
            {
                {1.0,  1.0, 1.0},
                {1.0, -8.0, 1.0},
                {1.0,  1.0, 1.0}
            };

            m_bitmapImageOriginal = null;
            m_bitmapImageFilter = null;
        }
        
        ~MainWindow()
        {
        }

        private void BtnFileSelect_Click(object sender, RoutedEventArgs e)
        {
            var openFileDlg = new OpenFileDialog();

            openFileDlg.FileName = "default.jpg";
            openFileDlg.InitialDirectory = @"C:\";
            openFileDlg.Filter = "All Files(*.*)|*.*";
            openFileDlg.FilterIndex = 1;
            openFileDlg.Title = "Please select a file to open";
            openFileDlg.RestoreDirectory = true;
            openFileDlg.CheckFileExists = true;
            openFileDlg.CheckPathExists = true;

            if (openFileDlg.ShowDialog() == true)
            {
                pictureBoxOriginal.Source = null;
                pictureBoxFilter.Source = null;
                m_strOpenFileName = openFileDlg.FileName;
                try
                {
                    LoadImage();
                }
                catch (Exception)
                {
                    MessageBox.Show(this, "Open File Error", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                btnFilterStart.IsEnabled = true;
                btnStop.IsEnabled = true;
                pictureBoxOriginal.Source = m_bitmapImageOriginal;

                labelStart.Content = "0";
                labelEnd.Content = ((int)m_bitmapImageOriginal.Width * (int)m_bitmapImageOriginal.Height).ToString();
            }
            return;
        }

        public void LoadImage()
        {
            m_bitmapImageOriginal = new BitmapImage();
            m_bitmapImageOriginal.BeginInit();
            m_bitmapImageOriginal.UriSource = new Uri(m_strOpenFileName);
            m_bitmapImageOriginal.EndInit();
            m_bitmapImageOriginal.Freeze();
            return;
        }

        private async void BtnFilterStart_Click(object sender, RoutedEventArgs e)
        {
            pictureBoxFilter.Source = null;

            btnFileSelect.IsEnabled = false;

            LoadImage();

            progressBar.Value = 0;
            progressBar.Minimum = 0;
            progressBar.Maximum = m_bitmapImageOriginal.PixelWidth * m_bitmapImageOriginal.PixelHeight;

            pictureBoxOriginal.Source = m_bitmapImageOriginal;
            bool bResult = await TaskWorkUnsafe();
            if (bResult)
            {
                pictureBoxFilter.Source = m_bitmapImageFilter;
            }
            btnFileSelect.IsEnabled = true;
            m_tokenSource = null;
            m_bitmapImageFilter = null;
            m_bitmapImageOriginal = null;
            return;
        }

        public void SetProgressBar(int nCount)
        {
            progressBar.Value = nCount;
        }

        public async Task<bool> TaskWorkUnsafe()
        {
            m_tokenSource = new CancellationTokenSource();
            CancellationToken token = m_tokenSource.Token;
            bool bResult = await Task.Run(() => FilterUnsafe(token));
            return bResult;
        }

        public bool FilterUnsafe(CancellationToken token)
        {
            bool bResult = true;
            int nWidthSize = m_bitmapImageOriginal.PixelWidth;
            int nHeightSize = m_bitmapImageOriginal.PixelHeight;
            int nMasksize = m_dMask.GetLength(0);

            m_bitmapImageFilter = new WriteableBitmap(m_bitmapImageOriginal);
            m_bitmapImageFilter.Lock();

            int nIndexWidth;
            int nIndexHeight;
            int nCount = 0;
            unsafe
            {
                for (nIndexHeight = 0; nIndexHeight < nHeightSize; nIndexHeight++)
                {
                    for (nIndexWidth = 0; nIndexWidth < nWidthSize; nIndexWidth++)
                    {
                        if (token.IsCancellationRequested)
                        {
                            bResult = false;
                            break;
                        }

                        byte* pPixel = (byte*)m_bitmapImageFilter.BackBuffer + nIndexHeight * m_bitmapImageFilter.BackBufferStride + nIndexWidth * 4;

                        double dCalB = 0.0;
                        double dCalG = 0.0;
                        double dCalR = 0.0;
                        double dCalA = 0.0;
                        int nIndexWidthMask;
                        int nIndexHightMask;
                        int nFilter = 0;

                        while (nFilter < m_nFilterMax)
                        {
                            for (nIndexHightMask = 0; nIndexHightMask < nMasksize; nIndexHightMask++)
                            {
                                for (nIndexWidthMask = 0; nIndexWidthMask < nMasksize; nIndexWidthMask++)
                                {
                                    if (nIndexWidth + nIndexWidthMask > 0 &&
                                        nIndexWidth + nIndexWidthMask < nWidthSize &&
                                        nIndexHeight + nIndexHightMask > 0 &&
                                        nIndexHeight + nIndexHightMask < nHeightSize)
                                    {
                                        byte* pPixel2 = (byte*)m_bitmapImageFilter.BackBuffer + (nIndexHeight + nIndexHightMask) * m_bitmapImageFilter.BackBufferStride + (nIndexWidth + nIndexWidthMask) * 4;

                                        dCalB += pPixel2[0] * m_dMask[nIndexWidthMask, nIndexHightMask];
                                        dCalG += pPixel2[1] * m_dMask[nIndexWidthMask, nIndexHightMask];
                                        dCalR += pPixel2[2] * m_dMask[nIndexWidthMask, nIndexHightMask];
                                        dCalA += pPixel2[3] * m_dMask[nIndexWidthMask, nIndexHightMask];
                                    }
                                }
                            }
                            nFilter++;
                        }
                        pPixel[0] = DoubleToByte(dCalB);
                        pPixel[1] = DoubleToByte(dCalG);
                        pPixel[2] = DoubleToByte(dCalR);
                        pPixel[3] = DoubleToByte(dCalA);

                        nCount++;
                    }
                    this.Dispatcher.Invoke(new Action<int>(SetProgressBar), nCount);
                }
            }
            m_bitmapImageFilter.AddDirtyRect(new Int32Rect(0, 0, nWidthSize, nHeightSize));
            m_bitmapImageFilter.Unlock();
            m_bitmapImageFilter.Freeze();

            return bResult;
        }

        public byte DoubleToByte(double dValue)
        {
            byte byteCnvValue = 0;
            if (dValue > 255.0)
            {
                byteCnvValue = 255;
            }
            else if (dValue < 0)
            {
                byteCnvValue = 0;
            }
            else
            {
                byteCnvValue = (byte)dValue;
            }
            return byteCnvValue;
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            if (m_tokenSource != null)
            {
                m_tokenSource.Cancel();
            }

            return;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (m_tokenSource != null)
            {
                e.Cancel = true;
            }

            return;
        }
    }
}