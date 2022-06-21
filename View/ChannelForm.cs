using CefSharp;
using CefSharp.Handler;
using CefSharp.WinForms;
using DarkUI.Forms;
using LiveSplit.Racetime.Controller;
using LiveSplit.Racetime.Model;
using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace LiveSplit.Racetime.View
{
    public partial class ChannelForm : DarkForm
    {
        public RacetimeChannel Channel { get; set; }

        public ChannelForm(RacetimeChannel channel, string channelId, bool alwaysOnTop = true)
        {
            Channel = channel;
            Channel.Disconnected += Channel_Disconnected;
            Channel.RaceChanged += Channel_RaceChanged;
            Channel.Authorized += Channel_Authorized;
            InitializeComponent();
            var settings = new CefSettings();
            settings.CefCommandLineArgs.Add("no-proxy-server");
            settings.SetOffScreenRenderingBestPerformanceArgs();
            settings.LogSeverity = LogSeverity.Disable;
            try
            {
                Cef.Initialize(settings);
            }
            catch { }
            TopMost = alwaysOnTop;
            Show();
            chatBox.Hide();
            Text = "Connecting to " + channelId.Substring(channelId.IndexOf('/') + 1);
            Channel.Connect(channelId);
            chatBox.LifeSpanHandler = new ChatBoxLifeSpanHandler();
            if (Channel.Token != null)
            {
                chatBox.RequestHandler = new BearerAuthRequestHandler(Channel.Token.AccessToken);
            }
            chatBox.AddressChanged += OnBrowserAddressChanged;
        }
        private int retries = 0;
        private DateTime LastRetry = DateTime.Now;
        private void OnBrowserAddressChanged(object sender, AddressChangedEventArgs e)
        {
            if (chatBox.Address != Channel.FullWebRoot + Channel.Race.Id + "/livesplit")
            {
                if (retries >= 5)
                {
                    loadMessage.BeginInvoke((Action)(() => loadMessage.Text = "Error loading page."));
                    chatBox = null;
                }
                else
                {
                    if (Channel.Token != null)
                    {
                        chatBox.BeginInvoke((Action)(() => chatBox.RequestHandler = new BearerAuthRequestHandler(Channel.Token.AccessToken)));
                    }
                    Channel_RaceChanged(null, null);
                }
                if (LastRetry.AddSeconds(10) >= DateTime.Now)
                {
                    LastRetry = DateTime.Now;
                    retries++;
                }
            }
            else
            {
                chatBox.BeginInvoke((Action)(() => chatBox.Show()));
                retries = 0;
            }
        }


        private void Channel_RaceChanged(object sender, EventArgs e)
        {
            try
            {
                if (!IsDisposed)
                {
                    Text = $"{Channel.Race.Goal} [{Channel.Race.GameName}] - {Channel.Race.ChannelName}";

                    if (chatBox.IsBrowserInitialized == true)
                    {
                        new Thread(() =>
                        {
                            Thread.CurrentThread.IsBackground = true;
                            System.Threading.Thread.Sleep(3000);
                            if (retries <= 5)
                            {
                                if (Channel.Token != null)
                                {
                                    if (IsHandleCreated)
                                    {
                                        chatBox.BeginInvoke((Action)(() => chatBox.RequestHandler = new BearerAuthRequestHandler(Channel.Token.AccessToken)));
                                        chatBox.BeginInvoke((Action)(() => chatBox.Load(Channel.FullWebRoot + Channel.Race.Id + "/livesplit")));
                                    }
                                }
                            }
                        }).Start();
                    }
                }
            }
            catch (Exception) { }
        }

        private void Channel_Authorized(object sender, EventArgs e)
        {
            chatBox.BeginInvoke((Action)(() => Focus()));

        }

        private void Channel_Disconnected(object sender, EventArgs e)
        {
            if (!IsDisposed)
            {
                Text = "Disconnected";
            }
        }

        private void ChannelForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (Channel.Race?.State == RaceState.Started && Channel.PersonalStatus == UserStatus.Racing)
            {
                DialogResult r = MessageBox.Show("Do you want to FORFEIT before closing the window?", "", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                if (r == DialogResult.Yes)
                {
                    Channel.Forfeit();
                }
                else if (r == DialogResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
            }
            Channel.Authorized -= Channel_Authorized;
            Channel.RaceChanged -= Channel_RaceChanged;
            Channel.Disconnect();
        }

    }
    public class ChatBoxLifeSpanHandler : ILifeSpanHandler
    {
        public bool OnBeforePopup(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, string targetUrl, string targetFrameName, WindowOpenDisposition targetDisposition, bool userGesture, IPopupFeatures popupFeatures, IWindowInfo windowInfo, IBrowserSettings browserSettings, ref bool noJavascriptAccess, out IWebBrowser newBrowser)
        {
            System.Diagnostics.Process.Start(targetUrl);
            newBrowser = null;
            return true;
        }
        public bool DoClose(IWebBrowser chromiumWebBrowser, IBrowser browser) { return true; }
        public void OnBeforeClose(IWebBrowser browser) { }
        public void OnAfterCreated(IWebBrowser chromiumWebBrowser, IBrowser browser) { }
        public void OnBeforeClose(IWebBrowser chromiumWebBrowser, IBrowser browser) { }
    }
    class BearerAuthResourceRequestHandler : ResourceRequestHandler
    {
        public BearerAuthResourceRequestHandler(string token)
        {
            _token = token;
        }

        private string _token;

        protected override CefReturnValue OnBeforeResourceLoad(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, IRequestCallback callback)
        {
            if (!string.IsNullOrEmpty(_token))
            {
                var headers = request.Headers;
                Regex rg = new Regex(Properties.Resources.PROTOCOL_REST.ToLower() + @":\/\/" + Properties.Resources.DOMAIN.ToLower() + @"/.*\/", RegexOptions.IgnoreCase);
                if (rg.Match(request.Url.ToLower()).Success)
                    headers["Authorization"] = $"Bearer {_token}";
                request.Headers = headers;
                return CefReturnValue.Continue;
            }
            else return base.OnBeforeResourceLoad(chromiumWebBrowser, browser, frame, request, callback);
        }

    }
    class BearerAuthRequestHandler : RequestHandler
    {
        public BearerAuthRequestHandler(string token)
        {
            _token = token;
        }

        private string _token;

        protected override IResourceRequestHandler GetResourceRequestHandler(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, bool isNavigation, bool isDownload, string requestInitiator, ref bool disableDefaultHandling)
        {
            if (!string.IsNullOrEmpty(_token)) return new BearerAuthResourceRequestHandler(_token);
            else return base.GetResourceRequestHandler(chromiumWebBrowser, browser, frame, request, isNavigation, isDownload, requestInitiator, ref disableDefaultHandling);
        }
    }
}
