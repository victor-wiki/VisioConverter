using Microsoft.Web.WebView2.WinForms;
using Newtonsoft.Json;
using VisioConverter.Converter;
using VisioConverter.Model;
using System.ComponentModel;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace VisioConverter.Viewer
{
    public partial class frmMain : Form
    {
        private ConvertResult result;
        private int pageCount = 0;
        private bool isLoading = false;
        private string filePath = null;
        private double? pageWidth = null;
        private int currentZoomPercent = 100;

        BackgroundWorker bgWorker = new BackgroundWorker();

        public frmMain()
        {
            InitializeComponent();

            Label.CheckForIllegalCrossThreadCalls = false;
            WebView2.CheckForIllegalCrossThreadCalls = false;

            this.bgWorker.DoWork += this.BgWorker_DoWork;

            this.webView.EnsureCoreWebView2Async();
        }

        private void BgWorker_DoWork(object? sender, DoWorkEventArgs e)
        {
            this.Convert(this.filePath);
        }

        private void frmMain_Load(object sender, EventArgs e)
        {

        }

        private void Reset()
        {
            this.pageCount = 0;
            this.cboNumber.Items.Clear();
            this.btnFirst.Enabled = this.btnLast.Enabled = this.btnPrevious.Enabled = this.btnNext.Enabled = false;
            this.lblTotal.Text = "0";
            this.lblMessage.Text = "";
            this.lblMessage.ForeColor = Color.Black;
            this.filePath = null;

            this.webView.CoreWebView2.NavigateToString("");
        }

        private void tsmiOpenFile_Click(object sender, EventArgs e)
        {
            this.openFileDialog1.FileName = "";

            DialogResult result = this.openFileDialog1.ShowDialog();

            if (result == DialogResult.OK)
            {
                this.Reset();

                string filePath = this.openFileDialog1.FileName;

                this.filePath = filePath;

                this.Text = filePath;

                if (this.bgWorker.IsBusy)
                {
                    MessageBox.Show("It's processing the selected file, please wait for one moment.");
                    return;
                }

                this.bgWorker.RunWorkerAsync();
            }
        }

        private void Convert(string filePath)
        {
            ConvertOption option = new ConvertOption()
            {
                EnableLog = this.chkEnableLog.Checked,
                //PageNumbers = new List<int>() { }
            };

            Visio2Html converter = new Visio2Html(filePath, option);

            converter.OnPageBeginConvert += this.Converter_OnPageBeginConvert;
            converter.OnPageEndConvert += this.Converter_OnPageEndConvert;
            converter.OnPageConvertError += this.Converter_OnPageConvertError;

            this.result = converter.Convert();

            if (this.result.IsOK == false)
            {
                MessageBox.Show(this.result.Message);
            }

            if (this.result.Infos != null && this.result.Infos.Count > 0)
            {
                this.pageCount = this.result.Infos.Count;

                this.lblTotal.Text = this.pageCount.ToString();

                for (int i = 1; i <= this.pageCount; i++)
                {
                    this.cboNumber.Items.Add(i.ToString());
                }

                this.cboNumber.SelectedIndex = 0;
            }
        }

        private void Converter_OnPageConvertError(int pageIndex, string message)
        {
            this.lblMessage.ForeColor = Color.Red;
            this.ShowMessage($"Error occurs when convert page{(pageIndex + 1)}:{message}");
        }

        private void Converter_OnPageEndConvert(int pageIndex, HtmlConvertInfo info)
        {
            this.lblMessage.ForeColor = Color.Black;
            this.ShowMessage($"End convert page{(pageIndex + 1)}.");
        }

        private void Converter_OnPageBeginConvert(int pageIndex)
        {
            this.lblMessage.ForeColor = Color.Black;
            this.ShowMessage($"Start to convert page{(pageIndex + 1)}...");
        }

        private void ShowMessage(string message)
        {
            this.lblMessage.Invoke(() =>
            {
                this.lblMessage.Text = message;
            });
        }

        private void cboNumber_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (this.isLoading)
            {
                return;
            }

            int index = this.cboNumber.SelectedIndex;

            this.ShowHtml(index);
        }

        private async void ShowHtml(int index)
        {
            this.lblMessage.Text = "";

            if (index >= 0 && index < this.pageCount)
            {
                try
                {
                    var info = this.result.Infos[index];

                    var html = info.Html;

                    this.pageWidth = info.Width;

                    await this.webView.Invoke(async () =>
                    {
                        this.webView.NavigateToString("");

                        this.webView.Source = new Uri("about:blank");

                        string encodedHtml = JsonConvert.SerializeObject(html);
                        string script = "window.document.write(" + encodedHtml + ")";

                        await this.webView.EnsureCoreWebView2Async();
                        await this.webView.ExecuteScriptAsync(script);
                    });
                }
                catch (Exception ex)
                {
                    this.webView.Invoke(() =>
                    {
                        this.webView.CoreWebView2.NavigateToString("");
                    });

                    MessageBox.Show(ex.Message);
                }
                finally
                {
                    this.isLoading = true;

                    this.cboNumber.SelectedIndex = index;

                    this.isLoading = false;

                    this.SetControlStatus(index);
                }
            }
        }

        private void SetControlStatus(int index)
        {
            this.btnFirst.Enabled = index > 0;
            this.btnPrevious.Enabled = index > 0;
            this.btnNext.Enabled = index < this.pageCount - 1;
            this.btnLast.Enabled = index < this.pageCount - 1;
        }

        private void btnFirst_Click(object sender, EventArgs e)
        {
            this.ShowHtml(0);
        }

        private void btnPrevious_Click(object sender, EventArgs e)
        {
            this.ShowHtml(this.cboNumber.SelectedIndex - 1);
        }

        private void btnNext_Click(object sender, EventArgs e)
        {
            this.ShowHtml(this.cboNumber.SelectedIndex + 1);
        }

        private void btnLast_Click(object sender, EventArgs e)
        {
            this.ShowHtml(this.pageCount - 1);
        }

        private void btnZoomIn_Click(object sender, EventArgs e)
        {
            this.SetZoom(true);
        }

        private void btnZoomOut_Click(object sender, EventArgs e)
        {
            this.SetZoom(false);
        }

        private async void SetZoom(bool isZoomIn)
        {
            int currentZoomPercent = this.currentZoomPercent;

            if (isZoomIn)
            {
                currentZoomPercent += 10;
            }
            else
            {
                currentZoomPercent -= 10;
            }

            this.currentZoomPercent = currentZoomPercent;

            var scale = Math.Round(currentZoomPercent / 100.0, 2);
            var currentWidth = this.pageWidth * scale;


            await this.webView.CoreWebView2.ExecuteScriptAsync($"var svg =document.getElementsByTagName('svg')[0]; svg.style.transform='scale({scale})'; svg.style.transformOrigin = '0 0'; svg.style.transformBox ='fill-box'; document.getElementById('container').style.width='{currentWidth}px';");
        }
    }
}
