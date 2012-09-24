using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Hardcodet.Wpf.TaskbarNotification;
using System.Net;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Windows.Threading;


namespace PingdomNotifier
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private static Timer timUpdate;
        private bool wasADown = false;

        public MainWindow()
        {
            InitializeComponent();
            txtUsername.Text = Properties.Settings.Default.Username;
            txtPassword.Password = Properties.Settings.Default.Password;
            
            timUpdate = new Timer(new TimerCallback(Tick), null, 0, 1 * 60 * 1000);

            if (Properties.Settings.Default.Username == "") this.WindowState = WindowState.Normal;
            else this.WindowState = WindowState.Minimized;
        }

        public void Tick(object t)
        {
            if (MyNotifyIcon.Dispatcher.CheckAccess())
            {
                RunQuery();
            }
            else
            {
                MyNotifyIcon.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate(){
                    RunQuery();
                }));
            }
        }

        private void RunQuery()
        {
            bool isADown = false;
            string strTitle = "";
            string strMessage = "";
            string strResponse = _doRequest("/api/2.0/checks");
            if (strResponse != null) {
                JObject jVers = JObject.Parse(strResponse);
                foreach (JObject j in jVers["checks"]) {
                    if ((string)j["status"] == "down")
                    {
                        if (strTitle == "")
                        {
                            strTitle = (string)j["name"];
                        }
                        else
                        {
                            strTitle = "Multiple checks are down";
                        }
                        strMessage += (string)j["name"] + " is " + (string)j["status"] + "\n";
                        isADown = true;
                    }
                }
                if (!isADown)
                {
                    ChangeIcon("green");
                    if (this.wasADown)
                    {
                        this.wasADown = false;
                        ShowStandardBalloon("All Checks are now Up", "", "green");
                    }
                }
                else
                {
                    this.wasADown = true;
                    ShowStandardBalloon(strTitle, strMessage, "red");
                }
            }
        }

        private void ShowStandardBalloon(string title, string text, string iconColour)
        {

            ChangeIcon(iconColour);

            //show balloon with custom icon
            BalloonIcon icon = BalloonIcon.Error;
            switch (iconColour)
            {
                case "red":
                    icon = BalloonIcon.Error;
                    break;
                case "green":
                    icon = BalloonIcon.Info;
                    break;
                case "yellow":
                case "grey":
                    icon = BalloonIcon.Warning;
                    break;

            }

            MyNotifyIcon.ShowBalloonTip(title, text, icon);

            //hide balloon
            //MyNotifyIcon.HideBalloonTip();

        }

        private string UriFromIconColour(string iconColour)
        {
            return "pack://application:,,,/Pingdom-Notifier;component/res/bullet_ball_glass_" + iconColour + ".ico";
        }

        private string _doRequest(string path, string data = null, string method = "GET")
        {
            string strReturn = null;
            
            WebRequest request = WebRequest.Create("https://api.pingdom.com" + path);
            ((HttpWebRequest)request).UserAgent = "Pingdom Notifier by Dixcart";
            string authInfo = Properties.Settings.Default.Username + ":" + Properties.Settings.Default.Password;
            authInfo = Convert.ToBase64String(Encoding.Default.GetBytes(authInfo));
            request.Headers["Authorization"] = "Basic " + authInfo;
            request.Headers["App-Key"] = "sp86t6xzpxffcf078e7q9auh9fz572dm";
            ((HttpWebRequest)request).AutomaticDecompression = DecompressionMethods.GZip;
            request.Method = method;
            if (method == "POST" && data != null)
            {
                byte[] byteArray = Encoding.UTF8.GetBytes(data);
                request.ContentLength = byteArray.Length;
                Stream dataStream = request.GetRequestStream();
                dataStream.Write(byteArray, 0, byteArray.Length);
                dataStream.Close();
            }

            try
            {
                WebResponse response = request.GetResponse();
                switch (((HttpWebResponse)response).StatusCode)
                {
                    case HttpStatusCode.OK:
                        //All went ok, handle the response.
                        ChangeIcon("green");
                        MyNotifyIcon.ToolTipText = "Connected";
                        Stream receiveStream = response.GetResponseStream();
                        StreamReader readStream = new StreamReader(receiveStream, Encoding.UTF8);
                        strReturn = readStream.ReadToEnd();
                        break;
                }
                response.Close();
            }
            catch (WebException e)
            {
                switch (((HttpWebResponse)e.Response).StatusCode)
                {
                    case HttpStatusCode.NotFound:
                        ChangeIcon("yellow");
                        //should never trigger in a controlled environ like this
                        break;
                    case HttpStatusCode.InternalServerError:
                        //There has been an error at pingdom's end.
                        ChangeIcon("yellow");
                        break;
                    case HttpStatusCode.Forbidden:
                    case HttpStatusCode.Unauthorized:
                        //Login details are wrong
                        ShowStandardBalloon("Login failed", "Please check login details are correct", "grey");
                        break;
                }
            }

            return strReturn;
        }

        private Boolean ChangeIcon(string colour)
        {
            try
            {
                Uri oUri = new Uri(UriFromIconColour(colour), UriKind.RelativeOrAbsolute);
                MyNotifyIcon.IconSource = BitmapFrame.Create(oUri);
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        private void btnSaveLogin_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.Username = txtUsername.Text;
            Properties.Settings.Default.Password = txtPassword.Password;
            Properties.Settings.Default.Save();
            this.WindowState = WindowState.Minimized;
            RunQuery();
        }

        private void ExitApp(object sender, RoutedEventArgs e)
        {
            timUpdate.Dispose();
            System.Environment.Exit(0);
        }

        private void ShowWindow(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Normal;
        }

        private void OpenPingdom(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://my.pingdom.com/checks/down");
        }


        private void Window_StateChanged_1(object sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Minimized)
            {
                this.ShowInTaskbar = false;
                this.Hide();
            }
            else if (this.WindowState == WindowState.Normal)
            {
                this.ShowInTaskbar = true;
                this.Show();
            }
        }

    }
}
